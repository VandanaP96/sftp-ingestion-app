using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Mappers;
using Meduit.ShareNormalizer.Snowflake.Models;


namespace Meduit.ShareNormalizer.Snowflake.Repository
{
    /// <summary>
    /// Repository responsible for FILE_BATCH_DETAIL operations.
    /// </summary>
    internal sealed class DetailRepository : RepositoryBase
    {

        public DetailRepository(
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
        /// Checks whether the file already exists.
        /// </summary>
        public bool Exists(
            long folderId,
            string fileHash)
        {
            string sql =
                SnowflakeSqlBuilder.DetailExists(
                    Context.Config,
                    folderId,
                    fileHash);

            string result =
                Executor.ExecuteScalar(sql);

            return result == "1";
        }


        /// <summary>
/// Checks multiple hashes.
/// Current implementation is sequential.
/// Can later become one SQL IN() query.
/// </summary>
public Dictionary<string, bool> ExistsBatch(
    long folderId,
    IEnumerable<string> hashes)
{
    Dictionary<string, bool> result =
        new Dictionary<string, bool>();

    List<string> hashList =
        new List<string>(hashes);

    if (hashList.Count == 0)
        return result;

    string sql =
        SnowflakeSqlBuilder.DetailExistsBatch(
            Context.Config,
            hashList);

    List<string[]> rows =
        Executor.ExecuteQueryRows(sql);

    foreach (string hash in hashList)
    {
        result[hash] = false;
    }

    foreach (string[] row in rows)
    {
        if (row.Length > 0)
        {
            result[row[0]] = true;
        }
    }

    return result;
}

        /// <summary>
        /// Inserts metadata and returns DETAIL_ID.
        /// </summary>
        public long Insert(
            DetailRecord record)
        {
            string insertSql =
                SnowflakeSqlBuilder.InsertDetail(
                    Context.Config,
                    record);

            Logger.Log(
                "DETAIL      Registering : "
                + record.CurrentFileName);

            bool inserted =
                Executor.ExecuteSql(
                    insertSql);

            if (!inserted)
                return 0;

            string lookupSql =
                SnowflakeSqlBuilder.GetDetailId(
                    Context.Config,
                    record.FolderId,
                    record.FileHash);

            string result =
                Executor.ExecuteScalar(
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
        /// Inserts multiple detail records.
        /// Future implementation can use .NET Connector bulk insert.
        /// Current implementation executes sequentially.
        /// </summary>
        public List<long> InsertBatch(
            IEnumerable<DetailRecord> records)
        {
            List<long> ids =
                new List<long>();

            List<DetailRecord> detailList =
                new List<DetailRecord>(records);

            if (detailList.Count == 0)
                return ids;

            Logger.Log(
                "Bulk inserting "
                + detailList.Count
                + " detail records...");

            lock (Executor)
            {
                Executor.BeginTransaction();

                try
                {
                    foreach (DetailRecord record in detailList)
                    {
                        long id =
                            Insert(record);

                        ids.Add(id);
                    }

                    Executor.CommitTransaction();
                }
                catch
                {
                    Executor.RollbackTransaction();
                    throw;
                }
            }

            return ids;
        }

        public List<long> InsertBatchTransaction(
    IEnumerable<DetailRecord> records)
{
    List<long> ids =
        new List<long>();

    const int BatchSize = 250;

    foreach (List<DetailRecord> batch
        in SplitBatch(records, BatchSize))
    {
        Executor.BeginTransaction();

        try
        {
            foreach (DetailRecord record in batch)
            {
                ids.Add(
                    Insert(record));
            }

            Executor.CommitTransaction();
        }
        catch
        {
            Executor.RollbackTransaction();
            throw;
        }
    }

    return ids;
}

public bool ExecuteBatchInsert(
    IEnumerable<DetailRecord> records)
{
    string sql =
        SnowflakeSqlBuilder.InsertDetailBatch(
            Context.Config,
            records);

    return
        Executor.ExecuteSql(sql);
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
                    Context.Config,
                    detailId,
                    status,
                    approvedBy);

            return
                Executor.ExecuteSql(sql);
        }

        /// <summary>
        /// Marks upload started.
        /// </summary>
        public bool UpdateIngestionStart(
            long detailId)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateIngestionStart(
                    Context.Config,
                    detailId);

            return
                Executor.ExecuteSql(sql);
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
                    Context.Config,
                    detailId,
                    status,
                    rowCount);

            return
                Executor.ExecuteSql(sql);
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
                    Context.Config,
                    detailId,
                    error);

            return
                Executor.ExecuteSql(sql);
        }

        public List<StageUploadJob> GetStageUploadJobs()
        {
            string sql =
                SnowflakeSqlBuilder.GetStageUploadJobs(
                    Context.Config);

            Logger.Log("");
            Logger.Log("========== STAGE UPLOAD SQL ==========");
            Logger.Log(sql);
            Logger.Log("======================================");

            List<string[]> rows =
                Executor.ExecuteQueryRows(sql);

            Logger.Log(
                "Rows returned = " +
                rows.Count);

            return
                StageUploadJobMapper.Map(rows);
        }


        public bool ClearQuarantinePath(
    long detailId)
{
    string sql =
        SnowflakeSqlBuilder.ClearQuarantinePath(
            Context.Config,
            detailId);

    return Executor.ExecuteSql(sql);
}


public bool FinishRename(
    long detailId,
    string currentFileName,
    string currentPath)
{
    string sql =
        SnowflakeSqlBuilder.FinishRename(
            Context.Config,
            detailId,
            currentFileName,
            currentPath);

    return
        Executor.ExecuteSql(sql);
}

public bool FinishUpload(
    long detailId,
    string stagePath,
    string archivePath)
{
    string sql =
        SnowflakeSqlBuilder.FinishUpload(
            Context.Config,
            detailId,
            stagePath,
            archivePath);

    return
        Executor.ExecuteSql(sql);
}


public List<RenameJob> GetRenameJobs()
{
    string sql =
        SnowflakeSqlBuilder.GetRenameJobs(
            Context.Config);

    List<string[]> rows =
        Executor.ExecuteQueryRows(
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
            Context.Config,
            folderId,
            fileHash);

    string result =
        Executor.ExecuteScalar(
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


/// <summary>
/// Retrieves multiple Detail IDs.
/// Current implementation is sequential.
/// Future implementation will use one query.
/// </summary>
public Dictionary<string, long> GetDetailIds(
    long folderId,
    IEnumerable<string> hashes)
{
    Dictionary<string, long> result =
        new Dictionary<string, long>();

    List<string> hashList =
        new List<string>(hashes);

    if (hashList.Count == 0)
        return result;

    string sql =
        SnowflakeSqlBuilder.GetDetailIdsBatch(
            Context.Config,
            hashList);

    List<string[]> rows =
        Executor.ExecuteQueryRows(sql);

            Logger.Log("StageUpload SQL returned " + rows.Count + " rows");

            foreach (string[] row in rows)
            {
                Logger.Log(
                    string.Join(" | ", row));
            }

            foreach (string[] row in rows)
    {
        if (row.Length < 2)
            continue;

        long id;

        if (long.TryParse(row[0], out id))
        {
            result[row[1]] = id;
        }
    }

    return result;
}

private IEnumerable<List<DetailRecord>> SplitBatch(
    IEnumerable<DetailRecord> records,
    int batchSize)
{
    List<DetailRecord> batch =
        new List<DetailRecord>(batchSize);

    foreach (DetailRecord record in records)
    {
        batch.Add(record);

        if (batch.Count >= batchSize)
        {
            yield return batch;

            batch =
                new List<DetailRecord>(batchSize);
        }
    }

    if (batch.Count > 0)
    {
        yield return batch;
    }
}


    }
}