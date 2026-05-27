using System;
using System.Linq;
using RimMind.Dialogue.Core;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    /// <summary>
    /// DialogueClassifier 全面的分类和配对键测试
    /// </summary>
    public class DialogueClassifierComprehensiveTests
    {
        [Fact]
        public void MakePairKey_零值ID_返回有序对()
        {
            var key = DialogueClassifier.MakePairKey(0, 5);
            Assert.Equal((0, 5), key);
        }

        [Fact]
        public void MakePairKey_超大ID_返回有序对()
        {
            var key = DialogueClassifier.MakePairKey(int.MaxValue, 1);
            Assert.Equal((1, int.MaxValue), key);
        }

        [Fact]
        public void MakePairKey_两个负数ID_返回有序对()
        {
            var key = DialogueClassifier.MakePairKey(-5, -3);
            Assert.Equal((-5, -3), key);
        }

        [Fact]
        public void MakePairKey_正负混合ID_返回有序对()
        {
            var key = DialogueClassifier.MakePairKey(-10, 10);
            Assert.Equal((-10, 10), key);
        }

        [Theory]
        [InlineData(DialogueTriggerType.Chitchat)]
        [InlineData(DialogueTriggerType.Hediff)]
        [InlineData(DialogueTriggerType.LevelUp)]
        [InlineData(DialogueTriggerType.Thought)]
        [InlineData(DialogueTriggerType.Auto)]
        public void Classify_非PlayerInput触发类型_接收者为null_殖民者返回独白(DialogueTriggerType triggerType)
        {
            var result = DialogueClassifier.Classify(true, null, triggerType);
            Assert.Equal(DialogueCategory.ColonistMonologue, result);
        }

        [Theory]
        [InlineData(DialogueTriggerType.Chitchat)]
        [InlineData(DialogueTriggerType.Hediff)]
        [InlineData(DialogueTriggerType.LevelUp)]
        [InlineData(DialogueTriggerType.Thought)]
        [InlineData(DialogueTriggerType.Auto)]
        public void Classify_非PlayerInput触发类型_接收者为null_非殖民者返回独白(DialogueTriggerType triggerType)
        {
            var result = DialogueClassifier.Classify(false, null, triggerType);
            Assert.Equal(DialogueCategory.NonColonistMonologue, result);
        }

        [Fact]
        public void Classify_双方都不是殖民者_返回NonColonistDialogue()
        {
            var result = DialogueClassifier.Classify(false, false, DialogueTriggerType.Chitchat);
            Assert.Equal(DialogueCategory.NonColonistDialogue, result);
        }

        [Fact]
        public void Classify_发起方殖民者接收方非殖民者_返回NonColonistDialogue()
        {
            var result = DialogueClassifier.Classify(true, false, DialogueTriggerType.Auto);
            Assert.Equal(DialogueCategory.NonColonistDialogue, result);
        }

        [Fact]
        public void Classify_PlayerInput_双方非殖民者_仍为PlayerDialogue()
        {
            var result = DialogueClassifier.Classify(false, false, DialogueTriggerType.PlayerInput);
            Assert.Equal(DialogueCategory.PlayerDialogue, result);
        }

        [Fact]
        public void DialogueTriggerType_枚举值完整()
        {
            // 确保所有预期的枚举值都存在
            var expectedValues = new[] { "Chitchat", "Hediff", "LevelUp", "Thought", "Auto", "PlayerInput" };
            var actualValues = Enum.GetNames(typeof(DialogueTriggerType));
            Assert.Equal(expectedValues.Length, actualValues.Length);
            foreach (var expected in expectedValues)
            {
                Assert.Contains(expected, actualValues);
            }
        }

        [Fact]
        public void DialogueCategory_枚举值完整()
        {
            // 确保所有预期的分类枚举值都存在
            var expectedValues = new[] { "ColonistMonologue", "ColonistDialogue", "PlayerDialogue", "NonColonistMonologue", "NonColonistDialogue" };
            var actualValues = Enum.GetNames(typeof(DialogueCategory));
            Assert.Equal(expectedValues.Length, actualValues.Length);
            foreach (var expected in expectedValues)
            {
                Assert.Contains(expected, actualValues);
            }
        }

        [Fact]
        public void MakePairKey_交换律_多次调用结果一致()
        {
            // 验证交换律的稳定性
            for (int i = 0; i < 10; i++)
            {
                var key1 = DialogueClassifier.MakePairKey(42, 17);
                var key2 = DialogueClassifier.MakePairKey(17, 42);
                Assert.Equal(key1, key2);
            }
        }
    }
}
