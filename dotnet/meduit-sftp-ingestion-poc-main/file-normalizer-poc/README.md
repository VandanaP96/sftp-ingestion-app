# Share File Normalizer (POC)

A small **.NET console application** for the first step of the Meduit MDM SFTP/share ingestion:
crawl the customer's share, surface the **input files**, and copy them into one clean, normalized
folder layout plus an inventory CSV the team can use to **load files manually** today. It does not
touch enables, Snowflake, or the legacy output files yet - that is the later, full solution.

Grounded in the 2026-06-19 customer call and `docs/SFTP_INGESTION/` (the analysis doc + FILE_LIST).

## Scope of this POC (intentionally small)

- **Get the input files organized.** No Snowflake load - just a normalized copy + an inventory so
  the current team can load manually.
- **Scan only the three MCD roots.** We point straight at the per-system top folders and pin each
  to its system (BOPC and everything else are out of scope):
  `MCD1 = \\...\NewBusinessProcessing\FCS`, `MCD2 = \\...\IMC\IMC`, `MCD3 = \\...\ABS`.
- **Only recent files (`minYear`, default off; set to 2026).** Keep files whose Date Modified year
  is `minYear` or later.
- **The file's Date Modified drives the bucket (`yearSource=modified`).** Bucket = the Date Modified
  `yyyy-MM`. The name and folder are ignored, *except* that if the filename or a folder names a year
  other than `minYear`, the **year defaults to `minYear`** - so a stray 2020 / 2027 / 2028 (or a
  misread number) in a name or folder can never make a wrong-year folder. (`yearSource=filename`
  restores the legacy "filename date wins" behavior.)
- **Client + enable from `FILE_LIST.xlsx`.** The workbook gives the canonical client name (matched
  exactly or by substring, since real folders carry extra suffixes) and a best-effort
  `EnableName`/`ClientCode` (only when exactly one of that client's file patterns matches). It is
  also the system authority for any root left un-pinned.
- **Skip in-flight folders.** Anything under a `to process` (working) folder is ignored - those
  hold files mid-flight (per David on the call). The settled tree (e.g. `Processed`) is read.
- **Never copy acknowledgment / message files.** Filenames matching `excludeFilePatterns` (e.g.
  `.ack.txt`, `_ack.txt`, `.msg`) are skipped before they're even hashed, recorded as
  `SKIPPED_EXCLUDED`. A plain pattern is a case-insensitive suffix; a `*` makes it a wildcard.
- **Input vs output is a hint, not a filter.** Each row gets a `Kind` flag
  (`INPUT_CANDIDATE` / `INPUT_MAYBE` / `OUTPUT_CANDIDATE` / `UNKNOWN`) from simple filename
  signals. Nothing is dropped - everything is inventoried, and by default everything is copied.
  The SME confirms input-vs-output later.
- **Read-only on the source.** Files are copied, never moved or deleted.

## Project layout

```
ShareNormalizer.sln                  Visual Studio solution
src\ShareNormalizer\
  ShareNormalizer.csproj             .NET Framework 4.8 console project
  Program.cs                         entry point + arg validation + loop
  Config.cs                          settings POCO (defaults live here)
  ConfigLoader.cs                    defaults <- normalizer.conf <- command-line args
  Classifier.cs                      system / client / yyyy-MM / Kind heuristics
  FileListCatalog.cs                 reads FILE_LIST.xlsx -> system + client + enable lookups
  XlsxReader.cs                      minimal in-box .xlsx reader (zip + XML, no NuGet)
  Scanner.cs                         one scan pass: crawl -> hash -> dedup -> copy -> inventory
  InventoryWriter.cs                 append-only inventory.csv writer
  HashUtil.cs                        SHA-256 content hash
  Logger.cs                          console + normalize.log
  normalizer.conf                    optional config (copied next to the exe on build)
build.bat                            no-Visual-Studio fallback build (in-box csc.exe)
sample\source\                       sample with literal MCD* folders (token fallback)
sample\realistic-source\             sample with real FCS/IMC/ABS/BOPC layout (workbook mapping)
sample\multi\                        multi-root sample with 2025/2026 year folders (minYear demo)
```

## Build

Zero downloadable dependencies: it targets **.NET Framework 4.8**, which is in-box on Windows
Server, and uses only the core BCL (no NuGet, no non-default references) - so the result is a
single, self-contained `.exe`.

**Option A - Visual Studio (recommended for development).** Open `ShareNormalizer.sln`, pick the
`Release` configuration, and Build. Output: `src\ShareNormalizer\bin\Release\ShareNormalizer.exe`
(with `normalizer.conf` copied beside it). MSBuild on the command line works too:

```
msbuild ShareNormalizer.sln /p:Configuration=Release
```

**Option B - no Visual Studio / no SDK.** On a locked-down build box, compile with the **in-box
`csc.exe`** (no SDK, no NuGet, no internet):

```
build.bat
```

Output: `bin\ShareNormalizer.exe` - a single, self-contained executable.

## Deploy (to the server)

Copy `bin\ShareNormalizer.exe`, **`FILE_LIST.xlsx`**, and a **`normalizer.conf`** (with the three
roots + `minYear`) to the Windows Server. The exe references only in-box .NET Framework assemblies.

## Run

The intended setup defines the three roots once in `normalizer.conf` (see below) and runs with no
flags - the exe reads `normalizer.conf` from beside itself automatically:

```
ShareNormalizer.exe
```

Everything is also available on the command line (args override the conf). `--source` is repeatable
and each value may pin a system as `SYSTEM=path`:

```
ShareNormalizer.exe ^
  --source "MCD1=\\Newbusiness.rmp.local\NewBusinessProcessing\FCS" ^
  --source "MCD2=\\Newbusiness.rmp.local\NewBusinessProcessing\IMC\IMC" ^
  --source "MCD3=\\Newbusiness.rmp.local\NewBusinessProcessing\ABS" ^
  --out "D:\meduit\normalized" --filelist ".\FILE_LIST.xlsx" --min-year 2026
```

Options:

| Flag | Meaning |
|---|---|
| `--source <[SYS=]path>` | Root to crawl; optionally pin a system (`MCD1=...`). Repeatable. Required (or set `sources` in conf). |
| `--out <path>`     | Where the normalized tree, inventory and log are written. Required. |
| `--filelist <path>`| `FILE_LIST.xlsx` - client + enable authority. Default: `FILE_LIST.xlsx` beside the exe. |
| `--min-year <yyyy>`| Keep only files from this year or later; prune older year folders. `0`/omitted = no filter. |
| `--year-source <m>`| `modified` (Date Modified sets the year; default) or `filename` (filename date wins). |
| `--dry-run`        | Scan + inventory only; copy nothing. |
| `--input-only`     | Copy only files flagged `INPUT_CANDIDATE` (still inventories everything). |
| `--loop <seconds>` | Re-scan every N seconds (0 / omitted = run once and exit). |
| `--config <path>`  | Path to an optional `normalizer.conf`. |

Try it with no real share using the bundled samples:

```
:: literal MCD* folders (system from a folder token, no workbook needed)
ShareNormalizer.exe --source ".\sample\source" --out ".\sample\out"

:: realistic multi-root layout with year folders, pinned systems + 2026 filter
ShareNormalizer.exe --source "MCD1=.\sample\multi\FCS" --source "MCD2=.\sample\multi\IMC\IMC" ^
  --source "MCD3=.\sample\multi\ABS" --out ".\sample\multi-out" ^
  --filelist "..\docs\FILE_LIST.xlsx" --min-year 2026
```

## Optional config (`normalizer.conf`)

A plain `key=value` file (lists are comma-separated). Everything has a baked-in default and
command-line args override it. The shipped `normalizer.conf` already defines the three MCD roots,
`outputRoot`, `fileListPath`, and `minYear=2026`; edit it to retarget without a rebuild. Key
settings: `sources` (semicolon list of `SYSTEM=path`), `minYear`, `yearSource`, `clientMatchContains`,
`skipFolderNames`, and the acceptable/non-acceptable file lists - `dataExtensions` (acceptable
formats, a hint) and `excludeFilePatterns` (formats/suffixes to never copy, e.g.
`.ack.txt,_ack.txt,.msg`).

## Output

```
<out>\organized\<System>\<Client>\<yyyy-MM>\<original-filename>   the normalized copy
<out>\inventory.csv        one row per file seen (append-only, human-readable, Excel-friendly)
<out>\copied-hashes.txt    SHA-256 of every copied file (cross-run dedup record)
<out>\normalize.log        run log
```

- **System** is the system pinned to the root the file came from (e.g. the `FCS` root is `MCD1`).
  For an un-pinned root it falls back to the workbook (top folder / client), then a literal
  `MCD*`/`BOPC` token, else `UNKNOWN`.
- **Client** is the canonical folder name from the workbook - matched exactly, or by substring when
  `clientMatchContains=true` (e.g. disk `ADVANCED RADIOLOGY` -> `ADVANCED RAD`) - else the folder
  above a `processed` segment, else the top folder under the root.
- **EnableName / ClientCode** are filled from the workbook only when exactly one of that client's
  file patterns matches the file name (otherwise left blank - a best-effort hint, not authoritative).
- **yyyy-MM** (with `yearSource=modified`, the default): the bucket is the file's Date Modified
  `yyyy-MM`. If the filename or a folder names a year other than `minYear`, the **year** defaults to
  `minYear` (the month stays the Date Modified month), so a stray year in a name or folder never
  makes a wrong-year folder. With `yearSource=filename`, a filename date wins, else last-modified.
- **Year filter** (`minYear`): a file whose Date Modified year is below `minYear` is inventoried as
  `SKIPPED_OLD` and not copied. (In `filename` mode, older year folders are also pruned for speed.)
- **Dedup** is by content hash (SHA-256): the same file under different folders/dates is copied
  once; re-runs do not re-copy. Duplicates are ignored silently - not copied and **not listed** in
  the inventory (they are still counted in the run-summary log line as `dup=N`).

`inventory.csv` columns: `ScanTimeUtc, System, Client, EnableName, ClientCode, FileName, Extension,
SizeBytes, LastWriteUtc, Kind, Action, Sha256, SourceFullPath, NormalizedPath`. `Action` is one of
`COPIED / WOULD_COPY / SKIPPED_EXISTS / SKIPPED_OLD / SKIPPED_EXCLUDED / SKIPPED_NONINPUT /
ERROR_READ` (duplicates are dropped from the inventory, not listed).

## Run as a scheduled job (optional)

Use Windows Task Scheduler to run the exe (single pass) on a schedule, or run it once with
`--loop 300` to keep it scanning every 5 minutes.

## What this is NOT (yet) - next steps for the full solution

- No canonical file renaming, and the `EnableName`/`ClientCode` from the workbook are a best-effort
  hint (matched only when unambiguous) - not a confirmed enable mapping. The SME confirms later.
- No Snowflake `PUT` / `CTL_LANDING_LOAD` registration - loading stays manual for now.
- The input-vs-output `Kind` is a heuristic for triage, not a confirmed classification.

These are the next phases in `docs/SFTP_INGESTION_CURATION_ANALYSIS.md`.
