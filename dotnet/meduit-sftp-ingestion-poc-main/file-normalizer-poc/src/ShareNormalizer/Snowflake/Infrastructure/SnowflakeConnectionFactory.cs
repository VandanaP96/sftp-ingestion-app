using System;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal static class SnowflakeConnectionFactory
    {
        public static string BuildConnectionString(
            SnowflakeContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            SnowflakeConfig cfg =
                context.Config;

            return string.Format(
    "account={0};" +
    "host={1};" +
    "user={2};" +
    "authenticator={3};" +
    "private_key_file={4};" +
    "warehouse={5};" +
    "role={6};",

    cfg.Account,
    cfg.Host,
    cfg.User,
    cfg.Authenticator,
    cfg.PrivateKeyFile,
    cfg.Warehouse,
    cfg.Role);
        }
    }
}