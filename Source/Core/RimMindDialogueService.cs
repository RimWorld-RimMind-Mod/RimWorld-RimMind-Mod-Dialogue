using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using RimMind.Core;
using RimMind.Core.Client;
using RimMind.Core.Prompt;
using RimMind.Dialogue.Settings;
using RimMind.Dialogue;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Core
{
    public enum DialogueTriggerType { Chitchat, Hediff, LevelUp, Thought, Auto, PlayerInput }

    public enum DialogueCategory { ColonistMonologue, ColonistDialogue, PlayerDialogue, NonColonistMonologue, NonColonistDialogue }

    public static class RimMindDialogueService
    {
        private static readonly HashSet<int> _pendingPawns = new HashSet<int>();
        private static readonly HashSet<(int, int)> _pendingDialoguePairs = new HashSet<(int, int)>();

        private static readonly List<(int tick, int pawnId, DialogueTriggerType type)> _recentTriggers
            = new List<(int, int, DialogueTriggerType)>();

        private static int _gameStartTick = -1;

        private static readonly List<DialogueLogEntry> _logEntries = new List<DialogueLogEntry>();
        private const int MaxLogEntries = 500;

        private static readonly Dictionary<(int, int), List<int>> _dailyDialogueCounts
            = new Dictionary<(int, int), List<int>>();
        private static int _lastCountDay = -1;

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
                                         bool isReply = false, bool isImmediate = false)
        {
            if (!RimMindDialogueSettings.Get().enabled) return;
            if (!RimMindAPI.IsConfigured()) return;
            if (!IsReady) return;
            if (_pendingPawns.Contains(pawn.thingIDNumber)) return;

            bool isMonologue = recipient == null;
            if (!isMonologue)
            {
                var pairKey = MakePairKey(pawn.thingIDNumber, recipient!.thingIDNumber);
                if (_pendingDialoguePairs.Contains(pairKey)) return;
            }

            if (isMonologue && IsMonologueOnCooldown(pawn, type)) return;

            if (!isMonologue && !isReply && IsDailyDialogueLimitReached(pawn.thingIDNumber, recipient!.thingIDNumber))
                return;

            _pendingPawns.Add(pawn.thingIDNumber);
            if (!isMonologue)
                _pendingDialoguePairs.Add(MakePairKey(pawn.thingIDNumber, recipient!.thingIDNumber));
            CleanExpiredTriggers();
            _recentTriggers.Add((Find.TickManager.TicksGame, pawn.thingIDNumber, type));

            var session = DialogueSessionManager.GetOrCreate(pawn);
            session.Recipient = recipient;

            string triggerLabel = GetTriggerLabel(type);
            string systemPrompt = BuildSystemPrompt(pawn, triggerLabel, recipient);
            string userPrompt = BuildUserPrompt(pawn, context, type, recipient);

            var request = new AIRequest
            {
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                MaxTokens = 200,
                Temperature = 0.8f,
                UseJsonMode = true,
                RequestId = $"RimMindDialogue_{type}_{pawn.thingIDNumber}_{Find.TickManager.TicksGame}",
                ModId = "Dialogue",
                ExpireAtTicks = Find.TickManager.TicksGame + (isMonologue
                    ? RimMindDialogueSettings.Get().monologueExpireTicks
                    : RimMindDialogueSettings.Get().dialogueExpireTicks)
            };

            Log.Message($"[RimMind-Dialogue] Trigger: {pawn.Name.ToStringShort} | Reason: {triggerLabel} | Context: {context}");

            Action<AIResponse> onResponse = response =>
            {
                _pendingPawns.Remove(pawn.thingIDNumber);
                if (!isMonologue)
                    _pendingDialoguePairs.Remove(MakePairKey(pawn.thingIDNumber, recipient!.thingIDNumber));
                if (!response.Success)
                {
                    Log.Warning($"[RimMind-Dialogue] AI request failed for {pawn.Name.ToStringShort}: {response.Error}");
                    if (!isMonologue)
                    {
                        Messages.Message(
                            "RimMind.Dialogue.UI.FloatMenu.RequestFailed".Translate(pawn.Name.ToStringShort),
                            MessageTypeDefOf.RejectInput, false);
                    }
                    return;
                }

                AutoDialogueResponse? result = null;
                try { result = JsonConvert.DeserializeObject<AutoDialogueResponse>(response.Content); }
                catch (Exception ex)
                {
                    Log.Warning($"[RimMind-Dialogue] JSON parse failed: {ex.Message}");
                    return;
                }

                string replyText = result?.reply ?? string.Empty;
                if (replyText.NullOrEmpty()) return;

                DisplayInteraction(pawn, recipient, replyText);

                string? tag = result?.thought?.tag;
                string? desc = result?.thought?.description;
                if (!tag.NullOrEmpty() && tag != "NONE")
                    ThoughtInjector.Inject(pawn, recipient, tag!, desc);

                if (result?.relation_delta.HasValue == true && recipient != null)
                {
                    float delta = result.relation_delta.Value;
                    if (Mathf.Abs(delta) >= 0.01f)
                        ThoughtInjector.InjectRelationDelta(pawn, recipient, delta);
                }

                AddLogEntry(pawn, recipient, triggerLabel, context, replyText, tag, desc);

                if (!isMonologue && Verse.ModsConfig.IsActive("mcocdaa.RimMindMemory"))
                {
                    try
                    {
                        string memContent = replyText.Length > 60 ? replyText.Substring(0, 60) + "..." : replyText;
                        MemoryBridge.AddMemory(
                            $"与{recipient!.Name.ToStringShort}的对话: {memContent}",
                            "Event", Find.TickManager.TicksGame, 0.5f, pawn.ThingID);
                        MemoryBridge.AddMemory(
                            $"与{pawn.Name.ToStringShort}的对话: {memContent}",
                            "Event", Find.TickManager.TicksGame, 0.5f, recipient!.ThingID);
                    }
                    catch (System.Exception ex)
                    {
                        Log.Warning($"[RimMind-Dialogue] Memory add failed: {ex.Message}");
                    }
                }

                if (!isMonologue)
                    RecordDailyDialogue(pawn.thingIDNumber, recipient!.thingIDNumber);

                if (RimMindDialogueSettings.Get().showThoughtNotification && tag != "NONE" && !tag.NullOrEmpty())
                {
                    Messages.Message(
                        $"[RimMind] {pawn.Name.ToStringShort}: {replyText}",
                        pawn, MessageTypeDefOf.SilentInput, historical: false);
                }

                if (!isMonologue && RimMindDialogueSettings.Get().enableDialogueReply)
                {
                    TryTriggerReply(pawn, recipient!, replyText);
                }
            };

            if (isImmediate)
                RimMindAPI.RequestImmediate(request, onResponse);
            else
                RimMindAPI.RequestAsync(request, onResponse);
        }

        private static void TryTriggerReply(Pawn originalSender, Pawn replier, string originalMessage)
        {
            if (IsDailyDialogueLimitReached(originalSender.thingIDNumber, replier.thingIDNumber)) return;
            if (_pendingPawns.Contains(replier.thingIDNumber)) return;

            string replyContext = "RimMind.Dialogue.Context.ReplyTrigger".Translate(originalSender.Name.ToStringShort, originalMessage);
            HandleTrigger(replier, replyContext, DialogueTriggerType.Chitchat, originalSender, isReply: true);
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

        private static void RecordDailyDialogue(int idA, int idB)
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

        public static int GetDailyDialogueCount(int idA, int idB)
        {
            CleanExpiredDailyCounts();
            var key = MakePairKey(idA, idB);
            return _dailyDialogueCounts.TryGetValue(key, out var ticks) ? ticks.Count : 0;
        }

        public static bool IsDialoguePending(int pawnIdA, int pawnIdB)
        {
            if (_pendingPawns.Contains(pawnIdA) || _pendingPawns.Contains(pawnIdB)) return true;
            return _pendingDialoguePairs.Contains(MakePairKey(pawnIdA, pawnIdB));
        }

        private static bool IsDailyDialogueLimitReached(int idA, int idB)
        {
            int limit = RimMindDialogueSettings.Get().maxHistoryRounds;
            return GetDailyDialogueCount(idA, idB) >= limit;
        }

        public static void AddPlayerDialogueLog(Pawn pawn, string playerMessage, string replyText,
            string? thoughtTag, string? thoughtDesc)
        {
            AddLogEntry(pawn, null, "RimMind.Dialogue.Trigger.PlayerInput".Translate(), playerMessage, replyText, thoughtTag, thoughtDesc);
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

        private static string GetTriggerLabel(DialogueTriggerType type)
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

        private static string GetRoleConstraint(Pawn pawn)
        {
            if (pawn.IsPrisoner)
                return "RimMind.Dialogue.Prompt.Role.Prisoner".Translate();
            if (pawn.IsSlave)
                return "RimMind.Dialogue.Prompt.Role.Slave".Translate();
            if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer))
                return "RimMind.Dialogue.Prompt.Role.Enemy".Translate();
            if (!pawn.IsColonist && pawn.Faction != null && !pawn.Faction.HostileTo(Faction.OfPlayer))
                return "RimMind.Dialogue.Prompt.Role.Visitor".Translate();
            return string.Empty;
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

        private static string BuildSystemPrompt(Pawn pawn, string triggerLabel, Pawn? recipient)
        {
            string? custom = RimMindDialogueSettings.Get().dialogueCustomPrompt?.Trim();
            return DialoguePromptBuilder.BuildAutoSystemPrompt(pawn, triggerLabel, recipient, custom);
        }

        private static string BuildUserPrompt(Pawn pawn, string context,
            DialogueTriggerType type, Pawn? recipient)
        {
            return DialoguePromptBuilder.BuildAutoUserPrompt(pawn, context, type, recipient);
        }

        private static void AddLogEntry(Pawn pawn, Pawn? recipient, string triggerLabel,
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
                trigger = triggerLabel,
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

        private static void CleanExpiredTriggers()
        {
            int maxCooldown = RimMindDialogueSettings.Get().monologueCooldownTicks;
            int now = Find.TickManager.TicksGame;
            _recentTriggers.RemoveAll(e => now - e.tick >= maxCooldown);
        }
    }

    public class AutoDialogueResponse
    {
        public string? reply;
        public ThoughtPart? thought;
        public float? relation_delta;
    }

    public class ThoughtPart
    {
        public string? tag;
        public string? description;
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
