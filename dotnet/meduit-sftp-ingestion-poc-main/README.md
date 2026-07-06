# Meduit SFTP Ingestion - POC bundle

Standalone handover folder. Two parts:

- `docs/` - the working documents: the meeting transcript (2026-06-19 stand-up), `FILE_LIST.xlsx`,
  the analysis/design (`SFTP_INGESTION_CURATION_ANALYSIS.md`), and the architecture deck.
- `file-normalizer-poc/` - the .NET console POC (Phase 1: crawl the three MCD roots, skip in-flight
  `to process` folders, keep only recent files via `minYear`, copy them into a normalized layout +
  an inventory CSV for manual loading). Client + enable enrichment comes from `docs/FILE_LIST.xlsx`;
  no Snowflake load yet. A proper Visual Studio solution (`ShareNormalizer.sln`); see its README.

## Quick start (Windows)

1. On a dev/build machine, build the POC - either open `file-normalizer-poc\ShareNormalizer.sln`
   in Visual Studio and Build (Release), or, on a box with no Visual Studio/SDK, run `build.bat`
   in `file-normalizer-poc\` (uses the in-box .NET Framework `csc.exe` - no SDK/NuGet/internet).
   Either way you get a single `ShareNormalizer.exe`.
2. Copy `bin\ShareNormalizer.exe`, **`FILE_LIST.xlsx`**, and a **`normalizer.conf`** (with the three
   MCD roots + `minYear=2026`) to the Windows Server. The conf defines what to scan; the workbook
   supplies client + enable details.
3. Run (it reads `normalizer.conf` from beside the exe - no flags needed):
   ```
   ShareNormalizer.exe
   ```
   Or try it first against the bundled multi-root sample (year folders + 2026 filter):
   ```
   ShareNormalizer.exe --source "MCD1=.\sample\multi\FCS" --source "MCD2=.\sample\multi\IMC\IMC" --source "MCD3=.\sample\multi\ABS" --out ".\sample\multi-out" --filelist "..\docs\FILE_LIST.xlsx" --min-year 2026
   ```

See `file-normalizer-poc\README.md` for full options, output layout, and next steps.
