using System;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Repository
{
    /// <summary>
    /// Repository responsible for FILE_BATCH_HEADER operations.
    /// </summary>
    internal sealed class HeaderRepository
    {
        private readonly SnowflakeContext _context;
        private readonly SnowCliExecutor _executor;
        private readonly Logger _logger;

        public HeaderRepository(
            SnowflakeContext context,
            SnowCliExecutor executor,
            Logger logger)
        {
            _context = context;
            _executor = executor;
            _logger = logger;
        }

        public bool Exists(
            string clientCode,
            string sourceSystem,
            string rootFolder)
        {
            string sql =
                SnowflakeSqlBuilder.HeaderExists(
                    _context.Config,
                    clientCode,
                    sourceSystem,
                    rootFolder);

            string result =
                _executor.ExecuteScalar(sql);

            return result == "1";
        }

        public bool Insert(
            HeaderRecord record)
        {
            string sql =
                SnowflakeSqlBuilder.InsertHeader(
                    _context.Config,
                    record);

            _logger.Log(
                "HEADER      Creating : " +
                record.ClientCode);

            return _executor.ExecuteSql(sql);
        }

        public long GetHeaderId(
            string clientCode,
            string sourceSystem,
            string rootFolder)
        {
            string sql =
                SnowflakeSqlBuilder.GetHeaderId(
                    _context.Config,
                    clientCode,
                    sourceSystem,
                    rootFolder);

            string value =
                _executor.ExecuteScalar(sql);

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
            if (!Exists(
                    record.ClientCode,
                    record.SourceSystem,
                    record.RootFolder))
            {
                Insert(record);
            }

            return GetHeaderId(
                record.ClientCode,
                record.SourceSystem,
                record.RootFolder);
        }

        public bool UpdateAudit(
            long headerId,
            string updatedBy)
        {
            string sql =
                SnowflakeSqlBuilder.UpdateHeaderAudit(
                    _context.Config,
                    headerId,
                    updatedBy);

            return _executor.ExecuteSql(sql);
        }
    }
}