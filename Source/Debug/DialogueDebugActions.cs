using System.Text;
using LudeonTK;
using RimMind.Dialogue;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Dialogue.UI;
using RimWorld;
using Verse;

namespace RimMind.Dialogue.Debug
{
    internal static class DialogueDebugActions
    {
        [DebugAction("RimMind-Dialogue", "Force Chitchat (selected + target)", actionType = DebugActionType.Action)]
        private static void ForceChitchat()
        {
            var pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) { Log.Message("[RimMind-Dialogue] No pawn selected."); return; }

            var target = FindAnotherColonist(pawn);
            if (target == null) { Log.Message("[RimMind-Dialogue] No other colonist found for target."); return; }

            string context = "RimMind.Dialogue.Debug.ChitchatContext".Translate(target.LabelShort);
            RimMindDialogueService.HandleTrigger(pawn, context, DialogueTriggerType.Chitchat, target);
        }

        [DebugAction("RimMind-Dialogue", "Force Hediff Trigger (selected)", actionType = DebugActionType.Action)]
        private static void ForceHediffTrigger()
        {
            var pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) { Log.Message("[RimMind-Dialogue] No pawn selected."); return; }

            string context = "RimMind.Dialogue.Debug.HediffContext".Translate();
            RimMindDialogueService.HandleTrigger(pawn, context, DialogueTriggerType.Hediff, null);
        }

        [DebugAction("RimMind-Dialogue", "Show Active Thoughts (selected)", actionType = DebugActionType.Action)]
        private static void ShowActiveThoughts()
        {
            var pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) { Log.Message("[RimMind-Dialogue] No pawn selected."); return; }

            var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null) { Log.Message("[RimMind-Dialogue] No memories found."); return; }

            var sb = new StringBuilder($"[RimMind-Dialogue] Active RimMindDialogue thoughts for {pawn.Name.ToStringShort}:\n");
            bool any = false;
            foreach (var t in memories)
            {
                if (!t.def.defName.StartsWith("RimMindDialogue_")) continue;
                var dialogue = t as Thought_RimMindDialogue;
                sb.AppendLine($"- {t.def.defName}: label={dialogue?.aiLabel ?? t.def.label}, desc={dialogue?.aiDescription ?? "N/A"}, mood={dialogue?.aiMoodOffset ?? 0}");
                any = true;
            }
            if (!any) sb.AppendLine("  (none)");
            Log.Message(sb.ToString());
        }

        [DebugAction("RimMind-Dialogue", "Open Dialogue Window (selected)", actionType = DebugActionType.Action)]
        private static void OpenDialogueWindow()
        {
            var pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) { Log.Message("[RimMind-Dialogue] No pawn selected."); return; }

            Find.WindowStack.Add(new Window_Dialogue(pawn));
        }

        [DebugAction("RimMind-Dialogue", "Open Dialogue Log", actionType = DebugActionType.Action)]
        private static void OpenDialogueLog()
        {
            Find.WindowStack.Add(new Window_DialogueLog());
        }

        [DebugAction("RimMind-Dialogue", "Toggle Overlay", actionType = DebugActionType.Action)]
        private static void ToggleOverlay()
        {
            var settings = RimMindDialogueSettings.Get();
            settings.overlayEnabled = !settings.overlayEnabled;
            Log.Message($"[RimMind-Dialogue] Overlay {(settings.overlayEnabled ? "enabled" : "disabled")}");
        }

        private static Pawn? FindAnotherColonist(Pawn exclude)
        {
            var map = exclude.Map;
            if (map == null) return null;

            foreach (var p in map.mapPawns.FreeColonistsSpawned)
            {
                if (p != exclude) return p;
            }
            return null;
        }
    }
}
