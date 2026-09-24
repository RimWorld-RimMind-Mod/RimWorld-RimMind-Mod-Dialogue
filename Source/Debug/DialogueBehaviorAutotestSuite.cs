using System;
using System.Linq;
using LudeonTK;
using RimMind.Dialogue.Comps;
using RimMind.Dialogue.Core;
using RimMind.Presentation.Api;
using RimWorld;
using Verse;

namespace RimMind.Dialogue.Debug
{
    /// <summary>
    /// In-game behavioral autotest suite for RimMind-Dialogue.
    /// Discovered automatically by Core's BehaviorAutotestRunner and exposed to Dev menu.
    /// </summary>
    public sealed class DialogueBehaviorAutotestSuite : BehaviorAutotestSuiteBase
    {
        public override string ModId => "Dialogue";
        public override string SuiteId => "Behavior.DialogueCore";

        [DebugAction("Autotests", "Run Dialogue In-Game Behavior Test", actionType = DebugActionType.Action)]
        public static void RunFromDevMenu() => RunSuiteFromDevMenu<DialogueBehaviorAutotestSuite>();

        public override void RunSuite(IInGameBehaviorSuiteContext context)
        {
            Pawn? pawn = context.ActiveColonist;

            // 1. CompRimMindDialogue Attachment
            if (context.CurrentMap?.mapPawns?.FreeColonists != null && context.CurrentMap.mapPawns.FreeColonists.Count > 0)
            {
                bool anyAttached = context.CurrentMap.mapPawns.FreeColonists.Any(p => p.GetComp<CompRimMindDialogue>() != null);
                context.Assert(anyAttached, "Free colonists on map have CompRimMindDialogue attached");
            }
            else if (pawn != null)
            {
                context.Assert(pawn.GetComp<CompRimMindDialogue>() != null, "Active colonist has CompRimMindDialogue attached");
            }

            // 2. ThoughtInjector Tag Mapping
            int encouragedOffset = ThoughtInjector.MapTagToMoodOffset("ENCOURAGED");
            int stressedOffset = ThoughtInjector.MapTagToMoodOffset("STRESSED");
            context.Assert(encouragedOffset == 1, $"ThoughtInjector ENCOURAGED maps to +1 mood offset (got: {encouragedOffset})");
            context.Assert(stressedOffset == -2, $"ThoughtInjector STRESSED maps to -2 mood offset (got: {stressedOffset})");

            // 3. Live Thought Injection & Cleanup
            if (pawn != null && pawn.needs?.mood?.thoughts?.memories != null)
            {
                var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail("RimMindDialogue_Thought");
                if (thoughtDef != null)
                {
                    int memBefore = pawn.needs.mood.thoughts.memories.Memories.Count;
                    ThoughtInjector.Inject(pawn, null, "ENCOURAGED", "Autotest test description");

                    var memAfter = pawn.needs.mood.thoughts.memories.Memories;
                    bool injected = memAfter.Any(t => t.def == thoughtDef);
                    context.Assert(injected, "ThoughtInjector successfully gained RimMindDialogue_Thought memory on colonist");

                    // Zero-pollution cleanup: remove test memory
                    pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(thoughtDef);
                    bool cleaned = !pawn.needs.mood.thoughts.memories.Memories.Any(t => t.def == thoughtDef);
                    context.Assert(cleaned, "Test dialogue thought memory was cleaned up cleanly");
                }
                else
                {
                    context.Warn("RimMindDialogue_Thought ThoughtDef silent fail (XML not loaded in test session?)");
                }
            }

            // 4. Rate Limiting and Activity State Cooldowns
            try
            {
                var cleared = RimMindDialogueService.ClearAllCooldowns();
                context.Assert(RimMindDialogueService.RecentTriggerCount == 0, "RecentTriggerCount is 0 after clearing dialogue cooldowns");
            }
            catch (Exception ex)
            {
                context.Assert(false, $"ClearAllCooldowns threw exception: {ex.Message}");
            }

            // 5. Cadence Evaluator Soft Pacing
            var evaluator = new DialogueCadenceEvaluator();
            var pair = (101, 102);
            evaluator.RecordDialogue(pair, 1000);
            float immediateProb = evaluator.CalculateProbability(pair, 1100, 3.0f, 0f);
            context.Assert(immediateProb == 0f, $"CadenceEvaluator immediate probability within grace period is 0 (got: {immediateProb})");

            float fullProb = evaluator.CalculateProbability(pair, 1000 + 180000, 3.0f, 0f);
            context.Assert(fullProb >= 1.0f, $"CadenceEvaluator probability after full target cadence is 1.0 (got: {fullProb})");

            // 6. Dialogue Storage Persistence & Roundtrip
            var testEntry = new DialogueLogEntry
            {
                tick = 12345,
                initiatorName = "Alice",
                initiatorId = 101,
                initiatorIsColonist = true,
                recipientName = "Bob",
                recipientId = 102,
                recipientIsColonist = true,
                category = DialogueCategory.ColonistDialogue,
                trigger = "Chitchat",
                context = "Talking in dining room",
                reply = "Nice weather today.",
                thoughtTag = "ENCOURAGED",
                thoughtDesc = "Enjoyed conversation"
            };

            var originalEntries = RimMindDialogueService.ExportLogEntries(500);
            RimMindDialogueService.ImportLogEntries(new[] { testEntry }, 100);
            var exported = RimMindDialogueService.ExportLogEntries(10);
            bool entryImported = exported.Any(e => e.tick == 12345 && e.initiatorName == "Alice" && e.reply == "Nice weather today.");
            context.Assert(entryImported, "DialogueLogStore successfully imports and exports structured dialogue entry");

            // Restore original entries
            RimMindDialogueService.ImportLogEntries(originalEntries, 500);
        }
    }
}
