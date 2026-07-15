using System;

namespace Meduit.ShareNormalizer.Snowflake.Models
{
    /// <summary>
    /// Strongly typed Snowflake configuration.
    /// Loaded from Config.cs once during startup.
    /// </summary>
    internal sealed class SnowflakeConfig
    {
        /// <summary>
        /// Enable / Disable Snowflake processing.
        /// </summary>
        public bool Enabled { get; set; }

        public string Account { get; set; }

        public string Host { get; set; }

        public string User { get; set; }

        public string Warehouse { get; set; }

        public string Role { get; set; }

        public string Authenticator { get; set; }

        public string PrivateKeyFile { get; set; }

        public string SnowCliPath { get; set; }

        public string SnowConnection { get; set; }

        /// <summary>
        /// Database name.
        /// </summary>
        public string Database { get; set; }

        /// <summary>
        /// Schema name.
        /// </summary>
        public string Schema { get; set; }

        /// <summary>
        /// Internal stage name.
        /// Example:
        /// LANDING
        /// </summary>
        public string Stage { get; set; }

        /// <summary>
        /// Root folder produced by ShareNormalizer.
        /// </summary>
        public string NormalizedRoot { get; set; }

        /// <summary>
        /// Successfully uploaded files are moved here.
        /// </summary>
        public string ArchiveRoot { get; set; }

        /// <summary>
        /// Invalid files are moved here.
        /// </summary>
        public string QuarantineRoot { get; set; }

        /// <summary>
        /// Header table.
        /// </summary>
        public string HeaderTable { get; set; }

        /// <summary>
        /// Folder table.
        /// </summary>
        public string FolderTable { get; set; }

        /// <summary>
        /// Detail table.
        /// </summary>
        public string DetailTable { get; set; }

        /// <summary>
        /// Activity log table.
        /// </summary>
        public string ActivityTable { get; set; }

        /// <summary>
        /// Fully qualified header table.
        /// </summary>
        public string FullHeaderTable
        {
            get
            {
                return Database + "." + Schema + "." + HeaderTable;
            }
        }

        /// <summary>
        /// Fully qualified folder table.
        /// </summary>
        public string FullFolderTable
        {
            get
            {
                return Database + "." + Schema + "." + FolderTable;
            }
        }

        /// <summary>
        /// Fully qualified detail table.
        /// </summary>
        public string FullDetailTable
        {
            get
            {
                return Database + "." + Schema + "." + DetailTable;
            }
        }

        /// <summary>
        /// Fully qualified activity table.
        /// </summary>
        public string FullActivityTable
        {
            get
            {
                return Database + "." + Schema + "." + ActivityTable;
            }
        }
    }
}