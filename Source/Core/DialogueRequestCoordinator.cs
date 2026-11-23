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
            if (!RimMind.Presentation.Api.RimMindPawnLookup.IsActiveSpawnedColonist(pawn) || pawn.Downed)
                return "Dialogue participant is unavailable.";
            if (recipient != null && (!RimMind.Presentation.Api.RimMindPawnLookup.IsActiveSpawnedColonist(recipient) || recipient.Downed))
                return "Dialogue recipient is unavailable.";
            if (!RimMindAPI.Settings.IsConfigured())
                return "AI is not configured.";
            if (!_activityState.IsReady(settings, currentTick))
                return "Dialogue startup delay is active.";
            if (_reservations.IsPawnPending(pawn.thingIDNumber))
                return "A dialogue request is already pending for this pawn.";
            if (RimMindAPI.Ext.ShouldSkipDialogue(pawn, ToTriggerKind(type)))
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

        private const int AutoDialogueMaxQueueWaitTicks = 5000;

        private static string ToTriggerKind(DialogueTriggerType type)
            => DialogueTriggerRegistry.GetTriggerKind(type);

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
                return;
            }

            string senderName = originalSender.Name?.ToStringShort ?? originalSender.LabelShort;
            string replyContext = RimMindAPI.Text.TrOr(
                "RimMind.Dialogue.Context.ReplyTrigger",
                "{0} said to you: {1}",
                senderName,
                originalMessage);
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
            int completed = 0;
            CancellationTokenSource? lifetime = null;
            CancellationTokenRegistration cancellation = default;

            bool TryFinish(bool fromCancellationCallback = false)
            {
                if (Interlocked.Exchange(ref completed, 1) != 0) return false;
                if (!fromCancellationCallback)
                {
                    try { cancellation.Dispose(); } catch (ObjectDisposedException) { }
                }
                try { lifetime?.Dispose(); } catch (ObjectDisposedException) { }
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
                if (type == DialogueTriggerType.PlayerInput)
                {
                    string pawnName = pawn.Name?.ToStringShort ?? pawn.LabelShort;
                    Messages.Message(
                        RimMindAPI.Text.TrOr(
                            "RimMind.Dialogue.UI.FloatMenu.RequestFailed",
                            "Dialogue request for {0} failed",
                            pawnName),
                        MessageTypeDefOf.RejectInput, false);
                }
            }

            try
            {
                lifetime = CancellationTokenSource.CreateLinkedTokenSource(
                    _gameLifetime.Token, cancellationToken);
                cancellation = lifetime.Token.Register(() =>
                {
                    if (TryFinish(fromCancellationCallback: true))
                        ReportFailure("Dialogue request cancelled.", false);
                });
                if (Volatile.Read(ref completed) != 0) return;

                if (type != DialogueTriggerType.PlayerInput)
                {
                    _activityState.RecordTrigger(currentTick, pawn.thingIDNumber,
                        type, settings.monologueCooldownTicks);
                }
                RimMindPawnLookup.CachePawn(pawn);
                if (recipient != null)
                {
                    _activityState.SetRequestRecipient(
                        pawn.thingIDNumber,
                        recipient.thingIDNumber,
                        reservationId,
                        recipient);
                    RimMindPawnLookup.CachePawn(recipient);
                    // The active recipient feeds dialogue_relation; drop the cached copy now.
                    DialogueContextChangedEvent.Publish(pawn.thingIDNumber);
                }

                string formattedContext = FormatContext(type, context);
                bool isMonologue = DialogueFlowPolicy.IsMonologue(type, recipient != null);
                string? speakerName = recipient != null
                    ? (recipient.Name?.ToStringShort ?? recipient.LabelShort)
                    : (type == DialogueTriggerType.PlayerInput ? "Player" : null);

                string npcId = $"NPC-{pawn.thingIDNumber}";
                bool isCrisis = pawn.InMentalState || (pawn.needs?.mood != null && pawn.needs.mood.CurLevel < 0.20f);
                var gameState = new GameStateInfo();
                if (isCrisis)
                {
                    string mentalReason = pawn.MentalStateDef?.label ?? RimMindAPI.Text.TrOr(
                        "RimMind.Dialogue.Prompt.SevereCrisis",
                        "Severe Crisis");
                    string crisisSection = RimMindAPI.Text.TrOr(
                        "RimMind.Dialogue.Prompt.CrisisBreakdown",
                        "[CRISIS BREAKDOWN: {0}] Speaker is currently suffering a mental break or severe distress. Do NOT engage in calm polite small talk. Output an intense, raw, emotional monologue or confrontation reflecting extreme distress, agony, panic, or rage.",
                        mentalReason);
                    gameState.AddSection("crisis_expression", crisisSection);
                }
                string autoTriggerInstruction = RimMindAPI.Text.TrOr(
                    "RimMind.Dialogue.Prompt.AutoTrigger",
                    "Based on your current state and surroundings, respond naturally.");
                string triggerPayload = string.IsNullOrWhiteSpace(formattedContext)
                    ? autoTriggerInstruction
                    : $"{formattedContext}\n{autoTriggerInstruction}";
                gameState.AddSection(
                    type == DialogueTriggerType.PlayerInput ? "dialogue_input" : "dialogue_trigger",
                    type == DialogueTriggerType.PlayerInput
                        ? context
                        : triggerPayload);

                var envelopeBuilder = LlmRequestEnvelopeBuilder
                    .ForNpc(
                        npcId,
                        gameStateInfo: gameState)
                    .ForScenarioId(isCrisis ? ScenarioIds.CrisisDialogue : ScenarioIds.Dialogue)
                    .WithModId("RimMind.Dialogue")
                    .WithHint("IsMonologue", isMonologue ? "true" : "false")
                    .WithHint("TriggerContext", formattedContext)
                    .WithTools(DialogueToolDefinitions.AsList())
                    .WithToolDispatchMode(ToolCallDispatchMode.Manual)
                    .WithMaxTokens(800)
                    .WithTemperature(type == DialogueTriggerType.PlayerInput ? 0.85f : 0.8f)
                    .WithReasoningEffort(ReasoningEffort.None)
                    .WithRequireAliveColonist(true)
                    .WithCancellation(lifetime.Token);
                if (type != DialogueTriggerType.PlayerInput)
                {
                    string coalesceKey = BuildCoalesceKey(pawn, recipient, type);
                    envelopeBuilder
                        .WithCoalesceKey(coalesceKey)
                        .WithMaxQueueWaitTicks(AutoDialogueMaxQueueWaitTicks);
                }
                if (!string.IsNullOrEmpty(speakerName))
                {
                    envelopeBuilder.WithHint("SpeakerName", speakerName);
                }
                var envelope = envelopeBuilder.Build();

                RimMindAPI.Request.Send(envelope, result =>
                {
                    // Core delivers terminal callbacks on the main thread. Release before
                    // response handling so an A-B reply can immediately reserve the pair.
                    if (!TryFinish()) return;
                    try
                    {
                        if (result.IsErr)
                        {
                            if (result.Error.Code != RimMindErrorCode.Cancelled) ReportFailure(result.Error.ToString());
                            return;
                        }
                        if (!RimMindPawnLookup.IsActiveSpawnedColonist(pawn) || pawn.Downed
                            || (recipient != null && (!RimMindPawnLookup.IsActiveSpawnedColonist(recipient) || recipient.Downed)))
                        {
                            ReportFailure("Dialogue participant is unavailable.");
                            return;
                        }
                        if (string.IsNullOrWhiteSpace(result.Value.Content) && string.IsNullOrWhiteSpace(result.Value.ToolCallsJson))
                        {
                            ReportFailure("Empty reply.");
                            return;
                        }

                        string? speech = NpcResponseHandler.Handle(result.Value, npcId, pawn, recipient,
                            type == DialogueTriggerType.PlayerInput ? context : formattedContext,
                            type, isReply);
                        if (!string.IsNullOrWhiteSpace(speech))
                        {
                            // Reply applied thoughts/relation changes that feed dialogue context.
                            DialogueContextChangedEvent.Publish(pawn.thingIDNumber);
                            Notify(onReply, speech!);
                        }
                        else
                        {
                            ReportFailure("Empty reply.");
                        }
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

        private static string BuildCoalesceKey(Pawn pawn, Pawn? recipient, DialogueTriggerType type)
        {
            if (recipient != null)
            {
                var pairKey = DialogueClassifier.MakePairKey(pawn.thingIDNumber, recipient.thingIDNumber);
                return $"dialogue:{pairKey.Item1}:{pairKey.Item2}:{type}";
            }
            return $"dialogue:{pawn.thingIDNumber}:{type}";
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

        private static string FormatContext(DialogueTriggerType type, string context)
            => DialogueTriggerRegistry.FormatContext(type, context);
    }
}
