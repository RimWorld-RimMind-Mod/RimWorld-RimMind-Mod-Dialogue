using System;
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
                ("plain malformed and partial replies preserve prior values", InvalidRepliesPreservePriorValues));
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
    }
}
