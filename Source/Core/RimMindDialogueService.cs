using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RimMind.Dialogue.Settings;
using RimMind.Presentation.Api;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class RimMindDialogueService
    {
        private static readonly DialogueActivityState _activityState =
            new DialogueActivityState();
        private static readonly DialogueRequestCoordinator _requestCoordinator =
            new DialogueRequestCoordinator(_activityState);

        internal static DialogueRequestCoordinator RequestCoordinator => _requestCoordinator;

        private static readonly DialogueLogStore _logStore =
            new DialogueLogStore();

        private static readonly DialogueCadenceEvaluator _cadenceEvaluator =
            new DialogueCadenceEvaluator();

        public static DialogueCadenceEvaluator CadenceEvaluator => _cadenceEvaluator;

        internal static ConcurrentDictionary<string, string> RegisteredTriggerLabels => DialogueTriggerRegistry.CustomLabels;

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

        internal static int ActiveRequestCount =>
            _requestCoordinator.ActiveRequestCount;

        internal static int ActivePairCount =>
            _requestCoordinator.ActivePairCount;

        internal static int RecentTriggerCount =>
            _activityState.RecentTriggerCount;

        internal static int DailyDialoguePairCount =>
            _activityState.DailyPairCount;

        internal static (int RecentTriggers, int DailyPairs) ClearAllCooldowns()
            => _activityState.ClearCooldowns();

        public static void ClearLog()
        {
            _logStore.Clear();
            _cadenceEvaluator.Clear();
        }

        public static List<DialogueLogEntry> ExportLogEntries(int maxCount)
            => _logStore.ExportEntries(maxCount);

        public static void ImportLogEntries(IEnumerable<DialogueLogEntry>? entries, int maxCount)
        {
            _logStore.ImportEntries(entries, maxCount);
            _cadenceEvaluator.RebuildFromEntries(_logStore.Entries);
        }

        public static void NotifyGameLoaded(bool clearLog = true)
        {
            _requestCoordinator.Reset();
            _activityState.Reset(Find.TickManager.TicksGame);
            if (clearLog)
            {
                _logStore.Clear();
                _cadenceEvaluator.Clear();
            }
            else
            {
                _cadenceEvaluator.RebuildFromEntries(_logStore.Entries);
            }
        }

        public static void HandleTrigger(
            Pawn pawn,
            string context,
            DialogueTriggerType type,
            Pawn? recipient,
            bool isReply = false,
            Action<string>? onReply = null,
            Action<string>? onError = null)
            => _requestCoordinator.HandleTrigger(
                pawn,
                context,
                type,
                recipient,
                isReply,
                onReply,
                onError);

        // ── 供 NpcResponseHandler 调用的公共方法 ──

        public static void TryTriggerReply(
            Pawn originalSender,
            Pawn replier,
            string originalMessage)
            => _requestCoordinator.TryTriggerReply(
                originalSender,
                replier,
                originalMessage);

        internal const float OverheadMoteBaseZOffset = 1.35f;
        internal const float OverheadMotePerExtraLineZOffset = 0.25f;
        internal const float OverheadMoteMaxExtraZOffset = 0.75f;

        /// <summary>
        /// Computes the world-space spawn position for an overhead speech mote so multi-line
        /// dialogue floats cleanly above the colonist's head and never collides with the pawn's
        /// nameplate below <see cref="Thing.DrawPos"/>.
        /// </summary>
        internal static Vector3 ComputeOverheadMotePosition(Vector3 pawnDrawPos, string? moteText)
        {
            int extraLines = 0;
            if (!string.IsNullOrEmpty(moteText))
            {
                for (int i = 0; i < moteText!.Length; i++)
                {
                    if (moteText[i] == '\n')
                        extraLines++;
                }
            }

            float extraZ = Math.Min(OverheadMoteMaxExtraZOffset, extraLines * OverheadMotePerExtraLineZOffset);
            return new Vector3(pawnDrawPos.x, pawnDrawPos.y, pawnDrawPos.z + OverheadMoteBaseZOffset + extraZ);
        }

        public static void DisplayInteraction(Pawn initiator, Pawn? recipient, string replyText)
        {
            if (initiator.Map == null) return;

            string moteText = DialogueTextSanitizer.FormatOverheadMoteText(replyText);
            if (string.IsNullOrWhiteSpace(moteText)) return;

            Vector3 spawnPos = ComputeOverheadMotePosition(initiator.DrawPos, moteText);
            MoteMaker.ThrowText(spawnPos, initiator.Map, moteText,
                new Color(0.85f, 0.95f, 1f), 6f);
        }

        public static void RegisterTriggerType(string typeId, string labelKey)
            => DialogueTriggerRegistry.RegisterLabel(typeId, labelKey);

        public static string GetTriggerLabel(DialogueTriggerType type)
            => DialogueTriggerRegistry.GetLabel(type);

        public static void AddLogEntry(Pawn pawn, Pawn? recipient, DialogueTriggerType triggerType,
            string context, string reply, string? thoughtTag, string? thoughtDesc)
        {
            var entry = new DialogueLogEntry
            {
                tick = Find.TickManager.TicksGame,
                initiatorName = pawn.Name?.ToStringShort ?? pawn.LabelShort,
                initiatorId = pawn.thingIDNumber,
                initiatorIsColonist = pawn.IsColonist,
                recipientName = recipient == null ? null : (recipient.Name?.ToStringShort ?? recipient.LabelShort),
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
            if (recipient != null && recipient.thingIDNumber > 0)
            {
                var pairKey = DialogueClassifier.MakePairKey(pawn.thingIDNumber, recipient.thingIDNumber);
                _cadenceEvaluator.RecordDialogue(pairKey, entry.tick);
            }
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
                recipient?.thingIDNumber,
                recipient);

        // ── 查询方法 ──

        public static int GetDailyDialogueCount(int idA, int idB)
            => _activityState.GetDailyDialogueCount(
                Find.TickManager.TicksGame,
                idA,
                idB);

        public static bool IsDialoguePending(int pawnIdA, int pawnIdB)
            => _requestCoordinator.IsDialoguePending(pawnIdA, pawnIdB);

        public static List<DialogueLogEntry> GetDialogueHistory(int pawnId, int maxCount = 20)
        {
            return _logStore.HistoryFor(pawnId, maxCount).ToList();
        }

        public static DialogueLogEntry? GetPrecedingDialogueEntry(int pawnId, int? recipientId = null)
        {
            var history = _logStore.HistoryFor(pawnId, 10);
            if (recipientId.HasValue && recipientId.Value > 0)
            {
                int otherId = recipientId.Value;
                var direct = history.FirstOrDefault(e =>
                    (e.initiatorId == otherId && e.recipientId == pawnId) ||
                    (e.initiatorId == pawnId && e.recipientId == otherId));
                if (direct != null) return direct;
            }
            return history.FirstOrDefault();
        }
    }

}
