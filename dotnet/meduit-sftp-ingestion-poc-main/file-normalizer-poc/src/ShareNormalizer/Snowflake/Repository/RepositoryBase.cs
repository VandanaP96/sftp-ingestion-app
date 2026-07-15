using System;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;

namespace Meduit.ShareNormalizer.Snowflake.Repository
{
    internal abstract class RepositoryBase
    {
        protected readonly SnowflakeContext Context;

        protected readonly Logger Logger;

        protected readonly ISnowflakeExecutor Executor;

        protected RepositoryBase(
    SnowflakeContext context,
    ISnowflakeExecutor executor,
    Logger logger)
{
    Context = context;

    Executor = executor;

    Logger = logger;
}
    }
}