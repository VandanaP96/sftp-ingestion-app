namespace Meduit.ShareNormalizer.Snowflake.Models
{
    /// <summary>
    /// Represents one approved file waiting
    /// for upload to Snowflake stage.
    /// </summary>
    internal sealed class StageUploadJob
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