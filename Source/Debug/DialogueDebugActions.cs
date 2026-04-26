using System.Reflection;
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

        [DebugAction("RimMind-Dialogue", "Force LevelUp Trigger (selected)", actionType = DebugActionType.Action)]
        private static void ForceLevelUpTrigger()
        {
            var pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) { Log.Message("[RimMind-Dialogue] No pawn selected."); return; }

            RimMindDialogueService.HandleTrigger(pawn, "[Debug] Skill level up", DialogueTriggerType.LevelUp, null);
        }

        [DebugAction("RimMind-Dialogue", "Force Thought Trigger (selected)", actionType = DebugActionType.Action)]
        private static void ForceThoughtTrigger()
        {
            var pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) { Log.Message("[RimMind-Dialogue] No pawn selected."); return; }

            RimMindDialogueService.HandleTrigger(pawn, "[Debug] Mood thought change", DialogueTriggerType.Thought, null);
        }

        [DebugAction("RimMind-Dialogue", "Show Dialogue Service State", actionType = DebugActionType.Action)]
        private static void ShowDialogueServiceState()
        {
            var sb = new StringBuilder("[RimMind-Dialogue] Service State:\n");
            sb.AppendLine($"  IsReady: {RimMindDialogueService.IsReady}");
            sb.AppendLine($"  LogEntries.Count: {RimMindDialogueService.LogEntries.Count}");

            var flags = BindingFlags.NonPublic | BindingFlags.Static;

            var recentField = typeof(RimMindDialogueService).GetField("_recentTriggers", flags);
            if (recentField != null)
            {
                var recent = recentField.GetValue(null) as System.Collections.IList;
                sb.AppendLine($"  _recentTriggers.Count: {recent?.Count.ToString() ?? "N/A"}");
            }

            var pendingField = typeof(RimMindDialogueService).GetField("_pendingPawns", flags);
            if (pendingField != null)
            {
                var pending = pendingField.GetValue(null);
                var countProp = pending?.GetType().GetProperty("Count");
                sb.AppendLine($"  _pendingPawns.Count: {countProp?.GetValue(pending)?.ToString() ?? "N/A"}");
            }

            var pairsField = typeof(RimMindDialogueService).GetField("_pendingDialoguePairs", flags);
            if (pairsField != null)
            {
                var pairs = pairsField.GetValue(null);
                var countProp = pairs?.GetType().GetProperty("Count");
                sb.AppendLine($"  _pendingDialoguePairs.Count: {countProp?.GetValue(pairs)?.ToString() ?? "N/A"}");
            }

            var dailyField = typeof(RimMindDialogueService).GetField("_dailyDialogueCounts", flags);
            if (dailyField != null)
            {
                var daily = dailyField.GetValue(null) as System.Collections.IDictionary;
                sb.AppendLine($"  _dailyDialogueCounts.Count: {daily?.Count.ToString() ?? "N/A"}");
            }

            sb.AppendLine("[RimMind-Dialogue] Settings:");
            var s = RimMindDialogueSettings.Get();
            sb.AppendLine($"  enabled: {s.enabled}");
            sb.AppendLine($"  monologueCooldownTicks: {s.monologueCooldownTicks}");
            sb.AppendLine($"  maxDailyDialogueRounds: {s.maxDailyDialogueRounds}");
            sb.AppendLine($"  autoDialogueCooldownHours: {s.autoDialogueCooldownHours}");
            sb.AppendLine($"  moodChangeThreshold: {s.moodChangeThreshold}");
            sb.AppendLine($"  startDelayEnabled: {s.startDelayEnabled}");
            sb.AppendLine($"  startDelaySeconds: {s.startDelaySeconds}");
            sb.AppendLine($"  enableDialogueReply: {s.enableDialogueReply}");
            sb.AppendLine($"  overlayEnabled: {s.overlayEnabled}");

            Log.Message(sb.ToString());
        }

        [DebugAction("RimMind-Dialogue", "Clear All Dialogue Cooldowns", actionType = DebugActionType.Action)]
        private static void ClearAllDialogueCooldowns()
        {
            int clearedRecent = 0;
            int clearedDaily = 0;

            var flags = BindingFlags.NonPublic | BindingFlags.Static;

            var recentField = typeof(RimMindDialogueService).GetField("_recentTriggers", flags);
            if (recentField != null)
            {
                var recent = recentField.GetValue(null) as System.Collections.IList;
                if (recent != null)
                {
                    clearedRecent = recent.Count;
                    recent.Clear();
                }
            }

            var dailyField = typeof(RimMindDialogueService).GetField("_dailyDialogueCounts", flags);
            if (dailyField != null)
            {
                var daily = dailyField.GetValue(null) as System.Collections.IDictionary;
                if (daily != null)
                {
                    clearedDaily = daily.Count;
                    daily.Clear();
                }
            }

            if (clearedRecent > 0 || clearedDaily > 0)
                Log.Message($"[RimMind-Dialogue] Cleared cooldowns: _recentTriggers={clearedRecent}, _dailyDialogueCounts={clearedDaily}");
            else
                Log.Message("[RimMind-Dialogue] No cooldowns to clear (or fields inaccessible). Adjust monologueCooldownTicks in settings if needed.");
        }
    }
}
