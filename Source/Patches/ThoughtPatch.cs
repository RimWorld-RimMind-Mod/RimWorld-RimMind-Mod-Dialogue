using System;
using HarmonyLib;
using RimMind.Application.Common.Helpers;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Presentation.Api;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Patches
{
    [HarmonyPatch(typeof(MemoryThoughtHandler), "TryGainMemory",
        new Type[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class ThoughtPatch
    {
        internal const int SameThoughtCooldownTicks = 60000; // 1 in-game day
        internal const int MaxTrackedThoughtEntries = 256;
        internal static readonly KeyedCooldownGate<(int PawnId, string ThoughtDefName)> LastTriggeredTicks =
            new KeyedCooldownGate<(int PawnId, string ThoughtDefName)>(MaxTrackedThoughtEntries);

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
            if (!RimMindPawnLookup.IsEligibleColonist(pawn) || pawn.Downed) return;

            if (newThought.def.defName.StartsWith("RimMindDialogue_")) return;

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            var key = (pawn.thingIDNumber, newThought.def.defName);
            if (!LastTriggeredTicks.TryEnter(key, nowTick, SameThoughtCooldownTicks))
            {
                return;
            }

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
