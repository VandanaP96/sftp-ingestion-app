# Ingestion + Curation Service - Design (for sign-off)

Status: **design proposal for review**. Once approved, this becomes the Technical Design Document
(`Meduit_FACS_SFTP_Ingestion_Technical_Design.docx`), structured like the MDM TDD.

This design takes the working POC (`file-normalizer-poc/ShareNormalizer.exe`) and evolves it into the
full **Ingestion + Curation Service**: it keeps everything the POC already does, moves all
configuration into **Snowflake CFG tables**, adds the **Snowflake control plane**, a
**service-account PUT to the Snowflake stage** (configurable), and the **Streamlit SME review UI** from
the architecture deck. It plugs into the existing MDM landing contract so the downstream DAG
(`FILE_RECEIVE -> RUN_CLOSE`) is unchanged.

**Confirmed decisions (this revision):**
- **Backend: .NET 10 (latest LTS). UI: Streamlit in Snowflake.**
- **All configuration lives in Snowflake CFG tables**; only the Snowflake *connection bootstrap* is
  local (environment variables).
- **The Excel `FILE_LIST.xlsx` is replaced by a Snowflake table** (`CFG_FILE_CATALOG`) holding only
  the relevant columns; the workbook becomes a one-time seed.
- **One service** hosting both workers; **SME confirms every file** initially, auto-confirm added later.

---

## 1. Scope and what changes

| Layer | POC today | This design adds |
|---|---|---|
| Discover/normalize | crawl roots, hash, classify, copy to `organized\`, write `inventory.csv` | upsert each file as a row in `CTL_FILE_INVENTORY` (Snowflake) - no local CSV |
| Curate | (none - SME reads the CSV) | **Streamlit** SME review/classify UI over `CTL_FILE_INVENTORY` |
| Promote | (none - manual load) | on `CONFIRMED`: **service-account** PUT to `@DEX.LANDING_STAGE` + register `CTL_LANDING_LOAD` |
| Lifecycle | `organized\` only | `moved\ / archive\ / quarantine\` driven by run outcome |
| Config | `normalizer.conf` on the box | **Snowflake `CFG_*` tables** - no config file on the box |
| Dedup | `copied-hashes.txt` on the box | `CTL_FILE_INVENTORY.CONTENT_HASH` (UNIQUE) in Snowflake - no hash file |
| State / inventory | `inventory.csv` | `CTL_FILE_INVENTORY.STATE` - the single source of truth in Snowflake |

**No local stores.** The only things on the server are the **published Windows Service binaries**
(self-contained .NET 10), the env-var bootstrap (to reach Snowflake), and the `organized\...` PHI
file tree (the actual data files). Configuration, inventory, dedup hashes, and the client/enable
catalog **all live in Snowflake** - nothing is kept in files on the box.

**Out of scope (YAGNI):** no SFTP client (we read the Windows share), no second app server / queue
(the Snowflake table *is* the work queue), no managed connectors. The downstream MDM DAG is untouched.

---

## 2. Target architecture

```
   CONTROL PLANE - Snowflake (single source of truth for state; PHI-Zero)
   +---------------------------------------------------------------------------+
   | CFG_SFTP_SOURCE     roots to crawl + the discovery knobs (optional)        |
   | CTL_FILE_INVENTORY  one row per file: hash, paths, system/client/enable,   |
   |                     kind, year-month, STATE, SME fields, run_id            |
   | CTL_LANDING_LOAD    (existing) the handoff to the MDM pipeline             |
   +---------------------------------------------------------------------------+
        ^  write DISCOVERED/CLASSIFIED        ^ SME read/write        ^ poll run status
        |                                     | (Streamlit)           |
   Windows share  ->  INGEST SERVICE (.NET Windows Service on the RDP box)
   (fed by an          A. Discovery worker:  crawl -> hash -> classify -> copy to organized
    external system)    B. Promotion worker:  poll CONFIRMED -> PUT @stage + register PENDING
                                              -> poll run -> move organized->archive/quarantine
                                                     |
                                                     v   (UNCHANGED)
                                      FILE_RECEIVE -> ... -> RUN_CLOSE
```

Two roles, one deployable. **Stateless** between runs - all durable state lives in Snowflake, so the
service can restart/scale freely (each worker claims rows with an atomic `UPDATE ... WHERE STATE=...`).

---

## 3. Component design (SOLID layering)

The POC classes become the inner domain; new seams are added by interface, so adding Snowflake never
touches the scan logic (Open/Closed). Dependencies point inward and are wired in one composition root
(Dependency Inversion).

```
Host (.NET 10 Worker Service, run as a Windows Service via the SCM)
                                  ->  composition root: builds config + wires implementations
  Discovery worker
    ISourceScanner        crawl a root, yield files            (POC: Scanner)
    IClassifier           system/client/year-month/kind/exclude (POC: Classifier)
    ICatalog              client/enable reference, cached: reads CFG_FILE_CATALOG (Snowflake)
                          (POC FileListCatalog match logic kept; XlsxReader demoted to a one-time seeder)
    IContentHasher        SHA-256 identity                      (POC: HashUtil)
    IFileStore            copy into organized\ ; move between trees (POC: File.Copy)
    IInventoryStore       persist/read a file row; claim by STATE; dedup by CONTENT_HASH
        - SnowflakeInventoryStore  (CTL_FILE_INVENTORY) - the only durable store
        - InMemoryInventoryStore   (unit tests only - no files)
  Promotion worker
    IInventoryStore.ClaimConfirmed()    atomic claim of CONFIRMED rows
    IFileStager           PUT to @stage + INSERT CTL_LANDING_LOAD (the landing contract)
    IRunStatusReader      poll AUD_PIPELINE_RUN / CTL_LANDING_LOAD.STATUS
  Cross-cutting
    ISnowflakeSession     one connection factory; key-pair (service-account) auth, configurable
    IClock, ILogger
```

- **SRP** - each interface one job. **DRY** - one `IClassifier` feeds both discovery and the UI's
  auto-classify; one `IFileStager` is the only place the landing contract lives.
- **ISP** - small interfaces; the discovery worker never sees staging, the UI never sees the filesystem.
- **LSP** - the Snowflake store and the in-memory test fake are interchangeable behind
  `IInventoryStore`; there is no file-based store and no offline fork.
- **KISS** - a polling worker over a Snowflake table; no orchestration engine. **YAGNI** - no SFTP,
  no queue, no managed connector.

---

## 4. Configuration model (Snowflake CFG tables)

**All runtime configuration lives in Snowflake**, so it is managed centrally, versioned and audited
(no editing files on the box). Only the minimal **bootstrap** needed to *reach* Snowflake is local -
supplied as **environment variables** (Snowflake credentials cannot themselves live in Snowflake):

```
SF_ACCOUNT, SF_USER, SF_ROLE (DEX_INGEST), SF_WAREHOUSE, SF_DATABASE, SF_SCHEMA,
SF_AUTH_MODE=keypair, SF_PRIVATE_KEY (path; interim team key-pair -> service account at go-live),
SF_STAGE=@DEX.LANDING_STAGE   (configurable target stage)
```

Everything else is read from CFG tables at startup and refreshed each cycle:

```
CFG_SFTP_SOURCE      source roots                : SYSTEM, ROOT_PATH, IS_ACTIVE
CFG_INGEST_SETTING   key/value knobs (extensible): minYear, yearSource, clientMatchContains,
                     excludeFilePatterns, skipFolderNames, dataExtensions, outputFileMarkers,
                     inputCandidatesOnly, loopSeconds, promote.enabled, promote.autoConfirm
CFG_FILE_CATALOG     client/enable reference (replaces FILE_LIST.xlsx; see s5)
```

`CFG_INGEST_SETTING` is a typed key/value table, so a new knob is a new row - no schema change
(Open/Closed). The POC `.conf` keys map **1:1** to `CFG_INGEST_SETTING` rows (DRY - same names,
same meanings). There is **no `.conf` or any other config/state file on the server**: the box holds
only the env-var bootstrap and the published Windows Service binaries. Unit tests back the storage
seams with in-memory fakes, not files.

The service-account swap (interim key-pair -> dedicated service account at go-live) is then a change
of the `SF_USER` / `SF_PRIVATE_KEY` env vars only - no code, no redeploy (deck slide 6, analysis 14.2).

---

## 5. Snowflake schema (control plane)

PHI-Zero: names, hashes, paths, classification, SME decisions - **never patient values**.

**CFG_SFTP_SOURCE** - the source roots to crawl: `SOURCE_ID PK, SYSTEM, ROOT_PATH, IS_ACTIVE`.

**CFG_INGEST_SETTING** - the discovery/promotion knobs as typed key/value rows (extensible without a
schema change): `SETTING_KEY PK, SETTING_VALUE, VALUE_TYPE (string|int|bool|list), DESCRIPTION`.
Seeded 1:1 from the current `.conf` (`minYear`, `yearSource`, `clientMatchContains`,
`excludeFilePatterns`, `skipFolderNames`, `dataExtensions`, `outputFileMarkers`, ...).

**CFG_FILE_CATALOG** - the client/enable reference that **replaces `FILE_LIST.xlsx`**, holding only
the columns the classifier actually uses (the workbook's emails, network paths, batch/frequency
columns are dropped):

| Column | From the workbook | Used for |
|---|---|---|
| `CATALOG_ID` (PK) | - | key |
| `SYSTEM` | Planner Label / tab (MCD1/2/3/BOPC) | system bucket |
| `DIRECTORY` | New Business / Directory (FCS/IMC/ABS/BOPC) | top-folder -> system |
| `CLIENT_FOLDER` | folder name | client match (exact/contains) |
| `ENABLE_NAME` | Enable Name | enrichment + landing |
| `CLIENT_CODE` | Client Code | enrichment |
| `FILE_NAME_PATTERN` | File Name / `_Ini` | filename -> enable match |
| `IS_ACTIVE`, `LOADED_AT` | - | governance |

A small one-time **seeder** (the POC `XlsxReader` + `FileListCatalog` parse logic, reused) loads the
workbook into `CFG_FILE_CATALOG`; thereafter the table is the source of truth and the SME's confirmed
mappings are written back to it (the "learn over time" loop). The runtime `ICatalog` reads this table
(cached), so no Excel ships to the server.

**CTL_FILE_INVENTORY** - one row per discovered file (the catalog). Columns map 1:1 to what the POC
already computes, plus state/SME fields:

| Column | Source in the POC |
|---|---|
| `FILE_ID` (PK), `CONTENT_HASH` (UNIQUE) | SHA-256 (dedup identity) |
| `SYSTEM, CLIENT, ENABLE_NAME, CLIENT_CODE` | Classifier + FILE_LIST.xlsx |
| `KIND` (source/processed/unknown) | `ResolveKind` |
| `YEAR_MONTH` | `ResolveYearMonth` (Date-Modified rule) |
| `SOURCE_PATH, SERVER_PATH` | crawl path, organized path |
| `SIZE_BYTES, LAST_MODIFIED` | `FileInfo` |
| `STATE` | the state machine (s6) |
| `SME_BY, SME_AT, REJECT_REASON` | set by the UI |
| `RUN_ID` (FK) | set at promotion |

**CTL_LANDING_LOAD** - existing; the service writes `PENDING` rows exactly as `SP_SFTP_POLL` does
(`RECORD_SOURCE`, `SOURCE_FILE_NAME`, `SOURCE_FILE_HASH`, `STATUS`).

---

## 6. State machine and folder lifecycle

`CTL_FILE_INVENTORY.STATE` (from analysis 5.1), with the POC's per-file `Action` mapped in:

```
DISCOVERED   crawled, hashed, copied to organized\      (POC: COPIED)
CLASSIFIED   auto system/client/enable + confidence
NEEDS_REVIEW low confidence / ambiguous -> SME UI
CONFIRMED    SME or high-confidence auto -> promote
STAGING      PUT to @stage + register CTL_LANDING_LOAD
LANDED       PENDING registered; MDM DAG owns it
PROCESSED    run reached RUN_CLOSE OK   -> move organized->archive\
REJECTED     SME ignored (dup/processed-output)         (POC: SKIPPED_DUP / SKIPPED_EXCLUDED / SKIPPED_OLD)
QUARANTINED  transfer/run failure -> quarantine\ + dead-letter
```

Folder trees (analysis 5.2): `organized\ -> moved\ -> archive\` (or `quarantine\`). **Move only after
the Snowflake state commit**; a reconciliation pass re-derives disk location from `STATE` on restart
(no lost/dup state on crash).

The POC's silent skips stay silent: duplicates / excluded / too-old files are simply never promoted
(recorded as terminal `REJECTED` sub-reasons, or not inventoried, per the current behavior).

**Dedup is Snowflake-enforced** by the `UNIQUE` `CONTENT_HASH` - the discovery worker does an
INSERT/MERGE that only writes a row for a hash not already present, so the same content is copied
once across runs and machines. A per-run in-memory hash cache (seeded from Snowflake at startup)
avoids re-staging within a run. There is **no `copied-hashes.txt`** - the table is the dedup memory.

---

## 7. Streamlit SME UI (from the deck)

Two screens, reading/writing `CTL_FILE_INVENTORY` only (PHI-Zero), built as **Streamlit in Snowflake**.

1. **Review Queue - original -> normalized** (deck slide 3): a table of messy original name/path ->
   proposed normalized name, with `System` and `State` (AUTO / REVIEW / IGNORE); multi-select
   **Confirm selected** / **Ignore selected**. Ignore = `REJECTED` (kept in catalog, never promoted).
2. **Normalized Folder View + Classify** (deck slide 4): browse `organized\system\client\feed\month`;
   the classifier proposes `System / Client / Enable` for the SME to **Confirm / Reject**. SME
   decisions feed back as learned rules so the queue shrinks (analysis 6.4).

---

## 8. Security (deck slide 6)

- **One Windows service identity** - reads the share (LocalSystem if local, domain account for UNC),
  granted at install. **No SFTP credential** on our side.
- **One Snowflake login** - RSA key-pair, `DEX_INGEST` least-privilege role (PUT to the stage; write
  inventory + landing; read config). Interim team key-pair for the POC -> **dedicated service account**
  before go-live; user + key path in **env vars**, so the swap is config-only.
- **PHI** lives on the share + server `organized/...` (HIPAA zone: encrypt at rest, audit, retention).
  The Snowflake catalog stays **PHI-Zero**.

---

## 9. Design principles (explicit)

- **SOLID** - SRP per class; OCP via `IInventoryStore`/`IFileStager` (add Snowflake without touching
  scan logic); LSP (CSV vs Snowflake store interchangeable); ISP (focused seams); DIP (composition root).
- **KISS** - polling worker over a table; no orchestrator/queue.
- **DRY** - one classifier (discovery + UI), one landing-contract implementation.
- **YAGNI** - share-only (no SFTP), no extra infra, offline mode kept because it is free (it is just
  the CSV store).
- **Extensible / scalable / maintainable** - new source types, classifiers, or stores are new
  implementations of an existing interface; horizontal scale by running N stateless workers.

---

## 10. Phased delivery (maps to analysis 11)

- **Phase 0** - Snowflake schema (`CFG_SFTP_SOURCE`, `CFG_INGEST_SETTING`, `CFG_FILE_CATALOG`,
  `CTL_FILE_INVENTORY`) + the `ISnowflakeSession` seam + the one-time catalog **seeder** (Excel ->
  `CFG_FILE_CATALOG`). No behavior change.
- **Phase 1 (POC - done)** - crawl + hash + classify + copy + inventory. *Already built (port the
  classifier/catalog/scanner logic into the .NET service unchanged).*
- **Phase 2** - read config + catalog from Snowflake; `SnowflakeInventoryStore` (write
  `CTL_FILE_INVENTORY`) + Streamlit review UI.
- **Phase 3** - promotion worker: service-account PUT to `@stage` + register `CTL_LANDING_LOAD`;
  run both old/new in parallel (hash dedup makes it safe).
- **Phase 4** - moved/archive/quarantine lifecycle + reconciliation + ops metrics.
- **Phase 5** - learn from SME decisions; sunset `SP_SFTP_POLL`.

---

## 11. Decisions (confirmed)

1. **Backend = .NET 10 (latest LTS) Worker Service, hosted as a Windows Service** (registered with the
   Service Control Manager via `Microsoft.Extensions.Hosting.WindowsServices`; auto-start at boot,
   managed lifecycle). **UI = Streamlit in Snowflake.** Deploy as a self-contained publish so the
   locked-down RDP box needs no SDK/NuGet; restore/build on a dev box. Uses the Snowflake .NET
   connector for the control plane + stage PUT.
2. **All configuration, inventory, dedup, and catalog in Snowflake** (`CFG_SFTP_SOURCE`,
   `CFG_INGEST_SETTING`, `CFG_FILE_CATALOG`, `CTL_FILE_INVENTORY`); the server holds **no config or
   state files** - only the env-var connection bootstrap and the service binaries.
3. **Excel replaced by `CFG_FILE_CATALOG`** (relevant columns only); workbook is a one-time seed.
4. **One Windows Service**, both workers; **SME confirms every file** to start, auto-confirm
   (high-confidence) added in Phase 5.

---

## 12. TDD outline (what the .docx will contain)

Mirrors the MDM TDD: Document Control + **ADRs** -> 1 Introduction (purpose/scope/principles) ->
2 Codebase Structure (the SOLID layering) -> 3 Snowflake Schema -> 4 Configuration Reference (the
`.conf`) -> 5 Discovery Worker -> 6 Classification + FILE_LIST catalog -> 7 Promotion + Landing
Contract -> 8 State Machine + Folder Lifecycle -> 9 Streamlit UI -> 10 Security -> 11 Reliability
(idempotency, reconcile, restart) -> 12 Deployment (self-contained publish, Windows service install)
-> 13 Testing -> 14 Operations/Monitoring.
