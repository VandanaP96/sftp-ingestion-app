using System;

namespace Meduit.ShareNormalizer.Snowflake.Models
{
    /// <summary>
    /// Represents one client/source combination.
    /// Maps to SFTP_INGESTION.FILE_BATCH_HEADER.
    /// </summary>
    internal sealed class HeaderRecord
    {
        public long HeaderId { get; set; }

        public string ClientCode { get; set; }

        public string ClientName { get; set; }

        public string SourceSystem { get; set; }

        public string RootFolder { get; set; }

        public int TotalFolderCount { get; set; }

        public int TotalFileCount { get; set; }

        public string ActiveFlag { get; set; }

        public string CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; }

        public string UpdatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public HeaderRecord()
        {
            ActiveFlag = "Y";
            CreatedBy = Environment.UserName;
            CreatedDate = DateTime.Now;
        }
    }
}