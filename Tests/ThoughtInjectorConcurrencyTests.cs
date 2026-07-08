using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using RimMind.Dialogue;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    /// <summary>
    /// ThoughtInjector 并发安全测试。
    /// RegisterThoughtTag 是公开 API（鼓励外部 mod 调用），
    /// MapTagToMoodOffset / MapTagToLabel 在并发注册时被读取，
    /// 若底层字典非线程安全，会抛 InvalidOperationException（集合已修改）。
    /// </summary>
    public class ThoughtInjectorConcurrencyTests
    {
        [Fact]
        public async Task 并发注册与读取_不应抛InvalidOperationException()
        {
            const int writerCount = 4;
            const int readerCount = 4;
            const int iterations = 200;
            var exceptions = new ConcurrentBag<System.Exception>();

            var tasks = new List<Task>();

            // 写入线程：并发注册新标签
            for (int w = 0; w < writerCount; w++)
            {
                int wLocal = w;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            ThoughtInjector.RegisterThoughtTag(
                                $"CONCURRENT_{wLocal}_{i}",
                                i % 5,
                                $"RimMind.Dialogue.Thought.CONCURRENT_{wLocal}_{i}");
                        }
                    }
                    catch (System.Exception ex) { exceptions.Add(ex); }
                }));
            }

            // 读取线程：并发读取内置与新增标签
            for (int r = 0; r < readerCount; r++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        for (int i = 0; i < iterations; i++)
                        {
                            ThoughtInjector.MapTagToMoodOffset("ENCOURAGED");
                            ThoughtInjector.MapTagToLabel("HURT");
                            ThoughtInjector.MapTagToMoodOffset($"CONCURRENT_0_{i % 100}");
                            ThoughtInjector.MapTagToLabel($"CONCURRENT_1_{i % 100}");
                        }
                    }
                    catch (System.Exception ex) { exceptions.Add(ex); }
                }));
            }

            await Task.WhenAll(tasks);

            Assert.Empty(exceptions);

            // Cleanup：恢复 ENCOURAGED / HURT 的内置值，避免污染其他测试
            ThoughtInjector.RegisterThoughtTag("ENCOURAGED", 1, "RimMind.Dialogue.Thought.ENCOURAGED");
            ThoughtInjector.RegisterThoughtTag("HURT", -1, "RimMind.Dialogue.Thought.HURT");
        }
    }
}
