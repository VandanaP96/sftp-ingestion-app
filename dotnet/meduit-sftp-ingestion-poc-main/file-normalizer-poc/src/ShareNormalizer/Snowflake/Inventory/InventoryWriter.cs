using System.Collections.Generic;

namespace Meduit.ShareNormalizer.Snowflake.Inventory
{
    internal sealed class InventoryWriter
    {
        private readonly InventoryBatch _batch =
            new InventoryBatch();

        public void Add(
            InventoryWorkItem item)
        {
            if (item.Header != null)
                _batch.Headers.Add(
                    item.Header);

            if (item.Folder != null)
                _batch.Folders.Add(
                    item.Folder);

            if (item.Detail != null)
                _batch.Details.Add(
                    item.Detail);

            if (item.Activity != null)
                _batch.Activities.Add(
                    item.Activity);
        }

        public InventoryBatch Flush()
        {
            InventoryBatch batch =
                new InventoryBatch();

            batch.Headers.AddRange(
                _batch.Headers);

            batch.Folders.AddRange(
                _batch.Folders);

            batch.Details.AddRange(
                _batch.Details);

            batch.Activities.AddRange(
                _batch.Activities);

            _batch.Headers.Clear();

            _batch.Folders.Clear();

            _batch.Details.Clear();

            _batch.Activities.Clear();

            return batch;
        }
    }
}