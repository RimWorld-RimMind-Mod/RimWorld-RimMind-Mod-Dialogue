using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RimMind.Core;
using RimMind.Core.Context;
using RimMind.Core.Npc;
using RimMind.Dialogue.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Core
{
    public enum DialogueTriggerType { Chitchat, Hediff, LevelUp, Thought, Auto, PlayerInput }

    public enum DialogueCategory { ColonistMonologue, ColonistDialogue, PlayerDialogue, NonColonistMonologue, NonColonistDialogue }

    public static class RimMindDialogueService
    {
        private static readonly ConcurrentDictionary<int, byte> _pendingPawns = new ConcurrentDictionary<int, byte>();
        private static readonly ConcurrentDictionary<(int, int), byte> _pendingDialoguePairs = new ConcurrentDictionary<(int, int), byte>();

        private static readonly List<(int tick, int pawnId, DialogueTriggerType type)> _recentTriggers
            = new List<(int, int, DialogueTriggerType)>();

        private static int _gameStartTick = -1;

        private static readonly List<DialogueLogEntry> _logEntries = new List<DialogueLogEntry>();
        private const int MaxLogEntries = 500;

        private static readonly Dictionary<(int, int), List<int>> _dailyDialogueCounts
            = new Dictionary<(int, int), List<int>>();
        private static int _lastCountDay = -1;

        // 当前活跃对话对象映射（替代 DialogueSession.Recipient）
        private static readonly Dictionary<int, int> _activeRecipients = new Dictionary<int, int>();

        public static event Action? OnLogUpdated;

        public static bool IsReady
        {
            get
            {
                if (_gameStartTick < 0) _gameStartTick = Find.TickManager.TicksGame;
                var settings = RimMindDialogueSettings.Get();
                if (!settings.startDelayEnabled) return true;
                return Find.TickManager.TicksGame - _gameStartTick >= settings.startDelayTicks;
            }
        }

        public static IReadOnlyList<DialogueLogEntry> LogEntries => _logEntries;

        public static void ClearLog() => _logEntries.Clear();

        public static void NotifyGameLoaded()
        {
            _gameStartTick = Find.TickManager.TicksGame;
        }

        public static void HandleTrigger(Pawn pawn, string context,
                                         DialogueTriggerType type, Pawn? recipient,
                                         bool isReply = false)
        {
            if (!RimMindDialogueSettings.Get().enabled) return;
            if (!RimMindAPI.IsConfigured()) return;
            if (!IsReady) return;
            if (_pendingPawns.ContainsKey(pawn.thingIDNumber)) return;

            if (RimMindAPI.ShouldSkipDialogue(pawn, type.ToString())) return;

            bool isMonologue = recipient == null;
            if (!isMonologue)
            {
                var pairKey = MakePairKey(pawn.thingIDNumber, recipient!.thingIDNumber);
                if (_pendingDialoguePairs.ContainsKey(pairKey)) return;
            }

            if (isMonologue && IsMonologueOnCooldown(pawn, type)) return;

            if (!isMonologue && !isReply && IsDailyDialogueLimitReached(pawn.thingIDNumber, recipient!.thingIDNumber))
                return;

            _pendingPawns.TryAdd(pawn.thingIDNumber, 0);
            if (!isMonologue)
                _pendingDialoguePairs.TryAdd(MakePairKey(pawn.thingIDNumber, recipient!.thingIDNumber), 0);
            CleanExpiredTriggers();
            _recentTriggers.Add((Find.TickManager.TicksGame, pawn.thingIDNumber, type));

            // 记录当前对话对象
            if (recipient != null)
                _activeRecipients[pawn.thingIDNumber] = recipient.thingIDNumber;
            else
                _activeRecipients.Remove(pawn.thingIDNumber);

            string triggerLabel = GetTriggerLabel(type);
            var npcId = $"NPC-{pawn.thingIDNumber}";

            Log.Message($"[RimMind-Dialogue] Trigger: {pawn.Name.ToStringShort} | Reason: {triggerLabel} | Context: {context}");

            var request = new ContextRequest
            {
                NpcId = npcId,
                Scenario = ScenarioIds.Dialogue,
                CurrentQuery = context,
                MaxTokens = 200,
                Temperature = 0.8f,
            };

            RimMindAPI.Chat(request).ContinueWith(task =>
            {
                _pendingPawns.TryRemove(pawn.thingIDNumber, out _);
                if (!isMonologue)
                    _pendingDialoguePairs.TryRemove(MakePairKey(pawn.thingIDNumber, recipient!.thingIDNumber), out _);

                if (task.IsFaulted || task.IsCanceled)
                {
                    Log.Warning($"[RimMind-Dialogue] Chat faulted for {pawn.Name.ToStringShort}: {task.Exception?.InnerException?.Message ?? "cancelled"}");
                    if (!isMonologue)
                    {
                        Messages.Message(
                            "RimMind.Dialogue.UI.FloatMenu.RequestFailed".Translate(pawn.Name.ToStringShort),
                            MessageTypeDefOf.RejectInput, false);
                    }
                    return;
                }

                var result = task.Result;
                NpcResponseHandler.Handle(result, pawn, recipient, context, type);

                // 清理已完成的对话对象映射，防止 _activeRecipients 堆积过期条目
                if (isMonologue)
                    _activeRecipients.Remove(pawn.thingIDNumber);
            });
        }

        // ── 供 NpcResponseHandler 调用的公共方法 ──

        public static void TryTriggerReply(Pawn originalSender, Pawn replier, string originalMessage)
        {
            if (IsDailyDialogueLimitReached(originalSender.thingIDNumber, replier.thingIDNumber)) return;
            if (_pendingPawns.ContainsKey(replier.thingIDNumber)) return;

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

        public static string GetTriggerLabel(DialogueTriggerType type)
        {
            return type switch
            {
                DialogueTriggerType.Chitchat => "RimMind.Dialogue.Trigger.Chitchat".Translate(),
                DialogueTriggerType.Hediff => "RimMind.Dialogue.Trigger.Hediff".Translate(),
                DialogueTriggerType.LevelUp => "RimMind.Dialogue.Trigger.LevelUp".Translate(),
                DialogueTriggerType.Thought => "RimMind.Dialogue.Trigger.Thought".Translate(),
                DialogueTriggerType.Auto => "RimMind.Dialogue.Trigger.Auto".Translate(),
                DialogueTriggerType.PlayerInput => "RimMind.Dialogue.Trigger.PlayerInput".Translate(),
                _ => type.ToString()
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
                category = GetCategory(pawn, recipient),
                trigger = triggerType.ToString(),
                context = context,
                reply = reply,
                thoughtTag = thoughtTag ?? "NONE",
                thoughtDesc = thoughtDesc ?? ""
            };

            _logEntries.Add(entry);

            if (_logEntries.Count > MaxLogEntries)
                _logEntries.RemoveRange(0, _logEntries.Count - MaxLogEntries);

            OnLogUpdated?.Invoke();
        }

        public static void AddPlayerDialogueLog(Pawn pawn, string playerMessage, string replyText,
            string? thoughtTag, string? thoughtDesc)
        {
            AddLogEntry(pawn, null, DialogueTriggerType.PlayerInput, playerMessage, replyText, thoughtTag, thoughtDesc);
        }

        public static void RecordDailyDialogue(int idA, int idB)
        {
            CleanExpiredDailyCounts();
            var key = MakePairKey(idA, idB);
            if (!_dailyDialogueCounts.TryGetValue(key, out var ticks))
            {
                ticks = new List<int>();
                _dailyDialogueCounts[key] = ticks;
            }
            ticks.Add(Find.TickManager.TicksGame);
        }

        public static Pawn? GetActiveRecipient(Pawn pawn)
        {
            if (!_activeRecipients.TryGetValue(pawn.thingIDNumber, out var recipientId))
                return null;
            return Find.WorldPawns?.AllPawnsAlive?.FirstOrDefault(p => p.thingIDNumber == recipientId)
                ?? Find.Maps.SelectMany(m => m.mapPawns?.AllPawns ?? Enumerable.Empty<Pawn>())
                    .FirstOrDefault(p => p.thingIDNumber == recipientId);
        }

        public static void SetActiveRecipient(Pawn pawn, Pawn? recipient)
        {
            if (recipient != null)
                _activeRecipients[pawn.thingIDNumber] = recipient.thingIDNumber;
            else
                _activeRecipients.Remove(pawn.thingIDNumber);
        }

        // ── 查询方法 ──

        public static int GetDailyDialogueCount(int idA, int idB)
        {
            CleanExpiredDailyCounts();
            var key = MakePairKey(idA, idB);
            return _dailyDialogueCounts.TryGetValue(key, out var ticks) ? ticks.Count : 0;
        }

        public static bool IsDialoguePending(int pawnIdA, int pawnIdB)
        {
            if (_pendingPawns.ContainsKey(pawnIdA) || _pendingPawns.ContainsKey(pawnIdB)) return true;
            return _pendingDialoguePairs.ContainsKey(MakePairKey(pawnIdA, pawnIdB));
        }

        public static DialogueCategory GetCategory(Pawn initiator, Pawn? recipient)
        {
            if (recipient == null)
            {
                return initiator.IsColonist ? DialogueCategory.ColonistMonologue : DialogueCategory.NonColonistMonologue;
            }

            bool initiatorColonist = initiator.IsColonist;
            bool recipientColonist = recipient.IsColonist;

            if (!initiatorColonist || !recipientColonist)
                return DialogueCategory.NonColonistDialogue;

            return DialogueCategory.ColonistDialogue;
        }

        public static List<DialogueLogEntry> GetDialogueHistory(int pawnIdA, int pawnIdB, int maxRounds)
        {
            var pairKey = MakePairKey(pawnIdA, pawnIdB);
            var entries = _logEntries
                .Where(e => !e.IsMonologue && MakePairKey(e.initiatorId, e.recipientId) == pairKey)
                .ToList();

            if (maxRounds < 0) return entries;

            int take = Math.Min(maxRounds, entries.Count);
            return entries.TakeLast(take).ToList();
        }

        // ── 内部方法 ──

        private static bool IsDailyDialogueLimitReached(int idA, int idB)
        {
            int limit = RimMindDialogueSettings.Get().maxDailyDialogueRounds;
            return GetDailyDialogueCount(idA, idB) >= limit;
        }

        private static bool IsMonologueOnCooldown(Pawn pawn, DialogueTriggerType type)
        {
            int cooldownTicks = RimMindDialogueSettings.Get().monologueCooldownTicks;
            int now = Find.TickManager.TicksGame;
            foreach (var entry in _recentTriggers)
            {
                if (entry.pawnId == pawn.thingIDNumber
                    && entry.type == type
                    && now - entry.tick < cooldownTicks)
                    return true;
            }
            return false;
        }

        private static int CurrentGameDay()
        {
            return (int)(Find.TickManager.TicksGame / 2500f / 24f);
        }

        private static void CleanExpiredDailyCounts()
        {
            int today = CurrentGameDay();
            if (today != _lastCountDay)
            {
                _dailyDialogueCounts.Clear();
                _lastCountDay = today;
            }
        }

        private static (int, int) MakePairKey(int idA, int idB)
        {
            return idA < idB ? (idA, idB) : (idB, idA);
        }

        private static void CleanExpiredTriggers()
        {
            int maxCooldown = RimMindDialogueSettings.Get().monologueCooldownTicks;
            int now = Find.TickManager.TicksGame;
            _recentTriggers.RemoveAll(e => now - e.tick >= maxCooldown);
        }
    }

    public class DialogueLogEntry
    {
        public int tick;
        public string initiatorName = string.Empty;
        public int initiatorId;
        public bool initiatorIsColonist;
        public string? recipientName;
        public int recipientId;
        public bool recipientIsColonist;
        public DialogueCategory category;
        public string trigger = string.Empty;
        public string context = string.Empty;
        public string reply = string.Empty;
        public string thoughtTag = "NONE";
        public string thoughtDesc = string.Empty;

        public bool IsMonologue => recipientName == null;

        public string PairKey
        {
            get
            {
                if (recipientName == null) return initiatorName;
                return string.CompareOrdinal(initiatorName, recipientName) < 0
                    ? $"{initiatorName}|{recipientName}"
                    : $"{recipientName}|{initiatorName}";
            }
        }

        public string TimeStr
        {
            get
            {
                float hours = tick / 2500f;
                int days = (int)(hours / 24f);
                float remHours = hours % 24f;
                return "RimMind.Dialogue.UI.TimeFormat".Translate((days + 1).ToString(), $"{remHours:F1}");
            }
        }
    }
}
