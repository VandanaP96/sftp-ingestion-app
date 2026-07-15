using System;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal static class SnowflakeSessionFactory
    {
        public static SnowflakeSession Create(
            SnowflakeContext context,
            ProcessRunner runner,
            Logger logger)
        {
            SnowflakeExecutorFactory factory =
                new SnowflakeExecutorFactory(
                    context,
                    runner,
                    logger);

            return new SnowflakeSession(
                factory.SqlExecutor);
        }
    }
}