using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Meduit.ShareNormalizer
{
    /// <summary>
    /// Builds a <see cref="Config"/> by layering: baked-in defaults &lt;- optional normalizer.conf
    /// &lt;- command-line args. The exe is standalone and runs on args alone; normalizer.conf is a
    /// plain key=value file (lists comma-separated) for tweaking things without a rebuild.
    ///
    /// Source roots can be given as "SYSTEM=path" (pinned) or just "path" (auto-detect). In the conf
    /// the "sources" key is a semicolon-separated list; on the command line --source is repeatable.
    /// </summary>
    internal static class ConfigLoader
    {
        public static Config Load(string[] args)
        {
            var cfg = new Config();

            string confPath = GetArg(args, "--config") ??
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "normalizer.conf");
            if (File.Exists(confPath))
                ApplyConfFile(cfg, confPath);

            ApplyArgs(cfg, args);
            return cfg;
        }

        private static void ApplyConfFile(Config cfg, string confPath)
        {
            foreach (var rawLine in File.ReadAllLines(confPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                switch (key)
                {
                    case "sources": foreach (var s in val.Split(';')) AddSource(cfg, s); break;
                    case "sourceRoot": AddSource(cfg, val); break;     // legacy single root
                    case "outputRoot": if (val.Length > 0) cfg.OutputRoot = val; break;
                    case "fileListPath": if (val.Length > 0) cfg.FileListPath = val; break;
                    case "minYear": int my; if (int.TryParse(val, out my)) cfg.MinYear = my; break;
                    case "clientMatchContains": cfg.ClientMatchContains = ParseBool(val, cfg.ClientMatchContains); break;
                    case "yearSource": cfg.YearFromModified = ParseYearSource(val, cfg.YearFromModified); break;
                    case "skipFolderNames": cfg.SkipFolderNames = SplitList(val, cfg.SkipFolderNames); break;
                    case "outputFileMarkers": cfg.OutputFileMarkers = SplitList(val, cfg.OutputFileMarkers); break;
                    case "excludeFilePatterns": cfg.ExcludeFilePatterns = val.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList(); break;
                    case "dataExtensions": cfg.DataExtensions = SplitList(val, cfg.DataExtensions); break;
                    case "systemTokens": cfg.SystemTokens = SplitList(val, cfg.SystemTokens); break;
                    case "dryRun": cfg.DryRun = ParseBool(val, cfg.DryRun); break;
                    case "inputCandidatesOnly": cfg.InputCandidatesOnly = ParseBool(val, cfg.InputCandidatesOnly); break;
                    case "loopSeconds": int li; if (int.TryParse(val, out li)) cfg.LoopSeconds = li; break;
                    // ---------------------------------------------------
                    // SnowCLI Configuration
                    // ---------------------------------------------------

                    case "snowflakeEnabled":
                        cfg.SnowflakeEnabled = ParseBool(val, cfg.SnowflakeEnabled);
                        break;

                    case "snowCliPath":
    cfg.SnowCliPath = val;
    break;

case "snowConnection":
    cfg.SnowConnection = val;
    break;

                    case "snowflakeHost":
                        cfg.snowflakeHost = val;
                        break;

                    case "snowflakeAccount":
    cfg.SnowflakeAccount = val;
    break;

case "snowflakeUser":
    cfg.SnowflakeUser = val;
    break;

case "snowflakeWarehouse":
    cfg.SnowflakeWarehouse = val;
    break;

case "snowflakeRole":
    cfg.SnowflakeRole = val;
    break;

case "snowflakeAuthenticator":
    cfg.SnowflakeAuthenticator = val;
    break;

case "snowflakePrivateKeyFile":
    cfg.SnowflakePrivateKeyFile = val;
    break;

case "stageUploadThreads":
    int stageThreads;
    if (int.TryParse(val, out stageThreads))
        cfg.StageUploadThreads = stageThreads;
    break;

case "maxSnowCliProcesses":
    int maxCli;
    if (int.TryParse(val, out maxCli))
        cfg.MaxSnowCliProcesses = maxCli;
    break;    

                    case "snowflakeDatabase":
                        cfg.SnowflakeDatabase = val;
                        break;

                    case "snowflakeSchema":
                        cfg.SnowflakeSchema = val;
                        break;

                    case "snowflakeStage":
                        cfg.SnowflakeStage = val;
                        break;

                    case "normalizedRoot":
                        cfg.NormalizedRoot = val;
                        break;

                    case "archiveRoot":
                        cfg.ArchiveRoot = val;
                        break;

                    case "quarantineRoot":
                        cfg.QuarantineRoot = val;
                        break;

                    case "headerTable":
                        cfg.HeaderTable = val;
                        break;

                    case "folderTable":
                        cfg.FolderTable = val;
                        break;

                    case "detailTable":
                        cfg.DetailTable = val;
                        break;

                    case "activityTable":
                        cfg.ActivityTable = val;
                        break;

                

                    case "scannerFolderThreads":
    int sft;
    if (int.TryParse(val, out sft))
        cfg.ScannerFolderThreads = sft;
    break;

case "scannerFileThreads":
    int sfile;
    if (int.TryParse(val, out sfile))
        cfg.ScannerFileThreads = sfile;
    break;

case "inventoryFolderThreads":
    int inventoryThreads;
    if (int.TryParse(val, out inventoryThreads))
    {
        cfg.InventoryFolderThreads = inventoryThreads;
    }
    break;

case "snowCliThreads":
    int snowThreads;
    if (int.TryParse(val, out snowThreads))
    {
        cfg.SnowCliThreads = snowThreads;
    }
    break;    

case "inventoryFileThreads":
    int ifile;
    if (int.TryParse(val, out ifile))
        cfg.InventoryFileThreads = ifile;
    break;

case "renameThreads":
    int rt;
    if (int.TryParse(val, out rt))
        cfg.RenameThreads = rt;
    break;

case "uploadThreads":
    int ut;
    if (int.TryParse(val, out ut))
        cfg.UploadThreads = ut;
    break;
                }
            }
        }

        private static void ApplyArgs(Config cfg, string[] args)
        {
            foreach (var s in GetAllArgs(args, "--source")) AddSource(cfg, s);
            string o = GetArg(args, "--out"); if (o != null) cfg.OutputRoot = o;
            string f = GetArg(args, "--filelist"); if (f != null) cfg.FileListPath = f;
            string y = GetArg(args, "--min-year"); if (y != null) cfg.MinYear = int.Parse(y);
            string ys = GetArg(args, "--year-source"); if (ys != null) cfg.YearFromModified = ParseYearSource(ys, cfg.YearFromModified);
            string l = GetArg(args, "--loop"); if (l != null) cfg.LoopSeconds = int.Parse(l);
            if (HasFlag(args, "--dry-run")) cfg.DryRun = true;
            if (HasFlag(args, "--input-only")) cfg.InputCandidatesOnly = true;
        }

        // A spec is "SYSTEM=path" when the part before '=' is a short token with no path separators
        // (e.g. MCD1); otherwise the whole value is the path (handles UNC/drive paths with no '=').
        private static void AddSource(Config cfg, string raw)
        {
            if (raw == null) return;
            raw = raw.Trim();
            if (raw.Length == 0) return;

            string system = null, path = raw;
            int eq = raw.IndexOf('=');
            if (eq > 0)
            {
                string left = raw.Substring(0, eq).Trim();
                string right = raw.Substring(eq + 1).Trim();
                if (right.Length > 0 && left.Length > 0 && left.IndexOfAny(new[] { '\\', '/', ':' }) < 0)
                {
                    system = left.ToUpperInvariant();
                    path = right;
                }
            }
            cfg.Sources.Add(new SourceSpec(path, system));
        }

        private static List<string> SplitList(string val, List<string> fallback)
        {
            var list = val.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            return list.Count > 0 ? list : fallback;
        }

        private static bool ParseBool(string val, bool fallback)
        {
            bool b;
            return bool.TryParse(val, out b) ? b : fallback;
        }

        // "modified" -> true, "filename" -> false, anything else keeps the current value.
        private static bool ParseYearSource(string val, bool fallback)
        {
            if (val.Equals("modified", StringComparison.OrdinalIgnoreCase)) return true;
            if (val.Equals("filename", StringComparison.OrdinalIgnoreCase)) return false;
            return fallback;
        }

        private static string GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static List<string> GetAllArgs(string[] args, string name)
        {
            var list = new List<string>();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) list.Add(args[i + 1]);
            return list;
        }

        private static bool HasFlag(string[] args, string name)
        {
            return args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
