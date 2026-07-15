using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Repository
{
    /// <summary>
    /// Repository responsible for FILE_ACTIVITY_LOG operations.
    /// </summary>
    internal sealed class ActivityRepository : RepositoryBase
    {

        public ActivityRepository(
    SnowflakeContext context,
    ISnowflakeExecutor executor,
    Logger logger)
    : base(
        context,
        executor,
        logger)
{
}

        /// <summary>
        /// Inserts a processing activity.
        /// </summary>
        public bool Insert(
            ActivityRecord record)
        {
            string sql =
    SnowflakeSqlBuilder.InsertActivity(
        Context.Config,
        record);

return Executor.ExecuteSql(sql);
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
                    Context.Config,
                    activityId,
                    status);

            return Executor.ExecuteSql(sql);
        }

        public void InsertBatch(
    IEnumerable<ActivityRecord> activities)
{
    List<ActivityRecord> list =
        new List<ActivityRecord>(activities);

    if (list.Count == 0)
    {
        return;
    }

    Executor.BeginTransaction();

    try
    {
        foreach (ActivityRecord activity in list)
        {
            Insert(activity);
        }

        Executor.CommitTransaction();
    }
    catch
    {
        Executor.RollbackTransaction();

        throw;
    }
}

public void InsertBatchTransaction(
    IEnumerable<ActivityRecord> activities)
{
    List<ActivityRecord> activityList =
        new List<ActivityRecord>(activities);

    if (activityList.Count == 0)
        return;

    Logger.Log(
        "Writing "
        + activityList.Count
        + " activity records...");

    Executor.BeginTransaction();

    try
    {
        foreach (ActivityRecord activity in activityList)
        {
            Insert(activity);
        }

        Executor.CommitTransaction();

        Logger.Log(
            "Activity batch committed.");
    }
    catch
    {
        Executor.RollbackTransaction();

        throw;
    }
}


public bool ExecuteBatchInsert(
    IEnumerable<ActivityRecord> records)
{
    StringBuilder builder =
        new StringBuilder();

    bool first = true;

    foreach (ActivityRecord activity in records)
    {
        if (!first)
        {
            builder.AppendLine(";");
        }

        builder.Append(
            SnowflakeSqlBuilder.InsertActivity(
                Context.Config,
                activity));

        first = false;
    }

    return
        Executor.ExecuteSql(
            builder.ToString());
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
                    Context.Config,
                    activityId,
                    errorCode,
                    errorMessage);

            return Executor.ExecuteSql(sql);
        }


        private static List<ActivityRecord> ToList(
    IEnumerable<ActivityRecord> activities)
{
    if (activities is List<ActivityRecord>)
        return
            (List<ActivityRecord>)activities;

    return
        new List<ActivityRecord>(
            activities);
}

    }
}