using System.Collections.Concurrent;

namespace Meduit.ShareNormalizer.Snowflake.Inventory
{
    internal sealed class InventoryCache
    {
        public ConcurrentDictionary<string,bool>
            HeaderExists =
                new ConcurrentDictionary<string,bool>();

        public ConcurrentDictionary<string,bool>
            FolderExists =
                new ConcurrentDictionary<string,bool>();

        public ConcurrentDictionary<string,bool>
            DetailExists =
                new ConcurrentDictionary<string,bool>();
    }
}