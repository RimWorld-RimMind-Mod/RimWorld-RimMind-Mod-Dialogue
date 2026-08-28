using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RimMind.Application.Common.Models.Context;
using RimMind.Application.Features.Llm;
using RimMind.Domain.Llm;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;
using RimMind.Application.Common.Interfaces.Context;
using RimMind.Dialogue.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class RimMindDialogueService
    {
        private static readonly DialogueRequestReservations _requestReservations =
            new DialogueRequestReservations();
        private static readonly DialogueActivityState _activityState =
            new DialogueActivityState();

        private static readonly DialogueLogStore _logStore =
            new DialogueLogStore();

        internal static readonly ConcurrentDictionary<string, string> RegisteredTriggerLabels = new ConcurrentDictionary<string, string>();

        public static event Action? OnLogUpdated
        {
            add => _logStore.Updated += value;
            remove => _logStore.Updated -= value;
        }

        public static event Action<Pawn, Pawn?, string, string?>? OnDialogueCompleted;

        public static void RaiseOnDialogueCompleted(Pawn pawn, Pawn? recipient, string replyText, string? thoughtTag)
        {
            OnDialogueCompleted?.Invoke(pawn, recipient, replyText, thoughtTag);
        }

        public static bool IsReady => _activityState.IsReady(
            RimMindDialogueSettings.Get(),
            Find.TickManager.TicksGame);

        public static IReadOnlyList<DialogueLogEntry> LogEntries => _logStore.Entries;

        public static void ClearLog() => _logStore.Clear();

        public static void NotifyGameLoaded()
        {
            _requestReservations.Reset();
            _activityState.Reset(Find.TickManager.TicksGame);
            _logStore.Clear();
        }

        public static void HandleTrigger(Pawn pawn, string context,
                                         DialogueTriggerType type, Pawn? recipient,
                                         bool isReply = false)
        {
            if (!RimMindDialogueSettings.Get().enabled) return;
            if (!RimMindAPI.IsConfigured()) return;
            if (!IsReady) return;

            bool isMonologue = DialogueFlowPolicy.IsMonologue(type, recipient != null);

            if (_requestReservations.IsPawnPending(pawn.thingIDNumber))
            {
                if (isMonologue) Log.Message($"[RimMind-Dialogue] Monologue SKIPPED for {pawn.LabelShort}: pending request exists");
                else Log.Message($"[RimMind-Dialogue] Dialogue SKIPPED for {pawn.LabelShort}: pending request exists");
                return;
            }

            if (RimMindAPI.ShouldSkipDialogue(pawn, type.ToString()))
            {
                Log.Message($"[RimMind-Dialogue] {(isMonologue ? "Monologue" : "Dialogue")} SKIPPED for {pawn.LabelShort} ({type}): AI condition not met");
                return;
            }

            (int, int)? pairKey = recipient == null
                ? null
                : DialogueClassifier.MakePairKey(pawn.thingIDNumber, recipient.thingIDNumber);
            if (pairKey.HasValue && _requestReservations.IsPairPending(pairKey.Value))
                return;

            if (DialogueFlowPolicy.UsesMonologueCooldown(type, recipient != null)
                && _activityState.IsMonologueOnCooldown(
                    Find.TickManager.TicksGame,
                    pawn.thingIDNumber,
                    type,
                    RimMindDialogueSettings.Get().monologueCooldownTicks))
            {
                return;
            }

            if (DialogueFlowPolicy.UsesDailyQuota(type, recipient != null, isReply)
                && recipient != null
                && _activityState.IsDailyLimitReached(
                    Find.TickManager.TicksGame,
                    pawn.thingIDNumber,
                    recipient.thingIDNumber,
                    RimMindDialogueSettings.Get().maxDailyDialogueRounds))
            {
                return;
            }

            int globalConcurrency = RimMindDialogueSettings.Get().globalConcurrency;
            if (!_requestReservations.TryAcquire(
                    pawn.thingIDNumber,
                    pairKey,
                    globalConcurrency,
                    out var reservation))
            {
                Log.Message($"[RimMind-Dialogue] Request reservation unavailable (limit {globalConcurrency}) for {pawn.LabelShort}");
                return;
            }

            long reservationId = reservation!.Id;
            try
            {
                _activityState.RecordTrigger(
                    Find.TickManager.TicksGame,
                    pawn.thingIDNumber,
                    type,
                    RimMindDialogueSettings.Get().monologueCooldownTicks);

                if (recipient != null)
                {
                    _activityState.SetRequestRecipient(
                        pawn.thingIDNumber,
                        recipient.thingIDNumber,
                        reservationId);
                }

                string triggerLabel = GetTriggerLabel(type);
                var npcId = $"NPC-{pawn.thingIDNumber}";

                string formattedContext = type switch
                {
                    DialogueTriggerType.Chitchat => "RimMind.Dialogue.Prompt.Context.Chitchat".Translate(context),
                    DialogueTriggerType.Hediff => "RimMind.Dialogue.Prompt.Context.Hediff".Translate(context),
                    DialogueTriggerType.LevelUp => "RimMind.Dialogue.Prompt.Context.LevelUp".Translate(context),
                    DialogueTriggerType.Thought => "RimMind.Dialogue.Prompt.Context.Thought".Translate(context),
                    DialogueTriggerType.Auto => "RimMind.Dialogue.Prompt.Context.Auto".Translate(context),
                    DialogueTriggerType.PlayerInput => context,
                    _ => context
                };

                if (recipient != null)
                {
                    string? roleKey = GetRecipientRoleKey(recipient);
                    if (roleKey != null)
                    {
                        formattedContext += "\n"
                            + "RimMind.Dialogue.Prompt.Context.Recipient".Translate(recipient.Name.ToStringShort)
                            + "\n"
                            + roleKey.Translate();
                    }
                }

                Log.Message($"[RimMind-Dialogue] Trigger: {pawn.Name.ToStringShort} | Reason: {triggerLabel} | Context: {formattedContext}");

                var envelope = LlmRequestEnvelopeBuilder
                    .ForNpc(npcId, gameStateInfo: new GameStateInfo().AddSection("dialogue_trigger",
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
                            reservation!.Dispose();
                            try
                            {
                                if (result.IsErr)
                                {
                                    RimMindErrors.Warn($"[RimMind-Dialogue] Chat failed for {pawn.Name.ToStringShort}: {result.Error}");
                                    if (!isMonologue)
                                    {
                                        Messages.Message(
                                            "RimMind.Dialogue.UI.FloatMenu.RequestFailed".Translate(pawn.Name.ToStringShort),
                                            MessageTypeDefOf.RejectInput, false);
                                    }
                                    return;
                                }

                                NpcResponseHandler.Handle(result.Value, npcId, pawn, recipient, formattedContext, type, isReply);
                            }
                            finally
                            {
                                _activityState.ClearRequestRecipientIfOwned(
                                    pawn.thingIDNumber,
                                    reservationId);
                            }
                        });
                    }
                    catch
                    {
                        reservation!.Dispose();
                        _activityState.ClearRequestRecipientIfOwned(
                            pawn.thingIDNumber,
                            reservationId);
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                reservation!.Dispose();
                _activityState.ClearRequestRecipientIfOwned(
                    pawn.thingIDNumber,
                    reservationId);
                RimMindErrors.Warn($"[RimMind-Dialogue] Request dispatch failed for {pawn.Name.ToStringShort}: {ex.Message}");
            }
        }

        // ── 供 NpcResponseHandler 调用的公共方法 ──

        public static void TryTriggerReply(Pawn originalSender, Pawn replier, string originalMessage)
        {
            var pairKey = DialogueClassifier.MakePairKey(
                originalSender.thingIDNumber,
                replier.thingIDNumber);
            if (!_activityState.TryConsumeReply(
                    pairKey,
                    (int)(Find.TickManager.TicksGame / 2500f / 24f),
                    RimMindDialogueSettings.Get().maxDailyReplyRounds))
            {
                Log.Message($"[RimMind-Dialogue] Auto-reply daily limit reached for pair {pairKey.Item1}|{pairKey.Item2}");
                return;
            }

            string replyContext = "RimMind.Dialogue.Context.ReplyTrigger".Translate(originalSender.Name.ToStringShort, originalMessage);
            HandleTrigger(replier, replyContext, DialogueTriggerType.Chitchat, originalSender, isReply: true);
        }

        public static void DisplayInteraction(Pawn initiator, Pawn? recipient, string replyText)
        {
            if (initiator.Map == null) return;

            MoteMaker.ThrowText(initiator.DrawPos, initiator.Map, replyText,
                new UnityEngine.Color(0.85f, 0.95f, 1f), 6f);

            if (recipient != null && recipient.Map != null && recipient.Map == initiator.Map)
            {
                MoteMaker.ThrowText(recipient.DrawPos, recipient.Map, replyText,
                    new UnityEngine.Color(0.85f, 0.95f, 1f), 6f);
            }
        }

        public static void RegisterTriggerType(string typeId, string labelKey)
        {
            RegisteredTriggerLabels[typeId] = labelKey;
        }

        public static string GetTriggerLabel(DialogueTriggerType type)
        {
            string typeStr = type.ToString();
            if (RegisteredTriggerLabels.TryGetValue(typeStr, out var label))
                return label.Translate();
            return type switch
            {
                DialogueTriggerType.Chitchat => "RimMind.Dialogue.Trigger.Chitchat".Translate(),
                DialogueTriggerType.Hediff => "RimMind.Dialogue.Trigger.Hediff".Translate(),
                DialogueTriggerType.LevelUp => "RimMind.Dialogue.Trigger.LevelUp".Translate(),
                DialogueTriggerType.Thought => "RimMind.Dialogue.Trigger.Thought".Translate(),
                DialogueTriggerType.Auto => "RimMind.Dialogue.Trigger.Auto".Translate(),
                DialogueTriggerType.PlayerInput => "RimMind.Dialogue.Trigger.PlayerInput".Translate(),
                _ => typeStr
            };
        }

        public static void AddLogEntry(Pawn pawn, Pawn? recipient, DialogueTriggerType triggerType,
            string context, string reply, string? thoughtTag, string? thoughtDesc)
        {
            var entry = new DialogueLogEntry
            {
                tick = Find.TickManager.TicksGame,
                initiatorName = pawn.Name.ToStringShort,
                initiatorId = pawn.thingIDNumber,
                initiatorIsColonist = pawn.IsColonist,
                recipientName = recipient?.Name.ToStringShort,
                recipientId = recipient?.thingIDNumber ?? -1,
                recipientIsColonist = recipient?.IsColonist ?? false,
                category = DialogueClassifier.Classify(pawn.IsColonist, recipient?.IsColonist, triggerType),
                trigger = triggerType.ToString(),
                context = context,
                reply = reply,
                thoughtTag = thoughtTag ?? "NONE",
                thoughtDesc = thoughtDesc ?? ""
            };

            _logStore.Add(entry);
        }

        public static void RecordDailyDialogue(int idA, int idB)
            => _activityState.RecordDailyDialogue(
                Find.TickManager.TicksGame,
                idA,
                idB);

        public static Pawn? GetActiveRecipient(Pawn pawn)
            => _activityState.GetActiveRecipient(
                pawn,
                Find.TickManager.TicksGame);

        public static void SetActiveRecipient(Pawn pawn, Pawn? recipient)
            => _activityState.SetManualRecipient(
                pawn.thingIDNumber,
                recipient?.thingIDNumber);

        // ── 查询方法 ──

        public static int GetDailyDialogueCount(int idA, int idB)
            => _activityState.GetDailyDialogueCount(
                Find.TickManager.TicksGame,
                idA,
                idB);

        public static bool IsDialoguePending(int pawnIdA, int pawnIdB)
        {
            if (_requestReservations.IsPawnPending(pawnIdA)
                || _requestReservations.IsPawnPending(pawnIdB))
            {
                return true;
            }

            return _requestReservations.IsPairPending(
                DialogueClassifier.MakePairKey(pawnIdA, pawnIdB));
        }

        public static List<DialogueLogEntry> GetDialogueHistory(int pawnId, int maxCount = 20)
        {
            return _logStore.HistoryFor(pawnId, maxCount).ToList();
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
