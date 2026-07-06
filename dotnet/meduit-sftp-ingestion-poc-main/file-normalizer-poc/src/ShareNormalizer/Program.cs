// -----------------------------------------------------------------------------------------------
// Meduit MDM - share file normalizer (POC, Phase 1: input-file discovery + normalization).
//
// Crawls a Windows share (the customer's "processed" tree), copies the files it finds into one
// stable, normalized folder layout, and writes an inventory CSV the team uses to drive manual
// loading. Read-only against the source (copy, never move/delete).
//
// Scope (kept deliberately small - see README.md):
//   - Surface and organize the INPUT files. No enable mapping, no Snowflake load yet.
//   - In-flight working folders (e.g. "to process") are skipped (they hold files mid-flight).
//   - Input-vs-output is a HINT only ("Kind" column); the SME confirms later. Nothing is dropped:
//     every file is inventoried, and by default every file is copied.
//
// Zero downloadable dependencies: targets .NET Framework 4.x (in-box on Windows Server) and uses
// only the core BCL - no NuGet, no non-default assembly references - so the build is a single,
// self-contained .exe. Build it in Visual Studio (ShareNormalizer.sln) or with the in-box C#
// compiler via build.bat (no SDK, no NuGet, no internet).
// -----------------------------------------------------------------------------------------------



// -----------------------------------------------------------------------------------------------
// Meduit MDM - Share File Normalizer
//
// Execution Flow
// -------------------------------------------------------
// 1. Load Configuration
// 2. Validate Sources
// 3. Load FILE_LIST.xlsx
// 4. Execute Normalization (Scanner)
// 5. Execute Snowflake Workflow
//      a. Inventory Service
//      b. Rename Service
//      c. Stage Upload Service
//
// Loop Mode
// -------------------------------------------------------
// Scanner
//      ↓
// Workflow
//      ↓
// Sleep
//      ↓
// Repeat
// -----------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Threading;

using Meduit.ShareNormalizer.Snowflake.Services;

namespace Meduit.ShareNormalizer
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                Config cfg =
                    ConfigLoader.Load(args);

                ValidateConfiguration(cfg);

                Directory.CreateDirectory(
                    cfg.OutputRoot);

                Logger log =
                    new Logger(
                        Path.Combine(
                            cfg.OutputRoot,
                            "normalize.log"));

                log.Log("");
                log.Log("==============================================================");
                log.Log("Meduit MDM Share Normalizer Started");
                log.Log("==============================================================");

                cfg.Sources =
                    ValidSources(
                        cfg.Sources,
                        log);

                FileListCatalog catalog =
                    LoadCatalog(
                        cfg,
                        log);

                Scanner scanner =
                    new Scanner(
                        cfg,
                        log,
                        catalog);

                WorkflowService workflow =
                    new WorkflowService(
                        cfg,
                        log);

                if (cfg.LoopSeconds > 0)
                {
                    log.Log(
                        string.Format(
                            "LOOP mode enabled. Interval : {0} seconds.",
                            cfg.LoopSeconds));

                    while (true)
                    {
                        ExecuteWorkflow(
                            scanner,
                            workflow,
                            log);

                        Thread.Sleep(
                            cfg.LoopSeconds * 1000);
                    }
                }

                ExecuteWorkflow(
                    scanner,
                    workflow,
                    log);

                log.Log("");
                log.Log("Application completed successfully.");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "FATAL : " + ex);

                return 1;
            }
        }

        /// <summary>
        /// Executes normalization followed by
        /// the complete Snowflake workflow.
        /// </summary>
        private static void ExecuteWorkflow(
            Scanner scanner,
            WorkflowService workflow,
            Logger log)
        {
            log.Log("");
            log.Log("==============================================================");
            log.Log("NORMALIZATION STARTED");
            log.Log("==============================================================");

            scanner.RunOnce();

            log.Log("");
            log.Log("Normalization completed.");

            log.Log("");
            log.Log("==============================================================");
            log.Log("SNOWFLAKE INGESTION STARTED");
            log.Log("==============================================================");

            workflow.Execute();

            log.Log("");
            log.Log("Snowflake ingestion completed.");
        }

        /// <summary>
        /// Validates mandatory configuration.
        /// </summary>
        private static void ValidateConfiguration(
            Config cfg)
        {
            if (cfg == null)
                throw new ArgumentNullException(
                    "cfg");

            if (cfg.Sources == null ||
                cfg.Sources.Count == 0)
            {
                throw new ArgumentException(
                    "At least one source root is required.");
            }

            if (string.IsNullOrWhiteSpace(
                cfg.OutputRoot))
            {
                throw new ArgumentException(
                    "OutputRoot is required.");
            }
        }

        // Load FILE_LIST.xlsx (the system-bucketing authority). Missing/unreadable is non-fatal:
        // we log a warning and fall back to folder-token detection (which yields UNKNOWN on the
        // real share - that is the symptom the catalog exists to fix).
        private static FileListCatalog LoadCatalog(
            Config cfg,
            Logger log)
        {
            string path =
                cfg.FileListPath;

            if (string.IsNullOrWhiteSpace(path))
            {
                path =
                    Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "FILE_LIST.xlsx");
            }

            if (!File.Exists(path))
            {
                log.Log(
                    "WARNING : FILE_LIST.xlsx not found.");

                return null;
            }

            try
            {
                FileListCatalog catalog =
                    FileListCatalog.Load(
                        path,
                        cfg.ClientMatchContains);

                log.Log(
                    string.Format(
                        "FILELIST loaded successfully. Systems={0} Clients={1} Rows={2}",
                        catalog.DirCount,
                        catalog.ClientCount,
                        catalog.RowCount));

                return catalog;
            }
            catch (Exception ex)
            {
                log.Log(
                    "WARNING : Unable to load FILE_LIST.xlsx");

                log.Log(
                    ex.Message);

                return null;
            }
        }

        // Keep only roots that exist; warn on the rest. Fatal only if none are reachable.
		
        private static System.Collections.Generic.List<SourceSpec> ValidSources(
            System.Collections.Generic.List<SourceSpec> sources,
            Logger log)
        {
            System.Collections.Generic.List<SourceSpec> valid =
                new System.Collections.Generic.List<SourceSpec>();

            foreach (SourceSpec source in sources)
            {
                if (Directory.Exists(source.Path))
                {
                    valid.Add(source);
                }
                else
                {
                    log.Log(
                        "WARNING : Source root not found : "
                        + source.Path);
                }
            }

            if (valid.Count == 0)
            {
                throw new DirectoryNotFoundException(
                    "None of the configured source roots exist.");
            }

            return valid;
        }
    }
}