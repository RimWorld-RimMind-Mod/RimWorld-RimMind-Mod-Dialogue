using System;

namespace UnityEngine
{
    // Mathf 存根，提供 ThoughtInjector 所需的数学方法
    public static class Mathf
    {
        public static float Abs(float f) => Math.Abs(f);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Clamp(float value, float min, float max) =>
            value < min ? min : (value > max ? max : value);
        public static float Clamp01(float value) =>
            value < 0f ? 0f : (value > 1f ? 1f : value);
        public static int RoundToInt(float f) => (int)Math.Round(f);
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < 0.00001f;
    }

    public static class Random
    {
        private static readonly System.Random _rnd = new();
        public static float value => (float)_rnd.NextDouble();
    }
}
