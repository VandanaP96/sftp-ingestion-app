using System;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Repository
{
    internal sealed class FolderRepository
    {
        private readonly SnowflakeContext _context;
        private readonly SnowCliExecutor _executor;
        private readonly Logger _logger;

        public FolderRepository(
            SnowflakeContext context,
            SnowCliExecutor executor,
            Logger logger)
        {
            _context = context;
            _executor = executor;
            _logger = logger;
        }

        public bool Exists(
            long headerId,
            string folderHash)
        {
            string sql =
                SnowflakeSqlBuilder.FolderExists(
                    _context.Config,
                    headerId,
                    folderHash);

            string result =
                _executor.ExecuteScalar(sql);

            return result == "1";
        }

        public bool Insert(
            FolderRecord record)
        {
            string sql =
                SnowflakeSqlBuilder.InsertFolder(
                    _context.Config,
                    record);

            _logger.Log(
                "FOLDER      Creating : " +
                record.FolderPath);

            return _executor.ExecuteSql(sql);
        }

        public long GetFolderId(
            long headerId,
            string folderHash)
        {
            string sql =
                SnowflakeSqlBuilder.GetFolderId(
                    _context.Config,
                    headerId,
                    folderHash);

            string value =
                _executor.ExecuteScalar(sql);

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
            if (!Exists(
                    record.HeaderId,
                    record.FolderHash))
            {
                Insert(record);
            }

            return GetFolderId(
                record.HeaderId,
                record.FolderHash);
        }

        public bool UpdateStatus(
            long folderId,
            string status)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateFolderStatus(
                    _context.Config,
                    folderId,
                    status);

            return _executor.ExecuteSql(sql);
        }

        public bool UpdateApproval(
            long folderId,
            string status,
            string approvedBy)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateFolderApproval(
                    _context.Config,
                    folderId,
                    status,
                    approvedBy);

            return _executor.ExecuteSql(sql);
        }
    }
}