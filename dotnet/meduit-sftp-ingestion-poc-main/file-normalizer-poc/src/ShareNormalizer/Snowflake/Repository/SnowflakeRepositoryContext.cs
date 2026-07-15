using System;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;

namespace Meduit.ShareNormalizer.Snowflake.Repository
{
    /// <summary>
    /// Holds all repositories for one Snowflake execution.
    /// All repositories share the same executor instance.
    /// </summary>
    internal sealed class SnowflakeRepositoryContext
    {
        private readonly HeaderRepository _header;

        private readonly FolderRepository _folder;

        private readonly DetailRepository _detail;

        private readonly ActivityRepository _activity;

        public HeaderRepository Header
        {
            get
            {
                return _header;
            }
        }

        public FolderRepository Folder
        {
            get
            {
                return _folder;
            }
        }

        public DetailRepository Detail
        {
            get
            {
                return _detail;
            }
        }

        public ActivityRepository Activity
        {
            get
            {
                return _activity;
            }
        }

        public SnowflakeRepositoryContext(
            SnowflakeContext context,
            ISnowflakeExecutor executor,
            Logger logger)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            if (executor == null)
                throw new ArgumentNullException("executor");

            if (logger == null)
                throw new ArgumentNullException("logger");

            _header =
                new HeaderRepository(
                    context,
                    executor,
                    logger);

            _folder =
                new FolderRepository(
                    context,
                    executor,
                    logger);

            _detail =
                new DetailRepository(
                    context,
                    executor,
                    logger);

            _activity =
                new ActivityRepository(
                    context,
                    executor,
                    logger);
        }
    }
}