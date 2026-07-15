using System.Threading;

namespace Meduit.ShareNormalizer.Snowflake.Inventory
{
    internal sealed class InventoryProcessor
    {
        private readonly InventoryQueue _queue;

        private readonly InventoryWriter _writer;

        public InventoryProcessor(
            InventoryQueue queue,
            InventoryWriter writer)
        {
            _queue = queue;

            _writer = writer;
        }

        public void Start()
        {
            Thread thread =
                new Thread(Process);

            thread.IsBackground = true;

            thread.Start();
        }

        private void Process()
        {
            foreach (
                InventoryWorkItem item
                    in _queue.Queue.GetConsumingEnumerable())
            {
                _writer.Add(
                    item);
            }
        }
    }
}