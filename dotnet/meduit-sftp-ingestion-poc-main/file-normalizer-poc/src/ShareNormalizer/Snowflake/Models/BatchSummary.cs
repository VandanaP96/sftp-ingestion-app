using System.Collections.Generic;

namespace Meduit.ShareNormalizer.Snowflake.Models
{
    /// <summary>
    /// Represents one complete batch.
    ///
    /// One Client
    ///     -> One Year-Month Folder
    ///         -> Many Files
    /// </summary>
    internal sealed class BatchSummary
    {
        public HeaderRecord Header { get; set; }

        public FolderRecord Folder { get; set; }

        public List<DetailRecord> Files { get; set; }

        public BatchSummary()
        {
            Files = new List<DetailRecord>();
        }
    }
}