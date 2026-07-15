using System.IO;
using Meduit.ShareNormalizer.Snowflake.Helpers;

namespace Meduit.ShareNormalizer.Snowflake.Models
{
    /// <summary>
    /// Represents one file travelling through
    /// the Inventory pipeline.
    /// </summary>
    internal sealed class InventoryWorkItem
    {
        public FileInfo File
        {
            get;
            set;
        }

        public string FileHash
        {
            get;
            set;
        }

        public ValidationResult Validation
        {
            get;
            set;
        }

        public DetailRecord Detail
        {
            get;
            set;
        }

        public bool AlreadyExists
        {
            get;
            set;
        }
    }
}