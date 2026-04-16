using HarmonyLib;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimWorld;
using Verse;

namespace RimMind.Dialogue.Patches
{
    [HarmonyPatch(typeof(SkillRecord), "Learn")]
    public static class SkillLearnPatch
    {
        private static int _levelBefore;

        [HarmonyPrefix]
        public static void Prefix(SkillRecord __instance)
        {
            _levelBefore = __instance.Level;
        }

        [HarmonyPostfix]
        public static void Postfix(SkillRecord __instance)
        {
            if (__instance.Level <= _levelBefore) return;
            if (!RimMindDialogueSettings.Get().levelUpEnabled) return;

            Pawn pawn = __instance.Pawn;
            if (pawn == null || !pawn.IsColonist || pawn.Map == null) return;

            string context = "RimMind.Dialogue.Context.SkillLevelUp".Translate(__instance.def.label, __instance.Level);
            RimMindDialogueService.HandleTrigger(pawn, context, DialogueTriggerType.LevelUp, null);
        }
    }
}
