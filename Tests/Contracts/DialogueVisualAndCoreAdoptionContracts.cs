using System;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Presentation.Api;
using RimWorld;
using UnityEngine;
using Verse;
using Xunit;

namespace RimMind.Dialogue.Tests.Contracts
{
    public sealed class DialogueVisualAndCoreAdoptionContracts
    {
        [Fact]
        public void DialogueOverlayLayout_ComputeRenderHeight_ShrinkWrapsToVisibleMessages()
        {
            // 3 short lines (~22px each) with maxHeight=220 must shrink-wrap tightly instead of leaving 140px void
            float idleThreeLines = DialogueOverlayLayout.ComputeRenderHeight(
                new[] { 22f, 22f, 22f },
                maxHeight: 220f,
                minHeight: 34f,
                chromeHeight: 12f,
                forceExpandToMax: false);
            Assert.Equal(78f, idleThreeLines);

            // Hovering adds the options bar (24px) and never shrinks below idle height (zero hover flicker)
            float hoveredThreeLines = DialogueOverlayLayout.ComputeRenderHeight(
                new[] { 22f, 22f, 22f },
                maxHeight: 220f,
                minHeight: 76f,
                chromeHeight: 36f,
                forceExpandToMax: false);
            Assert.Equal(102f, hoveredThreeLines);
            Assert.True(hoveredThreeLines >= idleThreeLines);

            // Overflowing content clamps cleanly to user's max height cap
            float overflowing = DialogueOverlayLayout.ComputeRenderHeight(
                new[] { 30f, 30f, 30f, 30f, 30f, 30f, 30f, 30f },
                maxHeight: 220f,
                minHeight: 34f,
                chromeHeight: 12f,
                forceExpandToMax: false);
            Assert.Equal(220f, overflowing);

            // Empty list clamps to minHeight unless resizing forces expansion to maxHeight
            Assert.Equal(34f, DialogueOverlayLayout.ComputeRenderHeight(
                Array.Empty<float>(),
                maxHeight: 220f,
                minHeight: 34f,
                chromeHeight: 12f,
                forceExpandToMax: false));
            Assert.Equal(220f, DialogueOverlayLayout.ComputeRenderHeight(
                new[] { 22f },
                maxHeight: 220f,
                minHeight: 76f,
                chromeHeight: 36f,
                forceExpandToMax: true));
        }

        [Fact]
        public void OverheadSpeechMote_SpawnsAbovePawnHeadAndNameplate_AndScalesWithLineCount()
        {
            var basePos = new Vector3(10f, 0f, 20f);

            Vector3 singleLinePos = RimMindDialogueService.ComputeOverheadMotePosition(basePos, "Short line.");
            Assert.Equal(10f, singleLinePos.x);
            Assert.Equal(0f, singleLinePos.y);
            Assert.True(singleLinePos.z >= basePos.z + 1.35f);

            Vector3 twoLinePos = RimMindDialogueService.ComputeOverheadMotePosition(basePos, "First line\nSecond line");
            Assert.True(twoLinePos.z > singleLinePos.z);
            Assert.Equal(basePos.z + 1.60f, twoLinePos.z, 3);

            Vector3 manyLinesPos = RimMindDialogueService.ComputeOverheadMotePosition(basePos, "1\n2\n3\n4\n5\n6");
            Assert.Equal(
                basePos.z + RimMindDialogueService.OverheadMoteBaseZOffset + RimMindDialogueService.OverheadMoteMaxExtraZOffset,
                manyLinesPos.z,
                3);

            // Verify DisplayInteraction throws the mote at the elevated Z position
            MoteMaker.Thrown.Clear();
            var map = new Map();
            var pawn = new Pawn { thingIDNumber = 77, IsColonist = true, Faction = Faction.OfPlayer, Map = map, Spawned = true };
            RimMindDialogueService.DisplayInteraction(pawn, null, "Can you shut up? I'm losing my mind.");
            var thrown = Assert.Single(MoteMaker.Thrown);
            Assert.Same(map, thrown.Map);
            Assert.True(thrown.Position.z >= pawn.DrawPos.z + RimMindDialogueService.OverheadMoteBaseZOffset);
            Assert.Contains("\n", thrown.Text);
        }

        [Fact]
        public void FormatOverheadMoteText_DelegatesCjkAwareWrappingToCore()
        {
            // Wide CJK characters count as 2 display units in Core MoteTextFormatter, so 20 Chinese chars wrap cleanly
            string cjk = "今天外面冷得要命，我们得赶紧把木头搬进棚子里取暖。";
            string wrappedCjk = DialogueTextSanitizer.FormatOverheadMoteText(cjk, maxLineLength: 26, maxTotalLength: 52);
            Assert.Contains("\n", wrappedCjk);

            string en = "Can you shut up? I'm losing my mind right now.";
            string wrappedEn = DialogueTextSanitizer.FormatOverheadMoteText(en, maxLineLength: 26, maxTotalLength: 52);
            Assert.Contains("\n", wrappedEn);
        }

        [Fact]
        public void DialogueToolParser_HandlesThinkingBlocksAndTruncatedJsonViaCore()
        {
            string raw = "<thinking>Consider mood and frostbite.</thinking>\n```json\n{\"name\":\"express_dialogue\",\"arguments\":{\"speech\":\"Too cold out here.\",\"thought_tag\":\"STRESSED\"}}\n```";
            Assert.True(DialogueToolParser.TryParse(raw, isMonologue: true, out var output));
            Assert.Equal("Too cold out here.", output.Speech);
            Assert.Equal("STRESSED", output.ThoughtTag);
            Assert.Equal(0, output.RelationDelta);

            string truncated = "{\"speech\":\"Hang in there\",\"thought_tag\":\"ENCOURAGED\"";
            Assert.True(DialogueToolParser.TryParse(truncated, isMonologue: true, out var repaired));
            Assert.Equal("Hang in there", repaired.Speech);
            Assert.Equal("ENCOURAGED", repaired.ThoughtTag);
        }

        [Fact]
        public void RimMindDialogueSettings_DrawSettingsContent_OnlyWritesWhenDirty()
        {
            var settings = new RimMindDialogueSettings();
            settings.WriteCount = 0;

            GUI.changed = false;
            RimMindDialogueSettings.DrawSettingsContent(new Rect(0, 0, 600, 500));
            Assert.Equal(0, settings.WriteCount);

            // Simulate a control mutating GUI.changed during draw via onDirtySave contract
            var prev = GUI.changed;
            try
            {
                GUI.changed = false;
                RimMind.Presentation.UI.SettingsUIDrawer.DrawScrollableSettingsPage(
                    new Rect(0, 0, 600, 500),
                    ref scrollPos,
                    ref measuredH,
                    _ => { GUI.changed = true; },
                    onDirtySave: () => settings.Write());
                Assert.Equal(1, settings.WriteCount);
            }
            finally
            {
                GUI.changed = prev;
            }
        }

        private static Vector2 scrollPos;
        private static float measuredH = 500f;
    }
}
