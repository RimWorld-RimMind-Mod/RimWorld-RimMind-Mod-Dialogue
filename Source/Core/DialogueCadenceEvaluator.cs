using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace RimMind.Dialogue.Core
{
    public sealed class DialogueCadenceEvaluator
    {
        private readonly ConcurrentDictionary<(int, int), int> _lastPairDialogueTicks =
            new ConcurrentDictionary<(int, int), int>();

        public void RecordDialogue((int, int) pairKey, int tick)
        {
            _lastPairDialogueTicks.AddOrUpdate(pairKey, tick, (_, prev) => Math.Max(prev, tick));
        }

        public void RebuildFromEntries(IEnumerable<DialogueLogEntry>? entries)
        {
            _lastPairDialogueTicks.Clear();
            if (entries == null) return;
            foreach (var entry in entries)
            {
                if (entry == null || entry.recipientId <= 0 || entry.initiatorId <= 0) continue;
                var key = DialogueClassifier.MakePairKey(entry.initiatorId, entry.recipientId);
                _lastPairDialogueTicks.AddOrUpdate(key, entry.tick, (_, prev) => Math.Max(prev, entry.tick));
            }
        }

        public void Clear()
        {
            _lastPairDialogueTicks.Clear();
        }

        public int GetLastDialogueTick((int, int) pairKey)
        {
            return _lastPairDialogueTicks.TryGetValue(pairKey, out int tick) ? tick : -1;
        }

        /// <summary>
        /// Calculates admission probability [0, 1] for an interaction between pairKey at currentTick.
        /// </summary>
        public float CalculateProbability(
            (int, int) pairKey,
            int currentTick,
            float targetCadenceDays,
            float opinion)
        {
            if (targetCadenceDays <= 0.05f) return 1.0f;

            float targetTicks = targetCadenceDays * 60000f;
            if (!_lastPairDialogueTicks.TryGetValue(pairKey, out int lastTick) || lastTick < 0)
            {
                int staggerOffset = Math.Abs((pairKey.Item1 * 397) ^ pairKey.Item2) % (int)(targetTicks * 0.75f);
                lastTick = Math.Max(0, currentTick - staggerOffset);
                _lastPairDialogueTicks.TryAdd(pairKey, lastTick);
            }

            int elapsed = Math.Max(0, currentTick - lastTick);
            float minGracePeriod = targetTicks * 0.20f;
            if (elapsed < minGracePeriod)
            {
                return 0f;
            }

            float progress = (elapsed - minGracePeriod) / (targetTicks - minGracePeriod);
            float baseChance = Mathf.Clamp01(progress);

            float opinionFactor = 1.0f + Mathf.Clamp(opinion / 500f, -0.2f, 0.2f);
            return Mathf.Clamp01(baseChance * opinionFactor);
        }

        public bool ShouldAdmit(
            (int, int) pairKey,
            int currentTick,
            float targetCadenceDays,
            float opinion,
            float? testRoll = null)
        {
            float prob = CalculateProbability(pairKey, currentTick, targetCadenceDays, opinion);
            if (prob <= 0f) return false;
            if (prob >= 1f) return true;

            float roll = testRoll ?? UnityEngine.Random.value;
            return roll < prob;
        }
    }
}
