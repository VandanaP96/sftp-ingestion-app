using System;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal sealed class SnowflakeConnection
        : IDisposable
    {
        private readonly SnowflakeContext _context;

        private readonly Logger _logger;

        public SnowflakeConnection(
            SnowflakeContext context,
            Logger logger)
        {
            _context = context;

            _logger = logger;
        }

        public SnowflakeContext Context
        {
            get
            {
                return _context;
            }
        }

        public Logger Logger
        {
            get
            {
                return _logger;
            }
        }

        public void Dispose()
        {
        }
    }
}