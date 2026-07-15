using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Data.Common;
using Snowflake.Data.Client;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal sealed class SnowflakeConnectorExecutor :
        ISnowflakeExecutor
    {
        private readonly SnowflakeContext _context;

        private readonly Logger _logger;

        private readonly SnowflakeConnectorConnection _connection;

        private IDbTransaction _transaction;

        public SnowflakeConnectorExecutor(
            SnowflakeContext context,
            Logger logger)
        {
            _context = context;
            _logger = logger;

            _connection =
                new SnowflakeConnectorConnection(
                    context);
        }

        public bool TestConnection()
{
    try
    {
        using (var cmd =
            _connection.CreateCommand())
        {
            cmd.CommandText =
                "SELECT CURRENT_VERSION()";

            cmd.ExecuteScalar();

            return true;
        }
    }
    catch(Exception ex)
{
    _logger.Log(ex.ToString());

    throw;
}
}

        public bool ExecuteSql(string sql)
{
    return ExecuteNonQuery(sql);
}

        public string ExecuteScalar(string sql)
{
    return ExecuteScalarInternal(sql);
}

        public List<string[]> ExecuteQueryRows(string sql)
{
    return ExecuteReaderInternal(sql);
}

public HashSet<string> ExecuteHashSet(
    string sql)
{
    HashSet<string> result =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

    using (var cmd =
        _connection.CreateCommand())
    {
        cmd.CommandText = sql;

        using (var reader =
            cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                result.Add(
                    reader.GetString(0));
            }
        }
    }

    return result;
}

        public bool ExecuteBatch(params string[] sqlStatements)
{
    foreach (string sql in sqlStatements)
    {
        ExecuteNonQuery(sql);
    }

    return true;
}

        public bool ExecuteTransaction(params string[] sqlStatements)
{
    using (IDbTransaction transaction = _connection.BeginTransaction())
    {
        try
        {
            foreach (string sql in sqlStatements)
            {
                using (IDbCommand cmd = _connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = sql;
                    cmd.ExecuteNonQuery();
                }
            }

            transaction.Commit();

            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}

        public bool PutFile(
    string localFile,
    string stageFolder)
{
    if (!File.Exists(localFile))
        throw new FileNotFoundException(localFile);

            string sql =
            string.Format(
                "PUT 'file://{0}' @{1}/{2} AUTO_COMPRESS=FALSE OVERWRITE=TRUE",
                localFile.Replace("\\", "/"),
                _context.Config.Stage,
                stageFolder.Replace("\\", "/"));

            ExecuteNonQuery(sql);

    return true;
}

        private string ExecuteScalarInternal(string sql)
{
    using (var cmd = _connection.CreateCommand())
    {
        if (_transaction != null)
        {
            cmd.Transaction =
                (System.Data.Common.DbTransaction)_transaction;
        }

        cmd.CommandText = sql;

        object value =
            cmd.ExecuteScalar();

        if (value == null ||
            value == DBNull.Value)
        {
            return "";
        }

        return Convert.ToString(value);
    }
}

private List<string[]> ExecuteReaderInternal(string sql)
{
    List<string[]> rows =
        new List<string[]>();

    using (var cmd = _connection.CreateCommand())
    {
        if (_transaction != null)
{
    cmd.Transaction =
        (System.Data.Common.DbTransaction)_transaction;
}

        cmd.CommandText = sql;

        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                string[] values =
                    new string[reader.FieldCount];

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    values[i] =
                        reader.IsDBNull(i)
                            ? ""
                            : reader.GetValue(i).ToString();
                }

                rows.Add(values);
            }
        }
    }

    return rows;
}

        private bool ExecuteNonQuery(string sql)
        {
            EnsureConnection();

            LogSql(sql);

            using (var cmd =
                _connection.CreateCommand())
            {
                if (_transaction != null)
                {
                    cmd.Transaction =
                        (DbTransaction)_transaction;
                }

                cmd.CommandText = sql;

                int rows =
                    cmd.ExecuteNonQuery();

                return rows >= 0;
            }
        }

        public string ListStage(
    string folder)
{
    string sql =
        string.Format(
            "LIST @{0}/{1}",
            _context.Config.Stage,
            folder);

    List<string[]> rows =
        ExecuteReaderInternal(sql);

    StringBuilder builder =
        new StringBuilder();

    foreach (string[] row in rows)
    {
        builder.AppendLine(
            string.Join("|", row));
    }

    return builder.ToString();
}

private void LogSql(
    string sql)
{
    if (string.IsNullOrWhiteSpace(sql))
        return;

    _logger.Log(
        "SQL -> " +
        sql);
}


private void EnsureConnection()
{
    if (_connection.IsOpen)
        return;

    throw new InvalidOperationException(
        "Snowflake connection is closed.");
}

        public bool RemoveStageFile(
    string stageFile)
{
    string sql =
        string.Format(
            "REMOVE @{0}/{1}",
            _context.Config.Stage,
            stageFile);

    ExecuteNonQuery(sql);

    return true;
}

        public bool StageFileExists(
    string stageFolder,
    string fileName)
{
    string sql =
        string.Format(
            "LIST @{0}/{1}",
            _context.Config.Stage,
            stageFolder);

    List<string[]> rows =
        ExecuteReaderInternal(sql);

    foreach (string[] row in rows)
    {
        if (row.Length == 0)
            continue;

        if (row[0].EndsWith(
            "/" + fileName,
            StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

        public void Dispose()
{
    if (_connection != null)
    {
        _connection.Dispose();
    }
}

public void BeginTransaction()
{
    if (_transaction != null)
        return;

    _transaction =
        _connection.BeginTransaction();
}

public void CommitTransaction()
{
    if (_transaction == null)
        return;

    _transaction.Commit();

    _transaction.Dispose();

    _transaction = null;
}

public void RollbackTransaction()
{
    if (_transaction == null)
        return;

    _transaction.Rollback();

    _transaction.Dispose();

    _transaction = null;
}


    }
}