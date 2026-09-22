using System;
using System.Threading;
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
        private CancellationTokenSource _gameLifetime = new CancellationTokenSource();

        public DialogueRequestCoordinator(DialogueActivityState activityState)
        {
            _activityState = activityState
                ?? throw new ArgumentNullException(nameof(activityState));
        }

        public int ActiveRequestCount => _reservations.ActivePawnCount;

        public int ActivePairCount => _reservations.ActivePairCount;

        public void Reset()
        {
            CancellationTokenSource previous = _gameLifetime;
            _gameLifetime = new CancellationTokenSource();
            _reservations.Reset();
            previous.Cancel();
            previous.Dispose();
        }

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
            bool isReply = false,
            Action<string>? onReply = null,
            Action<string>? onError = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                RimMindDialogueSettings settings = RimMindDialogueSettings.Get();
                int currentTick = Find.TickManager.TicksGame;
                string? rejection = cancellationToken.IsCancellationRequested
                    ? "Dialogue request cancelled."
                    : GetRejection(pawn, recipient, type, isReply, settings, currentTick);
                if (rejection != null)
                {
                    Notify(onError, rejection);
                    return;
                }

                (int, int)? pairKey = recipient == null
                    ? null
                    : DialogueClassifier.MakePairKey(pawn.thingIDNumber, recipient.thingIDNumber);
                if (!_reservations.TryAcquire(
                        pawn.thingIDNumber,
                        pairKey,
                        settings.globalConcurrency,
                        out DialogueRequestReservations.DialogueReservation? reservation))
                {
                    Notify(onError, "Dialogue request capacity or reservation unavailable.");
                    return;
                }

                DispatchRequest(pawn, recipient, context, type, isReply, currentTick,
                    settings, reservation!, onReply, onError, cancellationToken);
            }
            catch (Exception ex)
            {
                RimMindErrors.Warn($"[RimMind-Dialogue] Request admission failed: {ex.Message}");
                Notify(onError, ex.Message);
            }
        }

        private string? GetRejection(
            Pawn pawn,
            Pawn? recipient,
            DialogueTriggerType type,
            bool isReply,
            RimMindDialogueSettings settings,
            int currentTick)
        {
            if (!settings.enabled
                || (type == DialogueTriggerType.PlayerInput && !settings.playerDialogueEnabled))
                return "Dialogue is disabled.";
            if (pawn == null || pawn.Dead || pawn.Destroyed
                || (recipient != null && (recipient.Dead || recipient.Destroyed)))
                return "Dialogue participant is unavailable.";
            if (!RimMindAPI.IsConfigured())
                return "AI is not configured.";
            if (!_activityState.IsReady(settings, currentTick))
                return "Dialogue startup delay is active.";
            if (_reservations.IsPawnPending(pawn.thingIDNumber))
                return "A dialogue request is already pending for this pawn.";
            if (RimMindAPI.ShouldSkipDialogue(pawn, type.ToString()))
                return "AI dialogue condition is not met.";

            if (DialogueFlowPolicy.UsesMonologueCooldown(
                    type,
                    recipient != null)
                && _activityState.IsMonologueOnCooldown(
                    currentTick,
                    pawn.thingIDNumber,
                    type,
                    settings.monologueCooldownTicks))
            {
                return "Monologue cooldown is active.";
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
                return "Daily dialogue limit reached.";
            }

            return null;
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
            int currentTick,
            RimMindDialogueSettings settings,
            DialogueRequestReservations.DialogueReservation reservation,
            Action<string>? onReply,
            Action<string>? onError,
            CancellationToken cancellationToken)
        {
            long reservationId = reservation.Id;
            bool completed = false;
            CancellationTokenSource? lifetime = null;
            CancellationTokenRegistration cancellation = default;

            bool TryFinish()
            {
                if (completed) return false;
                completed = true;
                cancellation.Dispose();
                lifetime?.Dispose();
                reservation.Dispose();
                ClearRequestRecipient(pawn.thingIDNumber, reservationId);
                return true;
            }

            void ReportFailure(string error, bool showAutomaticError = true)
            {
                if (onError != null)
                {
                    Notify(onError, error);
                    return;
                }
                if (!showAutomaticError) return;
                RimMindErrors.Warn($"[RimMind-Dialogue] Request failed: {error}");
                if (!DialogueFlowPolicy.IsMonologue(type, recipient != null))
                {
                    Messages.Message(
                        "RimMind.Dialogue.UI.FloatMenu.RequestFailed".Translate(pawn.Name.ToStringShort),
                        MessageTypeDefOf.RejectInput, false);
                }
            }

            try
            {
                lifetime = CancellationTokenSource.CreateLinkedTokenSource(
                    _gameLifetime.Token, cancellationToken);
                cancellation = lifetime.Token.Register(() =>
                {
                    if (TryFinish()) ReportFailure("Dialogue request cancelled.", false);
                });
                if (completed) return;

                if (type != DialogueTriggerType.PlayerInput)
                {
                    _activityState.RecordTrigger(currentTick, pawn.thingIDNumber,
                        type, settings.monologueCooldownTicks);
                }
                RimMind.Presentation.Api.RimMindPawnLookup.CachePawn(pawn);
                if (recipient != null)
                {
                    _activityState.SetRequestRecipient(
                        pawn.thingIDNumber,
                        recipient.thingIDNumber,
                        reservationId,
                        recipient);
                    RimMind.Presentation.Api.RimMindPawnLookup.CachePawn(recipient);
                }

                string formattedContext = FormatContext(type, context, recipient);
                string triggerLabel = RimMindDialogueService.GetTriggerLabel(type);
                Log.Message($"[RimMind-Dialogue] Trigger: {pawn.Name.ToStringShort} | Reason: {triggerLabel}");

                string npcId = $"NPC-{pawn.thingIDNumber}";
                var envelope = LlmRequestEnvelopeBuilder
                    .ForNpc(
                        npcId,
                        gameStateInfo: new GameStateInfo().AddSection(
                            type == DialogueTriggerType.PlayerInput ? "dialogue_input" : "dialogue_trigger",
                            type == DialogueTriggerType.PlayerInput
                                ? context
                                : "RimMind.Dialogue.Prompt.AutoTrigger".Translate()))
                    .ForScenarioId(ScenarioIds.Dialogue)
                    .WithModId("RimMind.Dialogue")
                    .WithMaxTokens(400)
                    .WithTemperature(type == DialogueTriggerType.PlayerInput ? 0.85f : 0.8f)
                    .WithCancellation(lifetime.Token)
                    .Build();

                RimMindAPI.Request.Send(envelope, result =>
                {
                    // Core delivers terminal callbacks on the main thread. Release before
                    // response handling so an A-B reply can immediately reserve the pair.
                    if (!TryFinish()) return;
                    try
                    {
                        if (result.IsErr)
                        {
                            ReportFailure(result.Error.ToString());
                            return;
                        }
                        if (pawn.Dead || pawn.Destroyed
                            || (recipient != null && (recipient.Dead || recipient.Destroyed)))
                        {
                            ReportFailure("Dialogue participant is unavailable.");
                            return;
                        }
                        if (string.IsNullOrWhiteSpace(result.Value.Content))
                        {
                            ReportFailure("Empty reply.");
                            return;
                        }

                        NpcResponseHandler.Handle(result.Value, npcId, pawn, recipient,
                            type == DialogueTriggerType.PlayerInput ? context : formattedContext,
                            type, isReply);
                        Notify(onReply, result.Value.Content);
                    }
                    catch (Exception ex)
                    {
                        ReportFailure(ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                if (TryFinish()) ReportFailure(ex.Message);
            }
        }

        private static void Notify(Action<string>? callback, string value)
        {
            try { callback?.Invoke(value); }
            catch (Exception ex)
            {
                RimMindErrors.Warn($"[RimMind-Dialogue] Completion subscriber failed: {ex.Message}");
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
