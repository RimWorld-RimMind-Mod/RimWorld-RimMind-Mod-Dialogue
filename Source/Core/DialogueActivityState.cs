using System.Collections.Concurrent;
using System.Collections.Generic;
using RimMind.Dialogue.Settings;
using Verse;

namespace RimMind.Dialogue.Core
{
    internal sealed class DialogueActivityState
    {
        private readonly DialoguePairRateLimiter _replyRateLimiter =
            new DialoguePairRateLimiter();
        private readonly DialogueActiveRecipientRegistry _activeRecipients =
            new DialogueActiveRecipientRegistry();
        private readonly List<(int tick, int pawnId, DialogueTriggerType type)> _recentTriggers =
            new List<(int, int, DialogueTriggerType)>();
        private readonly ConcurrentDictionary<(int, int), List<int>> _dailyDialogueCounts =
            new ConcurrentDictionary<(int, int), List<int>>();
        private readonly Dictionary<int, Pawn> _pawnCache =
            new Dictionary<int, Pawn>();

        private int _gameStartTick = -1;
        private int _lastCountDay = -1;
        private int _pawnCacheTick = -1;

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
            _pawnCache.Clear();
            _pawnCacheTick = -1;
        }

        public void RecordTrigger(
            int currentTick,
            int pawnId,
            DialogueTriggerType type,
            int cooldownTicks)
        {
            CleanExpiredTriggers(currentTick, cooldownTicks);
            _recentTriggers.Add((currentTick, pawnId, type));
        }

        public bool IsMonologueOnCooldown(
            int currentTick,
            int pawnId,
            DialogueTriggerType type,
            int cooldownTicks)
        {
            foreach (var entry in _recentTriggers)
            {
                if (entry.pawnId == pawnId
                    && entry.type == type
                    && currentTick - entry.tick < cooldownTicks)
                {
                    return true;
                }
            }

            return false;
        }

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
            List<int> ticks = _dailyDialogueCounts.GetOrAdd(
                key,
                _ => new List<int>());
            ticks.Add(currentTick);
        }

        public int GetDailyDialogueCount(
            int currentTick,
            int idA,
            int idB)
        {
            CleanExpiredDailyCounts(currentTick);
            var key = DialogueClassifier.MakePairKey(idA, idB);
            return _dailyDialogueCounts.TryGetValue(key, out List<int> ticks)
                ? ticks.Count
                : 0;
        }

        public void SetRequestRecipient(
            int pawnId,
            int recipientId,
            long reservationId)
            => _activeRecipients.SetRequest(pawnId, recipientId, reservationId);

        public bool ClearRequestRecipientIfOwned(
            int pawnId,
            long reservationId)
            => _activeRecipients.ClearRequestIfOwned(pawnId, reservationId);

        public void SetManualRecipient(int pawnId, int? recipientId)
        {
            if (recipientId.HasValue)
                _activeRecipients.SetManual(pawnId, recipientId.Value);
            else
                _activeRecipients.ClearManual(pawnId);
        }

        public Pawn? GetActiveRecipient(Pawn pawn, int currentTick)
        {
            if (!_activeRecipients.TryGetRecipient(
                    pawn.thingIDNumber,
                    out int recipientId))
            {
                return null;
            }

            if (_pawnCacheTick < 0 || currentTick - _pawnCacheTick >= 600)
                RebuildPawnCache(currentTick);

            return _pawnCache.TryGetValue(recipientId, out Pawn cached)
                ? cached
                : null;
        }

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

        private void RebuildPawnCache(int currentTick)
        {
            _pawnCache.Clear();
            foreach (Map map in Find.Maps)
            {
                if (map.mapPawns == null)
                    continue;

                foreach (Pawn candidate in map.mapPawns.AllPawns)
                    _pawnCache[candidate.thingIDNumber] = candidate;
            }

            if (Find.WorldPawns?.AllPawnsAlive != null)
            {
                foreach (Pawn candidate in Find.WorldPawns.AllPawnsAlive)
                    _pawnCache[candidate.thingIDNumber] = candidate;
            }

            _pawnCacheTick = currentTick;
        }

        private void CleanExpiredDailyCounts(int currentTick)
        {
            int today = CurrentGameDay(currentTick);
            if (today == _lastCountDay)
                return;

            _dailyDialogueCounts.Clear();
            _lastCountDay = today;
        }

        private void CleanExpiredTriggers(
            int currentTick,
            int cooldownTicks)
            => _recentTriggers.RemoveAll(
                entry => currentTick - entry.tick >= cooldownTicks);

        private static int CurrentGameDay(int currentTick)
            => (int)(currentTick / 2500f / 24f);
    }
}
