using System;
using RimMind.Dialogue.Core;
using Xunit;

namespace RimMind.Dialogue.Tests.Contracts
{
    public sealed class DialogueToolParserContracts
    {
        [Fact]
        public void StructuredToolCallPreservesDialogueSemantics()
        {
            string toolCallsJson = "[{\"name\":\"express_dialogue\",\"arguments\":\"{\\\"speech\\\":\\\"Hello\\\",\\\"thought_tag\\\":\\\"VALUED\\\",\\\"thought_desc\\\":\\\"Seen\\\",\\\"relation_delta\\\":2}\"}]";

            bool success = DialogueToolParser.TryParse(toolCallsJson, false, out var output);

            Assert.True(success);
            Assert.Equal("Hello", output.Speech);
            Assert.Equal("VALUED", output.ThoughtTag);
            Assert.Equal("Seen", output.ThoughtDesc);
            Assert.Equal(2, output.RelationDelta);
        }

        [Fact]
        public void MonologuesIgnoreRelationChangesAndClamps()
        {
            // Monologue: relation_delta forced to 0
            string monoJson = "[{\"name\":\"express_dialogue\",\"arguments\":\"{\\\"speech\\\":\\\"Thinking\\\",\\\"relation_delta\\\":-5}\"}]";
            bool monoSuccess = DialogueToolParser.TryParse(monoJson, true, out var monoOutput);

            Assert.True(monoSuccess);
            Assert.Equal("Thinking", monoOutput.Speech);
            Assert.Equal(0, monoOutput.RelationDelta);

            // Dialogue: relation_delta clamped to [-5, 5]
            string overMaxJson = "[{\"name\":\"express_dialogue\",\"arguments\":\"{\\\"speech\\\":\\\"Love you\\\",\\\"relation_delta\\\":15}\"}]";
            Assert.True(DialogueToolParser.TryParse(overMaxJson, false, out var overMaxOutput));
            Assert.Equal(5, overMaxOutput.RelationDelta);

            string underMinJson = "[{\"name\":\"express_dialogue\",\"arguments\":\"{\\\"speech\\\":\\\"Hate you\\\",\\\"relation_delta\\\":-20}\"}]";
            Assert.True(DialogueToolParser.TryParse(underMinJson, false, out var underMinOutput));
            Assert.Equal(-5, underMinOutput.RelationDelta);
        }

        [Fact]
        public void InvalidToolCallsFailGracefully()
        {
            Assert.False(DialogueToolParser.TryParse(null, false, out _));
            Assert.False(DialogueToolParser.TryParse(string.Empty, false, out _));
            Assert.False(DialogueToolParser.TryParse("plain text", false, out _));
            Assert.False(DialogueToolParser.TryParse("{invalid json", false, out _));
            Assert.False(DialogueToolParser.TryParse("[]", false, out _));
            Assert.False(DialogueToolParser.TryParse("[{\"name\":\"express_dialogue\",\"arguments\":\"{}\"}]", false, out _));
        }

        [Fact]
        public void OpenAiNestedFunctionAndMarkdownCodeBlockRepliesAreParsed()
        {
            // Case 1: OpenAI nested function format with markdown code block
            string nestedJson = "[{\"id\":\"call_123\",\"type\":\"function\",\"function\":{\"name\":\"express_dialogue\",\"arguments\":\"```json\\n{\\\"speech\\\":\\\"你感到全身旧伤\\\",\\\"thought_tag\\\":\\\"STRESSED\\\",\\\"thought_desc\\\":\\\"疲惫\\\"}\\n```\"}}]";

            bool success1 = DialogueToolParser.TryParse(nestedJson, true, out var output1);

            Assert.True(success1);
            Assert.Equal("你感到全身旧伤", output1.Speech);
            Assert.Equal("STRESSED", output1.ThoughtTag);
            Assert.Equal("疲惫", output1.ThoughtDesc);
        }

        [Fact]
        public void DirectObjectArgumentsAreParsed()
        {
            // Case 2: Object arguments directly (without double-escaped string)
            string objectArgsJson = "[{\"name\":\"express_dialogue\",\"arguments\":{\"speech\":\"Hello friend!\",\"thought_tag\":\"CONNECTED\",\"thought_desc\":\"Joy\",\"relation_delta\":3}}]";

            bool success2 = DialogueToolParser.TryParse(objectArgsJson, false, out var output2);

            Assert.True(success2);
            Assert.Equal("Hello friend!", output2.Speech);
            Assert.Equal("CONNECTED", output2.ThoughtTag);
            Assert.Equal("Joy", output2.ThoughtDesc);
            Assert.Equal(3, output2.RelationDelta);
        }

        [Fact]
        public void InnerJsonInSpeechArgumentIsUnwrapped()
        {
            // Case 3: Model accidentally outputs JSON string inside speech argument
            string wrappedJson = "[{\"name\":\"express_dialogue\",\"arguments\":{\"speech\":\"{\\\"reply\\\":\\\"Unwrapped speech\\\",\\\"thought\\\":{\\\"tag\\\":\\\"INSPIRED\\\",\\\"description\\\":\\\"Spark\\\"}}\"}}]";

            bool success = DialogueToolParser.TryParse(wrappedJson, false, out var output);

            Assert.True(success);
            Assert.Equal("Unwrapped speech", output.Speech);
            Assert.Equal("INSPIRED", output.ThoughtTag);
            Assert.Equal("Spark", output.ThoughtDesc);
        }
    }
}
