using System;
using System.IO;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    /// <summary>
    /// Holds the runtime Snowflake configuration.
    /// This class converts the application's Config object into a
    /// strongly typed SnowflakeConfig and validates mandatory settings.
    /// </summary>
    internal sealed class SnowflakeContext
    {
        public SnowflakeConfig Config { get; private set; }

        public Logger Logger { get; private set; }

        public SnowflakeContext(Config cfg, Logger logger)
        {
            if (cfg == null)
                throw new ArgumentNullException("cfg");

            if (logger == null)
                throw new ArgumentNullException("logger");

            Logger = logger;

            Config = new SnowflakeConfig
{
                Host = cfg.snowflakeHost,

                Account = cfg.SnowflakeAccount,

                User = cfg.SnowflakeUser,

                Warehouse = cfg.SnowflakeWarehouse,

                Role = cfg.SnowflakeRole,

                Authenticator = cfg.SnowflakeAuthenticator,

                PrivateKeyFile = cfg.SnowflakePrivateKeyFile,

                Enabled = cfg.SnowflakeEnabled,

    SnowCliPath = cfg.SnowCliPath,

    SnowConnection = cfg.SnowConnection,

    Database = cfg.SnowflakeDatabase,

    Schema = cfg.SnowflakeSchema,

    Stage = cfg.SnowflakeStage,

    NormalizedRoot = cfg.NormalizedRoot,

    ArchiveRoot = cfg.ArchiveRoot,

    QuarantineRoot = cfg.QuarantineRoot,

    HeaderTable = cfg.HeaderTable,

    FolderTable = cfg.FolderTable,

    DetailTable = cfg.DetailTable,

    ActivityTable = cfg.ActivityTable
};
        }

        /// <summary>
        /// Validate all mandatory configuration before processing starts.
        /// </summary>
        public void Validate()
        {
            if (!Config.Enabled)
            {
                Logger.Log("SNOWFLAKE  Disabled in configuration.");
                return;
            }

            if (string.IsNullOrWhiteSpace(Config.SnowCliPath))
                throw new InvalidOperationException("snowCliPath is missing in normalizer.conf.");

            if (!File.Exists(Config.SnowCliPath))
                throw new FileNotFoundException(
                    "Snowflake CLI executable not found.",
                    Config.SnowCliPath);

            if (string.IsNullOrWhiteSpace(Config.SnowConnection))
                throw new InvalidOperationException(
                    "snowConnection is missing in normalizer.conf.");

            if (string.IsNullOrWhiteSpace(Config.Database))
                throw new InvalidOperationException(
                    "snowflakeDatabase is missing in normalizer.conf.");

            if (string.IsNullOrWhiteSpace(Config.Schema))
                throw new InvalidOperationException(
                    "snowflakeSchema is missing in normalizer.conf.");

            if (string.IsNullOrWhiteSpace(Config.Stage))
                throw new InvalidOperationException(
                    "snowflakeStage is missing in normalizer.conf.");

            if (string.IsNullOrWhiteSpace(Config.NormalizedRoot))
                throw new InvalidOperationException(
                    "normalizedRoot is missing in normalizer.conf.");

            if (string.IsNullOrWhiteSpace(Config.ArchiveRoot))
                throw new InvalidOperationException(
                    "archiveRoot is missing in normalizer.conf.");

            if (string.IsNullOrWhiteSpace(Config.QuarantineRoot))
                throw new InvalidOperationException(
                    "quarantineRoot is missing in normalizer.conf.");

        }

        /// <summary>
        /// Returns the fully qualified stage path.
        /// </summary>
        public string GetStageName()
        {
            return "@" + Config.Stage;
        }

        /// <summary>
        /// Returns the fully qualified database.schema.
        /// </summary>
        public string GetDatabaseSchema()
        {
            return Config.Database + "." + Config.Schema;
        }
    }
}