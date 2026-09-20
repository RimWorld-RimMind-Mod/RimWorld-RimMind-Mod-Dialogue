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
            if (recipient == null) return;

            var settings = RimMindDialogueSettings.Get();
            var pairKey = DialogueClassifier.MakePairKey(initiator.thingIDNumber, recipient.thingIDNumber);
            float opinion = initiator.relations?.OpinionOf(recipient) ?? 0f;
            int currentTick = Find.TickManager.TicksGame;

            if (!RimMindDialogueService.CadenceEvaluator.ShouldAdmit(pairKey, currentTick, settings.targetDialogueCadenceDays, opinion))
            {
                return;
            }

            string context = "RimMind.Dialogue.Context.InteractionContext".Translate(intDef.defName, recipient.LabelShort);
            RimMindDialogueService.HandleTrigger(initiator, context, DialogueTriggerType.Chitchat, recipient);
        }
    }
}
