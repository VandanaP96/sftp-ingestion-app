namespace Meduit.ShareNormalizer.Snowflake.Models
{
    internal sealed class StageRecord
    {
        public long DetailId
        {
            get;
            set;
        }

        public string CurrentFileName
        {
            get;
            set;
        }

        public string CurrentPath
        {
            get;
            set;
        }

        public string StagePath
        {
            get;
            set;
        }

        public string ArchivePath
        {
            get;
            set;
        }
    }
}