using System.Collections.Generic;

namespace Verse
{
    public struct TaggedString
    {
        public string Value;
        public static implicit operator string(TaggedString ts) => ts.Value;
        public static implicit operator TaggedString(string s) => new TaggedString { Value = s };
        public override string ToString() => Value ?? "";
    }

    public interface IExposable
    {
        void ExposeData();
    }

    public static class Log
    {
        public static void Warning(string msg) { }
        public static void Message(string msg) { }
        public static void Error(string msg) { }
    }

    public static class Extensions
    {
        public static bool NullOrEmpty(this string? s) => string.IsNullOrEmpty(s);

        // RimWorld 字符串首字母大写扩展方法存根
        public static string CapitalizeFirst(this string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        // RimWorld 翻译扩展方法存根，测试中直接返回原字符串
        public static TaggedString Translate(this string s) => new TaggedString { Value = s };

        // RimWorld 带参数的翻译扩展方法存根，测试中简单拼接
        public static TaggedString Translate(this string s, params object[] args) =>
            new TaggedString { Value = string.Format(s, args) };
    }

    // Def 基类存根
    public class Def : IExposable
    {
        public string defName = "";
        public virtual void ExposeData() { }
    }

    // DefDatabase<T> 存根，仅提供编译所需的最小接口
    public static class DefDatabase<T>
    {
        public static T? GetNamedSilentFail(string defName) => default;
    }

    // Scribe_Values 存根，序列化用，测试中不执行实际操作
    public static class Scribe_Values
    {
        public static void Look<T>(ref T value, string label, T defaultValue = default!) { }
    }
}
