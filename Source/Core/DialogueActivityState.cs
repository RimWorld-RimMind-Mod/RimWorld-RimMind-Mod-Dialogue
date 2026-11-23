using System.Collections.Concurrent;
using RimMind.Application.Common.Helpers;
using RimMind.Dialogue.Settings;
using RimMind.Presentation.Api;
using Verse;

namespace RimMind.Dialogue.Core
{
    internal sealed class DialogueActivityState
    {
        private readonly DialoguePairRateLimiter _replyRateLimiter =
            new DialoguePairRateLimiter();
        private readonly DialogueActiveRecipientRegistry _activeRecipients =
            new DialogueActiveRecipientRegistry();
        private readonly KeyedCooldownGate<(int PawnId, DialogueTriggerType Type)> _recentTriggers =
            new KeyedCooldownGate<(int PawnId, DialogueTriggerType Type)>(256);
        private readonly ConcurrentDictionary<(int, int), int> _dailyDialogueCounts =
            new ConcurrentDictionary<(int, int), int>();

        private int _gameStartTick = -1;
        private int _lastCountDay = -1;

        public int RecentTriggerCount => _recentTriggers.Count;

        public int DailyPairCount => _dailyDialogueCounts.Count;

        public bool IsReady(
            RimMindDialogueSettings settings,
            int currentTick)
        {
            if (_gameStartTick < 0)
                _gameStartTick = currentTick;

            return !settings.startDelayEnabled
                || currentTick - _gameStartTick >= settings.startDelayTicks;
        }

        public void Reset(int currentTick)
        {
            _gameStartTick = currentTick;
            _replyRateLimiter.Reset();
            _activeRecipients.Reset();
            _recentTriggers.Clear();
            _dailyDialogueCounts.Clear();
            _lastCountDay = -1;
        }

        public void RecordTrigger(
            int currentTick,
            int pawnId,
            DialogueTriggerType type,
            int cooldownTicks)
        {
            _recentTriggers.Record((pawnId, type), currentTick);
        }

        public bool IsMonologueOnCooldown(
            int currentTick,
            int pawnId,
            DialogueTriggerType type,
            int cooldownTicks)
            => _recentTriggers.IsOnCooldown((pawnId, type), currentTick, cooldownTicks);

        public bool TryConsumeReply(
            (int, int) pairKey,
            int currentDay,
            int maximumRounds)
            => _replyRateLimiter.TryConsume(pairKey, currentDay, maximumRounds);

        public bool IsDailyLimitReached(
            int currentTick,
            int idA,
            int idB,
            int maximumRounds)
            => GetDailyDialogueCount(currentTick, idA, idB) >= maximumRounds;

        public void RecordDailyDialogue(
            int currentTick,
            int idA,
            int idB)
        {
            CleanExpiredDailyCounts(currentTick);
            var key = DialogueClassifier.MakePairKey(idA, idB);
            _dailyDialogueCounts.AddOrUpdate(key, 1, (_, count) => count + 1);
        }

        public int GetDailyDialogueCount(
            int currentTick,
            int idA,
            int idB)
        {
            CleanExpiredDailyCounts(currentTick);
            var key = DialogueClassifier.MakePairKey(idA, idB);
            return _dailyDialogueCounts.TryGetValue(key, out int count)
                ? count
                : 0;
        }

        public void SetRequestRecipient(
            int pawnId,
            int recipientId,
            long reservationId,
            Pawn? recipient = null)
        {
            _activeRecipients.SetRequest(pawnId, recipientId, reservationId);
            if (recipient != null)
            {
                RimMindPawnLookup.CachePawn(recipient);
            }
        }

        public bool ClearRequestRecipientIfOwned(
            int pawnId,
            long reservationId)
            => _activeRecipients.ClearRequestIfOwned(pawnId, reservationId);

        public void SetManualRecipient(int pawnId, int? recipientId, Pawn? recipient = null)
        {
            if (recipientId.HasValue)
            {
                _activeRecipients.SetManual(pawnId, recipientId.Value);
                if (recipient != null)
                {
                    RimMindPawnLookup.CachePawn(recipient);
                }
            }
            else
            {
                _activeRecipients.ClearManual(pawnId);
            }
        }

        public Pawn? GetActiveRecipient(Pawn pawn, int currentTick)
        {
            if (!_activeRecipients.TryGetRecipient(
                    pawn.thingIDNumber,
                    out int recipientId))
            {
                return null;
            }

            var lookupPawn = FindPawnByNumber(recipientId);
            if (lookupPawn != null && !lookupPawn.DestroyedOrNull() && RimMindPawnLookup.IsActiveSpawnedColonist(lookupPawn))
            {
                return lookupPawn;
            }

            return null;
        }

        public static Pawn? FindPawnByNumber(int pawnNumber)
            => RimMindPawnLookup.FindPawnByNumber(pawnNumber);

        public (int RecentTriggers, int DailyPairs) ClearCooldowns()
        {
            int recentTriggers = _recentTriggers.Count;
            int dailyPairs = _dailyDialogueCounts.Count;
            _recentTriggers.Clear();
            _dailyDialogueCounts.Clear();
            _replyRateLimiter.Reset();
            _lastCountDay = -1;
            return (recentTriggers, dailyPairs);
        }

        private void CleanExpiredDailyCounts(int currentTick)
        {
            int today = CurrentGameDay(currentTick);
            if (today == _lastCountDay)
                return;

            _dailyDialogueCounts.Clear();
            _lastCountDay = today;
        }

        private static int CurrentGameDay(int currentTick)
            => (int)(currentTick / 2500f / 24f);
    }
}
