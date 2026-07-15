using System;
using System.Collections.Concurrent;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Repository
{
    internal sealed class FolderRepository : RepositoryBase
    {

        private readonly ConcurrentDictionary<string,long>
    _folderCache =
        new ConcurrentDictionary<string,long>();

        public FolderRepository(
    SnowflakeContext context,
    ISnowflakeExecutor executor,
    Logger logger)
    : base(
        context,
        executor,
        logger)
{
}

        public bool Exists(
            long headerId,
            string folderHash)
        {
            string sql =
                SnowflakeSqlBuilder.FolderExists(
                    Context.Config,
                    headerId,
                    folderHash);

            string result =
                Executor.ExecuteScalar(sql);

            return result == "1";
        }

        public bool Insert(
            FolderRecord record)
        {
            string sql =
                SnowflakeSqlBuilder.InsertFolder(
                    Context.Config,
                    record);

            Logger.Log(
                "FOLDER      Creating : " +
                record.FolderPath);

            return Executor.ExecuteSql(sql);
        }

        public long GetFolderId(
            long headerId,
            string folderHash)
        {
            string sql =
                SnowflakeSqlBuilder.GetFolderId(
                    Context.Config,
                    headerId,
                    folderHash);

            string value =
                Executor.ExecuteScalar(sql);

            long id;

            if (!long.TryParse(value, out id))
                return 0;

            return id;
        }

        /// <summary>
        /// Creates folder if required and always returns FOLDER_ID.
        /// </summary>
        public long GetOrCreate(
    FolderRecord record)
        {
            string key =
                record.HeaderId +
                "|" +
                record.FolderHash;

            long id;

            if (_folderCache.TryGetValue(
                    key,
                    out id))
            {
                return id;
            }

            lock (_folderCache)
            {
                if (_folderCache.TryGetValue(
                        key,
                        out id))
                {
                    return id;
                }

                if (!Exists(
                        record.HeaderId,
                        record.FolderHash))
                {
                    Insert(record);
                }

                id =
                    GetFolderId(
                        record.HeaderId,
                        record.FolderHash);

                _folderCache[key] = id;

                return id;
            }
        }

        public bool UpdateStatus(
            long folderId,
            string status)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateFolderStatus(
                    Context.Config,
                    folderId,
                    status);

            return Executor.ExecuteSql(sql);
        }

        public bool UpdateApproval(
            long folderId,
            string status,
            string approvedBy)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateFolderApproval(
                    Context.Config,
                    folderId,
                    status,
                    approvedBy);

            return Executor.ExecuteSql(sql);
        }
    }
}