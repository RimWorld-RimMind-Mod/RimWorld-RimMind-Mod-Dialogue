using HarmonyLib;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Presentation.Api;
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
            var settings = RimMindDialogueSettings.Get();
            if (!settings.enabled || !settings.chitchatEnabled) return;

            if (intDef?.defName != "Chitchat" && intDef?.defName != "DeepTalk") return;

            Pawn initiator = ___pawn;
            if (!RimMindPawnLookup.IsActiveSpawnedColonist(initiator)) return;
            if (!RimMindPawnLookup.IsActiveSpawnedColonist(recipient)) return;

            var pairKey = DialogueClassifier.MakePairKey(initiator.thingIDNumber, recipient.thingIDNumber);
            float opinion = initiator.relations?.OpinionOf(recipient) ?? 0f;
            int currentTick = Find.TickManager.TicksGame;

            if (!RimMindDialogueService.CadenceEvaluator.ShouldAdmit(pairKey, currentTick, settings.targetDialogueCadenceDays, opinion))
            {
                return;
            }

            string context = RimMindAPI.Text.TrOr(
                "RimMind.Dialogue.Context.InteractionContext",
                "[{0}] Talking with {1}",
                intDef.defName,
                recipient.LabelShort);
            RimMindDialogueService.HandleTrigger(initiator, context, DialogueTriggerType.Chitchat, recipient);
        }
    }
}
