using System.Collections.Concurrent;

namespace Meduit.ShareNormalizer.Snowflake.Inventory
{
    internal sealed class InventoryProcessingContext
    {
        public ConcurrentDictionary<string,long>
            HeaderIds =
                new ConcurrentDictionary<string,long>();

        public ConcurrentDictionary<string,long>
            FolderIds =
                new ConcurrentDictionary<string,long>();

        public ConcurrentDictionary<string,long>
            DetailIds =
                new ConcurrentDictionary<string,long>();

        public ConcurrentBag<string>
            SqlBatch =
                new ConcurrentBag<string>();

        public int HeaderCreated;

        public int FolderCreated;

        public int DetailCreated;

        public int DuplicateFiles;

        public int ActivityInserted;
    }
}