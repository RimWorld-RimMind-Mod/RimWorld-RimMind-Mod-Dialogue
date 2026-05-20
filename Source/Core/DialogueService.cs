using System;
using RimMind.Application.Common.Interfaces.Npc;
using RimMind.Application.Common.Models.Context;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation;
using RimMind.Application.Common.Interfaces.Context;
using RimMind.Dialogue.Settings;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class DialogueService
    {
        /// <summary>
        /// 玩家对话请求，统一RimMindAPI.Chat 路径
        /// </summary>
        public static void RequestReply(Pawn pawn, string playerMessage, Pawn? initiator,
            Action<string> onReply, Action<string> onError)
        {
            var npcId = $"NPC-{pawn.thingIDNumber}";

            var request = new ContextRequest
            {
                NpcId = npcId,
                Scenario = RimMindAPI.Context.ScenarioDialogue,
                CurrentQuery = playerMessage,
                MaxTokens = 400,
                Temperature = 0.85f,
                SpeakerName = initiator?.Name?.ToStringShort ?? "RimMind.Dialogue.Speaker.Player".Translate(),
            };

            RimMindAPI.Chat(request).ContinueWith(task =>
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        onError(task.Exception?.InnerException?.Message ?? "Chat cancelled");
                        return;
                    }

                    var result = task.Result;
                    if (result.IsErr)
                    {
                        onError(result.Error.ToString());
                        return;
                    }

                    string replyText = result.Value.Message ?? string.Empty;
                    if (replyText.NullOrEmpty())
                    {
                        onError("Empty reply");
                        return;
                    }

                    NpcResponseHandler.Handle(result, pawn, initiator, playerMessage, DialogueTriggerType.PlayerInput);

                    onReply(replyText);
                });
            });
        }
    }
}
