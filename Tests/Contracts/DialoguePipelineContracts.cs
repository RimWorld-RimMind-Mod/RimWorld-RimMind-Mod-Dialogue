using System;
using System.Linq;
using RimMind.Application.Common.Models.Debug;
using RimMind.Dialogue.Core;
using UnityEngine;
using Verse;
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
        public void DialogueTriggerRegistry_ProvidesConsistentMetadataAndCustomLabels()
        {
            foreach (DialogueTriggerType trigger in Enum.GetValues(typeof(DialogueTriggerType)))
            {
                Assert.False(string.IsNullOrWhiteSpace(DialogueTriggerRegistry.GetTriggerKind(trigger)));
                Assert.False(string.IsNullOrWhiteSpace(DialogueTriggerRegistry.GetLabel(trigger)));
                Assert.NotEqual(Color.white, DialogueTriggerRegistry.GetLogColor(trigger.ToString()));
            }

            Assert.Equal(Color.white, DialogueTriggerRegistry.GetLogColor("UnknownTriggerType"));

            Extensions.UntranslatableKeys.Add("RimMind.Dialogue.Prompt.Context.Hediff");
            try
            {
                Assert.Equal(
                    "[Health condition: frostbite]",
                    DialogueTriggerRegistry.FormatContext(DialogueTriggerType.Hediff, "frostbite"));
                Assert.Equal(
                    "raw input",
                    DialogueTriggerRegistry.FormatContext(DialogueTriggerType.PlayerInput, "raw input"));
            }
            finally
            {
                Extensions.UntranslatableKeys.Remove("RimMind.Dialogue.Prompt.Context.Hediff");
            }

            string customId = "CustomTrigger_" + Guid.NewGuid().ToString("N");
            DialogueTriggerRegistry.RegisterLabel(customId, "Custom Label");
            Assert.Equal("Custom Label", DialogueTriggerRegistry.GetLabel(customId));
            DialogueTriggerRegistry.CustomLabels.TryRemove(customId, out _);
        }

        [Fact]
        public void DialogueRequestTraceSummarizer_SuppressesExpressDialogueAndSummarizesTraces()
        {
            var summarizer = new DialogueRequestTraceSummarizer();
            Assert.True(summarizer.ShouldSuppressToolCall("express_dialogue"));
            Assert.True(summarizer.ShouldSuppressToolCall("EXPRESS_DIALOGUE"));
            Assert.False(summarizer.ShouldSuppressToolCall("take_job"));

            var entryWithTool = new AIRequestTraceEntry { ModId = "RimMind.Dialogue" };
            entryWithTool.ToolCalls.Add(new AIRequestToolCallTrace(
                "call-1",
                "express_dialogue",
                Succeeded: true,
                Error: null,
                ArgumentsJson: "{\"speech\":\"Watch the stove!\",\"thought_tag\":\"STRESSED\",\"thought_desc\":\"Smoke everywhere\"}"));

            Assert.True(summarizer.TrySummarizeResponse(entryWithTool, out var toolSummary));
            Assert.NotNull(toolSummary);
            Assert.Equal("\"Watch the stove!\" (Smoke everywhere)", toolSummary!.ContentSummary);
            Assert.Contains("express_dialogue", toolSummary.SuppressToolNames!);

            var entryFromResponse = new AIRequestTraceEntry
            {
                ModId = "RimMind.Dialogue",
                Response = "{\"speech\":\"Nice work today.\",\"thought_tag\":\"ENCOURAGED\"}"
            };
            Assert.True(summarizer.TrySummarizeResponse(entryFromResponse, out var responseSummary));
            Assert.NotNull(responseSummary);
            Assert.StartsWith("\"Nice work today.\"", responseSummary!.ContentSummary, StringComparison.Ordinal);

            var unrelatedEntry = new AIRequestTraceEntry
            {
                ModId = "RimMind.Advisor",
                Response = "{\"speech\":\"Ignored because not dialogue\"}"
            };
            Assert.False(summarizer.TrySummarizeResponse(unrelatedEntry, out _));

            Assert.True(summarizer.TrySummarizePrompt(
                "[L3] <dialogue_trigger>[Chitchat: sharing a meal]\nBased on your current state and surroundings, respond naturally.</dialogue_trigger>",
                out var promptSummary));
            Assert.Equal("[Chitchat: sharing a meal]", promptSummary);

            Extensions.UntranslatableKeys.Add("RimMind.UI.AgentsPage.Trace.DailyReaction");
            try
            {
                Assert.True(summarizer.TrySummarizePrompt(
                    "<dialogue_trigger>Based on your current state and surroundings, respond naturally.</dialogue_trigger>",
                    out var fallbackSummary));
                Assert.Equal("Daily reaction", fallbackSummary);
            }
            finally
            {
                Extensions.UntranslatableKeys.Remove("RimMind.UI.AgentsPage.Trace.DailyReaction");
            }

            Assert.False(summarizer.TrySummarizePrompt("Unrelated advisor decision prompt", out _));
        }

        [Fact]
        public void DialogueTextSanitizer_ClassifyThoughtTag_OnlyProducesRegisteredThoughtTags()
        {
            string[] samples =
            {
                "Feeling terribly hurt and grieving today",
                "Overwhelmed by stress and exhaustion",
                "Furious and irritated at that insult",
                "Grateful and happily connected with everyone",
                "Just an ordinary neutral observation"
            };

            foreach (string sample in samples)
            {
                string tag = DialogueTextSanitizer.ClassifyThoughtTag(sample);
                Assert.True(
                    tag == "NONE" || ThoughtInjector.MapTagToMoodOffset(tag) != 0,
                    $"ClassifyThoughtTag returned unregistered tag '{tag}' for '{sample}'.");
            }
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

        [Fact]
        public void DialogueOverlayLayoutClampsRightMargin()
        {
            // When overlay X + width exceeds screenWidth - rightMargin, X is clamped
            OverlayBounds clamped = DialogueOverlayLayout.Normalize(
                new OverlayBounds(1800f, 200f, 300f, 100f),
                1920f,
                1080f,
                300f,
                100f,
                170f);
            // safeWidth = 1920, rightMargin = 170, maxX = 1920 - 300 - 170 = 1450
            Assert.Equal(new OverlayBounds(1450f, 200f, 300f, 100f), clamped);

            // When overlay width itself exceeds safeWidth - rightMargin, width is clamped
            OverlayBounds clampedWidth = DialogueOverlayLayout.Normalize(
                new OverlayBounds(0f, 0f, 1900f, 200f),
                1920f,
                1080f,
                300f,
                100f,
                170f);
            Assert.Equal(new OverlayBounds(0f, 0f, 1750f, 200f), clampedWidth);
        }

        [Fact]
        public void DialogueOverlayLayoutShouldRenderCorrectness()
        {
            // When empty and not interacted with: keep screen completely clean (prevent ghost box leak)
            Assert.False(DialogueOverlayLayout.ShouldRender(0, isMouseOver: false, isDragging: false, isResizing: false));

            // When there are dialogue messages: always render
            Assert.True(DialogueOverlayLayout.ShouldRender(1, isMouseOver: false, isDragging: false, isResizing: false));
            Assert.True(DialogueOverlayLayout.ShouldRender(5, isMouseOver: true, isDragging: false, isResizing: false));

            // When empty but hovered: render box & controls
            Assert.True(DialogueOverlayLayout.ShouldRender(0, isMouseOver: true, isDragging: false, isResizing: false));

            // When empty but dragging: render box & controls
            Assert.True(DialogueOverlayLayout.ShouldRender(0, isMouseOver: false, isDragging: true, isResizing: false));

            // When empty but resizing: render box & controls
            Assert.True(DialogueOverlayLayout.ShouldRender(0, isMouseOver: false, isDragging: false, isResizing: true));
        }

        [Fact]
        public void NpcResponseHandler_ReturnsNull_WhenPawnOrRecipientNotActiveSpawnedColonist()
        {
            var map = new Verse.Map();
            var pawn = new Verse.Pawn { thingIDNumber = 1, IsColonist = true, Faction = RimWorld.Faction.OfPlayer, Map = map, Spawned = true };
            var recipient = new Verse.Pawn { thingIDNumber = 2, IsColonist = true, Faction = RimWorld.Faction.OfPlayer, Map = map, Spawned = true };
            var response = new RimMind.Domain.Llm.LlmResponse { Content = "Hello there" };

            // When both are active spawned colonists, dialogue succeeds
            var success = NpcResponseHandler.Handle(response, "NPC-1", pawn, recipient, "test", DialogueTriggerType.Chitchat);
            Assert.Equal("Hello there", success);

            // Initiator dead -> null
            pawn.Dead = true;
            Assert.Null(NpcResponseHandler.Handle(response, "NPC-1", pawn, recipient, "test", DialogueTriggerType.Chitchat));
            pawn.Dead = false;

            // Initiator destroyed -> null
            pawn.Destroyed = true;
            Assert.Null(NpcResponseHandler.Handle(response, "NPC-1", pawn, recipient, "test", DialogueTriggerType.Chitchat));
            pawn.Destroyed = false;

            // Initiator off-map (not spawned) -> null
            pawn.Spawned = false;
            Assert.Null(NpcResponseHandler.Handle(response, "NPC-1", pawn, recipient, "test", DialogueTriggerType.Chitchat));
            pawn.Spawned = true;

            // Initiator Map null -> null
            pawn.Map = null;
            Assert.Null(NpcResponseHandler.Handle(response, "NPC-1", pawn, recipient, "test", DialogueTriggerType.Chitchat));
            pawn.Map = map;

            // Recipient dead -> null
            recipient.Dead = true;
            Assert.Null(NpcResponseHandler.Handle(response, "NPC-1", pawn, recipient, "test", DialogueTriggerType.Chitchat));
            recipient.Dead = false;

            // Recipient off-map -> null
            recipient.Spawned = false;
            Assert.Null(NpcResponseHandler.Handle(response, "NPC-1", pawn, recipient, "test", DialogueTriggerType.Chitchat));
            recipient.Spawned = true;

            // Recipient Map null -> null
            recipient.Map = null;
            Assert.Null(NpcResponseHandler.Handle(response, "NPC-1", pawn, recipient, "test", DialogueTriggerType.Chitchat));
        }

        [Fact]
        public void DialogueActivityState_GetActiveRecipient_FiltersDeadOrOffMapRecipients()
        {
            var state = new DialogueActivityState();
            var map = new Verse.Map();
            var pawn = new Verse.Pawn { thingIDNumber = 10, IsColonist = true, Faction = RimWorld.Faction.OfPlayer, Map = map, Spawned = true };
            var recipient = new Verse.Pawn { thingIDNumber = 20, IsColonist = true, Faction = RimWorld.Faction.OfPlayer, Map = map, Spawned = true };

            state.SetRequestRecipient(10, 20, 12345, recipient);
            Assert.Same(recipient, state.GetActiveRecipient(pawn, 100));

            // Mark recipient dead -> GetActiveRecipient must return null and prune cache
            recipient.Dead = true;
            Assert.Null(state.GetActiveRecipient(pawn, 100));

            // Mark recipient unspawned (off-map) -> GetActiveRecipient must return null
            recipient.Dead = false;
            recipient.Spawned = false;
            state.SetRequestRecipient(10, 20, 12346, recipient);
            Assert.Null(state.GetActiveRecipient(pawn, 100));
        }
    }
}
