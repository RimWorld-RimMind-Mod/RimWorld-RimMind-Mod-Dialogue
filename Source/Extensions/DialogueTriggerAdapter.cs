using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Dialogue.Core;
using RimWorld;
using Verse;

namespace RimMind.Dialogue
{
    internal sealed class DialogueTriggerAdapter : IDialogueTrigger
    {
        public string Id => "dialogue";
        public string OwnerModId => "RimMind.Dialogue";
        // 限制：IDialogueTrigger.Trigger 接口签名无 triggerType 参数（Core 定义，有 ArchTest 保护），
        // 外部 mod 通过此扩展点触发时统一映射为 Chitchat 类型。
        // 若需区分触发类型（Hediff/LevelUp/Thought/Auto），需 Core 协同修改接口签名，
        // 参考 RimMind-Core/Tests/ArchTests/PhaseP6/P6_SubmodOrthogonalityTests.cs。
        public void Trigger(object pawn, string context, object? recipient) =>
            RimMindDialogueService.HandleTrigger((Pawn)pawn, context, DialogueTriggerType.Chitchat, recipient as Pawn);
    }
}
