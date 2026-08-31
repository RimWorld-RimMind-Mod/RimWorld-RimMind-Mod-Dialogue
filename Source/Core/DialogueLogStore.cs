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

        public void Add(DialogueLogEntry entry)
        {
            _entries.Add(entry);
            if (_entries.Count > MaximumEntries)
            {
                List<DialogueLogEntry> kept = _entries
                    .OrderByDescending(candidate => candidate.tick)
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
        }
    }
}
