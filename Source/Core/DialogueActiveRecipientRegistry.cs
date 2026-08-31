using System.Collections.Generic;

namespace RimMind.Dialogue.Core
{
    /// <summary>
    /// Ownership-aware active-recipient state. An older request cannot clear a
    /// recipient registered by a newer or UI-owned interaction.
    /// </summary>
    public sealed class DialogueActiveRecipientRegistry
    {
        private readonly object _gate = new object();
        private readonly Dictionary<int, int> _manualRecipients =
            new Dictionary<int, int>();
        private readonly Dictionary<int, Registration> _requestRecipients =
            new Dictionary<int, Registration>();

        public void SetRequest(int pawnId, int recipientId, long ownerId)
        {
            lock (_gate)
                _requestRecipients[pawnId] = new Registration(recipientId, ownerId);
        }

        public void SetManual(int pawnId, int recipientId)
        {
            lock (_gate)
                _manualRecipients[pawnId] = recipientId;
        }

        public bool TryGetRecipient(int pawnId, out int recipientId)
        {
            lock (_gate)
            {
                if (_manualRecipients.TryGetValue(pawnId, out recipientId))
                    return true;

                if (_requestRecipients.TryGetValue(pawnId, out Registration registration))
                {
                    recipientId = registration.RecipientId;
                    return true;
                }

                recipientId = default;
                return false;
            }
        }

        public bool ClearRequestIfOwned(int pawnId, long ownerId)
        {
            lock (_gate)
            {
                if (!_requestRecipients.TryGetValue(pawnId, out Registration registration)
                    || registration.OwnerId != ownerId)
                {
                    return false;
                }

                return _requestRecipients.Remove(pawnId);
            }
        }

        public void ClearManual(int pawnId)
        {
            lock (_gate)
                _manualRecipients.Remove(pawnId);
        }

        public void Reset()
        {
            lock (_gate)
            {
                _manualRecipients.Clear();
                _requestRecipients.Clear();
            }
        }

        private readonly struct Registration
        {
            public Registration(int recipientId, long ownerId)
            {
                RecipientId = recipientId;
                OwnerId = ownerId;
            }

            public int RecipientId { get; }
            public long OwnerId { get; }
        }
    }
}
