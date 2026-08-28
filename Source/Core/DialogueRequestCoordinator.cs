using System;
using RimMind.Application.Common.Interfaces.Context;
using RimMind.Application.Common.Models.Context;
using RimMind.Application.Features.Llm;
using RimMind.Dialogue.Settings;
using RimMind.Domain.Llm;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;
using RimWorld;
using Verse;

namespace RimMind.Dialogue.Core
{
    internal sealed class DialogueRequestCoordinator
    {
        private readonly DialogueActivityState _activityState;
        private readonly DialogueRequestReservations _reservations =
            new DialogueRequestReservations();

        public DialogueRequestCoordinator(DialogueActivityState activityState)
        {
            _activityState = activityState
                ?? throw new ArgumentNullException(nameof(activityState));
        }

        public int ActiveRequestCount => _reservations.ActivePawnCount;

        public int ActivePairCount => _reservations.ActivePairCount;

        public void Reset() => _reservations.Reset();

        public bool IsDialoguePending(int pawnIdA, int pawnIdB)
        {
            return _reservations.IsPawnPending(pawnIdA)
                || _reservations.IsPawnPending(pawnIdB)
                || _reservations.IsPairPending(
                    DialogueClassifier.MakePairKey(pawnIdA, pawnIdB));
        }

        public void HandleTrigger(
            Pawn pawn,
            string context,
            DialogueTriggerType type,
            Pawn? recipient,
            bool isReply = false)
        {
            RimMindDialogueSettings settings = RimMindDialogueSettings.Get();
            int currentTick = Find.TickManager.TicksGame;
            if (!settings.enabled || !RimMindAPI.IsConfigured())
                return;
            if (!_activityState.IsReady(settings, currentTick))
                return;

            bool isMonologue = DialogueFlowPolicy.IsMonologue(
                type,
                recipient != null);
            if (_reservations.IsPawnPending(pawn.thingIDNumber))
            {
                Log.Message(
                    $"[RimMind-Dialogue] {(isMonologue ? "Monologue" : "Dialogue")} SKIPPED for {pawn.LabelShort}: pending request exists");
                return;
            }

            if (RimMindAPI.ShouldSkipDialogue(pawn, type.ToString()))
            {
                Log.Message(
                    $"[RimMind-Dialogue] {(isMonologue ? "Monologue" : "Dialogue")} SKIPPED for {pawn.LabelShort} ({type}): AI condition not met");
                return;
            }

            (int, int)? pairKey = recipient == null
                ? null
                : DialogueClassifier.MakePairKey(
                    pawn.thingIDNumber,
                    recipient.thingIDNumber);
            if (pairKey.HasValue
                && _reservations.IsPairPending(pairKey.Value))
            {
                return;
            }

            if (DialogueFlowPolicy.UsesMonologueCooldown(
                    type,
                    recipient != null)
                && _activityState.IsMonologueOnCooldown(
                    currentTick,
                    pawn.thingIDNumber,
                    type,
                    settings.monologueCooldownTicks))
            {
                return;
            }

            if (DialogueFlowPolicy.UsesDailyQuota(
                    type,
                    recipient != null,
                    isReply)
                && recipient != null
                && _activityState.IsDailyLimitReached(
                    currentTick,
                    pawn.thingIDNumber,
                    recipient.thingIDNumber,
                    settings.maxDailyDialogueRounds))
            {
                return;
            }

            if (!_reservations.TryAcquire(
                    pawn.thingIDNumber,
                    pairKey,
                    settings.globalConcurrency,
                    out DialogueRequestReservations.DialogueReservation? reservation))
            {
                Log.Message(
                    $"[RimMind-Dialogue] Request reservation unavailable (limit {settings.globalConcurrency}) for {pawn.LabelShort}");
                return;
            }

            DispatchRequest(
                pawn,
                recipient,
                context,
                type,
                isReply,
                isMonologue,
                currentTick,
                settings,
                reservation!);
        }

        public void TryTriggerReply(
            Pawn originalSender,
            Pawn replier,
            string originalMessage)
        {
            var pairKey = DialogueClassifier.MakePairKey(
                originalSender.thingIDNumber,
                replier.thingIDNumber);
            int currentDay = (int)(Find.TickManager.TicksGame / 2500f / 24f);
            if (!_activityState.TryConsumeReply(
                    pairKey,
                    currentDay,
                    RimMindDialogueSettings.Get().maxDailyReplyRounds))
            {
                Log.Message(
                    $"[RimMind-Dialogue] Auto-reply daily limit reached for pair {pairKey.Item1}|{pairKey.Item2}");
                return;
            }

            string replyContext = "RimMind.Dialogue.Context.ReplyTrigger"
                .Translate(originalSender.Name.ToStringShort, originalMessage);
            HandleTrigger(
                replier,
                replyContext,
                DialogueTriggerType.Chitchat,
                originalSender,
                isReply: true);
        }

        private void DispatchRequest(
            Pawn pawn,
            Pawn? recipient,
            string context,
            DialogueTriggerType type,
            bool isReply,
            bool isMonologue,
            int currentTick,
            RimMindDialogueSettings settings,
            DialogueRequestReservations.DialogueReservation reservation)
        {
            long reservationId = reservation.Id;
            try
            {
                _activityState.RecordTrigger(
                    currentTick,
                    pawn.thingIDNumber,
                    type,
                    settings.monologueCooldownTicks);
                if (recipient != null)
                {
                    _activityState.SetRequestRecipient(
                        pawn.thingIDNumber,
                        recipient.thingIDNumber,
                        reservationId);
                }

                string formattedContext = FormatContext(type, context, recipient);
                string triggerLabel = RimMindDialogueService.GetTriggerLabel(type);
                Log.Message(
                    $"[RimMind-Dialogue] Trigger: {pawn.Name.ToStringShort} | Reason: {triggerLabel} | Context: {formattedContext}");

                string npcId = $"NPC-{pawn.thingIDNumber}";
                var envelope = LlmRequestEnvelopeBuilder
                    .ForNpc(
                        npcId,
                        gameStateInfo: new GameStateInfo().AddSection(
                            "dialogue_trigger",
                            type == DialogueTriggerType.PlayerInput
                                ? formattedContext
                                : "RimMind.Dialogue.Prompt.AutoTrigger".Translate()))
                    .ForScenarioId(ScenarioIds.Dialogue)
                    .WithModId("RimMind.Dialogue")
                    .WithMaxTokens(400)
                    .WithTemperature(0.8f)
                    .Build();

                RimMindAPI.Request.Send(envelope, result =>
                {
                    try
                    {
                        LongEventHandler.ExecuteWhenFinished(() =>
                        {
                            reservation.Dispose();
                            try
                            {
                                if (result.IsErr)
                                {
                                    RimMindErrors.Warn(
                                        $"[RimMind-Dialogue] Chat failed for {pawn.Name.ToStringShort}: {result.Error}");
                                    if (!isMonologue)
                                    {
                                        Messages.Message(
                                            "RimMind.Dialogue.UI.FloatMenu.RequestFailed"
                                                .Translate(pawn.Name.ToStringShort),
                                            MessageTypeDefOf.RejectInput,
                                            false);
                                    }
                                    return;
                                }

                                NpcResponseHandler.Handle(
                                    result.Value,
                                    npcId,
                                    pawn,
                                    recipient,
                                    formattedContext,
                                    type,
                                    isReply);
                            }
                            finally
                            {
                                ClearRequestRecipient(
                                    pawn.thingIDNumber,
                                    reservationId);
                            }
                        });
                    }
                    catch
                    {
                        reservation.Dispose();
                        ClearRequestRecipient(
                            pawn.thingIDNumber,
                            reservationId);
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                reservation.Dispose();
                ClearRequestRecipient(pawn.thingIDNumber, reservationId);
                RimMindErrors.Warn(
                    $"[RimMind-Dialogue] Request dispatch failed for {pawn.Name.ToStringShort}: {ex.Message}");
            }
        }

        private void ClearRequestRecipient(int pawnId, long reservationId)
            => _activityState.ClearRequestRecipientIfOwned(
                pawnId,
                reservationId);

        private static string FormatContext(
            DialogueTriggerType type,
            string context,
            Pawn? recipient)
        {
            string formatted = type switch
            {
                DialogueTriggerType.Chitchat =>
                    "RimMind.Dialogue.Prompt.Context.Chitchat".Translate(context),
                DialogueTriggerType.Hediff =>
                    "RimMind.Dialogue.Prompt.Context.Hediff".Translate(context),
                DialogueTriggerType.LevelUp =>
                    "RimMind.Dialogue.Prompt.Context.LevelUp".Translate(context),
                DialogueTriggerType.Thought =>
                    "RimMind.Dialogue.Prompt.Context.Thought".Translate(context),
                DialogueTriggerType.Auto =>
                    "RimMind.Dialogue.Prompt.Context.Auto".Translate(context),
                _ => context
            };

            string? roleKey = recipient == null
                ? null
                : GetRecipientRoleKey(recipient);
            if (recipient != null && roleKey != null)
            {
                formatted += "\n"
                    + "RimMind.Dialogue.Prompt.Context.Recipient"
                        .Translate(recipient.Name.ToStringShort)
                    + "\n"
                    + roleKey.Translate();
            }

            return formatted;
        }

        private static string? GetRecipientRoleKey(Pawn recipient)
        {
            if (recipient.IsPrisoner)
                return "RimMind.Dialogue.Prompt.Role.Prisoner";
            if (recipient.IsSlave)
                return "RimMind.Dialogue.Prompt.Role.Slave";
            if (recipient.Faction?.HostileTo(Faction.OfPlayer) == true)
                return "RimMind.Dialogue.Prompt.Role.Enemy";
            if (!recipient.IsColonist)
                return "RimMind.Dialogue.Prompt.Role.Visitor";
            return null;
        }
    }
}
