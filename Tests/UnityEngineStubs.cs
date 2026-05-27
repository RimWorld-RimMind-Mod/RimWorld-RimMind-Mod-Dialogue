using System;

namespace UnityEngine
{
    // Mathf 存根，提供 ThoughtInjector 所需的数学方法
    public static class Mathf
    {
        public static float Abs(float f) => Math.Abs(f);
        public static float Clamp(float value, float min, float max) =>
            value < min ? min : (value > max ? max : value);
    }
}
