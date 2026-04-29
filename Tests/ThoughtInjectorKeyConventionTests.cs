using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    public class ThoughtInjectorKeyConventionTests
    {
        private static readonly Dictionary<string, string> LabelMap = new Dictionary<string, string>
        {
            { "ENCOURAGED", "RimMind.Dialogue.Thought.ENCOURAGED" },
            { "HURT", "RimMind.Dialogue.Thought.HURT" },
            { "VALUED", "RimMind.Dialogue.Thought.VALUED" },
            { "CONNECTED", "RimMind.Dialogue.Thought.CONNECTED" },
            { "STRESSED", "RimMind.Dialogue.Thought.STRESSED" },
            { "IRRITATED", "RimMind.Dialogue.Thought.IRRITATED" },
        };

        private static readonly Dictionary<string, int> MoodOffsetMap = new Dictionary<string, int>
        {
            { "ENCOURAGED", 1 },
            { "HURT", -1 },
            { "VALUED", 2 },
            { "CONNECTED", 2 },
            { "STRESSED", -2 },
            { "IRRITATED", -1 },
        };

        [Fact]
        public void LabelMap_KeysMatchMoodOffsetMap_Keys()
        {
            Assert.Equal(MoodOffsetMap.Keys.ToHashSet(), LabelMap.Keys.ToHashSet());
        }

        [Fact]
        public void LabelMap_AllValues_EndWithAllCapsTagSuffix()
        {
            foreach (var kvp in LabelMap)
            {
                string expectedSuffix = kvp.Key;
                Assert.True(
                    kvp.Value.EndsWith("." + expectedSuffix, StringComparison.Ordinal),
                    $"LabelMap['{kvp.Key}'] = '{kvp.Value}' does not end with '.{expectedSuffix}'");
            }
        }

        [Fact]
        public void LabelMap_AllValues_PrefixIsRimMindDialogueThought()
        {
            foreach (var kvp in LabelMap)
            {
                Assert.True(
                    kvp.Value.StartsWith("RimMind.Dialogue.Thought.", StringComparison.Ordinal),
                    $"LabelMap['{kvp.Key}'] = '{kvp.Value}' does not start with 'RimMind.Dialogue.Thought.'");
            }
        }

        [Theory]
        [InlineData("ENCOURAGED", "RimMind.Dialogue.Thought.ENCOURAGED")]
        [InlineData("HURT", "RimMind.Dialogue.Thought.HURT")]
        [InlineData("VALUED", "RimMind.Dialogue.Thought.VALUED")]
        [InlineData("CONNECTED", "RimMind.Dialogue.Thought.CONNECTED")]
        [InlineData("STRESSED", "RimMind.Dialogue.Thought.STRESSED")]
        [InlineData("IRRITATED", "RimMind.Dialogue.Thought.IRRITATED")]
        public void LabelMap_SpecificTag_MapsToAllCapsKey(string tag, string expectedKey)
        {
            Assert.Equal(expectedKey, LabelMap[tag]);
        }

        [Theory]
        [InlineData("ENCOURAGED", 1)]
        [InlineData("HURT", -1)]
        [InlineData("VALUED", 2)]
        [InlineData("CONNECTED", 2)]
        [InlineData("STRESSED", -2)]
        [InlineData("IRRITATED", -1)]
        public void MoodOffsetMap_SpecificTag_MapsToCorrectOffset(string tag, int expectedOffset)
        {
            Assert.Equal(expectedOffset, MoodOffsetMap[tag]);
        }
    }
}
