using System.Collections.Generic;
using System.IO;

namespace Meduit.ShareNormalizer.Snowflake.Models
{
    /// <summary>
    /// Represents one Year-Month folder being processed.
    /// </summary>
    internal sealed class InventoryContext
    {
        // Discovery

        public string SourceSystem { get; set; }

        public string ClientCode { get; set; }

        public string ClientName { get; set; }

        public string FolderName { get; set; }

        public string FolderPath { get; set; }

        public string ClientRootFolder { get; set; }

        public string FolderHash { get; set; }

        public List<FileInfo> Files { get; set; }

        // Database

        public long HeaderId { get; set; }

        public long FolderId { get; set; }

        // Statistics

        public int TotalFiles { get; set; }

        public int SuccessfulFiles { get; set; }

        public int FailedFiles { get; set; }

        public int QuarantinedFiles { get; set; }

        public int ApprovedFiles { get; set; }

        public int AutoRejectedFiles { get; set; }

        public int UploadedFiles { get; set; }

        public int ArchivedFiles { get; set; }

        // Runtime

        public string CurrentUser { get; set; }

        public bool FolderAlreadyExists { get; set; }
    }
}