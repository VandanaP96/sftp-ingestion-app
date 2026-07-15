using System;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal sealed class SnowflakeOperationRouter
    {
        private readonly SnowflakeContext _context;

        private readonly ProcessRunner _runner;

        private readonly Logger _logger;

        private readonly SnowCliExecutor _cli;

        private readonly SnowflakeConnectorExecutor _connector;

        public SnowflakeOperationRouter(
            SnowflakeContext context,
            ProcessRunner runner,
            Logger logger)
        {
            _context = context;

            _runner = runner;

            _logger = logger;

            _cli =
                new SnowCliExecutor(
                    context,
                    runner,
                    logger);

            _connector =
                new SnowflakeConnectorExecutor(
                    context,
                    logger);
        }

        public ISnowflakeExecutor Route(
            SnowflakeOperation operation)
        {
            switch (operation)
            {
                case SnowflakeOperation.PutFile:

                case SnowflakeOperation.ListStage:

                case SnowflakeOperation.RemoveStage:

                case SnowflakeOperation.StageExists:

                    return _cli;

                default:

                    //
                    // For now use CLI.
                    // Later we'll change this to .NET
                    // operation by operation.
                    //
                    return _cli;
            }
        }
    }
}