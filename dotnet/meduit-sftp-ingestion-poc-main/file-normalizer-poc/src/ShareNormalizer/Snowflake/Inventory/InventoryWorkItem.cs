using System.IO;
using Meduit.ShareNormalizer.Snowflake.Helpers;

using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Inventory
{
    internal sealed class InventoryWorkItem
    {
        public HeaderRecord Header;

        public FolderRecord Folder;

        public DetailRecord Detail;

        public ActivityRecord Activity;
    }
}