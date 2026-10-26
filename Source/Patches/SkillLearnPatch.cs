using HarmonyLib;
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
            if (!RimMindDialogueSettings.Get().levelUpEnabled) return;

            Pawn pawn = __instance.Pawn;
            if (!RimMindPawnLookup.IsEligibleColonist(pawn) || pawn.Downed) return;

            string context = "RimMind.Dialogue.Context.SkillLevelUp".Translate(__instance.def.label, __instance.Level);
            RimMindDialogueService.HandleTrigger(pawn, context, DialogueTriggerType.LevelUp, null);
        }
    }
}
