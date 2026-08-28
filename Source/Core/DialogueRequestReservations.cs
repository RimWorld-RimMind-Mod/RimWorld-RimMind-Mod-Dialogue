using System;
using System.Collections.Generic;

namespace RimMind.Dialogue.Core
{
    /// <summary>
    /// Atomically owns the Pawn and pair reservations for an in-flight dialogue request.
    /// </summary>
    public sealed class DialogueRequestReservations
    {
        private readonly object _gate = new object();
        private readonly HashSet<int> _pawns = new HashSet<int>();
        private readonly HashSet<(int, int)> _pairs = new HashSet<(int, int)>();
        private long _generation;
        private long _nextReservationId;

        public int ActivePawnCount
        {
            get
            {
                lock (_gate)
                    return _pawns.Count;
            }
        }

        public int ActivePairCount
        {
            get
            {
                lock (_gate)
                    return _pairs.Count;
            }
        }

        public bool TryAcquire(
            int pawnId,
            (int, int)? pairKey,
            int maximumConcurrent,
            out DialogueReservation? reservation)
        {
            lock (_gate)
            {
                if (maximumConcurrent <= 0
                    || _pawns.Count >= maximumConcurrent
                    || _pawns.Contains(pawnId)
                    || (pairKey.HasValue && _pairs.Contains(pairKey.Value)))
                {
                    reservation = null;
                    return false;
                }

                _pawns.Add(pawnId);
                if (pairKey.HasValue)
                    _pairs.Add(pairKey.Value);

                reservation = new DialogueReservation(
                    this,
                    pawnId,
                    pairKey,
                    _generation,
                    ++_nextReservationId);
                return true;
            }
        }

        public bool IsPawnPending(int pawnId)
        {
            lock (_gate)
                return _pawns.Contains(pawnId);
        }

        public bool IsPairPending((int, int) pairKey)
        {
            lock (_gate)
                return _pairs.Contains(pairKey);
        }

        public void Reset()
        {
            lock (_gate)
            {
                _generation++;
                _pawns.Clear();
                _pairs.Clear();
            }
        }

        private void Release(
            int pawnId,
            (int, int)? pairKey,
            long generation)
        {
            lock (_gate)
            {
                if (generation != _generation)
                    return;

                _pawns.Remove(pawnId);
                if (pairKey.HasValue)
                    _pairs.Remove(pairKey.Value);
            }
        }

        public sealed class DialogueReservation : IDisposable
        {
            private DialogueRequestReservations? _owner;
            private readonly int _pawnId;
            private readonly (int, int)? _pairKey;
            private readonly long _generation;

            internal DialogueReservation(
                DialogueRequestReservations owner,
                int pawnId,
                (int, int)? pairKey,
                long generation,
                long id)
            {
                _owner = owner;
                _pawnId = pawnId;
                _pairKey = pairKey;
                _generation = generation;
                Id = id;
            }

            public long Id { get; }

            public void Dispose()
            {
                DialogueRequestReservations? owner =
                    System.Threading.Interlocked.Exchange(ref _owner, null);
                owner?.Release(_pawnId, _pairKey, _generation);
            }
        }
    }
}
