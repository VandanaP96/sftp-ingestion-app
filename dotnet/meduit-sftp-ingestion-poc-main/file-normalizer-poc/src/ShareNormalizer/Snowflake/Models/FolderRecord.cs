using System;

namespace Meduit.ShareNormalizer.Snowflake.Models
{
    /// <summary>
    /// Represents one Year-Month folder under a client.
    /// Approval is performed at this level.
    /// Maps to SFTP_INGESTION.FILE_BATCH_FOLDER.
    /// </summary>
    internal sealed class FolderRecord
    {
        public long FolderId { get; set; }

        public long HeaderId { get; set; }

        public string YearMonth { get; set; }

        public string FolderName { get; set; }

        public string FolderPath { get; set; }

        public string FolderHash { get; set; }

        public int TotalFiles { get; set; }

        public int ApprovedFiles { get; set; }

        public int RejectedFiles { get; set; }

        public string FolderStatus { get; set; }

        public DateTime ScannedDate { get; set; }

        public string ActiveFlag { get; set; }

        public string CreatedBy { get; set; }

        public DateTime CreatedDate { get; set; }

        public string UpdatedBy { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public string ApprovedBy { get; set; }

        public DateTime? ApprovedDate { get; set; }

        public FolderRecord()
        {
            FolderStatus = "DISCOVERED";
            ActiveFlag = "Y";
            ScannedDate = DateTime.Now;
            CreatedBy = Environment.UserName;
            CreatedDate = DateTime.Now;
        }
    }
}