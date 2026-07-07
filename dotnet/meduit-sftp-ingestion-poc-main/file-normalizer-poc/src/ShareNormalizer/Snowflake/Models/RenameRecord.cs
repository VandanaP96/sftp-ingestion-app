namespace Meduit.ShareNormalizer.Snowflake.Models
{
    internal sealed class RenameRecord
    {
        public long DetailId
        {
            get;
            set;
        }

        public string OriginalFileName
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

        public string QuarantinePath
        {
            get;
            set;
        }

        public string OriginalPath
        {
            get;
            set;
        }

        public string ApprovedBy
        {
            get;
            set;
        }
    }
}