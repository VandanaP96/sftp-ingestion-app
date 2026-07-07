using System;

namespace Meduit.ShareNormalizer.Snowflake.Services
{
    /// <summary>
    /// Represents the overall workflow execution result.
    /// </summary>
    internal sealed class WorkflowResult
    {
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public bool InventoryCompleted { get; set; }

        public bool RenameCompleted { get; set; }

        public bool StageUploadCompleted { get; set; }

        public int InventoryFiles { get; set; }

        public int RenamedFiles { get; set; }

        public int UploadedFiles { get; set; }

        public int ArchivedFiles { get; set; }

        public int FailedFiles { get; set; }

        public bool HasErrors
        {
            get
            {
                return FailedFiles > 0;
            }
        }

        public TimeSpan Duration
        {
            get
            {
                return EndTime - StartTime;
            }
        }
    }
}