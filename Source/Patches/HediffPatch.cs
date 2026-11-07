using System;
using System.Reflection;
using HarmonyLib;
using RimMind.Application.Common.Helpers;
using RimMind.Application.Features.GameEvents;
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
        public const float MinHediffSeverity = 0.20f;
        public const int HediffCooldownTicks = 2500;

        private static readonly FieldInfo? PawnField = AccessTools.Field(typeof(Pawn_HealthTracker), "pawn");
        internal static readonly KeyedCooldownGate<int> LastTriggerTickByPawn = new KeyedCooldownGate<int>();

        public static bool ShouldTriggerHediffDialogue(
            bool isBad,
            bool isTendable,
            bool makesSickThought,
            float severity,
            bool isLifeThreatening,
            int nowTick,
            int? lastTriggerTick,
            int cooldownTicks = HediffCooldownTicks)
        {
            if (!GameEventSignificancePolicy.IsSignificantInjury(
                    isBad,
                    isTendable,
                    makesSickThought,
                    isLifeThreatening,
                    severity,
                    MinHediffSeverity))
            {
                return false;
            }
            if (lastTriggerTick.HasValue
                && nowTick >= lastTriggerTick.Value
                && (nowTick - lastTriggerTick.Value) < cooldownTicks)
            {
                return false;
            }
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn_HealthTracker __instance, Hediff hediff)
        {
            var settings = RimMindDialogueSettings.Get();
            if (!settings.enabled || !settings.hediffEnabled) return;

            if (hediff?.def == null) return;
            bool isLifeThreatening = hediff.CurStage?.lifeThreatening == true
                || (hediff.def.lethalSeverity > 0f && hediff.Severity >= hediff.def.lethalSeverity * 0.5f);
            if (!ShouldTriggerHediffDialogue(
                    hediff.def.isBad,
                    hediff.def.tendable,
                    hediff.def.makesSickThought,
                    hediff.Severity,
                    isLifeThreatening,
                    0,
                    null))
            {
                return;
            }

            Pawn? pawn = PawnField?.GetValue(__instance) as Pawn;
            if (!RimMindPawnLookup.IsEligibleColonist(pawn) || pawn!.Downed) return;
            if (!RimMindDialogueService.IsReady) return;

            int now = Find.TickManager?.TicksGame ?? 0;
            int cooldown = Math.Max(HediffCooldownTicks, settings.monologueCooldownTicks);
            if (!LastTriggerTickByPawn.TryEnter(pawn.thingIDNumber, now, cooldown))
            {
                return;
            }

            string label = hediff.def.label ?? hediff.def.defName ?? string.Empty;
            RimMindAPI.GameEvents.Publish(new HediffAddedGameEvent(
                pawn.thingIDNumber,
                hediff.def.defName ?? string.Empty,
                label,
                hediff.Severity,
                hediff.def.isBad,
                hediff.def.tendable,
                hediff.def.makesSickThought,
                isLifeThreatening,
                hediff.Part?.Label));
            string context = "RimMind.Dialogue.Context.HediffOccurred".CanTranslate()
                ? (string)"RimMind.Dialogue.Context.HediffOccurred".Translate(label)
                : label;
            RimMindDialogueService.HandleTrigger(pawn, context, DialogueTriggerType.Hediff, null);
        }
    }
}
