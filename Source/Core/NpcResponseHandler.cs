using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using RimMind.Core.Npc;
using RimMind.Dialogue.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class NpcResponseHandler
    {
        private static readonly Dictionary<string, MethodInfo> _commandCache = new Dictionary<string, MethodInfo>();
        public static void Handle(NpcChatResult result, Pawn pawn, Pawn? recipient,
            string context, DialogueTriggerType type)
        {
            if (pawn.Dead || pawn.Destroyed) return;

            if (result.Error != null)
            {
                Log.Warning($"[RimMind] NpcChat error for {pawn.LabelShort}: {result.Error}");
                if (recipient != null)
                {
                    Messages.Message(
                        "RimMind.Dialogue.UI.FloatMenu.RequestFailed".Translate(pawn.Name.ToStringShort),
                        MessageTypeDefOf.RejectInput, false);
                }
                return;
            }

            string replyText = result.Message ?? string.Empty;
            if (replyText.NullOrEmpty())
            {
                Log.Warning($"[RimMind-Dialogue] Empty reply for {pawn.LabelShort}, context: {context}");
                return;
            }

            bool isMonologue = recipient == null && type != DialogueTriggerType.PlayerInput;

            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            TryParseResponseJson(result.Message, isMonologue, ref replyText, ref thoughtTag, ref thoughtDesc, ref relationDelta);

            // 显示气泡
            RimMindDialogueService.DisplayInteraction(pawn, recipient, replyText);

            // 注入 Thought
            if (!thoughtTag.NullOrEmpty() && thoughtTag != "NONE")
                ThoughtInjector.Inject(pawn, recipient, thoughtTag!, thoughtDesc);

            // 注入 RelationDelta (only for dialogue)
            if (!isMonologue && recipient != null && relationDelta != 0)
                ThoughtInjector.InjectRelationDelta(pawn, recipient, relationDelta);

            // 日志记录
            RimMindDialogueService.AddLogEntry(pawn, recipient, type, context, replyText, thoughtTag, thoughtDesc);

            // Broadcast dialogue completion for other mods
            try
            {
                string summary = replyText.Length > 80 ? replyText.Substring(0, 80) + "..." : replyText;
                RimMind.Core.RimMindAPI.PublishPerception(pawn.thingIDNumber, "dialogue_completed", summary, 0.4f);
            }
            catch (Exception ex) { Log.Warning($"[RimMind] PublishPerception dialogue_completed failed: {ex.Message}"); }

            // 记忆记录
            if (!isMonologue && recipient != null && Verse.ModsConfig.IsActive("mcocdaa.RimMindMemory"))
            {
                try
                {
                    string memContent = replyText.Length > 60 ? replyText.Substring(0, 60) + "..." : replyText;
                    MemoryBridge.AddMemory(
                        "RimMind.Dialogue.Memory.WithRecipient".Translate(recipient!.Name.ToStringShort, memContent),
                        "Event", Find.TickManager.TicksGame, 0.5f, pawn.ThingID);
                    MemoryBridge.AddMemory(
                        "RimMind.Dialogue.Memory.WithPawn".Translate(pawn.Name.ToStringShort, memContent),
                        "Event", Find.TickManager.TicksGame, 0.5f, recipient!.ThingID);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[RimMind-Dialogue] Memory add failed: {ex.Message}");
                }
            }

            // 每日对话计数
            if (!isMonologue && recipient != null)
                RimMindDialogueService.RecordDailyDialogue(pawn.thingIDNumber, recipient.thingIDNumber);

            // Thought 通知
            if (RimMindDialogueSettings.Get().showThoughtNotification && thoughtTag != "NONE" && !thoughtTag.NullOrEmpty())
            {
                Messages.Message(
                    $"[RimMind] {pawn.Name.ToStringShort}: {replyText}",
                    pawn, MessageTypeDefOf.SilentInput, historical: false);
            }

            // 尝试触发回复（仅自动对话）
            if (!isMonologue && type != DialogueTriggerType.PlayerInput
                && RimMindDialogueSettings.Get().enableDialogueReply)
            {
                RimMindDialogueService.TryTriggerReply(pawn, recipient!, replyText);
            }

            RimMindDialogueService.RaiseOnDialogueCompleted(pawn, recipient, replyText, thoughtTag);
        }

        private static void TryParseResponseJson(string? rawResponse, bool isMonologue,
            ref string replyText, ref string? thoughtTag, ref string? thoughtDesc, ref int relationDelta)
        {
            if (string.IsNullOrEmpty(rawResponse) || !rawResponse.TrimStart().StartsWith("{")) return;
            try
            {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(rawResponse);
                if (obj == null) return;

                if (obj.TryGetValue("reply", out var replyObj) && replyObj is string replyStr && !string.IsNullOrEmpty(replyStr))
                    replyText = replyStr;

                if (obj.TryGetValue("thought", out var thoughtObj) && thoughtObj is Newtonsoft.Json.Linq.JObject thoughtJObj)
                {
                    thoughtTag = thoughtJObj.Value<string>("tag");
                    thoughtDesc = thoughtJObj.Value<string>("description");
                }

                if (!isMonologue && obj.TryGetValue("relation_delta", out var relObj))
                {
                    if (relObj is long relLong) relationDelta = (int)relLong;
                    else if (relObj is int relInt) relationDelta = relInt;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimMind] TryParseResponseJson failed: {ex.Message}");
            }
        }

        private static void ExecuteCommand(NpcCommandResult cmd, Pawn pawn, Pawn? recipient)
        {
            if (!Verse.ModsConfig.IsActive("mcocdaa.RimMindActions")) return;

            try
            {
                if (!_commandCache.TryGetValue(cmd.Name, out var method))
                {
                    var type = System.Type.GetType("RimMind.Actions.RimMindActionsAPI, RimMindActions");
                    if (type == null)
                    {
                        _commandCache[cmd.Name] = null!;
                        return;
                    }

                    method = type.GetMethod("Execute",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string), typeof(Pawn), typeof(Pawn), typeof(string) },
                        null);
                    _commandCache[cmd.Name] = method;
                }

                method?.Invoke(null, new object?[] { cmd.Name, pawn, recipient, cmd.Arguments });
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimMind] Failed to execute command {cmd.Name}: {ex.Message}");
            }
        }
    }
}
