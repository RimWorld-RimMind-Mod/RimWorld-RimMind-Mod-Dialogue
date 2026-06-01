using System;
using RimMind.Application.Common.Models.Context;
using RimMind.Domain.Llm;
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
        /// 玩家对话请求，统一 RimMindAPI.Request.Send 路径
        /// </summary>
        public static void RequestReply(Pawn pawn, string playerMessage, Pawn? initiator,
            Action<string> onReply, Action<string> onError)
        {
            var npcId = $"NPC-{pawn.thingIDNumber}";

            var envelope = LlmRequestEnvelopeBuilder
                .ForNpc(npcId, gameStateInfo: new GameStateInfo().AddSection("dialogue_input", playerMessage))
                .ForScenarioId(ScenarioIds.Dialogue)
                .WithModId("RimMind.Dialogue")
                .WithMaxTokens(400)
                .WithTemperature(0.85f)
                .Build();

            RimMindAPI.Request.Send(envelope, result =>
            {
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    if (result.IsErr)
                    {
                        onError(result.Error.ToString());
                        return;
                    }

                    string replyText = result.Value.Content ?? string.Empty;
                    if (replyText.NullOrEmpty())
                    {
                        onError("Empty reply");
                        return;
                    }

                    NpcResponseHandler.Handle(result.Value, npcId, pawn, initiator, playerMessage, DialogueTriggerType.PlayerInput);

                    onReply(replyText);
                });
            });
        }
    }
}
