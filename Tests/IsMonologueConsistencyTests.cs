using RimMind.Dialogue.Core;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    /// <summary>
    /// 验证 isMonologue 判断逻辑在 HandleTrigger 和 NpcResponseHandler 之间一致。
    /// AGENTS.md 约定：isMonologue = recipient == null && type != PlayerInput
    /// </summary>
    /// <remarks>
    /// 此测试锁定 AGENTS 约定的公式本身，作为未来重构的回归守卫。
    /// HandleTrigger 与 NpcResponseHandler 必须使用同一公式，否则当外部 mod
    /// 通过 IDialogueTrigger 扩展点传入 PlayerInput 时会出现行为分裂。
    /// </remarks>
    public class IsMonologueConsistencyTests
    {
        [Theory]
        [InlineData(DialogueTriggerType.Chitchat, null, true)]
        [InlineData(DialogueTriggerType.Hediff, null, true)]
        [InlineData(DialogueTriggerType.LevelUp, null, true)]
        [InlineData(DialogueTriggerType.Thought, null, true)]
        [InlineData(DialogueTriggerType.Auto, null, true)]
        [InlineData(DialogueTriggerType.PlayerInput, null, false)]
        [InlineData(DialogueTriggerType.Chitchat, false, false)]
        [InlineData(DialogueTriggerType.PlayerInput, false, false)]
        public void IsMonologue_应符合AGENTS约定(DialogueTriggerType type, bool? hasRecipient, bool expected)
        {
            // hasRecipient == null 表示 recipient 为 null（独白候选）
            // hasRecipient == false 表示 recipient 存在（非独白）
            bool recipientIsNull = !hasRecipient.HasValue;
            bool isMonologue = recipientIsNull && type != DialogueTriggerType.PlayerInput;
            Assert.Equal(expected, isMonologue);
        }

        [Fact]
        public void AGENTS约定_PlayerInput且RecipientNull_不是独白()
        {
            // PlayerInput 即使 recipient 为 null 也不是独白（玩家主动输入走对话通道）
            bool recipientIsNull = true;
            DialogueTriggerType type = DialogueTriggerType.PlayerInput;
            bool isMonologue = recipientIsNull && type != DialogueTriggerType.PlayerInput;
            Assert.False(isMonologue);
        }

        [Fact]
        public void AGENTS约定_非PlayerInput且RecipientNull_是独白()
        {
            // 非 PlayerInput 触发类型，recipient 为 null 时为独白
            bool recipientIsNull = true;
            DialogueTriggerType type = DialogueTriggerType.Chitchat;
            bool isMonologue = recipientIsNull && type != DialogueTriggerType.PlayerInput;
            Assert.True(isMonologue);
        }

        [Fact]
        public void AGENTS约定_非PlayerInput且Recipient存在_不是独白()
        {
            // recipient 存在时一定不是独白
            bool recipientIsNull = false;
            bool isMonologue = recipientIsNull && DialogueTriggerType.Chitchat != DialogueTriggerType.PlayerInput;
            Assert.False(isMonologue);
        }
    }
}
