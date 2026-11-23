using HarmonyLib;
using RimMind.Application.Features.GameEvents;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Presentation.Api;
using RimWorld;
using Verse;

namespace RimMind.Dialogue.Patches
{
    [HarmonyPatch(typeof(SkillRecord), "Learn")]
    public static class SkillLearnPatch
    {
        [HarmonyPrefix]
        public static void Prefix(SkillRecord __instance, float xp, out int __state)
        {
            __state = xp > 0f && !__instance.TotallyDisabled ? __instance.Level : -1;
        }

        [HarmonyPostfix]
        public static void Postfix(SkillRecord __instance, float xp, int __state)
        {
            if (__state < 0 || __instance.Level <= __state) return;
            var settings = RimMindDialogueSettings.Get();
            if (!settings.enabled || !settings.levelUpEnabled) return;

            Pawn pawn = __instance.Pawn;
            if (!RimMindPawnLookup.IsEligibleColonist(pawn) || pawn.Downed) return;

            RimMindAPI.GameEvents.Publish(new SkillLevelUpGameEvent(
                pawn.thingIDNumber,
                __instance.def?.defName ?? string.Empty,
                __state,
                __instance.Level));
            string skillLabel = __instance.def?.label ?? __instance.def?.defName ?? string.Empty;
            string context = RimMindAPI.Text.TrOr(
                "RimMind.Dialogue.Context.SkillLevelUp",
                "{0} skill reached level {1}",
                skillLabel,
                __instance.Level);
            RimMindDialogueService.HandleTrigger(pawn, context, DialogueTriggerType.LevelUp, null);
        }
    }
}
