using System;
using HarmonyLib;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Patches
{
    [HarmonyPatch(typeof(MemoryThoughtHandler), "TryGainMemory",
        new Type[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class ThoughtPatch
    {
        private static readonly System.Collections.Generic.Dictionary<(int PawnId, string ThoughtDefName), int> LastTriggeredTicks =
            new System.Collections.Generic.Dictionary<(int PawnId, string ThoughtDefName), int>();
        private const int SameThoughtCooldownTicks = 60000; // 1 in-game day

        [HarmonyPostfix]
        public static void Postfix(MemoryThoughtHandler __instance, Thought_Memory newThought)
        {
            if (!RimMindDialogueSettings.Get().thoughtEnabled) return;

            if (newThought?.def == null) return;

            float moodEffect = 0f;
            var curStage = newThought.CurStage;
            if (curStage != null)
                moodEffect = curStage.baseMoodEffect;

            if (Mathf.Abs(moodEffect) < RimMindDialogueSettings.Get().moodChangeThreshold) return;

            Pawn pawn = __instance.pawn;
            if (pawn == null || !pawn.IsColonist || pawn.Map == null) return;

            if (newThought.def.defName.StartsWith("RimMindDialogue_")) return;

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            var key = (pawn.thingIDNumber, newThought.def.defName);
            if (LastTriggeredTicks.TryGetValue(key, out int lastTick) && nowTick >= lastTick && (nowTick - lastTick) < SameThoughtCooldownTicks)
            {
                return;
            }
            LastTriggeredTicks[key] = nowTick;

            string rawLabel = newThought.LabelCap ?? newThought.def.label ?? newThought.def.defName;
            string cleanLabel = System.Text.RegularExpressions.Regex.Replace(
                rawLabel.Replace("{ROLE_label}", "role").Replace("{0}", ""),
                @"\{[A-Za-z0-9_]+\}",
                "").Trim();
            string context = "RimMind.Dialogue.Context.MoodChanged".Translate(cleanLabel, $"{moodEffect:+0;-0}");
            RimMindDialogueService.HandleTrigger(pawn, context, DialogueTriggerType.Thought, null);
        }
    }
}
