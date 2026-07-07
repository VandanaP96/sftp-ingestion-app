using System;

namespace Meduit.ShareNormalizer.Snowflake.Models
{
    /// <summary>
    /// Represents FILE_ACTIVITY_LOG.
    /// </summary>
    internal sealed class ActivityRecord
    {
        public long ActivityId { get; set; }

        public long HeaderId { get; set; }

        public long FolderId { get; set; }

        public long DetailId { get; set; }

        public string ActivityType { get; set; }

        public string ActivityStatus { get; set; }

        public string ActivityMessage { get; set; }

        public string ExecutedBy { get; set; }

        public DateTime ExecutedTime { get; set; }

        public decimal DurationSeconds { get; set; }

        public string ErrorCode { get; set; }

        public string ErrorMessage { get; set; }
    }
}