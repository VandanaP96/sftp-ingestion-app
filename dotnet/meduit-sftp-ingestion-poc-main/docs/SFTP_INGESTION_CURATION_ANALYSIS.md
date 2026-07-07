# SFTP Ingestion + File Curation - Feasibility Analysis and Recommended Design

Status: analysis / proposal (no code changes). Author aid: Claude. Date context: 2026-06-17.

This document analyses the proposed "server-side ingestion + SME curation" service for the messy
client SFTP, judges its feasibility, compares it against alternatives, and recommends a refined
design that plugs into the existing Meduit MDM pipeline and lets us sunset the current
`SFTP_POLL` task / `SP_SFTP_POLL`.

---

## 1. The problem (customer's stated challenges)

1. Files are spread across many folders with no consistent naming or organization.
2. The folder structure keeps changing (year -> month -> sometimes weekly), so it cannot be
   navigated or standardized reliably.
3. It is hard to tell actual source files apart from processed / output files.
4. Files are not organized by system (MCD1, MCD2, ...), adding confusion.
5. The Excel "file list" helps a little but is not reliable enough to locate exact files.
6. This is now a major blocker for automation.
7. Suggestion: involve an SME to bring clarity and standardization.

The through-line: **the source SFTP is unstructured, unstable, and ambiguous, and there is no
trusted index of "which file is what."** Any automation that assumes clean, known feeds will keep
breaking. The fix has to add a *curation layer* (discover -> classify -> human-confirm -> promote)
in front of the deterministic pipeline.

---

## 2. Current-state architecture (what already exists)

The platform already ingests SFTP files entirely inside Snowflake:

```
CFG_SFTP (per-feed config: host, path, pattern, POLL_MINUTES, SECRET_NAME, client, enable)
   |
   v
SFTP_POLL task (every 60 min)  ->  SP_SFTP_POLL()            [Snowflake SP]
   - external access integration SFTP_ACCESS + SECRETS (key-pair creds)
   - paramiko transport (shared/processing/file_receive/file_transport.py)
   - for each active feed: list remote files, filter new vs CTL_LANDING_LOAD.RECORD_SOURCE,
     stream each to  @DEX.LANDING_STAGE/<bare-filename>  (64 MB chunks, put_stream),
     INSERT a PENDING row into CTL_LANDING_LOAD, then ALTER TASK FILE_RECEIVE RESUME
   |
   v
FILE_RECEIVE -> FEED_RESOLVE -> CONFIG_SYNC -> COLUMN_MAP (+ HITL) -> STAGE_SETUP
   -> STAGE_FINISH -> MDM_MERGE (entity fan-out) -> RUN_CLOSE
```

Key facts that matter for the new design (the integration contract):

- The **landing contract** is small and stable: a file is "landed" when it is (a) PUT to
  `@DEX.LANDING_STAGE/<bare-filename>` uncompressed, and (b) registered as a `PENDING` row in
  `CTL_LANDING_LOAD` with `RECORD_SOURCE` = `client/enable/filename` (idempotency key) and
  `SOURCE_FILE_NAME` = the bare filename. `FILE_RECEIVE` then takes over.
- Idempotency / dedup today is **filename-based** (`RECORD_SOURCE`), not content-hash based.
- The whole downstream DAG (`FILE_RECEIVE` ... `RUN_CLOSE`) is **agnostic to how the file arrived**
  -- it only reads `CTL_LANDING_LOAD` + the stage. So we can change the *front-end* without
  touching the pipeline.

**Why the current front-end is not enough for the stated problem:** `SP_SFTP_POLL` assumes each
feed is a known `CFG_SFTP` row with a stable `path` + `file_pattern`. It cannot crawl a
constantly-reshaping tree, cannot tell source from processed files, has no place to *stage and
curate* files before committing them, has no SME-in-the-loop step, and does in-Snowflake compute
for I/O-bound crawling (awkward + costs warehouse time). It is the right tool for a clean feed and
the wrong tool for an uncurated, shifting share.

---

## 3. The proposed solution (as described)

A server-side service ("does the powerlifting"):

1. Pulls files from the client SFTP.
2. Organizes them into a folder structure on our server.
3. Records them in Snowflake tables.
4. A Streamlit UI lets an SME review; when the SME confirms a file may be moved, it is marked in a
   Snowflake backend table.
5. The service picks up confirmed files, PUTs them directly to the Snowflake `LANDING` stage,
   updates the record, moves the file to a `moved` folder, and once processed moves it to
   `archive`.

Intended stack: **.NET Core** (Windows service / backend), **Streamlit** (frontend), **Snowflake**
(backend/control), for a "unified" feel. Once this is live, **sunset `SFTP_POLL` / `SP_SFTP_POLL`.**

---

## 4. Feasibility verdict

**Feasible, and architecturally sound.** It is the standard, correct pattern for taming a messy
source: an *ingestion + curation staging layer* in front of a deterministic pipeline, with a
human gate where automation is not yet trustworthy. Every component is well-trodden:

| Component | Feasible? | Notes |
|---|---|---|
| Pull from SFTP on a schedule | Yes | SSH.NET (.NET) or paramiko (Python). Resumable, key-pair auth. |
| Crawl a shifting folder tree | Yes | Recursive walk from configured roots; tolerate year/month/weekly nesting. |
| Organize into server folders | Yes | Normalize into `system/client/feed/date` regardless of source layout. |
| Catalog in Snowflake | Yes | One inventory table = the control plane / source of truth for state. |
| Streamlit SME review UI | Yes | Already the platform's UI tech; reads/writes the inventory table. |
| Push to `@DEX.LANDING_STAGE` + register `CTL_LANDING_LOAD` | Yes | Reuse the existing landing contract; the downstream DAG is unchanged. |
| moved / archive lifecycle | Yes | Driven by Snowflake state (landed / processed), not guessed. |
| Sunset `SP_SFTP_POLL` | Yes | The service becomes the only front-door; flip `SFTP_POLL` task off. |

The proposal already gets the big things right: **Snowflake as the system of record for state**, a
**human confirmation gate**, and a **clean separation** between "get + curate" (the service) and
"transform + load" (the existing DAG).

So the rest of this document is not "can we?" but "**what is the most efficient, robust shape of
it**" -- where the proposal can be tightened.

---

## 5. Recommended (refined) architecture

Same spirit, with the control plane / data plane / worker / UI roles made explicit:

```
                         CONTROL PLANE  (Snowflake = single source of truth for state)
        +-------------------------------------------------------------------------------+
        | CFG_SFTP_SOURCE   (roots to crawl, creds ref, schedule)                        |
        | CTL_FILE_INVENTORY(one row per discovered file: hash, paths, system, client,   |
        |                    enable, kind=source|processed|unknown, STATE, SME fields)    |
        | CTL_LANDING_LOAD  (existing -- the handoff to the pipeline)                     |
        +-------------------------------------------------------------------------------+
              ^   ^                         ^                          ^
              |   | (poll for work,         | (SME reads/writes        | (poll terminal
              |   |  write state)           |  via Streamlit)          |  run status)
   DATA PLANE |   |                         |                          |
   (files)    |   |                  +------+------+                   |
   SFTP  ->  INGEST SERVICE  ----->  | Streamlit   |          INGEST SERVICE (same worker)
   (client)  (.NET or Python)        | SME Review  |          - on CONFIRMED: PUT to
   - crawl + hash + classify          +-------------+            @DEX.LANDING_STAGE + register
   - copy into server staging                                    CTL_LANDING_LOAD(PENDING)
     /raw -> /organized                                        - move file raw->moved
   - write CTL_FILE_INVENTORY                                  - on RUN terminal: move ->archive
                                                                 (or ->quarantine on failure)
                                          |
                                          v
                       (UNCHANGED)  FILE_RECEIVE -> ... -> RUN_CLOSE
```

### 5.1 State machine (in `CTL_FILE_INVENTORY.STATE`)

```
DISCOVERED        crawler found the file, hashed it, copied to server /raw
CLASSIFIED        auto-classified (system, client, enable, source-vs-processed) with a confidence
NEEDS_REVIEW      low confidence or ambiguous -> shown in the SME UI
CONFIRMED         SME (or high-confidence auto) approved it as a source file to load
STAGING           service is PUT-ing to @DEX.LANDING_STAGE + registering CTL_LANDING_LOAD
LANDED            registered PENDING; the pipeline owns it now (-> existing DAG)
PROCESSED         downstream run reached a terminal OK state (RUN_CLOSE)
ARCHIVED          file moved to /archive
REJECTED          SME marked it not-a-source / duplicate / processed-output -> ignore
QUARANTINED       transfer/processing error -> /quarantine + dead-letter for triage
```

Transitions are owned as: the **service** does DISCOVERED..CONFIRMED-detection + STAGING/LANDED +
ARCHIVED/QUARANTINED; the **SME UI** does NEEDS_REVIEW -> CONFIRMED/REJECTED; the **pipeline**
(via `RUN_CLOSE`) drives LANDED -> PROCESSED. The service reconciles file moves *after* the
Snowflake state commit (see 6.3).

### 5.2 Server folder layout (normalized, stable -- independent of the source's chaos)

```
\inbound\<source>\...            (optional: a faithful mirror of what was pulled, short-lived)
\raw\<sha256-prefix>\<filename>  (de-duplicated landing on our side, content-addressed)
\organized\<system>\<client>\<feed>\<yyyy-mm>\<filename>   (the curated, navigable view)
\moved\...     (confirmed + pushed to Snowflake LANDING; awaiting processing)
\archive\...   (downstream run completed OK)
\quarantine\.. (failed transfer or failed run; dead-letter)
```

The normalized `organized\system\client\feed\date` layout is the antidote to challenges 1, 2 and 4:
no matter how the client reshuffles their tree, our side is always the same shape.

---

## 6. Where the proposal can be more efficient (recommendations)

### 6.1 Make Snowflake the single control plane; keep the service stateless

The service should hold **no durable state of its own** -- it reads "what to do" from Snowflake
(`CTL_FILE_INVENTORY` rows in a given STATE) and writes results back. Benefits: trivial restart /
failover, horizontal scale (run N workers, each claims rows with an atomic
`UPDATE ... SET STATE='STAGING', WORKER=? WHERE STATE='CONFIRMED' AND ...`), and one place to
observe progress. This matches how the existing DAG already works (tasks pick `PENDING`/
`IN_PROGRESS` rows).

### 6.2 Dedup by **content hash**, not filename

The same file reappears under year/month/weekly variants. Compute `SHA-256` on pull and make it the
identity (`CTL_FILE_INVENTORY.CONTENT_HASH`, unique). This collapses duplicates across the shifting
tree automatically (challenge 2) and is stronger than today's filename-only `RECORD_SOURCE`. Pass
the hash through to `CTL_LANDING_LOAD.SOURCE_FILE_HASH` (the column already exists) so downstream
idempotency improves too.

### 6.3 Move files only **after** the Snowflake state commit (no lost/dup state on crash)

Order every step so Snowflake is updated first, then the disk move; on restart, a reconciliation
pass re-derives disk location from `STATE`. Never leave "file moved but DB not updated" or vice
versa. The existing poller already uses checkpoints (`CTL_AGENT_EXEC_STATE`) -- reuse that idea.

### 6.4 Auto-classify first; send only the **uncertain** files to the SME

Do not make the SME confirm every file -- that does not scale and recreates the manual pain. Tier
it:

- **Auto-confident** (filename/folder matches a known pattern, hash seen before, clear system) ->
  STATE `CONFIRMED` automatically.
- **Uncertain** -> `NEEDS_REVIEW` in the UI.

Critically, **feed SME confirmations back as learned rules** (filename/path -> system/client/enable,
source-vs-processed). This is the *same* idea the platform already uses for column mapping
(self-learning layout fingerprint + `CFG_ENABLE` resolution): the SME teaches the classifier once,
and the queue shrinks over time. That directly delivers challenge 7 (SME brings standardization)
*and* makes the standardization durable instead of a one-off.

The "Excel file list" (challenge 5) is a useful but low-trust hint: load it as one *input signal*
to the classifier (a weak prior), never as the source of truth.

### 6.5 Source-vs-processed detection (challenge 3)

Combine signals into a score, then let the SME confirm the unsure ones: source-folder name
heuristics, filename patterns (output files often have suffixes/prefixes, report extensions),
presence in a known "outbound/processed" subtree, file age, and whether the content hash already
exists as a prior *output*. Store the decision + reason on the inventory row; learn from SME
overrides.

### 6.6 The "unified stack" question -- .NET Core vs Python (be deliberate here)

"Unified" depends on the reference point, and this is the single biggest efficiency decision:

- **The existing MDM platform is Python end-to-end**: Snowpark stored procedures, Streamlit apps,
  and -- importantly -- the SFTP transport already exists as
  `shared/processing/file_receive/file_transport.py` (a `FileTransport` seam with SFTP + local
  adapters) plus `sftp_poll.py` (`_stream_file_to_stage`, `_register_landed_file`,
  checkpoint/resume). A **Python** Windows service (`pywin32`/NSSM or a container/scheduled task)
  reuses all of that, shares the connector, and is genuinely "one stack: Python + SQL".
- **.NET Core** is a great fit if Meduit is a **Windows/.NET shop** (existing servers, ops
  familiarity, AD integration, robust Windows-service lifecycle) -- but it introduces a **third
  language** (C# + Python/Streamlit + SQL), duplicates the transport/landing logic in C#
  (SSH.NET + Snowflake .NET connector / `PUT`), and means the "landing contract" is implemented
  twice and must be kept in lockstep.

Recommendation: **prefer a Python service** if the goal word is "unified with this platform" and the
team is comfortable running a Python worker on the server -- it is less code, no duplication, and
reuses the proven landing contract. **Choose .NET Core** if operational reality (Windows server
estate, .NET ops maturity, security/AD requirements) makes a native Windows service the safer
production citizen. Either is feasible; the key is that the **service is decoupled from the pipeline
via the Snowflake control-plane contract**, so the language is an implementation detail that can be
swapped without touching the DAG. Do **not** pick .NET *for* unification -- it is the opposite for
this codebase; pick it for ops fit.

(Note: .NET Core is cross-platform, so "Windows service" and ".NET" are independent choices -- a
.NET worker can run on Linux, a Python worker can run as a Windows service.)

### 6.7 Keep the server folder, but treat the Snowflake stage as transient

The server `organized/` tree is worth keeping: it is the browsable, recoverable, SME-facing curated
copy and the PHI staging area. The Snowflake internal stage is just a transient handoff buffer;
files there can be purged after `PROCESSED`. Do not build a parallel "moved/archive" lifecycle
*inside* the stage -- keep that lifecycle on the server, and let `STATE` in Snowflake describe it.

### 6.8 What this does NOT need (alternatives considered and rejected)

- **Keep doing it in Snowflake (`SP_SFTP_POLL`)**: rejected for the curation problem -- no real
  filesystem, no staging/curation area, poor at crawling a shifting tree, in-warehouse compute for
  I/O-bound work. Fine for a clean feed, wrong for this.
- **Managed ingestion (Snowpipe, Snowflake SFTP/Fivetran/ADF/Airbyte connectors)**: these move
  bytes well but assume a *known, clean* source. They do not solve discovery, source-vs-processed,
  system bucketing, or SME curation -- the actual blockers. At most they could do the final
  PUT-to-stage leg, which is the easy part. Not worth the added platform.
- **A second always-on app server / queue (Kafka, etc.)**: overkill at this volume. The Snowflake
  table *is* the work queue; a polling worker is sufficient.

---

## 7. Integration contract (so the existing pipeline is untouched)

When the service promotes a `CONFIRMED` file it must do exactly what `SP_SFTP_POLL` does today, so
`FILE_RECEIVE` and the whole DAG keep working with zero changes:

1. `PUT` the file to `@DEX.LANDING_STAGE/<bare-filename>` (uncompressed; bare filename = the path
   `FILE_RECEIVE` and the format detector expect).
2. `INSERT` a `PENDING` row into `CTL_LANDING_LOAD` with `RUN_ID`, `CLIENT_ID`, `SERVICE_LINE`,
   `FILE_TYPE`, `RECORD_SOURCE` (idempotency key, now ideally including the content hash),
   `SOURCE_FILE_NAME`, `SOURCE_FILE_HASH`, `STATUS='PENDING'`.
3. Trigger the pipeline: `EXECUTE TASK DEX.FILE_RECEIVE` (or leave the scheduled `FILE_RECEIVE` to
   pick it up).
4. Poll `AUD_PIPELINE_RUN` / `CTL_LANDING_LOAD.STATUS` for the terminal outcome of that run to move
   the inventory row to `PROCESSED` (+ file to `/archive`) or `QUARANTINED` (+ `/quarantine`).

Everything from `FILE_RECEIVE` onward (format detect, enable resolution, column-map + HITL, FACS
multi-record split, delimited multi-entity fan-out, MDM merge, run close) stays exactly as built.

---

## 8. Sunsetting `SFTP_POLL` / `SP_SFTP_POLL`

Because the service uses the same landing contract, decommissioning the old poller is clean and
low-risk:

1. Stand up the service in parallel (it writes the same `CTL_LANDING_LOAD` rows).
2. Cut feeds over one at a time (or run both with the hash dedup preventing double-loads).
3. `ALTER TASK DEX.SFTP_POLL SUSPEND;` then drop `SP_SFTP_POLL`, the `SFTP_ACCESS` external access
   integration, and the `DEX_ALL_SFTP_CREDENTIALS` secret (creds now live with the service).
4. `CFG_SFTP` either retires or is repurposed as `CFG_SFTP_SOURCE` (crawl roots + creds ref for the
   service). The `file_transport.py` SFTP adapter can be lifted into the Python service if we go
   Python.

Net result: SFTP egress moves out of Snowflake entirely (often a security plus -- Snowflake no
longer needs outbound network + stored SFTP creds), and the curation gate is added.

---

## 9. Security / PHI considerations (important -- the files contain PHI)

- The server `raw/organized/moved/archive` tree **holds PHI** (real patient files). It is a HIPAA
  zone: encrypt at rest, lock down OS access (least-privilege service account), audit access,
  define retention, and minimize dwell time (purge `/archive` on a policy). This is a new control
  surface the proposal introduces and must be owned.
- The Snowflake **`CTL_FILE_INVENTORY` catalog stays PHI-Zero**: filenames, paths, hashes, sizes,
  classification, and SME decisions only -- never patient values. Same rule the rest of the
  platform follows.
- Auth (refined after the 2026-06-18 customer call -- see Section 14): there is NO SFTP credential
  on our side, because the service reads a Windows share that an external system populates (not the
  SFTP directly). Snowflake auth is key-pair: for the POC a team member's key-pair scoped to a
  dedicated least-privilege role (PUT + the inventory/landing tables); a dedicated service account
  replaces it before go-live. Key path + role come from environment variables on the Windows
  service; secrets never in the repo.

---

## 10. Reliability / operations

- **Resumable transfers** + partial-file detection (compare remote size / re-hash) before marking
  DISCOVERED. Reuse the existing checkpoint pattern.
- **Idempotent everywhere** (hash identity) so re-runs and crashes never double-load.
- **Dead-letter / quarantine** state + folder for files that fail transfer or processing, with a
  reason, surfaced in the SME UI for triage.
- **Heartbeat + metrics** (files discovered/confirmed/landed/processed/failed per run) to a small
  table or log, shown on a Streamlit ops page (reuse the Pipeline Monitor look).
- **Reconciliation job**: periodically verify disk reality matches `STATE` and repair drift.

---

## 11. Phased delivery plan

1. **Phase 0 - schema + contract**: `CFG_SFTP_SOURCE`, `CTL_FILE_INVENTORY` (state machine), and a
   written landing contract. No behaviour change yet.
2. **Phase 1 - crawler + catalog**: service pulls + hashes + copies to `raw/organized` + writes
   `CTL_FILE_INVENTORY` (DISCOVERED/CLASSIFIED). Read-only; nothing reaches the pipeline.
3. **Phase 2 - SME review UI**: Streamlit page over `CTL_FILE_INVENTORY` (filter by
   system/client/state; classify; confirm/reject). Auto-confident rows skip review.
4. **Phase 3 - promote to LANDING**: on CONFIRMED, service does the landing contract (section 7);
   run both old and new in parallel (hash dedup makes this safe).
5. **Phase 4 - lifecycle + reconcile**: moved/archive/quarantine driven by run outcome; reconcile
   job; ops metrics.
6. **Phase 5 - learn + sunset**: SME decisions train the classifier (shrinking the queue); once a
   feed is stable on the new path, suspend `SFTP_POLL` and decommission `SP_SFTP_POLL`.

---

## 12. Open questions for the SME / customer

- Where do we host the service, and is there a Windows server estate (drives the .NET-vs-Python
  call)?
- What are the (current) crawl roots, and is there *any* invariant we can rely on (a top-level
  `system` folder? an "incoming" vs "outbound" split)?
- Are output/processed files ever written back to the *same* SFTP we read? (If so, we must avoid
  re-ingesting our own outputs -- a strong source-vs-processed signal.)
- Expected volume / file sizes / freshness SLA (drives parallelism + schedule).
- Retention policy for the PHI-bearing server staging tree.
- Who owns the SME role, and what is their available bandwidth (sets the auto-vs-review threshold)?

---

## 13. Recommendation summary

- **Feasible: yes.** The proposal is the right pattern (curation staging + human gate in front of a
  deterministic pipeline) and integrates cleanly via the existing landing contract, letting us
  sunset `SP_SFTP_POLL`.
- **Adopt these refinements**: Snowflake as the single control plane with a stateless worker;
  content-hash identity + dedup; auto-classify with SME only for the uncertain *and* learn from
  their decisions; move-after-commit + reconciliation; keep the server `organized/` tree, treat the
  Snowflake stage as transient; strong PHI controls on the server staging.
- **Language**: lean **Python** for true unification with this platform and to reuse the existing
  transport/landing code; choose **.NET Core** only if the Windows/.NET operational reality makes a
  native service the safer production choice. Decouple via the Snowflake contract either way.
- **Net effect**: the messy SFTP becomes a normalized, deduplicated, SME-curated, system-bucketed
  catalog; automation stops fighting the source; and SFTP egress + creds leave Snowflake.

---

## 14. Refinements after the 2026-06-18 customer call

This curation service is a NEW, standalone application -- it is not part of, and does not reuse the
credentials of, the existing Meduit FACS MDM pipeline. Two points from the call simplify it.

### 14.1 Source is a Windows share, not SFTP (the SFTP account is removed)

An external system already moves files from the client SFTP onto a Windows share that is available
on the RDP / Windows server. Our service runs ON that server and simply READS that share -- it does
not connect to the SFTP at all. Consequences:

- There is NO SFTP credential or SFTP-side service account for us to hold or rotate.
- The service uses the existing `local_folder` transport (`file_transport.py`) pointed at the share
  root, instead of the SFTP transport.
- The Admin installs the Windows service once; it runs under an install-time identity that has read
  access to the share (LocalSystem if the share is local to the server; a domain service account if
  it is a remote UNC path). PHI now lives on that share + server, where the HIPAA controls apply.

### 14.2 Snowflake auth: interim key-pair for the POC, service account before go-live

Provisioning a dedicated Snowflake service account is slow, so for the POC this new application
authenticates with a key-pair created by a team member who has Snowflake access, scoped to a
dedicated least-privilege role (e.g. `DEX_INGEST`: PUT on `@DEX.LANDING_STAGE`, write the inventory
+ landing tables, read the source config). Before go-live, a dedicated Snowflake service account
replaces it. Because the user, key path, and role are all read from environment variables on the
Windows service, that swap is a configuration change with no code impact.

Net for this new application: the "two service accounts" become one Windows service identity (reads
the share, granted at install -- no SFTP secret) plus one Snowflake login (interim team-member
key-pair now, dedicated service account at go-live).
