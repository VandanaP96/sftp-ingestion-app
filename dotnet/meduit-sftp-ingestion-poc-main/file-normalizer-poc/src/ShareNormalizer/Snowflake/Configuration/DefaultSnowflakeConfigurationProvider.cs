using System;

using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Configuration
{
    internal sealed class DefaultSnowflakeConfigurationProvider
        : ISnowflakeConfigurationProvider
    {
        private readonly SnowflakeConfig _config;

        public DefaultSnowflakeConfigurationProvider(
            SnowflakeConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");

            _config = config;
        }

        public SnowflakeConfig GetConfiguration()
        {
            return _config;
        }
    }
}