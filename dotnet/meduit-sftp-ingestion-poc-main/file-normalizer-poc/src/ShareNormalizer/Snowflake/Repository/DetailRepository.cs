using System;
using System.Collections.Generic;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Mappers;
using Meduit.ShareNormalizer.Snowflake.Models;


namespace Meduit.ShareNormalizer.Snowflake.Repository
{
    /// <summary>
    /// Repository responsible for FILE_BATCH_DETAIL operations.
    /// </summary>
    internal sealed class DetailRepository
    {
        private readonly SnowflakeContext _context;

        private readonly SnowCliExecutor _executor;

        private readonly Logger _logger;

        public DetailRepository(
            SnowflakeContext context,
            SnowCliExecutor executor,
            Logger logger)
        {
            _context = context;
            _executor = executor;
            _logger = logger;
        }

        /// <summary>
        /// Checks whether the file already exists.
        /// </summary>
        public bool Exists(
            long folderId,
            string fileHash)
        {
            string sql =
                SnowflakeSqlBuilder.DetailExists(
                    _context.Config,
                    folderId,
                    fileHash);

            string result =
                _executor.ExecuteScalar(sql);

            return result == "1";
        }

        /// <summary>
        /// Inserts metadata and returns DETAIL_ID.
        /// </summary>
        public long Insert(
            DetailRecord record)
        {
            string insertSql =
                SnowflakeSqlBuilder.InsertDetail(
                    _context.Config,
                    record);

            _logger.Log(
                "DETAIL      Registering : "
                + record.CurrentFileName);

            bool inserted =
                _executor.ExecuteSql(
                    insertSql);

            if (!inserted)
                return 0;

            string lookupSql =
                SnowflakeSqlBuilder.GetDetailId(
                    _context.Config,
                    record.FolderId,
                    record.FileHash);

            string result =
                _executor.ExecuteScalar(
                    lookupSql);

            long detailId;

            if (!long.TryParse(
                    result,
                    out detailId))
            {
                return 0;
            }

            return detailId;
        }

        /// <summary>
        /// Updates approval status.
        /// </summary>
        public bool UpdateApproval(
            long detailId,
            string status,
            string approvedBy)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateApprovalStatus(
                    _context.Config,
                    detailId,
                    status,
                    approvedBy);

            return
                _executor.ExecuteSql(sql);
        }

        /// <summary>
        /// Marks upload started.
        /// </summary>
        public bool UpdateIngestionStart(
            long detailId)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateIngestionStart(
                    _context.Config,
                    detailId);

            return
                _executor.ExecuteSql(sql);
        }

        /// <summary>
        /// Marks upload completed.
        /// </summary>
        public bool UpdateIngestionStatus(
            long detailId,
            string status,
            long rowCount)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateIngestionStatus(
                    _context.Config,
                    detailId,
                    status,
                    rowCount);

            return
                _executor.ExecuteSql(sql);
        }

        /// <summary>
        /// Marks upload failed.
        /// </summary>
        public bool UpdateError(
            long detailId,
            string error)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateError(
                    _context.Config,
                    detailId,
                    error);

            return
                _executor.ExecuteSql(sql);
        }

public List<StageUploadJob> GetStageUploadJobs()
{
    string sql =
        SnowflakeSqlBuilder.GetStageUploadJobs(
            _context.Config);

    List<string[]> rows =
        _executor.ExecuteQueryRows(
            sql);

    return
        StageUploadJobMapper.Map(
            rows);
}


public bool ClearQuarantinePath(
    long detailId)
{
    string sql =
        SnowflakeSqlBuilder.ClearQuarantinePath(
            _context.Config,
            detailId);

    return _executor.ExecuteSql(sql);
}


public bool FinishRename(
    long detailId,
    string currentFileName,
    string currentPath)
{
    string sql =
        SnowflakeSqlBuilder.FinishRename(
            _context.Config,
            detailId,
            currentFileName,
            currentPath);

    return
        _executor.ExecuteSql(sql);
}

public bool FinishUpload(
    long detailId,
    string stagePath,
    string archivePath)
{
    string sql =
        SnowflakeSqlBuilder.FinishUpload(
            _context.Config,
            detailId,
            stagePath,
            archivePath);

    return
        _executor.ExecuteSql(sql);
}


public List<RenameJob> GetRenameJobs()
{
    string sql =
        SnowflakeSqlBuilder.GetRenameJobs(
            _context.Config);

    List<string[]> rows =
        _executor.ExecuteQueryRows(
            sql);

    return
        RenameJobMapper.Map(
            rows);
}



        /// <summary>
        /// Gets DETAIL_ID using file hash.
        /// </summary>
        public long GetDetailId(
    long folderId,
    string fileHash)
{
    string sql =
        SnowflakeSqlBuilder.GetDetailId(
            _context.Config,
            folderId,
            fileHash);

    string result =
        _executor.ExecuteScalar(
            sql);

    long detailId;

    if (!long.TryParse(
            result,
            out detailId))
    {
        return 0;
    }

    return detailId;
}
    }
}