using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RimMind.Dialogue.Settings;
using RimWorld;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class RimMindDialogueService
    {
        private static readonly DialogueActivityState _activityState =
            new DialogueActivityState();
        private static readonly DialogueRequestCoordinator _requestCoordinator =
            new DialogueRequestCoordinator(_activityState);

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

        public static void ClearLog() => _logStore.Clear();

        public static void NotifyGameLoaded()
        {
            _requestCoordinator.Reset();
            _activityState.Reset(Find.TickManager.TicksGame);
            _logStore.Clear();
        }

        public static void HandleTrigger(
            Pawn pawn,
            string context,
            DialogueTriggerType type,
            Pawn? recipient,
            bool isReply = false)
            => _requestCoordinator.HandleTrigger(
                pawn,
                context,
                type,
                recipient,
                isReply);

        // ── 供 NpcResponseHandler 调用的公共方法 ──

        public static void TryTriggerReply(
            Pawn originalSender,
            Pawn replier,
            string originalMessage)
            => _requestCoordinator.TryTriggerReply(
                originalSender,
                replier,
                originalMessage);

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
            => _requestCoordinator.IsDialoguePending(pawnIdA, pawnIdB);

        public static List<DialogueLogEntry> GetDialogueHistory(int pawnId, int maxCount = 20)
        {
            return _logStore.HistoryFor(pawnId, maxCount).ToList();
        }

    }

}
