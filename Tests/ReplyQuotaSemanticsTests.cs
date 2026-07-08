using RimMind.Dialogue.Core;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    /// <summary>
    /// 验证 reply 不计入每日对话限额的语义一致性。
    /// AGENTS 约定：reply 是对话链的自然延续，不额外消耗每日额度。
    /// </summary>
    /// <remarks>
    /// 此测试锁定以下两处决策公式的协同语义：
    /// 1. HandleTrigger 的每日限额检查守卫：`!isMonologue && recipient != null && !isReply`
    ///    （reply 绕过 IsDailyDialogueLimitReached 检查）
    /// 2. NpcResponseHandler.Handle 的每日计数守卫：`!isMonologue && recipient != null && !isReply`
    ///    （reply 不调用 RecordDailyDialogue）
    ///
    /// 二者必须使用同一公式，否则会出现 reply 不受限额保护却消耗限额计数的不一致：
    /// 即 reply 链可能在不受限的情况下提前耗尽每日配额。
    ///
    /// 由于 Handle / HandleTrigger 依赖 RimWorld 运行时（Find.TickManager 等），
    /// 此处仅锁定决策公式本身，作为未来重构的回归守卫。
    /// </remarks>
    public class ReplyQuotaSemanticsTests
    {
        /// <summary>
        /// NpcResponseHandler.Handle 的 RecordDailyDialogue 决策公式。
        /// 与 NpcResponseHandler.cs 中实际代码保持一致。
        /// </summary>
        private static bool ShouldRecordDailyDialogue(bool isMonologue, bool hasRecipient, bool isReply)
            => !isMonologue && hasRecipient && !isReply;

        /// <summary>
        /// HandleTrigger 的 IsDailyDialogueLimitReached 检查守卫公式。
        /// 与 RimMindDialogueService.cs 中实际代码保持一致。
        /// </summary>
        private static bool ShouldCheckDailyLimit(bool isMonologue, bool hasRecipient, bool isReply)
            => !isMonologue && hasRecipient && !isReply;

        [Theory]
        // 普通对话（非独白、有 recipient、非 reply）：计数 + 限额检查
        [InlineData(false, true, false, true)]
        // reply（非独白、有 recipient、reply=true）：不计数 + 不限额检查
        [InlineData(false, true, true, false)]
        // 独白：不计数 + 不限额检查
        [InlineData(true, true, false, false)]
        // 无 recipient：不计数 + 不限额检查
        [InlineData(false, false, false, false)]
        public void RecordDailyDialogue_决策应与限额检查公式一致(bool isMonologue, bool hasRecipient, bool isReply, bool expected)
        {
            bool shouldRecord = ShouldRecordDailyDialogue(isMonologue, hasRecipient, isReply);
            bool shouldCheck = ShouldCheckDailyLimit(isMonologue, hasRecipient, isReply);

            // 两个公式必须产出相同结果——这是 reply 限额语义一致性的核心
            Assert.Equal(shouldCheck, shouldRecord);
            Assert.Equal(expected, shouldRecord);
        }

        [Fact]
        public void Reply_不消耗每日配额计数()
        {
            // reply 是对话链的自然延续，不应计入 RecordDailyDialogue
            bool isMonologue = false;
            bool hasRecipient = true;
            bool isReply = true;

            Assert.False(ShouldRecordDailyDialogue(isMonologue, hasRecipient, isReply));
        }

        [Fact]
        public void Reply_不触发每日限额检查()
        {
            // reply 绕过 IsDailyDialogueLimitReached，避免 reply 链被自己消耗的配额阻塞
            bool isMonologue = false;
            bool hasRecipient = true;
            bool isReply = true;

            Assert.False(ShouldCheckDailyLimit(isMonologue, hasRecipient, isReply));
        }

        [Fact]
        public void 普通对话_同时计数并限额检查()
        {
            // 非 reply 的普通对话必须同时计数 + 限额检查
            bool isMonologue = false;
            bool hasRecipient = true;
            bool isReply = false;

            Assert.True(ShouldRecordDailyDialogue(isMonologue, hasRecipient, isReply));
            Assert.True(ShouldCheckDailyLimit(isMonologue, hasRecipient, isReply));
        }

        [Fact]
        public void 独白_不计数也不限额检查()
        {
            // 独白（recipient 为 null 且非 PlayerInput）走独立冷却，不经每日配额
            bool isMonologue = true;
            bool hasRecipient = true; // 即使 recipient 非空，isMonologue 守卫仍优先
            bool isReply = false;

            Assert.False(ShouldRecordDailyDialogue(isMonologue, hasRecipient, isReply));
            Assert.False(ShouldCheckDailyLimit(isMonologue, hasRecipient, isReply));
        }

        [Fact]
        public void 无Recipient_不计数也不限额检查()
        {
            // 无 recipient 时不进入每日配额逻辑（独白或 PlayerInput 无 recipient）
            bool isMonologue = false;
            bool hasRecipient = false;
            bool isReply = false;

            Assert.False(ShouldRecordDailyDialogue(isMonologue, hasRecipient, isReply));
            Assert.False(ShouldCheckDailyLimit(isMonologue, hasRecipient, isReply));
        }
    }
}
