using System;

namespace Meduit.ShareNormalizer.Core
{
    public sealed class PipelineSummary
    {
        public string ClientName { get; set; }

        public int FilesDiscovered { get; set; }

        public int FilesNormalized { get; set; }

        public int FilesUploaded { get; set; }

        public int FilesArchived { get; set; }

        public int Errors { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public TimeSpan Duration
        {
            get
            {
                return EndTime - StartTime;
            }
        }
    }
}