using System.Reflection;
using Verse;

namespace RimMind.Dialogue
{
    internal static class MemoryBridge
    {
        private static MethodInfo? _addMemoryMethod;
        private static bool _resolved;

        private static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            var type = System.Type.GetType("RimMind.Memory.RimMindMemoryAPI, RimMindMemory");
            if (type != null)
            {
                _addMemoryMethod = type.GetMethod("AddMemory",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string), typeof(int), typeof(float), typeof(string) },
                    null);
            }

            if (_addMemoryMethod == null)
                Log.Warning("[RimMind-Dialogue] MemoryBridge: RimMindMemoryAPI.AddMemory not found via reflection.");
        }

        public static void AddMemory(string content, string memoryType, int tick, float importance, string? pawnId = null)
        {
            Resolve();
            if (_addMemoryMethod == null) return;
            _addMemoryMethod.Invoke(null, new object?[] { content, memoryType, tick, importance, pawnId });
        }
    }
}
