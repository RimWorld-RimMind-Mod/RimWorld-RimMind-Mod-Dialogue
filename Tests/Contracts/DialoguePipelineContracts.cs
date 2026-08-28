using System;
using System.IO;
using RimMind.Dialogue.Core;
using RimMind.Testing;
using Xunit;

namespace RimMind.Dialogue.Tests.Contracts
{
    public sealed class DialoguePipelineContracts
    {
        [Fact]
        public void Stable_pipeline_boundaries()
        {
            ContractCaseRunner.Run(
                ("classification covers every public category", ClassificationCoversEveryCategory),
                ("pair keys are symmetric and monologues remain unpaired", PairKeysAreStable),
                ("structured replies preserve thought and relation semantics", StructuredRepliesPreserveSemantics),
                ("monologues ignore relation changes", MonologuesIgnoreRelationChanges),
                ("plain malformed and partial replies preserve prior values", InvalidRepliesPreservePriorValues),
                ("bounded log storage is isolated from the public facade", BoundedLogStorageIsIsolated),
                ("dialogue overlay bounds and newest-message selection remain visible", DialogueOverlayLayoutRemainsVisible),
                ("dialogue overlay renders on every GUI pass", DialogueOverlayRendersEveryGuiPass));
        }

        private static void ClassificationCoversEveryCategory()
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

        private static void PairKeysAreStable()
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

        private static void StructuredRepliesPreserveSemantics()
        {
            string reply = "raw";
            string? tag = "NONE";
            string? description = null;
            int relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(
                "{\"reply\":\"Hello\",\"thought\":{\"tag\":\"VALUED\",\"description\":\"Seen\"},\"relation_delta\":2}",
                false,
                ref reply,
                ref tag,
                ref description,
                ref relationDelta);

            Assert.Equal("Hello", reply);
            Assert.Equal("VALUED", tag);
            Assert.Equal("Seen", description);
            Assert.Equal(2, relationDelta);
        }

        private static void MonologuesIgnoreRelationChanges()
        {
            string reply = "raw";
            string? tag = null;
            string? description = null;
            int relationDelta = 9;

            ResponseJsonParser.TryParseResponseJson(
                "{\"reply\":\"Thinking\",\"relation_delta\":-5}",
                true,
                ref reply,
                ref tag,
                ref description,
                ref relationDelta);

            Assert.Equal("Thinking", reply);
            Assert.Equal(9, relationDelta);
        }

        private static void InvalidRepliesPreservePriorValues()
        {
            AssertPreserved("plain text");
            AssertPreserved("{invalid");
            AssertPreserved(string.Empty);
            AssertPreserved(null);

            string reply = "before";
            string? tag = "OLD";
            string? description = "old description";
            int relationDelta = 4;
            ResponseJsonParser.TryParseResponseJson(
                "{\"thought\":{\"tag\":\"CONNECTED\"}}",
                false,
                ref reply,
                ref tag,
                ref description,
                ref relationDelta);

            Assert.Equal("before", reply);
            Assert.Equal("CONNECTED", tag);
            Assert.Null(description);
            Assert.Equal(4, relationDelta);
        }

        private static void BoundedLogStorageIsIsolated()
        {
            string store = ReadDialogueSource("Core/DialogueLogStore.cs");
            string facade = ReadDialogueSource("Core/RimMindDialogueService.cs");

            Assert.Contains("internal sealed class DialogueLogStore", store, StringComparison.Ordinal);
            Assert.Contains("ConcurrentBag<DialogueLogEntry>", store, StringComparison.Ordinal);
            Assert.Contains("IReadOnlyList<DialogueLogEntry> Entries", store, StringComparison.Ordinal);
            Assert.DoesNotContain("ConcurrentBag<DialogueLogEntry>", facade, StringComparison.Ordinal);
            Assert.DoesNotContain("MaxLogEntries", facade, StringComparison.Ordinal);
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

        private static void DialogueOverlayRendersEveryGuiPass()
        {
            string source = ReadDialogueSource("UI/DialogueOverlay.cs");

            Assert.DoesNotContain("Time.frameCount", source, StringComparison.Ordinal);
            Assert.Contains("DialogueOverlayLayout.Normalize", source, StringComparison.Ordinal);
            Assert.Contains("FindFirstVisibleIndex", source, StringComparison.Ordinal);
            Assert.Contains("_cachedMaxMessages", source, StringComparison.Ordinal);
            Assert.Contains("finally", source, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "if (availableWidth < 50f) availableWidth = 50f",
                source,
                StringComparison.Ordinal);
        }

        private static void DialogueOverlayLayoutRemainsVisible()
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

        private static string ReadDialogueSource(string relativePath)
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "RimMind-Dialogue")))
                directory = directory.Parent;

            Assert.NotNull(directory);
            return File.ReadAllText(Path.Combine(
                directory!.FullName,
                "RimMind-Dialogue",
                "Source",
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
