using RimMind.Dialogue.Core;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    /// <summary>
    /// DialogueLogEntry.PairKey 身份标识测试。
    /// PairKey 必须基于小人 ID（而非名字）生成，以避免重名导致的配对冲突与列归属错误。
    /// </summary>
    public class DialogueLogEntryPairKeyTests
    {
        [Fact]
        public void PairKey_双方都有ID_用ID拼接()
        {
            var entry = new DialogueLogEntry
            {
                initiatorId = 101,
                initiatorName = "Alice",
                recipientId = 202,
                recipientName = "Bob"
            };
            Assert.Equal("101|202", entry.PairKey);
        }

        [Fact]
        public void PairKey_独白_仅用initiatorId()
        {
            var entry = new DialogueLogEntry
            {
                initiatorId = 303,
                initiatorName = "Solo",
                recipientId = -1,
                recipientName = null
            };
            Assert.Equal("303", entry.PairKey);
        }

        [Fact]
        public void PairKey_重名不同ID_不冲突()
        {
            // 两个不同的对话对，小人恰好重名——PairKey 必须不同
            var entry1 = new DialogueLogEntry
            {
                initiatorId = 1,
                initiatorName = "Bob",
                recipientId = 2,
                recipientName = "Alice"
            };
            var entry2 = new DialogueLogEntry
            {
                initiatorId = 3,
                initiatorName = "Bob",
                recipientId = 4,
                recipientName = "Alice"
            };
            Assert.NotEqual(entry1.PairKey, entry2.PairKey);
        }

        [Fact]
        public void PairKey_交换律_同一对ID结果一致()
        {
            // 同一对小人，无论谁是发起方，PairKey 必须相同
            var entry1 = new DialogueLogEntry
            {
                initiatorId = 10,
                recipientId = 20
            };
            var entry2 = new DialogueLogEntry
            {
                initiatorId = 20,
                recipientId = 10
            };
            Assert.Equal(entry1.PairKey, entry2.PairKey);
        }

        [Fact]
        public void PairKey_较小ID在前_保证有序()
        {
            var entry = new DialogueLogEntry
            {
                initiatorId = 202,
                recipientId = 101
            };
            Assert.Equal("101|202", entry.PairKey);
        }

        [Fact]
        public void PairKey_独白recipientId为零时_按独白处理()
        {
            // 防御性：若 recipientId 为 0（未初始化默认值），也应视为无接收者，
            // 因为真实小人 thingIDNumber 始终为正数。
            var entry = new DialogueLogEntry
            {
                initiatorId = 505,
                initiatorName = "Lone",
                recipientId = 0,
                recipientName = null
            };
            Assert.Equal("505", entry.PairKey);
        }
    }
}
