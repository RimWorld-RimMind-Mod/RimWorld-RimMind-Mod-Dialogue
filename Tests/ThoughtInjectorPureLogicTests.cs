using RimMind.Dialogue;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    /// <summary>
    /// ThoughtInjector 纯逻辑方法测试（MapTagToMoodOffset / MapTagToLabel / RegisterThoughtTag）
    /// 不依赖 RimWorld 运行时，仅测试字典映射逻辑
    /// </summary>
    public class ThoughtInjectorPureLogicTests
    {
        // ── MapTagToMoodOffset 测试 ──

        [Theory]
        [InlineData("ENCOURAGED", 1)]
        [InlineData("HURT", -1)]
        [InlineData("VALUED", 2)]
        [InlineData("CONNECTED", 2)]
        [InlineData("STRESSED", -2)]
        [InlineData("IRRITATED", -1)]
        public void MapTagToMoodOffset_内置标签_返回正确偏移值(string tag, int expectedOffset)
        {
            Assert.Equal(expectedOffset, ThoughtInjector.MapTagToMoodOffset(tag));
        }

        [Fact]
        public void MapTagToMoodOffset_未知标签_返回0()
        {
            Assert.Equal(0, ThoughtInjector.MapTagToMoodOffset("UNKNOWN_TAG"));
        }

        [Theory]
        [InlineData("encouraged", 1)]
        [InlineData("Hurt", -1)]
        [InlineData("valued", 2)]
        [InlineData("Connected", 2)]
        [InlineData("stressed", -2)]
        [InlineData("Irritated", -1)]
        public void MapTagToMoodOffset_大小写不敏感_返回正确偏移值(string tag, int expectedOffset)
        {
            Assert.Equal(expectedOffset, ThoughtInjector.MapTagToMoodOffset(tag));
        }

        [Fact]
        public void MapTagToMoodOffset_空字符串_返回0()
        {
            Assert.Equal(0, ThoughtInjector.MapTagToMoodOffset(""));
        }

        // ── MapTagToLabel 测试 ──

        [Theory]
        [InlineData("ENCOURAGED", "RimMind.Dialogue.Thought.ENCOURAGED")]
        [InlineData("HURT", "RimMind.Dialogue.Thought.HURT")]
        [InlineData("VALUED", "RimMind.Dialogue.Thought.VALUED")]
        [InlineData("CONNECTED", "RimMind.Dialogue.Thought.CONNECTED")]
        [InlineData("STRESSED", "RimMind.Dialogue.Thought.STRESSED")]
        [InlineData("IRRITATED", "RimMind.Dialogue.Thought.IRRITATED")]
        public void MapTagToLabel_内置标签_返回正确翻译键(string tag, string expectedLabelKey)
        {
            // Translate 存根直接返回原字符串，所以结果等于翻译键本身
            string result = ThoughtInjector.MapTagToLabel(tag);
            Assert.Equal(expectedLabelKey, result);
        }

        [Fact]
        public void MapTagToLabel_未知标签_返回标签本身()
        {
            string result = ThoughtInjector.MapTagToLabel("CUSTOM_TAG");
            Assert.Equal("CUSTOM_TAG", result);
        }

        [Theory]
        [InlineData("encouraged", "RimMind.Dialogue.Thought.ENCOURAGED")]
        [InlineData("Hurt", "RimMind.Dialogue.Thought.HURT")]
        [InlineData("valued", "RimMind.Dialogue.Thought.VALUED")]
        public void MapTagToLabel_大小写不敏感_返回正确翻译键(string tag, string expectedLabelKey)
        {
            string result = ThoughtInjector.MapTagToLabel(tag);
            Assert.Equal(expectedLabelKey, result);
        }

        // ── RegisterThoughtTag 测试 ──

        [Fact]
        public void RegisterThoughtTag_注册新标签_MapTagToMoodOffset返回注册值()
        {
            // 注册自定义标签
            ThoughtInjector.RegisterThoughtTag("TEST_HOPEFUL", 3, "RimMind.Dialogue.Thought.TEST_HOPEFUL");

            Assert.Equal(3, ThoughtInjector.MapTagToMoodOffset("TEST_HOPEFUL"));
        }

        [Fact]
        public void RegisterThoughtTag_注册新标签_MapTagToLabel返回注册翻译键()
        {
            ThoughtInjector.RegisterThoughtTag("TEST_HOPEFUL_LABEL", 1, "RimMind.Dialogue.Thought.TEST_HOPEFUL_LABEL");

            string result = ThoughtInjector.MapTagToLabel("TEST_HOPEFUL_LABEL");
            Assert.Equal("RimMind.Dialogue.Thought.TEST_HOPEFUL_LABEL", result);
        }

        [Fact]
        public void RegisterThoughtTag_覆盖已有标签_新值生效()
        {
            // ENCOURAGED 原始偏移为 1
            Assert.Equal(1, ThoughtInjector.MapTagToMoodOffset("ENCOURAGED"));

            // 覆盖注册
            ThoughtInjector.RegisterThoughtTag("ENCOURAGED", 5, "RimMind.Dialogue.Thought.ENCOURAGED_OVERRIDE");

            Assert.Equal(5, ThoughtInjector.MapTagToMoodOffset("ENCOURAGED"));
            Assert.Equal("RimMind.Dialogue.Thought.ENCOURAGED_OVERRIDE", ThoughtInjector.MapTagToLabel("ENCOURAGED"));

            // 恢复原始值，避免影响其他测试
            ThoughtInjector.RegisterThoughtTag("ENCOURAGED", 1, "RimMind.Dialogue.Thought.ENCOURAGED");
        }

        [Fact]
        public void RegisterThoughtTag_大小写不敏感_键统一为大写()
        {
            // 使用小写注册
            ThoughtInjector.RegisterThoughtTag("test_mixed_case", 4, "RimMind.Dialogue.Thought.TEST_MIXED_CASE");

            // 用大写查询
            Assert.Equal(4, ThoughtInjector.MapTagToMoodOffset("TEST_MIXED_CASE"));
            // 用混合大小写查询
            Assert.Equal(4, ThoughtInjector.MapTagToMoodOffset("test_Mixed_Case"));
        }

        [Fact]
        public void RegisterThoughtTag_负偏移值_正确注册()
        {
            ThoughtInjector.RegisterThoughtTag("TEST_DESPAIR", -5, "RimMind.Dialogue.Thought.TEST_DESPAIR");

            Assert.Equal(-5, ThoughtInjector.MapTagToMoodOffset("TEST_DESPAIR"));
        }

        [Fact]
        public void RegisterThoughtTag_零偏移值_正确注册()
        {
            ThoughtInjector.RegisterThoughtTag("TEST_NEUTRAL", 0, "RimMind.Dialogue.Thought.TEST_NEUTRAL");

            Assert.Equal(0, ThoughtInjector.MapTagToMoodOffset("TEST_NEUTRAL"));
        }
    }
}
