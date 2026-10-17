using HarmonyLib;
using RimMind.Dialogue.Core;
using Verse;

namespace RimMind.Dialogue.Patches
{
    public static class GameLoadPatch
    {
        public static void ClearTriggerCooldowns()
        {
            ThoughtPatch.LastTriggeredTicks.Clear();
        }
    }

    [HarmonyPatch(typeof(GameComponentUtility), "LoadedGame")]
    public static class GameLoadedPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            GameLoadPatch.ClearTriggerCooldowns();
            RimMindDialogueService.NotifyGameLoaded(clearLog: false);
        }
    }

    [HarmonyPatch(typeof(Game), "InitNewGame")]
    public static class GameNewPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            GameLoadPatch.ClearTriggerCooldowns();
            RimMindDialogueService.NotifyGameLoaded(clearLog: true);
        }
    }
}
