using System;

using Meduit.ShareNormalizer.Snowflake.Infrastructure;
using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Core
{
    internal sealed class SnowflakeExecutionContext
    {
        private readonly SnowflakeConfig _config;

        private readonly Logger _logger;

        private readonly ProcessRunner _runner;

        private readonly PipelineMetrics _metrics;

        public SnowflakeExecutionContext(
            SnowflakeConfig config,
            Logger logger,
            ProcessRunner runner)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            if (logger == null)
                throw new ArgumentNullException("logger");

            if (runner == null)
                throw new ArgumentNullException("runner");

            _config = config;
            _logger = logger;
            _runner = runner;

            _metrics =
                new PipelineMetrics();

            _metrics.StartTime =
                DateTime.Now;
        }

        public SnowflakeConfig Config
        {
            get
            {
                return _config;
            }
        }

        public Logger Logger
        {
            get
            {
                return _logger;
            }
        }

        public ProcessRunner Runner
        {
            get
            {
                return _runner;
            }
        }

        public PipelineMetrics Metrics
        {
            get
            {
                return _metrics;
            }
        }
    }
}