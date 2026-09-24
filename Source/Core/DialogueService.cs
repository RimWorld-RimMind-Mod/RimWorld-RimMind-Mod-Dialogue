using System;
using System.Threading;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class DialogueService
    {
        /// <summary>
        /// 玩家对话兼容入口；与自动触发共享唯一请求协调器。调用与取消均在主线程。
        /// </summary>
        public static void RequestReply(Pawn pawn, string playerMessage, Pawn? initiator,
            Action<string> onReply, Action<string> onError,
            CancellationToken cancellationToken = default)
            => RimMindDialogueService.RequestCoordinator.HandleTrigger(
                pawn, playerMessage, DialogueTriggerType.PlayerInput, initiator,
                onReply: onReply, onError: onError, cancellationToken: cancellationToken);
    }
}
