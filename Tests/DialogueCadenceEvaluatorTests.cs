using System;
using System.Collections.Generic;
using RimMind.Dialogue.Core;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    public sealed class DialogueCadenceEvaluatorTests
    {
        private const float DefaultCadenceDays = 3.0f; // 180,000 ticks
        private const float DefaultTargetTicks = DefaultCadenceDays * 60000f; // 180,000f
        private const float DefaultGracePeriodTicks = DefaultTargetTicks * 0.20f; // 36,000f

        [Fact]
        public void GracePeriod_WhenElapsedLessThanTwentyPercent_AdmissionProbabilityIsZeroAndShouldAdmitReturnsFalse()
        {
            var evaluator = new DialogueCadenceEvaluator();
            var pair = (1, 2);
            int lastTick = 100_000;
            evaluator.RecordDialogue(pair, lastTick);

            // Elapsed = 0 (immediately after previous dialogue)
            float probZero = evaluator.CalculateProbability(pair, currentTick: lastTick, targetCadenceDays: DefaultCadenceDays, opinion: 0f);
            Assert.Equal(0f, probZero);
            Assert.False(evaluator.ShouldAdmit(pair, currentTick: lastTick, targetCadenceDays: DefaultCadenceDays, opinion: 0f, testRoll: 0f));

            // Elapsed = 10,000 ticks (~5.5% of target interval, well within 20% grace period)
            int tickMidGrace = lastTick + 10_000;
            float probMidGrace = evaluator.CalculateProbability(pair, currentTick: tickMidGrace, targetCadenceDays: DefaultCadenceDays, opinion: 0f);
            Assert.Equal(0f, probMidGrace);
            Assert.False(evaluator.ShouldAdmit(pair, currentTick: tickMidGrace, targetCadenceDays: DefaultCadenceDays, opinion: 0f, testRoll: 0f));

            // Elapsed = 35,999 ticks (just 1 tick below the 36,000 tick threshold), even with maximum positive opinion (+100)
            int tickJustBelowGrace = lastTick + (int)DefaultGracePeriodTicks - 1;
            float probJustBelow = evaluator.CalculateProbability(pair, currentTick: tickJustBelowGrace, targetCadenceDays: DefaultCadenceDays, opinion: 100f);
            Assert.Equal(0f, probJustBelow);
            Assert.False(evaluator.ShouldAdmit(pair, currentTick: tickJustBelowGrace, targetCadenceDays: DefaultCadenceDays, opinion: 100f, testRoll: 0f));
        }

        [Fact]
        public void TargetIntervalReached_WhenElapsedGreaterOrEqualToTarget_ProbabilityIsAtLeastOneAndShouldAdmitReturnsTrue()
        {
            var evaluator = new DialogueCadenceEvaluator();
            var pair = (1, 2);
            evaluator.RecordDialogue(pair, 0);

            // Exactly at target interval (180,000 ticks = 3 days)
            int targetTick = (int)DefaultTargetTicks;
            float probAtTarget = evaluator.CalculateProbability(pair, currentTick: targetTick, targetCadenceDays: DefaultCadenceDays, opinion: 0f);
            Assert.True(probAtTarget >= 1.0f);
            Assert.Equal(1.0f, probAtTarget);
            Assert.True(evaluator.ShouldAdmit(pair, currentTick: targetTick, targetCadenceDays: DefaultCadenceDays, opinion: 0f, testRoll: 0.999f));
            Assert.True(evaluator.ShouldAdmit(pair, currentTick: targetTick, targetCadenceDays: DefaultCadenceDays, opinion: 0f, testRoll: 1.0f));

            // Well past target interval (250,000 ticks)
            int pastTargetTick = 250_000;
            float probPastTarget = evaluator.CalculateProbability(pair, currentTick: pastTargetTick, targetCadenceDays: DefaultCadenceDays, opinion: 0f);
            Assert.Equal(1.0f, probPastTarget);
            Assert.True(evaluator.ShouldAdmit(pair, currentTick: pastTargetTick, targetCadenceDays: DefaultCadenceDays, opinion: 0f, testRoll: 1.0f));

            // Extremely small cadence (<= 0.05 days) fast-paths to 1.0 immediately
            float instantProb = evaluator.CalculateProbability(pair, currentTick: 1, targetCadenceDays: 0.01f, opinion: 0f);
            Assert.Equal(1.0f, instantProb);
            Assert.True(evaluator.ShouldAdmit(pair, currentTick: 1, targetCadenceDays: 0.01f, opinion: 0f));
        }

        [Fact]
        public void IntermediateProgression_WhenElapsedBetweenTwentyAndOneHundredPercent_ScalesProbabilitySmoothly()
        {
            var evaluator = new DialogueCadenceEvaluator();
            var pair = (1, 2);
            evaluator.RecordDialogue(pair, 0);

            // Target = 180,000; Grace = 36,000; Active window = 144,000 ticks
            // Exactly at 20% (36,000 ticks): progress = 0.0
            float p20 = evaluator.CalculateProbability(pair, currentTick: 36_000, targetCadenceDays: DefaultCadenceDays, opinion: 0f);
            Assert.Equal(0f, p20);

            // At 40% of target interval (72,000 ticks): elapsed = 72k, progress = (72k - 36k) / 144k = 0.25
            float p40 = evaluator.CalculateProbability(pair, currentTick: 72_000, targetCadenceDays: DefaultCadenceDays, opinion: 0f);
            Assert.InRange(p40, 0.249f, 0.251f);

            // At 60% of target interval (108,000 ticks): elapsed = 108k, progress = (108k - 36k) / 144k = 0.50
            float p60 = evaluator.CalculateProbability(pair, currentTick: 108_000, targetCadenceDays: DefaultCadenceDays, opinion: 0f);
            Assert.InRange(p60, 0.499f, 0.501f);

            // At 80% of target interval (144,000 ticks): elapsed = 144k, progress = (144k - 36k) / 144k = 0.75
            float p80 = evaluator.CalculateProbability(pair, currentTick: 144_000, targetCadenceDays: DefaultCadenceDays, opinion: 0f);
            Assert.InRange(p80, 0.749f, 0.751f);

            // Strictly monotonically increasing
            Assert.True(p20 < p40);
            Assert.True(p40 < p60);
            Assert.True(p60 < p80);
            Assert.True(p80 < 1.0f);
        }

        [Fact]
        public void ShouldAdmit_WithDeterministicTestRoll_VerifiesRollBelowProbSucceedsAndAtOrAboveFails()
        {
            var evaluator = new DialogueCadenceEvaluator();
            var pair = (1, 2);
            evaluator.RecordDialogue(pair, 0);

            // At 108,000 ticks (60% of 180,000), prob = 0.50 with neutral opinion
            int currentTick = 108_000;
            float prob = evaluator.CalculateProbability(pair, currentTick, DefaultCadenceDays, opinion: 0f);
            Assert.InRange(prob, 0.499f, 0.501f);

            // Roll strictly lower than prob -> succeeds
            Assert.True(evaluator.ShouldAdmit(pair, currentTick, DefaultCadenceDays, opinion: 0f, testRoll: 0.0f));
            Assert.True(evaluator.ShouldAdmit(pair, currentTick, DefaultCadenceDays, opinion: 0f, testRoll: 0.49f));

            // Roll equal to prob -> fails
            Assert.False(evaluator.ShouldAdmit(pair, currentTick, DefaultCadenceDays, opinion: 0f, testRoll: 0.50f));

            // Roll strictly greater than prob -> fails
            Assert.False(evaluator.ShouldAdmit(pair, currentTick, DefaultCadenceDays, opinion: 0f, testRoll: 0.51f));
            Assert.False(evaluator.ShouldAdmit(pair, currentTick, DefaultCadenceDays, opinion: 0f, testRoll: 0.99f));
        }

        [Fact]
        public void Staggering_ForNewPairs_InitializesNonZeroDeterministicInitialTicks()
        {
            var evaluator = new DialogueCadenceEvaluator();
            var pairA = (10, 20);
            var pairB = (30, 40);

            // Unrecorded pairs initially return -1
            Assert.Equal(-1, evaluator.GetLastDialogueTick(pairA));
            Assert.Equal(-1, evaluator.GetLastDialogueTick(pairB));

            int currentTick = 200_000;
            // Calculating probability triggers staggering initialization
            evaluator.CalculateProbability(pairA, currentTick, DefaultCadenceDays, opinion: 0f);

            int initialTickA = evaluator.GetLastDialogueTick(pairA);
            Assert.True(initialTickA > 0, $"Expected positive initial tick, got {initialTickA}");

            // Verify stagger formula: staggerOffset is bounded by [0, 0.75 * targetTicks)
            int expectedOffsetA = Math.Abs((pairA.Item1 * 397) ^ pairA.Item2) % (int)(DefaultTargetTicks * 0.75f);
            Assert.Equal(currentTick - expectedOffsetA, initialTickA);
            Assert.InRange(expectedOffsetA, 0, (int)(DefaultTargetTicks * 0.75f) - 1);

            // Deterministic across different evaluator instances with same pairKey and tick
            var evaluator2 = new DialogueCadenceEvaluator();
            evaluator2.CalculateProbability(pairA, currentTick, DefaultCadenceDays, opinion: 0f);
            Assert.Equal(initialTickA, evaluator2.GetLastDialogueTick(pairA));

            // Different pairs produce different stagger offsets / initial ticks
            evaluator.CalculateProbability(pairB, currentTick, DefaultCadenceDays, opinion: 0f);
            int initialTickB = evaluator.GetLastDialogueTick(pairB);
            Assert.NotEqual(initialTickA, initialTickB);
        }

        [Fact]
        public void OpinionModifier_HighOpinionBoostsProbability_LowOpinionDampensProbability()
        {
            var evaluator = new DialogueCadenceEvaluator();
            var pair = (1, 2);
            evaluator.RecordDialogue(pair, 0);

            // Midpoint: currentTick = 108,000 -> baseChance = 0.50
            int currentTick = 108_000;

            // Neutral opinion: opinionFactor = 1.0 -> prob = 0.50
            float neutralProb = evaluator.CalculateProbability(pair, currentTick, DefaultCadenceDays, opinion: 0f);
            Assert.InRange(neutralProb, 0.499f, 0.501f);

            // High opinion (+100): opinionFactor = 1.0 + 100/500 = 1.20 -> prob = 0.50 * 1.20 = 0.60
            float highProb = evaluator.CalculateProbability(pair, currentTick, DefaultCadenceDays, opinion: 100f);
            Assert.InRange(highProb, 0.599f, 0.601f);
            Assert.True(highProb > neutralProb);

            // Low opinion (-100): opinionFactor = 1.0 + (-100)/500 = 0.80 -> prob = 0.50 * 0.80 = 0.40
            float lowProb = evaluator.CalculateProbability(pair, currentTick, DefaultCadenceDays, opinion: -100f);
            Assert.InRange(lowProb, 0.399f, 0.401f);
            Assert.True(lowProb < neutralProb);

            // Verify opinion clamp limits: +300 should clamp to +0.2 (factor = 1.20, same as +100)
            float extremeHighProb = evaluator.CalculateProbability(pair, currentTick, DefaultCadenceDays, opinion: 300f);
            Assert.Equal(highProb, extremeHighProb);

            // Verify opinion clamp limits: -300 should clamp to -0.2 (factor = 0.80, same as -100)
            float extremeLowProb = evaluator.CalculateProbability(pair, currentTick, DefaultCadenceDays, opinion: -300f);
            Assert.Equal(lowProb, extremeLowProb);
        }

        [Fact]
        public void RecordDialogue_UpdatesLastDialogueTick_AndRetainsMaximumTick()
        {
            var evaluator = new DialogueCadenceEvaluator();
            var pair = (5, 9);

            evaluator.RecordDialogue(pair, 1000);
            Assert.Equal(1000, evaluator.GetLastDialogueTick(pair));

            evaluator.RecordDialogue(pair, 3000);
            Assert.Equal(3000, evaluator.GetLastDialogueTick(pair));

            // Recording an earlier tick should NOT regress the last dialogue tick
            evaluator.RecordDialogue(pair, 2000);
            Assert.Equal(3000, evaluator.GetLastDialogueTick(pair));

            evaluator.Clear();
            Assert.Equal(-1, evaluator.GetLastDialogueTick(pair));
        }

        [Fact]
        public void RebuildFromEntries_UpdatesLastDialogueTickCorrectly_AndSkipsInvalidOrMonologueEntries()
        {
            var evaluator = new DialogueCadenceEvaluator();

            var entries = new List<DialogueLogEntry>
            {
                // Pair (1, 2) entry 1
                new DialogueLogEntry { initiatorId = 1, recipientId = 2, tick = 1000 },
                // Pair (1, 2) entry 2 in reverse order with higher tick
                new DialogueLogEntry { initiatorId = 2, recipientId = 1, tick = 2500 },
                // Pair (1, 2) entry 3 with earlier tick (should not overwrite 2500)
                new DialogueLogEntry { initiatorId = 1, recipientId = 2, tick = 1500 },
                // Pair (3, 4) entry
                new DialogueLogEntry { initiatorId = 3, recipientId = 4, tick = 4000 },
                // Monologue entry (recipientId <= 0) should be ignored
                new DialogueLogEntry { initiatorId = 5, recipientId = -1, tick = 5000 },
                new DialogueLogEntry { initiatorId = 6, recipientId = 0, tick = 6000 },
                // Invalid initiator (initiatorId <= 0) should be ignored
                new DialogueLogEntry { initiatorId = 0, recipientId = 7, tick = 7000 },
                // Null entry simulation handled safely
                null!
            };

            evaluator.RebuildFromEntries(entries);

            Assert.Equal(2500, evaluator.GetLastDialogueTick((1, 2)));
            Assert.Equal(4000, evaluator.GetLastDialogueTick((3, 4)));
            Assert.Equal(-1, evaluator.GetLastDialogueTick((5, -1)));
            Assert.Equal(-1, evaluator.GetLastDialogueTick((6, 0)));
            Assert.Equal(-1, evaluator.GetLastDialogueTick((0, 7)));

            // Calling with null should clear without throwing
            evaluator.RebuildFromEntries(null);
            Assert.Equal(-1, evaluator.GetLastDialogueTick((1, 2)));
            Assert.Equal(-1, evaluator.GetLastDialogueTick((3, 4)));
        }
    }
}
