using System;
using System.Data;
using Snowflake.Data.Client;
using System.Data.Common;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal sealed class SnowflakeConnectorConnection
        : IDisposable
    {
        private readonly SnowflakeDbConnection _connection;

        public SnowflakeDbConnection Connection
        {
            get
            {
                return _connection;
            }
        }

        public DbCommand CreateCommand()
{
    return _connection.CreateCommand();
}

public IDbTransaction BeginTransaction()
{
    return _connection.BeginTransaction();
}

public bool IsOpen
{
    get
    {
        return _connection.State == ConnectionState.Open;
    }
}

        public SnowflakeConnectorConnection(
            SnowflakeContext context)
        {
            string connectionString =
    SnowflakeConnectionFactory
        .BuildConnectionString(
            context);

context.Logger.Log("");
context.Logger.Log("SNOWFLAKE CONNECTION STRING:");
context.Logger.Log(connectionString);

try
{
    _connection =
        new SnowflakeDbConnection(
            connectionString);

    _connection.Open();

                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText =
                        $"USE DATABASE {context.Config.Database}";
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = _connection.CreateCommand())
                {
                    cmd.CommandText =
                        $"USE SCHEMA {context.Config.Schema}";
                    cmd.ExecuteNonQuery();
                }

                context.Logger.Log(
        "Snowflake .NET connection opened successfully.");
}
catch (Exception ex)
{
    context.Logger.Log(
        "Snowflake connection failed:");

    context.Logger.Log(
        ex.ToString());

    throw;
}
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                _connection.Close();
                _connection.Dispose();
            }
        }
    }
}