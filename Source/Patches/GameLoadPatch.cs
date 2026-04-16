using HarmonyLib;
using RimMind.Dialogue.Core;
using Verse;

namespace RimMind.Dialogue.Patches
{
    [HarmonyPatch(typeof(GameComponentUtility), "LoadedGame")]
    public static class GameLoadedPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            RimMindDialogueService.NotifyGameLoaded();
        }
    }

    [HarmonyPatch(typeof(Game), "InitNewGame")]
    public static class GameNewPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            RimMindDialogueService.NotifyGameLoaded();
        }
    }
}
