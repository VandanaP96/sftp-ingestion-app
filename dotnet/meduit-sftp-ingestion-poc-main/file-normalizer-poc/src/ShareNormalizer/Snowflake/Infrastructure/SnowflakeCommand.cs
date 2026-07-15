namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal sealed class SnowflakeCommand
    {
        public SnowflakeOperation Operation
        {
            get;
            set;
        }

        public string Sql
        {
            get;
            set;
        }

        public string LocalFile
        {
            get;
            set;
        }

        public string StageFolder
        {
            get;
            set;
        }

        public string StageFile
        {
            get;
            set;
        }
    }
}