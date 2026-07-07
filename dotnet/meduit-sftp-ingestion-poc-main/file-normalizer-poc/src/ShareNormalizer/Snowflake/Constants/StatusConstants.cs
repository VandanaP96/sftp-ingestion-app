namespace Meduit.ShareNormalizer.Snowflake.Constants
{
    /// <summary>
    /// Central location for all workflow statuses.
    /// Never hardcode status strings anywhere else.
    /// </summary>
    internal static class StatusConstants
    {
        internal static class FileStatus
        {
            public const string New = "NEW";

            public const string PendingApproval = "PENDING_APPROVAL";

            public const string AutoRejected = "AUTO_REJECTED";

            public const string Approved = "APPROVED";

            public const string Rejected = "REJECTED";

            public const string Renamed = "RENAMED";

            public const string Staged = "STAGED";

            public const string Archived = "ARCHIVED";

            public const string Failed = "FAILED";
        }

        internal static class ApprovalStatus
        {
            public const string Pending = "PENDING";

            public const string Approved = "APPROVED";

            public const string Rejected = "REJECTED";

            public const string RenameRequired = "RENAME_REQUIRED";
        }

        internal static class RenameStatus
        {
            public const string NotRequired = "NOT_REQUIRED";

            public const string Ready = "READY";

            public const string Completed = "COMPLETED";

            public const string Failed = "FAILED";
        }

        internal static class IngestionStatus
        {
            public const string NotStarted = "NOT_STARTED";

            public const string Uploading = "UPLOADING";

            public const string Success = "SUCCESS";

            public const string Failed = "FAILED";
        }

        internal static class ActivityType
        {
            public const string Header = "HEADER";

            public const string Folder = "FOLDER";

            public const string Validation = "VALIDATION";

            public const string AutoReject = "AUTO_REJECT";

            public const string Rename = "RENAME";

            public const string Upload = "UPLOAD";

            public const string Archive = "ARCHIVE";

            public const string Error = "ERROR";
        }
    }
}