using HarmonyLib;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimWorld;
using Verse;

namespace RimMind.Dialogue.Patches
{
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), "TryInteractWith")]
    public static class BubblePatch
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn ___pawn, Pawn recipient, InteractionDef intDef, bool __result)
        {
            if (!__result) return;
            if (!RimMindDialogueSettings.Get().chitchatEnabled) return;
            if (ModsConfig.IsActive("juicy.RimTalk")) return;

            if (intDef?.defName != "Chitchat" && intDef?.defName != "DeepTalk") return;

            Pawn initiator = ___pawn;
            if (initiator == null || !initiator.IsColonist) return;

            string context = "RimMind.Dialogue.Context.InteractionContext".Translate(intDef.defName, recipient?.LabelShort ?? "RimMind.Dialogue.Trigger.Auto".Translate());
            RimMindDialogueService.HandleTrigger(initiator, context, DialogueTriggerType.Chitchat, recipient);
        }
    }
}
