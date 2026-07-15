using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Configuration
{
    internal interface ISnowflakeConfigurationProvider
    {
        SnowflakeConfig GetConfiguration();
    }
}