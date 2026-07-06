using System;
using System.Collections.Generic;
using System.Text;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    /// <summary>
    /// Executes Snowflake CLI (snow.exe) commands.
    /// This class is the single gateway between the application
    /// and Snowflake CLI.
    /// </summary>
    internal sealed class SnowCliExecutor
    {
        private readonly SnowflakeContext _context;

        private readonly ProcessRunner _runner;

        private readonly Logger _logger;

        public SnowCliExecutor(
            SnowflakeContext context,
            ProcessRunner runner,
            Logger logger)
        {
            _context = context;

            _runner = runner;

            _logger = logger;
        }

        /// <summary>
        /// Verifies Snowflake CLI installation and connection.
        /// </summary>
        public bool TestConnection()
        {
            _logger.Log("");
            _logger.Log("SNOWCLI     Testing connection...");

            ProcessRunner.ProcessResult result =
                _runner.Execute(
                    _context.Config.SnowCliPath,
                    "--version");

            if (!result.Success)
            {
                _logger.Log(
                    "SNOWCLI     Unable to execute Snowflake CLI.");

                return false;
            }

            result =
                ExecuteCliSql(
                    "SELECT CURRENT_VERSION();");

            if (!result.Success)
            {
                _logger.Log(
                    "SNOWCLI     Connection test failed.");

                _logger.Log(
                    result.StandardError);

                return false;
            }

            _logger.Log(
                "SNOWCLI     Connection successful.");

            return true;
        }

        /// <summary>
        /// Executes INSERT/UPDATE/DELETE/MERGE.
        /// </summary>
        public bool ExecuteSql(
            string sql)
        {
            ProcessRunner.ProcessResult result =
                ExecuteCliSql(sql);

            return result.Success;
        }

        /// <summary>
        /// Executes a SELECT statement.
        /// </summary>
        public string ExecuteQuery(
            string sql)
        {
            ProcessRunner.ProcessResult result =
                ExecuteCliSql(sql);

            if (!result.Success)
            {
                throw new Exception(
                    result.StandardError);
            }

            return result.StandardOutput;
        }

        /// <summary>
/// Executes a scalar query and returns the first data value.
/// Compatible with Snowflake CLI output.
/// </summary>
public string ExecuteScalar(string sql)
{
    string output = ExecuteQuery(sql);

    if (string.IsNullOrWhiteSpace(output))
        return "";

    _logger.Log("SNOWCLI RAW OUTPUT");
    _logger.Log(output);

    string[] lines =
        output.Split(
            new[]
            {
                Environment.NewLine
            },
            StringSplitOptions.RemoveEmptyEntries);

    bool firstPipeSeen = false;

    foreach (string line in lines)
    {
        string value = line.Trim();

        if (value.Length == 0)
            continue;

        if (value.StartsWith("SELECT"))
            continue;

        if (value.StartsWith("+"))
            continue;

        if (!value.StartsWith("|"))
            continue;

        // first |.....| is always column header
        if (!firstPipeSeen)
        {
            firstPipeSeen = true;
            continue;
        }

        // second |-----| separator
        if (value.Contains("---"))
            continue;

        string cleaned =
            value.Replace("|", "").Trim();

        if (cleaned.Length == 0)
            continue;

        _logger.Log(
            "SNOWCLI SCALAR = " +
            cleaned);

        return cleaned;
    }

    return "";
}

        /// <summary>
        /// Executes a query and returns rows.
        /// </summary>
        public List<string[]> ExecuteQueryRows(
            string sql)
        {
            string output =
                ExecuteQuery(sql);

            return ParseCliTable(output);
        }

        /// <summary>
        /// Executes SQL using Snowflake CLI.
        /// </summary>
        private ProcessRunner.ProcessResult ExecuteCliSql(
            string sql)
        {
            string arguments =
                BuildSqlArguments(sql);

            _logger.Log(
                "SNOWCLI     Executing SQL...");

            return _runner.Execute(
                _context.Config.SnowCliPath,
                arguments);
        }

        /// <summary>
        /// Builds Snowflake CLI SQL arguments.
        /// </summary>
        private string BuildSqlArguments(
            string sql)
        {
            StringBuilder builder =
                new StringBuilder();

            builder.Append("sql ");

            builder.Append("--connection ");

            builder.Append(
                _context.Config.SnowConnection);

            builder.Append(" ");

            builder.Append("--query ");

            builder.Append("\"");

            builder.Append(
                Escape(sql));

            builder.Append("\"");

            return builder.ToString();
        }

                /// <summary>
        /// Parses Snowflake CLI table output into rows.
        /// </summary>
        private List<string[]> ParseCliTable(
            string output)
        {
            List<string[]> rows =
                new List<string[]>();

            if (string.IsNullOrWhiteSpace(output))
                return rows;

            string[] lines =
                output.Split(
                    new[]
                    {
                        Environment.NewLine
                    },
                    StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string value =
                    line.Trim();

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                // Skip command echo
                if (value.StartsWith("SELECT ",
                    StringComparison.OrdinalIgnoreCase))
                    continue;

                // Skip separator lines
                if (value.StartsWith("+"))
                    continue;

                // Only table rows begin with |
                if (!value.StartsWith("|"))
                    continue;

                string[] parts =
                    value.Split('|');

                List<string> columns =
                    new List<string>();

                for (int i = 1; i < parts.Length - 1; i++)
                {
                    columns.Add(
                        parts[i].Trim());
                }

                if (columns.Count > 0)
                {
                    rows.Add(
                        columns.ToArray());
                }
            }

            return rows;
        }

        /// <summary>
        /// Uploads a local file into the configured stage.
        /// </summary>
        public bool PutFile(
            string localFile,
            string stageFolder)
        {
            if (string.IsNullOrWhiteSpace(localFile))
                throw new ArgumentNullException("localFile");

            if (string.IsNullOrWhiteSpace(stageFolder))
                throw new ArgumentNullException("stageFolder");

            string sql =
                string.Format(
                    "PUT 'file://{0}' @{1}/{2} AUTO_COMPRESS=FALSE OVERWRITE=TRUE;",
                    localFile.Replace("\\", "/"),
                    _context.Config.Stage,
                    stageFolder.Replace("\\", "/"));

            _logger.Log("");

            _logger.Log(
                "SNOWCLI     Uploading file");

            _logger.Log(
                localFile);

            bool success =
                ExecuteSql(sql);

            if (!success)
            {
                _logger.Log(
                    "SNOWCLI     Upload failed.");

                return false;
            }

            _logger.Log(
                "SNOWCLI     Upload successful.");

            return true;
        }

        /// <summary>
        /// Executes LIST command on stage.
        /// </summary>
        public string ListStage(
            string folder)
        {
            string sql =
                string.Format(
                    "LIST @{0}/{1};",
                    _context.Config.Stage,
                    folder.Replace("\\", "/"));

            return ExecuteQuery(sql);
        }

        /// <summary>
        /// Removes a staged file.
        /// </summary>
        public bool RemoveStageFile(
            string stageFile)
        {
            string sql =
                string.Format(
                    "REMOVE @{0}/{1};",
                    _context.Config.Stage,
                    stageFile.Replace("\\", "/"));

            return ExecuteSql(sql);
        }

        /// <summary>
        /// Checks whether the specified file already exists
        /// inside the stage.
        /// </summary>
        public bool StageFileExists(
            string stageFolder,
            string fileName)
        {
            string output =
                ListStage(stageFolder);

            if (string.IsNullOrWhiteSpace(output))
                return false;

            return
                output.IndexOf(
                    fileName,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Initializes database and schema.
        /// </summary>
        public bool InitializeSession()
        {
            StringBuilder sql =
                new StringBuilder();

            sql.Append("USE DATABASE ");
            sql.Append(_context.Config.Database);
            sql.Append(";");

            sql.Append("USE SCHEMA ");
            sql.Append(_context.Config.Schema);
            sql.Append(";");

            return ExecuteSql(
                sql.ToString());
        }

                /// <summary>
        /// Executes any Snowflake CLI command directly.
        /// Useful for administration commands.
        /// </summary>
        public bool ExecuteCommand(
            string arguments)
        {
            ProcessRunner.ProcessResult result =
                _runner.Execute(
                    _context.Config.SnowCliPath,
                    arguments);

            return result.Success;
        }

        /// <summary>
        /// Returns current Snowflake server timestamp.
        /// </summary>
        public DateTime GetServerTime()
        {
            string value =
                ExecuteScalar(
                    "SELECT CURRENT_TIMESTAMP();");

            DateTime dt;

            if (DateTime.TryParse(value, out dt))
                return dt;

            return DateTime.Now;
        }

        /// <summary>
        /// Returns current Snowflake user.
        /// </summary>
        public string CurrentUser()
        {
            return ExecuteScalar(
                "SELECT CURRENT_USER();");
        }

        /// <summary>
        /// Returns current warehouse.
        /// </summary>
        public string CurrentWarehouse()
        {
            return ExecuteScalar(
                "SELECT CURRENT_WAREHOUSE();");
        }

        /// <summary>
        /// Returns current role.
        /// </summary>
        public string CurrentRole()
        {
            return ExecuteScalar(
                "SELECT CURRENT_ROLE();");
        }

        /// <summary>
        /// Returns current database.
        /// </summary>
        public string CurrentDatabase()
        {
            return ExecuteScalar(
                "SELECT CURRENT_DATABASE();");
        }

        /// <summary>
        /// Returns current schema.
        /// </summary>
        public string CurrentSchema()
        {
            return ExecuteScalar(
                "SELECT CURRENT_SCHEMA();");
        }

        /// <summary>
        /// Escapes SQL text before sending to Snowflake CLI.
        /// </summary>
        private static string Escape(
            string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return "";

            return sql
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }
}