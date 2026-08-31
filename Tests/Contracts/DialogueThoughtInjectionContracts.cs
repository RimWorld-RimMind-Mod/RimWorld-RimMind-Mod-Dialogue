using System;
using System.Linq;
using RimMind.Dialogue.Core;
using RimMind.Testing;
using Verse;
using Xunit;

namespace RimMind.Dialogue.Tests.Contracts
{
    public sealed class DialogueThoughtInjectionContracts
    {
        [Fact]
        public void Stable_thought_and_lifecycle_boundaries()
        {
            ContractCaseRunner.Run(
                ("built in thought tags retain mood offsets", BuiltInTagsRetainOffsets),
                ("thought tags are case insensitive and externally extensible", TagsAreExtensible),
                ("unknown thought tags degrade without mood changes", UnknownTagsDegradeSafely),
                ("dialogue lifecycle records only non reply paired dialogue", LifecycleQuotaSemanticsRemainExplicit),
                ("thought payload fields remain save compatible", ThoughtPayloadRemainsSaveCompatible));
        }

        private static void BuiltInTagsRetainOffsets()
        {
            Assert.Equal(1, ThoughtInjector.MapTagToMoodOffset("ENCOURAGED"));
            Assert.Equal(-1, ThoughtInjector.MapTagToMoodOffset("HURT"));
            Assert.Equal(2, ThoughtInjector.MapTagToMoodOffset("VALUED"));
            Assert.Equal(2, ThoughtInjector.MapTagToMoodOffset("CONNECTED"));
            Assert.Equal(-2, ThoughtInjector.MapTagToMoodOffset("STRESSED"));
            Assert.Equal(-1, ThoughtInjector.MapTagToMoodOffset("IRRITATED"));
        }

        private static void TagsAreExtensible()
        {
            string tag = "contract_external_tag_" + Guid.NewGuid().ToString("N");
            ThoughtInjector.RegisterThoughtTag(tag, 3, "RimMind.Dialogue.Thought.ContractExternal");

            Assert.Equal(3, ThoughtInjector.MapTagToMoodOffset(tag.ToLowerInvariant()));
            Assert.Equal(
                "RimMind.Dialogue.Thought.ContractExternal",
                ThoughtInjector.MapTagToLabel(tag.ToUpperInvariant()));
        }

        private static void UnknownTagsDegradeSafely()
        {
            string tag = "CONTRACT_UNKNOWN_" + Guid.NewGuid().ToString("N");
            Assert.Equal(0, ThoughtInjector.MapTagToMoodOffset(tag));
            Assert.Equal(tag, ThoughtInjector.MapTagToLabel(tag));
        }

        private static void LifecycleQuotaSemanticsRemainExplicit()
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

        private static void ThoughtPayloadRemainsSaveCompatible()
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
