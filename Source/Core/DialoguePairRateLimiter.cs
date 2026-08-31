using System.Collections.Generic;

namespace RimMind.Dialogue.Core
{
    /// <summary>
    /// Per-pair daily allowance for automatic reply continuation.
    /// </summary>
    public sealed class DialoguePairRateLimiter
    {
        private readonly object _gate = new object();
        private readonly Dictionary<(int, int), Counter> _counters =
            new Dictionary<(int, int), Counter>();

        public bool TryConsume(
            (int, int) pairKey,
            int day,
            int maximumPerDay)
        {
            if (maximumPerDay <= 0)
                return false;

            lock (_gate)
            {
                if (!_counters.TryGetValue(pairKey, out Counter counter)
                    || counter.Day != day)
                {
                    _counters[pairKey] = new Counter(day, 1);
                    return true;
                }

                if (counter.Count >= maximumPerDay)
                    return false;

                _counters[pairKey] = new Counter(day, counter.Count + 1);
                return true;
            }
        }

        public void Reset()
        {
            lock (_gate)
                _counters.Clear();
        }

        private readonly struct Counter
        {
            public Counter(int day, int count)
            {
                Day = day;
                Count = count;
            }

            public int Day { get; }
            public int Count { get; }
        }
    }
}
