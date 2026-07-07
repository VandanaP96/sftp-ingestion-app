using System;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Repository
{
    /// <summary>
    /// Repository responsible for FILE_ACTIVITY_LOG operations.
    /// </summary>
    internal sealed class ActivityRepository
    {
        private readonly SnowflakeContext _context;
        private readonly SnowCliExecutor _executor;
        private readonly Logger _logger;

        public ActivityRepository(
            SnowflakeContext context,
            SnowCliExecutor executor,
            Logger logger)
        {
            _context = context;
            _executor = executor;
            _logger = logger;
        }

        /// <summary>
        /// Inserts a processing activity.
        /// </summary>
        public bool Insert(
            ActivityRecord record)
        {
            string sql =
                SnowflakeSqlBuilder.InsertActivity(
                    _context.Config,
                    record);

            _logger.Log(
                "ACTIVITY   " +
                record.ActivityType +
                " : " +
                record.ActivityStatus);

            return _executor.ExecuteSql(sql);
        }

        /// <summary>
        /// Updates activity status.
        /// </summary>
        public bool UpdateStatus(
            long activityId,
            string status)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateActivityStatus(
                    _context.Config,
                    activityId,
                    status);

            return _executor.ExecuteSql(sql);
        }

        /// <summary>
        /// Updates activity failure.
        /// </summary>
        public bool UpdateError(
            long activityId,
            string errorCode,
            string errorMessage)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateActivityError(
                    _context.Config,
                    activityId,
                    errorCode,
                    errorMessage);

            return _executor.ExecuteSql(sql);
        }
    }
}