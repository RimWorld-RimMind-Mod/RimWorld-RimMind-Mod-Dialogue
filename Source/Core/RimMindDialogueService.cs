using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        private static ConcurrentBag<DialogueLogEntry> _logEntries = new ConcurrentBag<DialogueLogEntry>();
        private const int MaxLogEntries = 500;

        private static List<DialogueLogEntry>? _cachedLogEntries;
        private static bool _logDirty = true;

        private static readonly ConcurrentDictionary<(int, int), List<int>> _dailyDialogueCounts
            = new ConcurrentDictionary<(int, int), List<int>>();
        private static int _lastCountDay = -1;

        // 当前活跃对话对象映射（替代 DialogueSession.Recipient）
        private static readonly ConcurrentDictionary<int, int> _activeRecipients = new ConcurrentDictionary<int, int>();

        private static Dictionary<int, Pawn> _pawnCache = new Dictionary<int, Pawn>();
        private static int _pawnCacheTick = -1;

        internal static readonly Dictionary<string, string> RegisteredTriggerLabels = new Dictionary<string, string>();

        public static event Action? OnLogUpdated;

        public static event Action<Pawn, Pawn?, string, string?>? OnDialogueCompleted;

        public static void RaiseOnDialogueCompleted(Pawn pawn, Pawn? recipient, string replyText, string? thoughtTag)
        {
            OnDialogueCompleted?.Invoke(pawn, recipient, replyText, thoughtTag);
        }

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

        public static IReadOnlyList<DialogueLogEntry> LogEntries
        {
            get
            {
                if (!_logDirty && _cachedLogEntries != null) return _cachedLogEntries;
                _cachedLogEntries = _logEntries.ToList();
                _logDirty = false;
                return _cachedLogEntries;
            }
        }

        public static void ClearLog()
        {
            Interlocked.Exchange(ref _logEntries, new ConcurrentBag<DialogueLogEntry>());
            _logDirty = true;
        }

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

            if (recipient != null)
                _activeRecipients[pawn.thingIDNumber] = recipient.thingIDNumber;
            else
                _activeRecipients.TryRemove(pawn.thingIDNumber, out _);

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
                    formattedContext += "\n" + "RimMind.Dialogue.Prompt.Context.Recipient".Translate(recipient.Name.ToStringShort) + "\n" + roleKey.Translate();
            }

            Log.Message($"[RimMind-Dialogue] Trigger: {pawn.Name.ToStringShort} | Reason: {triggerLabel} | Context: {formattedContext}");

            var request = new ContextRequest
            {
                NpcId = npcId,
                Scenario = ScenarioIds.Dialogue,
                CurrentQuery = type == DialogueTriggerType.PlayerInput
                    ? formattedContext
                    : "RimMind.Dialogue.Prompt.AutoTrigger".Translate(),
                MaxTokens = 400,
                Temperature = 0.8f,
                SpeakerName = recipient?.Name?.ToStringShort,
            };

            ContextKeyRegistry.CurrentSpeakerName = request.SpeakerName;

            RimMindAPI.Chat(request).ContinueWith(task =>
            {
                LongEventHandler.ExecuteWhenFinished(() =>
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
                    NpcResponseHandler.Handle(result, pawn, recipient, formattedContext, type);

                    _activeRecipients.TryRemove(pawn.thingIDNumber, out _);
                });
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
                category = GetCategory(pawn, recipient, triggerType),
                trigger = triggerType.ToString(),
                context = context,
                reply = reply,
                thoughtTag = thoughtTag ?? "NONE",
                thoughtDesc = thoughtDesc ?? ""
            };

            _logEntries.Add(entry);

            if (_logEntries.Count > MaxLogEntries)
            {
                var kept = _logEntries.OrderByDescending(e => e.tick).Take(MaxLogEntries).ToList();
                Interlocked.Exchange(ref _logEntries, new ConcurrentBag<DialogueLogEntry>(kept));
            }

            OnLogUpdated?.Invoke();
            _logDirty = true;
        }

        public static void RecordDailyDialogue(int idA, int idB)
        {
            CleanExpiredDailyCounts();
            var key = MakePairKey(idA, idB);
            var ticks = _dailyDialogueCounts.GetOrAdd(key, _ => new List<int>());
            ticks.Add(Find.TickManager.TicksGame);
        }

        public static Pawn? GetActiveRecipient(Pawn pawn)
        {
            if (!_activeRecipients.TryGetValue(pawn.thingIDNumber, out var recipientId))
                return null;

            int now = Find.TickManager.TicksGame;
            if (_pawnCacheTick < 0 || now - _pawnCacheTick >= 600)
            {
                _pawnCache.Clear();
                foreach (var map in Find.Maps)
                {
                    if (map.mapPawns != null)
                    {
                        foreach (var p in map.mapPawns.AllPawns)
                            _pawnCache[p.thingIDNumber] = p;
                    }
                }
                if (Find.WorldPawns?.AllPawnsAlive != null)
                {
                    foreach (var p in Find.WorldPawns.AllPawnsAlive)
                        _pawnCache[p.thingIDNumber] = p;
                }
                _pawnCacheTick = now;
            }

            return _pawnCache.TryGetValue(recipientId, out var cached) ? cached : null;
        }

        public static void SetActiveRecipient(Pawn pawn, Pawn? recipient)
        {
            if (recipient != null)
                _activeRecipients[pawn.thingIDNumber] = recipient.thingIDNumber;
            else
                _activeRecipients.TryRemove(pawn.thingIDNumber, out _);
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

        public static List<DialogueLogEntry> GetDialogueHistory(int pawnId, int maxCount = 20)
        {
            return _logEntries
                .Where(e => e.initiatorId == pawnId || e.recipientId == pawnId)
                .OrderByDescending(e => e.tick)
                .Take(maxCount)
                .ToList();
        }

        public static DialogueCategory GetCategory(Pawn initiator, Pawn? recipient, DialogueTriggerType triggerType)
        {
            if (triggerType == DialogueTriggerType.PlayerInput)
                return DialogueCategory.PlayerDialogue;

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

        public bool IsMonologue => category == DialogueCategory.ColonistMonologue || category == DialogueCategory.NonColonistMonologue;

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
