using System;
using Newtonsoft.Json;
using RimMind.Application.Common.Interfaces.Npc;
using RimMind.Domain.Llm;
using RimMind.Domain.ValueObjects;
using RimMind.Dialogue.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class NpcResponseHandler
    {
        public static void Handle(LlmResponse response, string npcId, Pawn pawn, Pawn? recipient,
            string context, DialogueTriggerType type, bool isReply = false)
        {
            if (pawn.Dead || pawn.Destroyed) return;

            string replyText = response.Content ?? string.Empty;
            if (replyText.NullOrEmpty())
            {
                RimMindErrors.Warn($"[RimMind-Dialogue] Empty reply for {pawn.LabelShort}, context: {context}");
                return;
            }

            bool isMonologue = recipient == null && type != DialogueTriggerType.PlayerInput;

            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(replyText, isMonologue, ref replyText, ref thoughtTag, ref thoughtDesc, ref relationDelta);

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
                RimMind.Presentation.Api.RimMindAPI.PublishPerception(pawn.thingIDNumber, "dialogue_completed", summary, 0.4f);
            }
            catch (Exception ex) { RimMindErrors.Warn($"[RimMind] PublishPerception dialogue_completed failed: {ex.Message}"); }

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
                    RimMindErrors.Warn($"[RimMind-Dialogue] Memory add failed: {ex.Message}");
                }
            }

            // 每日对话计数（reply 不计入每日限额——reply 是对话链的自然延续，不额外消耗每日额度）
            if (!isMonologue && recipient != null && !isReply)
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
    }
}
