using System.Collections.Generic;

using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Inventory
{
    internal sealed class InventoryBatch
    {
        public List<HeaderRecord>
            Headers =
                new List<HeaderRecord>();

        public List<FolderRecord>
            Folders =
                new List<FolderRecord>();

        public List<DetailRecord>
            Details =
                new List<DetailRecord>();

        public List<ActivityRecord>
            Activities =
                new List<ActivityRecord>();
    }
}