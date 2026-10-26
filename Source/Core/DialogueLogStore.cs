using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RimMind.Dialogue.Core
{
    internal sealed class DialogueLogStore
    {
        private const int MaximumEntries = 500;

        private readonly object _sync = new object();
        private readonly List<DialogueLogEntry> _entries = new List<DialogueLogEntry>();
        private IReadOnlyList<DialogueLogEntry>? _cachedEntries;
        private int _revision;
        private static int _globalRevision;
        private static long _sequenceCounter;

        public event Action? Updated;

        public int Revision => Volatile.Read(ref _revision);

        public static int CurrentRevision => Volatile.Read(ref _globalRevision);

        public IReadOnlyList<DialogueLogEntry> Entries
        {
            get
            {
                lock (_sync)
                {
                    if (_cachedEntries != null)
                        return _cachedEntries;

                    _cachedEntries = _entries.ToArray();
                    return _cachedEntries;
                }
            }
        }

        public void Add(DialogueLogEntry entry)
        {
            if (entry == null) return;
            entry.sequence = Interlocked.Increment(ref _sequenceCounter);

            lock (_sync)
            {
                InsertChronological(entry);
                if (_entries.Count > MaximumEntries)
                {
                    int overflow = _entries.Count - MaximumEntries;
                    _entries.RemoveRange(0, overflow);
                }

                _cachedEntries = null;
                _revision++;
                Interlocked.Increment(ref _globalRevision);
            }

            Updated?.Invoke();
        }

        public IReadOnlyList<DialogueLogEntry> HistoryFor(
            int pawnId,
            int maximumCount)
        {
            if (maximumCount <= 0)
                return Array.Empty<DialogueLogEntry>();

            lock (_sync)
            {
                var result = new List<DialogueLogEntry>(Math.Min(maximumCount, _entries.Count));
                for (int i = _entries.Count - 1; i >= 0 && result.Count < maximumCount; i--)
                {
                    var entry = _entries[i];
                    if (entry.initiatorId == pawnId || entry.recipientId == pawnId)
                    {
                        result.Add(entry);
                    }
                }
                return result;
            }
        }

        public void Clear()
        {
            lock (_sync)
            {
                _entries.Clear();
                _cachedEntries = null;
                _revision++;
                Interlocked.Increment(ref _globalRevision);
            }

            Updated?.Invoke();
        }

        public List<DialogueLogEntry> ExportEntries(int maxCount)
        {
            if (maxCount <= 0)
                return new List<DialogueLogEntry>();

            lock (_sync)
            {
                int take = Math.Min(maxCount, _entries.Count);
                var result = new List<DialogueLogEntry>(take);
                for (int i = _entries.Count - 1; i >= 0 && result.Count < take; i--)
                {
                    result.Add(_entries[i]);
                }
                return result;
            }
        }

        public void ImportEntries(IEnumerable<DialogueLogEntry>? loadedEntries, int maxCount)
        {
            if (loadedEntries == null) return;
            int cap = Math.Min(Math.Max(0, maxCount), MaximumEntries);
            var materialized = loadedEntries.Where(e => e != null).ToList();
            for (int i = 0; i < materialized.Count; i++)
            {
                if (materialized[i].sequence == 0)
                    materialized[i].sequence = Interlocked.Increment(ref _sequenceCounter);
            }

            var kept = materialized
                .OrderByDescending(candidate => candidate.tick)
                .ThenByDescending(candidate => candidate.sequence)
                .Take(cap)
                .OrderBy(candidate => candidate.tick)
                .ThenBy(candidate => candidate.sequence)
                .ToList();

            lock (_sync)
            {
                _entries.Clear();
                _entries.AddRange(kept);
                _cachedEntries = null;
                _revision++;
                Interlocked.Increment(ref _globalRevision);
            }

            Updated?.Invoke();
        }

        private void InsertChronological(DialogueLogEntry entry)
        {
            int count = _entries.Count;
            if (count == 0 || CompareChronological(_entries[count - 1], entry) <= 0)
            {
                _entries.Add(entry);
                return;
            }

            int insertIndex = count - 1;
            while (insertIndex > 0 && CompareChronological(_entries[insertIndex - 1], entry) > 0)
            {
                insertIndex--;
            }
            _entries.Insert(insertIndex, entry);
        }

        private static int CompareChronological(DialogueLogEntry a, DialogueLogEntry b)
        {
            int tickCmp = a.tick.CompareTo(b.tick);
            return tickCmp != 0 ? tickCmp : a.sequence.CompareTo(b.sequence);
        }
    }
}
