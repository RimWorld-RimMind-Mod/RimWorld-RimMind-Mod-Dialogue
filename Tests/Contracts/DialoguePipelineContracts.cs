using System;
using System.Linq;
using RimMind.Dialogue.Core;
using Xunit;

namespace RimMind.Dialogue.Tests.Contracts
{
    public sealed class DialoguePipelineContracts
    {
        [Fact]
        public void ClassificationCoversEveryCategory()
        {
            Assert.Equal(
                DialogueCategory.ColonistMonologue,
                DialogueClassifier.Classify(true, null, DialogueTriggerType.Auto));
            Assert.Equal(
                DialogueCategory.NonColonistMonologue,
                DialogueClassifier.Classify(false, null, DialogueTriggerType.Thought));
            Assert.Equal(
                DialogueCategory.ColonistDialogue,
                DialogueClassifier.Classify(true, true, DialogueTriggerType.Chitchat));
            Assert.Equal(
                DialogueCategory.NonColonistDialogue,
                DialogueClassifier.Classify(true, false, DialogueTriggerType.Hediff));
            Assert.Equal(
                DialogueCategory.NonColonistDialogue,
                DialogueClassifier.Classify(false, true, DialogueTriggerType.LevelUp));
            Assert.Equal(
                DialogueCategory.PlayerDialogue,
                DialogueClassifier.Classify(false, null, DialogueTriggerType.PlayerInput));

            Assert.Equal(6, Enum.GetValues<DialogueTriggerType>().Length);
            Assert.Equal(5, Enum.GetValues<DialogueCategory>().Length);
        }

        [Fact]
        public void PairKeysAreStable()
        {
            Assert.Equal((7, 42), DialogueClassifier.MakePairKey(42, 7));
            Assert.Equal((7, 42), DialogueClassifier.MakePairKey(7, 42));

            var dialogue = new DialogueLogEntry { initiatorId = 42, recipientId = 7 };
            var reversed = new DialogueLogEntry { initiatorId = 7, recipientId = 42 };
            var monologue = new DialogueLogEntry { initiatorId = 42, recipientId = -1 };

            Assert.Equal("7|42", dialogue.PairKey);
            Assert.Equal(dialogue.PairKey, reversed.PairKey);
            Assert.Equal("42", monologue.PairKey);
        }

        [Fact]
        public void StructuredRepliesPreserveSemantics()
        {
            string reply = "raw";
            string? tag = "NONE";
            string? description = null;
            int relationDelta = 0;

            #pragma warning disable CS0618
            ResponseJsonParser.TryParseResponseJson(
                "{\"reply\":\"Hello\",\"thought\":{\"tag\":\"VALUED\",\"description\":\"Seen\"},\"relation_delta\":2}",
                false,
                ref reply,
                ref tag,
                ref description,
                ref relationDelta);
            #pragma warning restore CS0618

            Assert.Equal("Hello", reply);
            Assert.Equal("VALUED", tag);
            Assert.Equal("Seen", description);
            Assert.Equal(2, relationDelta);
        }

        [Fact]
        public void MonologuesIgnoreRelationChanges()
        {
            string reply = "raw";
            string? tag = null;
            string? description = null;
            int relationDelta = 9;

            #pragma warning disable CS0618
            ResponseJsonParser.TryParseResponseJson(
                "{\"reply\":\"Thinking\",\"relation_delta\":-5}",
                true,
                ref reply,
                ref tag,
                ref description,
                ref relationDelta);
            #pragma warning restore CS0618

            Assert.Equal("Thinking", reply);
            Assert.Equal(9, relationDelta);
        }

        [Fact]
        public void InvalidRepliesPreservePriorValues()
        {
            AssertPreserved("plain text");
            AssertPreserved("{invalid");
            AssertPreserved(string.Empty);
            AssertPreserved(null);

            string reply = "before";
            string? tag = "OLD";
            string? description = "old description";
            int relationDelta = 4;

            #pragma warning disable CS0618
            ResponseJsonParser.TryParseResponseJson(
                "{\"thought\":{\"tag\":\"CONNECTED\"}}",
                false,
                ref reply,
                ref tag,
                ref description,
                ref relationDelta);
            #pragma warning restore CS0618

            Assert.Equal("before", reply);
            Assert.Equal("CONNECTED", tag);
            Assert.Null(description);
            Assert.Equal(4, relationDelta);
        }

        [Fact]
        public void NarrationAndMarkdownCodeBlockRepliesAreParsed()
        {
            // Case 1: narration field instead of reply
            string reply = "raw";
            string? tag = null;
            string? description = null;
            int relationDelta = 0;

            #pragma warning disable CS0618
            ResponseJsonParser.TryParseResponseJson(
                "{\"narration\":\"你感到全身旧伤\",\"thought\":{\"tag\":\"STRESSED\",\"description\":\"疲惫\"}}",
                true,
                ref reply,
                ref tag,
                ref description,
                ref relationDelta);

            Assert.Equal("你感到全身旧伤", reply);
            Assert.Equal("STRESSED", tag);
            Assert.Equal("疲惫", description);

            // Case 2: Markdown code block wrapped JSON
            reply = "raw2";
            tag = null;
            description = null;
            relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(
                "```json\n{\"dialogue\":\"Hello friend!\",\"thought\":{\"tag\":\"CONNECTED\",\"description\":\"Joy\"}}\n```",
                false,
                ref reply,
                ref tag,
                ref description,
                ref relationDelta);
            #pragma warning restore CS0618

            Assert.Equal("Hello friend!", reply);
            Assert.Equal("CONNECTED", tag);
            Assert.Equal("Joy", description);
        }

        [Fact]
        public void BoundedLogRetainsNewestEntriesAndIsolatesPublishedSnapshots()
        {
            var store = new DialogueLogStore();
            int updates = 0;
            store.Updated += () => updates++;
            store.Add(new DialogueLogEntry { tick = 0, initiatorId = 1, recipientId = 2 });
            var snapshot = store.Entries;
            for (int tick = 1; tick <= 500; tick++)
                store.Add(new DialogueLogEntry { tick = tick, initiatorId = 1, recipientId = 2 });

            Assert.Single(snapshot);
            Assert.Equal(500, store.Entries.Count);
            Assert.DoesNotContain(store.Entries, entry => entry.tick == 0);
            Assert.Equal(new[] { 500, 499, 498 }, store.HistoryFor(2, 3).Select(entry => entry.tick));
            Assert.Empty(store.HistoryFor(3, 10));
            Assert.Equal(501, updates);
            store.Clear();
            Assert.Empty(store.Entries);
            Assert.Single(snapshot);
        }

        [Fact]
        public void ActivityStateSeparatesCooldownsAndResetsDailyQuota()
        {
            var state = new DialogueActivityState();
            state.RecordTrigger(100, 1, DialogueTriggerType.Thought, 60);
            Assert.True(state.IsMonologueOnCooldown(159, 1, DialogueTriggerType.Thought, 60));
            Assert.False(state.IsMonologueOnCooldown(160, 1, DialogueTriggerType.Thought, 60));
            Assert.False(state.IsMonologueOnCooldown(110, 2, DialogueTriggerType.Thought, 60));
            Assert.False(state.IsMonologueOnCooldown(110, 1, DialogueTriggerType.Hediff, 60));

            state.RecordDailyDialogue(100, 1, 2);
            Assert.True(state.IsDailyLimitReached(200, 2, 1, 1));
            Assert.False(state.IsDailyLimitReached(60000, 1, 2, 1));
            state.RecordDailyDialogue(60000, 1, 2);
            state.Reset(60000);
            Assert.Equal(0, state.RecentTriggerCount);
            Assert.Equal(0, state.GetDailyDialogueCount(60000, 1, 2));
        }

        private static void AssertPreserved(string? raw)
        {
            string reply = "before";
            string? tag = "OLD";
            string? description = "old description";
            int relationDelta = 4;

            ResponseJsonParser.TryParseResponseJson(
                raw,
                false,
                ref reply,
                ref tag,
                ref description,
                ref relationDelta);

            Assert.Equal("before", reply);
            Assert.Equal("OLD", tag);
            Assert.Equal("old description", description);
            Assert.Equal(4, relationDelta);
        }

        [Fact]
        public void DialogueOverlayLayoutRemainsVisible()
        {
            OverlayBounds normalized = DialogueOverlayLayout.Normalize(
                new OverlayBounds(-100f, 1200f, 200f, 50f),
                1920f,
                1080f,
                300f,
                100f);
            Assert.Equal(new OverlayBounds(0f, 980f, 300f, 100f), normalized);

            OverlayBounds smallScreen = DialogueOverlayLayout.Normalize(
                new OverlayBounds(10f, 10f, 600f, 300f),
                240f,
                80f,
                300f,
                100f);
            Assert.Equal(new OverlayBounds(0f, 0f, 240f, 80f), smallScreen);

            Assert.Equal(2, DialogueOverlayLayout.FindFirstVisibleIndex(
                new[] { 40f, 40f, 40f, 40f },
                85f));
            Assert.Equal(3, DialogueOverlayLayout.FindFirstVisibleIndex(
                new[] { 40f, 40f, 120f, 140f },
                80f));
        }

    }
}
