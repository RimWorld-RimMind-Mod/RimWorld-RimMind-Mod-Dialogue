using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    public class DialogueLifecycleSourceTests
    {
        [Fact]
        public void HandleTrigger_Callback_Always_Clears_ActiveRecipient()
        {
            var repoRoot = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            var sourcePath = Path.Combine(
                repoRoot,
                "RimMind-Dialogue",
                "Source",
                "Core",
                "RimMindDialogueService.cs");
            var source = File.ReadAllText(sourcePath);

            Assert.Matches(
                new Regex(
                    @"LongEventHandler\.ExecuteWhenFinished\(\(\)\s*=>\s*\{.*?try\s*\{.*?NpcResponseHandler\.Handle\(.*?\}\s*finally\s*\{.*?_activeRecipients\.TryRemove\(pawn\.thingIDNumber",
                    RegexOptions.Singleline),
                source);
        }
    }
}
