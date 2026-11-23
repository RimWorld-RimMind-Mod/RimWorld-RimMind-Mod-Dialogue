using RimMind.Dialogue.Core;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;
using RimMind.Presentation.UI;
using RimWorld;
using Verse;

namespace RimMind.Dialogue.UI
{
    public class Window_Dialogue : RimMindChatWindowBase
    {
        private readonly Pawn _pawn;
        private readonly Pawn? _initiator;

        public Window_Dialogue(Pawn pawn, Pawn? initiator = null) : base()
        {
            _pawn = pawn;
            _initiator = initiator;
            RimMindDialogueService.SetActiveRecipient(pawn, initiator);
        }

        public override void PostClose()
        {
            bool wasOpen = !IsClosed;
            base.PostClose();
            if (wasOpen)
            {
                RimMindDialogueService.SetActiveRecipient(_pawn, null);
            }
        }

        protected override bool ValidateCanStayOpen()
            => RimMindPawnLookup.IsEligibleColonist(_pawn) && !_pawn.Downed
               && (_initiator == null || (RimMindPawnLookup.IsEligibleColonist(_initiator) && !_initiator.Downed));

        protected override string GetTitle()
        {
            string pawnName = _pawn.Name?.ToStringShort ?? _pawn.LabelShort;
            return _initiator != null
                ? RimMindAPI.Text.TrOr(
                    "RimMind.Dialogue.UI.ChatTitleWithInitiator",
                    "{0} & {1}",
                    _initiator.Name?.ToStringShort ?? _initiator.LabelShort,
                    pawnName)
                : RimMindAPI.Text.TrOr(
                    "RimMind.Dialogue.UI.ChatTitle",
                    "Chat with {0}",
                    pawnName);
        }

        protected override string GetWaitingText()
            => RimMindAPI.Text.TrOr("RimMind.Dialogue.UI.Thinking", "Thinking...");

        protected override string GetSendButtonLabel()
            => RimMindAPI.Text.TrOr("RimMind.Dialogue.UI.Send", "Send");

        protected override string GetPrefixForRole(string role)
        {
            if (role == "user")
            {
                return _initiator != null
                    ? RimMindAPI.Text.TrOr("RimMind.Dialogue.UI.PawnPrefix", "{0}: ", _initiator.Name?.ToStringShort ?? _initiator.LabelShort)
                    : RimMindAPI.Text.TrOr("RimMind.Dialogue.UI.PlayerPrefix", "You: ");
            }
            return RimMindAPI.Text.TrOr("RimMind.Dialogue.UI.PawnPrefix", "{0}: ", _pawn.Name?.ToStringShort ?? _pawn.LabelShort);
        }

        protected override void OnSubmitUserMessage(string trimmedInput)
        {
            if (IsClosed) return;
            SetWaiting(true);
            AppendMessage("user", trimmedInput);

            DialogueService.RequestReply(_pawn, trimmedInput, _initiator,
                onReply: reply =>
                {
                    if (IsClosed) return;
                    PostReply("assistant", reply);
                },
                onError: error =>
                {
                    if (IsClosed) return;
                    SetWaiting(false);
                    RimMindErrors.Warn($"[RimMind-Dialogue] Player dialogue error: {error}");
                    Verse.Messages.Message(
                        RimMindAPI.Text.TrOr(
                            "RimMind.Dialogue.UI.FloatMenu.RequestFailed",
                            "Dialogue request failed for {0}",
                            _pawn.Name?.ToStringShort ?? _pawn.LabelShort),
                        MessageTypeDefOf.RejectInput, false);
                },
                cancellationToken: LifetimeToken);
        }
    }
}
