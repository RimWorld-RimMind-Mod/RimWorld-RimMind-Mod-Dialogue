using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RimMind.Dialogue.Core
{
    internal sealed class DialogueLogStore
    {
        private const int MaximumEntries = 500;

        private ConcurrentBag<DialogueLogEntry> _entries =
            new ConcurrentBag<DialogueLogEntry>();
        private List<DialogueLogEntry>? _cachedEntries;
        private bool _dirty = true;

        public event Action? Updated;

        public IReadOnlyList<DialogueLogEntry> Entries
        {
            get
            {
                if (!_dirty && _cachedEntries != null)
                    return _cachedEntries;

                _cachedEntries = _entries.ToList();
                _dirty = false;
                return _cachedEntries;
            }
        }

        private static long _sequenceCounter;

        public void Add(DialogueLogEntry entry)
        {
            entry.sequence = Interlocked.Increment(ref _sequenceCounter);
            _entries.Add(entry);
            if (_entries.Count > MaximumEntries)
            {
                List<DialogueLogEntry> kept = _entries
                    .OrderByDescending(candidate => candidate.tick)
                    .ThenByDescending(candidate => candidate.sequence)
                    .Take(MaximumEntries)
                    .ToList();
                Interlocked.Exchange(
                    ref _entries,
                    new ConcurrentBag<DialogueLogEntry>(kept));
            }

            _dirty = true;
            Updated?.Invoke();
        }

        public IReadOnlyList<DialogueLogEntry> HistoryFor(
            int pawnId,
            int maximumCount)
        {
            return _entries
                .Where(entry => entry.initiatorId == pawnId || entry.recipientId == pawnId)
                .OrderByDescending(entry => entry.tick)
                .ThenByDescending(entry => entry.sequence)
                .Take(maximumCount)
                .ToList();
        }

        public void Clear()
        {
            Interlocked.Exchange(
                ref _entries,
                new ConcurrentBag<DialogueLogEntry>());
            _cachedEntries = null;
            _dirty = true;
            Updated?.Invoke();
        }

        public List<DialogueLogEntry> ExportEntries(int maxCount)
        {
            return _entries
                .OrderByDescending(candidate => candidate.tick)
                .ThenByDescending(candidate => candidate.sequence)
                .Take(maxCount)
                .ToList();
        }

        public void ImportEntries(IEnumerable<DialogueLogEntry>? loadedEntries, int maxCount)
        {
            if (loadedEntries == null) return;
            var kept = loadedEntries
                .Where(e => e != null)
                .OrderByDescending(candidate => candidate.tick)
                .ThenByDescending(candidate => candidate.sequence)
                .Take(maxCount)
                .ToList();
            foreach (var entry in kept)
            {
                if (entry.sequence == 0)
                    entry.sequence = Interlocked.Increment(ref _sequenceCounter);
            }
            Interlocked.Exchange(
                ref _entries,
                new ConcurrentBag<DialogueLogEntry>(kept));
            _cachedEntries = null;
            _dirty = true;
            Updated?.Invoke();
        }
    }
}
