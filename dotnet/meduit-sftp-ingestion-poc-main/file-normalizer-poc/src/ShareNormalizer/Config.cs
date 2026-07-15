using System.Collections.Generic;

namespace Meduit.ShareNormalizer
{

    /// <summary>One root to crawl, optionally pinned to a system (e.g. MCD1) instead of auto-detecting.</summary>
    internal sealed class SourceSpec
    {
        public string Path;
        public string System;   // explicit system override (MCD1/MCD2/...), or null to auto-detect

        public SourceSpec(string path, string system)
        {
            Path = path;
            System = system;
        }
    }

    /// <summary>
    /// All runtime settings for a scan. Every value has a baked-in default; an optional
    /// <c>normalizer.conf</c> file and then command-line args layer on top (see <see cref="ConfigLoader"/>).
    /// </summary>
    internal sealed class Config
    {

        #region Existing Configuration
        /// <summary>
        /// Roots to crawl. Each may be pinned to a system - on the real share the system is the top
        /// folder (FCS=MCD1, IMC\IMC=MCD2, ABS=MCD3), so we point straight at those and tag them.
        /// </summary>
        public List<SourceSpec> Sources = new List<SourceSpec>();

        /// <summary>Where the normalized tree, inventory and log are written. Required.</summary>
        public string OutputRoot = "";

        /// <summary>
        /// Path to FILE_LIST.xlsx - the authority for system (MCD1/MCD2/MCD3/BOPC) bucketing and the
        /// source of client + enable enrichment. Blank = look for FILE_LIST.xlsx beside the exe.
        /// </summary>
        public string FileListPath = "";

        /// <summary>
        /// Only keep files from this year or later (0 = no filter). The year is taken from a year
        /// folder in the path, else a date in the filename, else last-modified. Older year folders
        /// are pruned during the crawl so we never descend into them.
        /// </summary>
        public int MinYear = 0;

        /// <summary>
        /// When matching a folder to a workbook client, also allow a substring ("contains") match,
        /// not just exact - real folders carry extra suffixes/codes. true by default.
        /// </summary>
        public bool ClientMatchContains = true;

        /// <summary>
        /// How the yyyy-MM bucket and the recency year are decided:
        ///   true  (yearSource=modified) - the file's last-modified date is authoritative for the
        ///         YEAR; the filename supplies the month only when its year agrees, so an older /
        ///         newer / misparsed year in the name can never mis-bucket. Default.
        ///   false (yearSource=filename) - a date in the filename wins, else last-modified (legacy).
        /// </summary>
        public bool YearFromModified = true;

        /// <summary>In-flight working folders to skip entirely (they hold files mid-write).</summary>
        public List<string> SkipFolderNames = new List<string>
            { "to process", "2 process", "in process", "in-process", "inflight", "working", "temp" };

        /// <summary>Filename markers that hint a legacy OUTPUT/ack file (Kind=OUTPUT_CANDIDATE).</summary>
        public List<string> OutputFileMarkers = new List<string>
            { "ack", "acknowledg", "_out", "output", "reject", "error", "_err", "response", "confirm", "receipt", "result" };

        /// <summary>
        /// Filename patterns to NOT copy at all (acknowledgment / message / output files). A plain
        /// pattern matches as a case-insensitive suffix (e.g. ".ack.txt", ".msg"); a pattern with '*'
        /// is a wildcard over the whole name. Empty = exclude nothing. Configured in normalizer.conf.
        /// </summary>
        public List<string> ExcludeFilePatterns = new List<string>();

        /// <summary>Extensions treated as data files when flagging input candidates.</summary>
        public List<string> DataExtensions = new List<string>
            { ".txt", ".csv", ".dat", ".pde", ".prn", ".xlsx", ".xls", ".zip", ".fixed", ".asc" };

        /// <summary>Tokens used to detect the system from the folder path (fallback when no FILE_LIST).</summary>
        public List<string> SystemTokens = new List<string> { "MCD1", "MCD2", "MCD3", "BOPC" };

        /// <summary>true = scan + inventory only, copy nothing.</summary>
        public bool DryRun = false;

        /// <summary>true = copy only files flagged INPUT_CANDIDATE (everything is still inventoried).</summary>
        public bool InputCandidatesOnly = false;

        /// <summary>0 = run once and exit; &gt;0 = re-scan every N seconds.</summary>
        public int LoopSeconds = 0;

        /// <summary>
/// Maximum number of folders scanned simultaneously.
/// </summary>
public int ScannerFolderThreads = 4;

public int InventoryThreads = 8;

/// <summary>
/// Maximum number of files processed simultaneously.
/// </summary>
public int ScannerFileThreads = 8;

/// <summary>
/// Inventory parallel folder processing.
/// </summary>
public int InventoryFolderThreads = 4;

/// <summary>
/// Inventory parallel file processing.
/// </summary>
public int InventoryFileThreads = 8;

/// <summary>
/// Rename jobs running simultaneously.
/// </summary>
public int RenameThreads = 4;

        public string snowflakeHost = "";

        public string SnowflakeAccount = "";

public string SnowflakeUser = "";

public string SnowflakeRole = "";

public string SnowflakeWarehouse = "";

public string SnowflakeAuthenticator = "SNOWFLAKE_JWT";

public string SnowflakePrivateKeyFile = "";

/// <summary>
/// Upload jobs running simultaneously.
/// </summary>
public int UploadThreads = 4;

public int StageUploadThreads = 4;

/// <summary>
/// Maximum number of SnowCLI processes that may execute simultaneously.
/// </summary>
public int SnowCliThreads = 4;


        #endregion
        
        #region SnowCLI Configuration

        /// <summary>
        /// Enable / Disable Snowflake processing.
        /// </summary>
        public bool SnowflakeEnabled = true;

        /// <summary>
/// Full path to Snowflake CLI executable.
/// Example:
/// C:\Program Files\Snowflake CLI\snow.exe
/// </summary>
public string SnowCliPath = "";

/// <summary>
/// Snowflake CLI connection name.
/// This must exist in config.toml.
///
/// Example:
/// MEDUIT_DEV
/// MEDUIT_UAT
/// MEDUIT_PROD
/// </summary>
public string SnowConnection = "";

        /// <summary>
        /// Snowflake Database
        /// </summary>
        public string SnowflakeDatabase = "";

        /// <summary>
        /// Snowflake Schema
        /// </summary>
        public string SnowflakeSchema = "";

        /// <summary>
        /// Landing Stage
        /// </summary>
        public string SnowflakeStage = "";

        /// <summary>
        /// Organized folder produced by Scanner.
        /// </summary>
        public string NormalizedRoot = "";

        /// <summary>
        /// Successfully uploaded files.
        /// </summary>
        public string ArchiveRoot = "";

        /// <summary>
        /// Invalid files.
        /// </summary>
        public string QuarantineRoot = "";

        public int MaxSnowCliProcesses = 8;

        /// <summary>
        /// Snowflake table names.
        /// </summary>
        public string HeaderTable = "FILE_BATCH_HEADER";

        public string FolderTable = "FILE_BATCH_FOLDER";

        public string DetailTable = "FILE_BATCH_DETAIL";

        public string ActivityTable = "FILE_ACTIVITY_LOG";

        #endregion
    }
}
