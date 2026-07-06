namespace Meduit.ShareNormalizer.Snowflake.Models
{
    /// <summary>
    /// Represents one file waiting for rename.
    /// </summary>
    internal sealed class RenameJob
    {
        public long DetailId
        {
            get;
            set;
        }

        public long FolderId
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

        public string OriginalPath
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

        public string ApprovedBy
        {
            get;
            set;
        }
    }
}