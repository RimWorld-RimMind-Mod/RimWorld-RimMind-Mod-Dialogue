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

            string context = "RimMind.Dialogue.Context.MoodChanged".Translate(newThought.def.label, $"{moodEffect:+0;-0}");
            RimMindDialogueService.HandleTrigger(pawn, context, DialogueTriggerType.Thought, null);
        }
    }
}
