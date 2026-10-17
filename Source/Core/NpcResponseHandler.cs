using System;
using Newtonsoft.Json;
using RimMind.Application.Common.Interfaces.Npc;
using RimMind.Application.Common.Models.Memory;
using RimMind.Domain.Llm;
using RimMind.Domain.ValueObjects;
using RimMind.Dialogue.Settings;
using RimMind.Presentation.Api;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class NpcResponseHandler
    {
        public static string? Handle(LlmResponse response, string npcId, Pawn pawn, Pawn? recipient,
            string context, DialogueTriggerType type, bool isReply = false)
        {
            if (!RimMind.Presentation.Api.RimMindPawnLookup.IsActiveSpawnedColonist(pawn) || pawn.Downed) return null;
            if (recipient != null && (!RimMind.Presentation.Api.RimMindPawnLookup.IsActiveSpawnedColonist(recipient) || recipient.Downed)) return null;

            bool isMonologue = DialogueFlowPolicy.IsMonologue(type, recipient != null);

            string replyText;
            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            if (DialogueToolParser.TryParse(response.ToolCallsJson, isMonologue, out var output)
                || DialogueToolParser.TryParse(response.Content, isMonologue, out output))
            {
                replyText = output.Speech;
                thoughtTag = output.ThoughtTag;
                thoughtDesc = output.ThoughtDesc;
                relationDelta = output.RelationDelta;
            }
            else if (!string.IsNullOrWhiteSpace(response.Content))
            {
                replyText = response.Content!.Trim();
            }
            else
            {
                RimMindErrors.Warn($"[RimMind-Dialogue] No valid express_dialogue tool call found for {pawn.LabelShort}, context: {context}");
                return null;
            }

            string? precedingText = FindPrecedingDialogueText(pawn, recipient, context);
            replyText = SanitizeReply(replyText, precedingText);
            if (string.IsNullOrWhiteSpace(replyText))
            {
                RimMindErrors.Warn($"[RimMind-Dialogue] Sanitized reply is empty for {pawn.LabelShort}, context: {context}");
                return null;
            }

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
                    RimMindAPI.Memory.AddPawnMemory(
                        "RimMind.Dialogue.Memory.WithRecipient".Translate(recipient!.Name.ToStringShort, memContent),
                        MemoryKind.Event, Find.TickManager.TicksGame, 0.5f, pawn.ThingID);
                    RimMindAPI.Memory.AddPawnMemory(
                        "RimMind.Dialogue.Memory.WithPawn".Translate(pawn.Name.ToStringShort, memContent),
                        MemoryKind.Event, Find.TickManager.TicksGame, 0.5f, recipient!.ThingID);
                }
                catch (Exception ex)
                {
                    RimMindErrors.Warn($"[RimMind-Dialogue] Memory add failed: {ex.Message}");
                }
            }

            // 每日对话计数（reply 不计入每日限额——reply 是对话链的自然延续，不额外消耗每日额度）
            if (DialogueFlowPolicy.UsesDailyQuota(type, recipient != null, isReply)
                && recipient != null)
            {
                RimMindDialogueService.RecordDailyDialogue(pawn.thingIDNumber, recipient.thingIDNumber);
            }

            // Thought 通知
            if (RimMindDialogueSettings.Get().showThoughtNotification && thoughtTag != "NONE" && !thoughtTag.NullOrEmpty())
            {
                string notifText = replyText.Length > 60 ? replyText.Substring(0, 60) + "..." : replyText;
                Messages.Message(
                    $"[RimMind] {pawn.Name.ToStringShort}: {notifText}",
                    pawn, MessageTypeDefOf.SilentInput, historical: false);
            }

            // 尝试触发回复（仅自动对话）
            if (DialogueFlowPolicy.ShouldAutoReply(
                    type,
                    recipient != null,
                    isReply,
                    RimMindDialogueSettings.Get().enableDialogueReply))
            {
                RimMindDialogueService.TryTriggerReply(pawn, recipient!, replyText);
            }

            RimMindDialogueService.RaiseOnDialogueCompleted(pawn, recipient, replyText, thoughtTag);
            return replyText;
        }

        /// <summary>
        /// Sanitizes reply text by stripping mirrored parenthetical stage directions and cleaning formatting glitches.
        /// </summary>
        public static string SanitizeReply(string replyText, string? previousDialogueText = null)
        {
            return DialogueTextSanitizer.SanitizeReply(replyText, previousDialogueText);
        }

        internal static string? FindPrecedingDialogueText(Pawn pawn, Pawn? recipient, string? context)
        {
            if (pawn != null)
            {
                var entry = RimMindDialogueService.GetPrecedingDialogueEntry(
                    pawn.thingIDNumber,
                    recipient?.thingIDNumber);
                if (entry != null && !string.IsNullOrWhiteSpace(entry.reply))
                {
                    return entry.reply;
                }
            }

            if (!string.IsNullOrWhiteSpace(context))
            {
                // "\u5bf9\u4f60\u8bf4\uff1a" is the Chinese format for "{0} said to you:"
                int idx = context!.IndexOf("\u5bf9\u4f60\u8bf4\uff1a", StringComparison.Ordinal);
                if (idx >= 0)
                {
                    return context.Substring(idx + 4).Trim();
                }
                idx = context.IndexOf("said to you:", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    return context.Substring(idx + 12).Trim();
                }
            }

            return null;
        }
    }
}
