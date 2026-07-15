using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Repository;

namespace Meduit.ShareNormalizer.Snowflake.Services
{
    internal abstract class BaseSnowflakeService
    {
        protected readonly Config Config;

        protected readonly Logger Logger;

        protected readonly SnowflakeContext Context;

        protected readonly ISnowflakeExecutor Executor;

        protected readonly SnowflakeRepositoryContext Repository;

        protected BaseSnowflakeService(
    Config config,
    Logger logger)
{
    Config = config;

    Logger = logger;

    Context =
        new SnowflakeContext(
            config,
            logger);

    ProcessRunner runner =
        new ProcessRunner(
            config,
            logger);

    SnowflakeExecutorFactory factory =
        new SnowflakeExecutorFactory(
            Context,
            runner,
            logger);

    Executor =
        factory.SqlExecutor;

    Repository =
        new SnowflakeRepositoryContext(
            Context,
            Executor,
            logger);
}
    }
}