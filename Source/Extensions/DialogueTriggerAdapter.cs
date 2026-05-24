using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Dialogue.Core;
using RimWorld;
using Verse;

namespace RimMind.Dialogue
{
    internal sealed class DialogueTriggerAdapter : IDialogueTrigger
    {
        public string Id => "dialogue";
        public string OwnerModId => "RimMind.Dialogue";
        public void Trigger(object pawn, string context, object? recipient) =>
            RimMindDialogueService.HandleTrigger((Pawn)pawn, context, DialogueTriggerType.Chitchat, recipient as Pawn);
    }
}
