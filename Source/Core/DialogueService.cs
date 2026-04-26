using System;
using RimMind.Core;
using RimMind.Core.Context;
using RimMind.Core.Npc;
using RimMind.Dialogue.Settings;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class DialogueService
    {
        /// <summary>
        /// 玩家对话请求，统一走 RimMindAPI.Chat 路径
        /// </summary>
        public static void RequestReply(Pawn pawn, string playerMessage, Pawn? initiator,
            Action<string> onReply, Action<string> onError)
        {
            var npcId = $"NPC-{pawn.thingIDNumber}";

            var request = new ContextRequest
            {
                NpcId = npcId,
                Scenario = ScenarioIds.Dialogue,
                CurrentQuery = playerMessage,
                MaxTokens = 400,
                Temperature = 0.85f,
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
                    if (result == null || !string.IsNullOrEmpty(result.Error))
                    {
                        onError(result?.Error ?? "null result");
                        return;
                    }

                    string replyText = result.Message ?? string.Empty;
                    if (replyText.NullOrEmpty())
                    {
                        onError("Empty reply");
                        return;
                    }

                    NpcResponseHandler.Handle(result, pawn, null, playerMessage, DialogueTriggerType.PlayerInput);

                    onReply(replyText);
                });
            });
        }
    }
}
