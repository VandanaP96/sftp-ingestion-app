using System;
using System.Collections.Concurrent;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Repository
{
    /// <summary>
    /// Repository responsible for FILE_BATCH_HEADER operations.
    /// </summary>
    internal sealed class HeaderRepository : RepositoryBase
    {
        

        private readonly ConcurrentDictionary<string,long>
    _headerCache =
        new ConcurrentDictionary<string,long>();

        public HeaderRepository(
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
            string clientCode,
            string sourceSystem,
            string rootFolder)
        {
            string sql =
                SnowflakeSqlBuilder.HeaderExists(
                    Context.Config,
                    clientCode,
                    sourceSystem,
                    rootFolder);

            string result =
                Executor.ExecuteScalar(sql);

            return result == "1";
        }

        public bool Insert(
            HeaderRecord record)
        {
            string sql =
                SnowflakeSqlBuilder.InsertHeader(
                    Context.Config,
                    record);

            Logger.Log(
                "HEADER      Creating : " +
                record.ClientCode);

            return Executor.ExecuteSql(sql);
        }

        public long GetHeaderId(
            string clientCode,
            string sourceSystem,
            string rootFolder)
        {
            string sql =
                SnowflakeSqlBuilder.GetHeaderId(
                    Context.Config,
                    clientCode,
                    sourceSystem,
                    rootFolder);

            string value =
                Executor.ExecuteScalar(sql);

            long id;

            if (!long.TryParse(value, out id))
                return 0;

            return id;
        }

        /// <summary>
        /// Creates header if required and always returns HEADER_ID.
        /// </summary>
        public long GetOrCreate(
    HeaderRecord record)
        {
            string key =
                record.ClientCode + "|" +
                record.SourceSystem + "|" +
                record.RootFolder;

            long id;

            if (_headerCache.TryGetValue(
                    key,
                    out id))
            {
                return id;
            }

            lock (_headerCache)
            {
                if (_headerCache.TryGetValue(
                        key,
                        out id))
                {
                    return id;
                }

                if (!Exists(
                        record.ClientCode,
                        record.SourceSystem,
                        record.RootFolder))
                {
                    Insert(record);
                }

                id =
                    GetHeaderId(
                        record.ClientCode,
                        record.SourceSystem,
                        record.RootFolder);

                _headerCache[key] = id;

                return id;
            }
        }

        public bool UpdateAudit(
            long headerId,
            string updatedBy)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateHeaderAudit(
                    Context.Config,
                    headerId,
                    updatedBy);

            return Executor.ExecuteSql(sql);
        }
    }
}