using System;
using System.Linq;
using RimMind.Dialogue.Core;
using Verse;
using Xunit;

namespace RimMind.Dialogue.Tests.Contracts
{
    public sealed class DialogueThoughtInjectionContracts
    {
        [Fact]
        public void BuiltInTagsRetainOffsets()
        {
            Assert.Equal(1, ThoughtInjector.MapTagToMoodOffset("ENCOURAGED"));
            Assert.Equal(-1, ThoughtInjector.MapTagToMoodOffset("HURT"));
            Assert.Equal(2, ThoughtInjector.MapTagToMoodOffset("VALUED"));
            Assert.Equal(2, ThoughtInjector.MapTagToMoodOffset("CONNECTED"));
            Assert.Equal(-2, ThoughtInjector.MapTagToMoodOffset("STRESSED"));
            Assert.Equal(-1, ThoughtInjector.MapTagToMoodOffset("IRRITATED"));
        }

        [Fact]
        public void TagsAreExtensible()
        {
            string tag = "contract_external_tag_" + Guid.NewGuid().ToString("N");
            ThoughtInjector.RegisterThoughtTag(tag, 3, "RimMind.Dialogue.Thought.ContractExternal");

            Assert.Equal(3, ThoughtInjector.MapTagToMoodOffset(tag.ToLowerInvariant()));
            Assert.Equal(
                "RimMind.Dialogue.Thought.ContractExternal",
                ThoughtInjector.MapTagToLabel(tag.ToUpperInvariant()));
        }

        [Fact]
        public void UnknownTagsDegradeSafely()
        {
            string tag = "CONTRACT_UNKNOWN_" + Guid.NewGuid().ToString("N");
            Assert.Equal(0, ThoughtInjector.MapTagToMoodOffset(tag));
            Assert.Equal(tag, ThoughtInjector.MapTagToLabel(tag));
        }

        [Fact]
        public void MapTagToLabelFallsBackToTagWhenKeyCannotTranslate()
        {
            string tag = "UNTRANSLATED_TAG_" + Guid.NewGuid().ToString("N");
            string key = "RimMind.Dialogue.Thought.MissingTranslation";
            ThoughtInjector.RegisterThoughtTag(tag, 1, key);

            try
            {
                Extensions.UntranslatableKeys.Add(key);
                // When key cannot be translated, raw key is NOT returned; falls back to tag
                string result = ThoughtInjector.MapTagToLabel(tag);
                Assert.Equal(tag, result);
            }
            finally
            {
                Extensions.UntranslatableKeys.Remove(key);
            }
        }

        [Fact]
        public void ThoughtRimMindDialogueLabelCapHandlesFallback()
        {
            var thought = new Thought_RimMindDialogue();

            // Case 1: Can translate
            thought.aiLabel = "TestLabel";
            Assert.Equal("[RimMind] TestLabel", thought.LabelCap);

            // Case 2: Cannot translate and starts with RimMind. -> strips to last segment
            string missingKey = "RimMind.Dialogue.Thought.SPECIAL_MOOD";
            try
            {
                Extensions.UntranslatableKeys.Add(missingKey);
                thought.aiLabel = missingKey;
                Assert.Equal("[RimMind] SPECIAL_MOOD", thought.LabelCap);
            }
            finally
            {
                Extensions.UntranslatableKeys.Remove(missingKey);
            }

            // Case 3: Cannot translate and does NOT start with RimMind. -> uses text directly
            string rawTag = "CUSTOM_EMOTE";
            try
            {
                Extensions.UntranslatableKeys.Add(rawTag);
                thought.aiLabel = rawTag;
                Assert.Equal("[RimMind] CUSTOM_EMOTE", thought.LabelCap);
            }
            finally
            {
                Extensions.UntranslatableKeys.Remove(rawTag);
            }
        }

        [Fact]
        public void LifecycleQuotaSemanticsRemainExplicit()
        {
            Assert.True(DialogueFlowPolicy.IsMonologue(DialogueTriggerType.Auto, hasRecipient: false));
            Assert.False(DialogueFlowPolicy.IsMonologue(
                DialogueTriggerType.PlayerInput,
                hasRecipient: false));
            Assert.True(DialogueFlowPolicy.UsesDailyQuota(
                DialogueTriggerType.Chitchat,
                hasRecipient: true,
                isReply: false));
            Assert.False(DialogueFlowPolicy.UsesDailyQuota(
                DialogueTriggerType.Chitchat,
                hasRecipient: true,
                isReply: true));
            Assert.True(DialogueFlowPolicy.UsesMonologueCooldown(
                DialogueTriggerType.Thought,
                hasRecipient: false));

            Assert.True(DialogueFlowPolicy.ShouldAutoReply(
                DialogueTriggerType.Chitchat,
                hasRecipient: true,
                isReply: false,
                repliesEnabled: true));
            Assert.True(DialogueFlowPolicy.ShouldAutoReply(
                DialogueTriggerType.Chitchat,
                hasRecipient: true,
                isReply: true,
                repliesEnabled: true));
            Assert.False(DialogueFlowPolicy.ShouldAutoReply(
                DialogueTriggerType.PlayerInput,
                hasRecipient: true,
                isReply: false,
                repliesEnabled: true));

            var replyLimiter = new DialoguePairRateLimiter();
            Assert.True(replyLimiter.TryConsume((1, 2), day: 1, maximumPerDay: 2));
            Assert.True(replyLimiter.TryConsume((1, 2), day: 1, maximumPerDay: 2));
            Assert.False(replyLimiter.TryConsume((1, 2), day: 1, maximumPerDay: 2));
            Assert.True(replyLimiter.TryConsume((1, 2), day: 2, maximumPerDay: 2));
        }

        [Fact]
        public void ThoughtPayloadRemainsSaveCompatible()
        {
            Scribe_Values.Reset();
            new Thought_RimMindDialogue().ExposeData();
            Assert.Equal(
                new[] { "aiLabel", "aiDesc", "aiMoodOffset" },
                Scribe_Values.Calls.Select(call => call.Label));
            Assert.Contains(
                Scribe_Values.Calls,
                call => call.Label == "aiMoodOffset" && Equals(call.DefaultValue, 0f));

            Scribe_Values.Reset();
            new Thought_RelationDialogue().ExposeData();
            Assert.Equal(
                new[] { "aiLabel", "aiDesc" },
                Scribe_Values.Calls.Select(call => call.Label));
        }
    }
}
