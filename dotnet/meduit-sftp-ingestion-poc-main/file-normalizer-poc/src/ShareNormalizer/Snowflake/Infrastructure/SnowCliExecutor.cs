using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;


namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    /// <summary>
    /// Executes Snowflake CLI (snow.exe) commands.
    /// This class is the single gateway between the application
    /// and Snowflake CLI.
    /// </summary>
    internal sealed class SnowCliExecutor : ISnowflakeExecutor
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
            ProcessRunner.ProcessResult result =
                _runner.Execute(
                    _context.Config.SnowCliPath,
                    "--version");

            return result.Success;
        }

        /// <summary>
        /// Executes INSERT/UPDATE/DELETE/MERGE.
        /// </summary>
        public bool ExecuteSql(string sql)
{
    throw new NotSupportedException(
        "SQL execution is handled by SnowflakeConnectorExecutor.");
}

        /// <summary>
        /// Executes a SELECT statement.
        /// </summary>
        public string ExecuteQuery(string sql)
{
    throw new NotSupportedException(
        "SQL execution is handled by SnowflakeConnectorExecutor.");
}

        /// <summary>
/// Executes multiple SQL statements
/// inside one SnowCLI session.
/// </summary>
public bool ExecuteBatch(
    params string[] sqlStatements)
{
    throw new NotSupportedException(
        "Batch SQL execution is handled by SnowflakeConnectorExecutor.");
}


/// <summary>
/// Executes SQL without expecting output.
/// </summary>
public bool ExecuteBatchNonQuery(
    params string[] sqlStatements)
{
    throw new NotSupportedException(
        "Batch SQL execution is handled by SnowflakeConnectorExecutor.");
}


/// <summary>
/// Executes SQL transaction.
/// </summary>
public bool ExecuteTransaction(
    params string[] sqlStatements)
{
    throw new NotSupportedException(
        "Transactions are handled by SnowflakeConnectorExecutor.");
}

        /// <summary>
/// Executes a scalar query and returns the first data value.
/// Compatible with Snowflake CLI output.
/// </summary>
public string ExecuteScalar(string sql)
{
    throw new NotSupportedException(
        "Scalar queries are handled by SnowflakeConnectorExecutor.");
}

        /// <summary>
        /// Executes a query and returns rows.
        /// </summary>
        public List<string[]> ExecuteQueryRows(
    string sql)
{
    throw new NotSupportedException(
        "Queries are handled by SnowflakeConnectorExecutor.");
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
            builder.Append("--format JSON ");

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
        private List<string[]> ParseCliTable(string output)
{
    List<string[]> rows =
        new List<string[]>();

    if (string.IsNullOrWhiteSpace(output))
        return rows;

    JavaScriptSerializer serializer =
        new JavaScriptSerializer();

    object obj =
        serializer.DeserializeObject(output);

    object[] records =
        obj as object[];

    if (records == null)
        return rows;

    bool headerAdded = false;

    foreach (Dictionary<string, object> record
                in records)
    {
        if (!headerAdded)
        {
            rows.Add(
                new List<string>(record.Keys)
                    .ToArray());

            headerAdded = true;
        }

        List<string> values =
            new List<string>();

        foreach (object value
                    in record.Values)
        {
            values.Add(
                value == null
                    ? ""
                    : value.ToString());
        }

        rows.Add(values.ToArray());
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
                throw new ArgumentNullException(nameof(localFile));

            if (!File.Exists(localFile))
                throw new FileNotFoundException(
                    "Upload file not found.",
                    localFile);

            if (string.IsNullOrWhiteSpace(stageFolder))
                throw new ArgumentNullException(nameof(stageFolder));

            stageFolder =
                stageFolder
                    .Replace("\\", "/")
                    .Trim('/');

            string stagePath =
                string.Format(
                    "@{0}/{1}",
                    _context.Config.Stage,
                    stageFolder);

            string arguments =
                string.Format(
                    "stage copy --connection {0} \"{1}\" \"{2}\" --overwrite --no-auto-compress",
                    _context.Config.SnowConnection,
                    localFile,
                    stagePath);

            _logger.Log("");
            _logger.Log("=======================================");
            _logger.Log("SNOWCLI UPLOAD");
            _logger.Log("LOCAL FILE : " + localFile);
            _logger.Log("STAGE PATH : " + stagePath);
            _logger.Log("COMMAND    : snow " + arguments);
            _logger.Log("=======================================");

            ProcessRunner.ProcessResult result =
                _runner.Execute(
                    _context.Config.SnowCliPath,
                    arguments);

            _logger.Log("");
            _logger.Log("========== SNOWCLI OUTPUT ==========");

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
                _logger.Log(result.StandardOutput);

            if (!string.IsNullOrWhiteSpace(result.StandardError))
                _logger.Log(result.StandardError);

            _logger.Log("Exit Code : " + result.ExitCode);
            _logger.Log("====================================");

            if (!result.Success)
            {
                throw new ApplicationException(
                    string.IsNullOrWhiteSpace(result.StandardError)
                        ? result.StandardOutput
                        : result.StandardError);
            }

            return true;
        }

        /// <summary>
        /// Executes LIST command on stage.
        /// </summary>
        public string ListStage(
    string folder)
        {
            string arguments =
                string.Format(
                    "stage list --connection {0} @{1}/{2}",
                    _context.Config.SnowConnection,
                    _context.Config.Stage,
                    folder.Replace("\\", "/"));

            ProcessRunner.ProcessResult result =
                _runner.Execute(
                    _context.Config.SnowCliPath,
                    arguments);

            if (!result.Success)
            {
                throw new ApplicationException(
                    result.StandardError);
            }

            return result.StandardOutput;
        }

        /// <summary>
        /// Removes a staged file.
        /// </summary>
        public bool RemoveStageFile(
    string stageFile)
        {
            string arguments =
                string.Format(
                    "stage remove --connection {0} @{1}/{2}",
                    _context.Config.SnowConnection,
                    _context.Config.Stage,
                    stageFile.Replace("\\", "/"));

            ProcessRunner.ProcessResult result =
                _runner.Execute(
                    _context.Config.SnowCliPath,
                    arguments);

            return result.Success;
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

            return output.IndexOf(
                fileName,
                StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Initializes database and schema.
        /// </summary>
        public bool InitializeSession()
{
    return true;
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

        public void Dispose()
{
    // Nothing to dispose.
}

public void BeginTransaction()
{
    // SnowCLI executes one command at a time.
    // Transactions are not supported.
}

public void CommitTransaction()
{
    // No transaction support for SnowCLI.
}

public void RollbackTransaction()
{
    // No transaction support for SnowCLI.
}

public HashSet<string> ExecuteHashSet(
    string sql)
{
    throw new NotSupportedException(
        "ExecuteHashSet is only supported by the .NET Snowflake connector.");
}

    }
}