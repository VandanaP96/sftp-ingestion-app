using System.Collections.Concurrent;
using System.Collections.Generic;

using Meduit.ShareNormalizer.Snowflake.Models;

namespace Meduit.ShareNormalizer.Snowflake.Infrastructure
{
    internal sealed class ActivityBuffer
    {
        private readonly ConcurrentQueue<ActivityRecord> _queue =
            new ConcurrentQueue<ActivityRecord>();

                public int Count
{
    get
    {
        return _queue.Count;
    }
}

        public void Add(
            ActivityRecord activity)
        {
            _queue.Enqueue(activity);
        }

        public List<ActivityRecord> Drain()
        {
            List<ActivityRecord> result =
                new List<ActivityRecord>();

            ActivityRecord activity;

            while (_queue.TryDequeue(
                out activity))
            {
                result.Add(activity);
            }

            return result;
        }
    }
}