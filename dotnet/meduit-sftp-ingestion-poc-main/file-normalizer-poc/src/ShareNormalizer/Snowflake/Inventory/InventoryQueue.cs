using System.Collections.Concurrent;

namespace Meduit.ShareNormalizer.Snowflake.Inventory
{
    internal sealed class InventoryQueue
    {
        private readonly BlockingCollection<InventoryWorkItem>
            _queue =
                new BlockingCollection<InventoryWorkItem>();

        public void Enqueue(
            InventoryWorkItem item)
        {
            _queue.Add(item);
        }

        public InventoryWorkItem Dequeue()
        {
            return _queue.Take();
        }

        public void Complete()
        {
            _queue.CompleteAdding();
        }

        public BlockingCollection<InventoryWorkItem> Queue
        {
            get
            {
                return _queue;
            }
        }
    }
}