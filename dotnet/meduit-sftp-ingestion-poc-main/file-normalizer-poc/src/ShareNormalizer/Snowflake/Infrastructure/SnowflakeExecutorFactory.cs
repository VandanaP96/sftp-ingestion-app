using System;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal sealed class SnowflakeExecutorFactory
    {
        private readonly SnowflakeConnectorExecutor _sqlExecutor;

        private readonly SnowCliExecutor _stageExecutor;

        public SnowflakeExecutorFactory(
            SnowflakeContext context,
            ProcessRunner runner,
            Logger logger)
        {
            _sqlExecutor =
                new SnowflakeConnectorExecutor(
                    context,
                    logger);

            _stageExecutor =
                new SnowCliExecutor(
                    context,
                    runner,
                    logger);
        }

        public ISnowflakeExecutor SqlExecutor
        {
            get
            {
                return _sqlExecutor;
            }
        }

        public ISnowflakeExecutor StageExecutor
        {
            get
            {
                return _stageExecutor;
            }
        }
    }
}