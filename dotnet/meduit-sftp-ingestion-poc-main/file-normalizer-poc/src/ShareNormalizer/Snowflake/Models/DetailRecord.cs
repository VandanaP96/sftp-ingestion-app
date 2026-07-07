using System;

namespace Meduit.ShareNormalizer.Snowflake.Models
{
    /// <summary>
    /// Represents FILE_BATCH_DETAIL.
    /// This table is the primary workflow table.
    /// </summary>
    internal sealed class DetailRecord
    {
        public long DetailId { get; set; }

        public long FolderId { get; set; }

        public string OriginalFileName { get; set; }

        public string CurrentFileName { get; set; }

        public string FileType { get; set; }

        public string FileExtension { get; set; }

        public string OriginalPath { get; set; }

        public string CurrentPath { get; set; }

        public string QuarantinePath { get; set; }

        public string StagePath { get; set; }

        public string ArchivePath { get; set; }

        public decimal FileSizeKb { get; set; }

        public DateTime LastModified { get; set; }

        public string FileHash { get; set; }

        public string DatePattern { get; set; }

        public string ValidDateFlag { get; set; }

        public string ValidationMessage { get; set; }

        public string FileStatus { get; set; }

        public string AutoRejectFlag { get; set; }

        public string ApprovalStatus { get; set; }

        public string RenameRequiredFlag { get; set; }

        public string RenameStatus { get; set; }

        public string IngestionStatus { get; set; }

        public DateTime? IngestionStartTime { get; set; }

        public DateTime? IngestionEndTime { get; set; }

        public long RowCount { get; set; }

        public string ErrorMessage { get; set; }

        public DateTime CreatedDate { get; set; }

        public DateTime? UpdatedDate { get; set; }

        public string ApprovedBy { get; set; }

        public DateTime? ApprovedDate { get; set; }

        public string RenamedBy { get; set; }

        public DateTime? RenamedDate { get; set; }

        public DateTime? ArchivedDate { get; set; }
    }
}