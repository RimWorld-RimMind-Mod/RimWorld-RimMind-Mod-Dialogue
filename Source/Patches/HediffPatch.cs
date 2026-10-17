using System;
using System.Reflection;
using HarmonyLib;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Presentation.Api;
using RimWorld;
using Verse;

namespace RimMind.Dialogue.Patches
{
    [HarmonyPatch(typeof(Pawn_HealthTracker), "AddHediff",
        new Type[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class HediffPatch
    {
        private static readonly FieldInfo? PawnField = AccessTools.Field(typeof(Pawn_HealthTracker), "pawn");

        [HarmonyPostfix]
        public static void Postfix(Pawn_HealthTracker __instance, Hediff hediff)
        {
            if (!RimMindDialogueSettings.Get().hediffEnabled) return;

            Pawn? pawn = PawnField?.GetValue(__instance) as Pawn;
            if (!RimMindPawnLookup.IsEligibleColonist(pawn) || pawn!.Downed) return;

            if (hediff?.def == null) return;
            if (!IsSignificantHediff(hediff.def)) return;

            string context = "RimMind.Dialogue.Context.HediffOccurred".Translate(hediff.def.label);
            RimMindDialogueService.HandleTrigger(pawn, context, DialogueTriggerType.Hediff, null);
        }

        private static bool IsSignificantHediff(HediffDef def)
        {
            return def.isBad || def.tendable || def.makesSickThought;
        }
    }
}
