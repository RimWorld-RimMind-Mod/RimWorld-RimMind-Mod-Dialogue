using RimMind.Application.Common.Interfaces.Extension;

namespace RimMind.Dialogue
{
    internal sealed class DialogueSkipCheck : ISkipCheck
    {
        public string Id => "dialogue.skip";
        public string OwnerModId => "RimMindDialogue";
        public SkipCheckKind Kind => SkipCheckKind.Dialogue;
        // Dialogue 模组自身不参与 SkipCheck 互斥决策。
        // 此实现仅为满足 IExtension 注册要求，让 RimMindAPI.Extensions<ISkipCheck>()
        // 能枚举到本 mod 的存在。实际 skip 逻辑由 RimMindAPI.ShouldSkipDialogue 统一处理。
        public bool ShouldSkip(in SkipCheckArgs args) => false;
    }
}
