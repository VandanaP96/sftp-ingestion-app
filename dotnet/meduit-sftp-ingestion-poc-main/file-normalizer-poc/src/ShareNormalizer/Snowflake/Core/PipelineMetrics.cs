using System;

namespace Meduit.ShareNormalizer.Snowflake.Core
{
    internal sealed class PipelineMetrics
    {
        public int FilesDiscovered;

        public int FilesNormalized;

        public int FilesInserted;

        public int FilesRenamed;

        public int FilesUploaded;

        public int FilesArchived;

        public int Errors;

        public DateTime StartTime;

        public DateTime EndTime;

        public TimeSpan Duration
        {
            get
            {
                return EndTime - StartTime;
            }
        }
    }
}