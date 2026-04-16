using System.Collections.Generic;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class DialogueSessionManager
    {
        private static readonly Dictionary<int, DialogueSession> _sessions
            = new Dictionary<int, DialogueSession>();

        public static DialogueSession GetOrCreate(Pawn pawn)
        {
            if (!_sessions.TryGetValue(pawn.thingIDNumber, out var session))
            {
                session = new DialogueSession(pawn);
                _sessions[pawn.thingIDNumber] = session;
            }
            return session;
        }

        public static void Clear(Pawn pawn) => _sessions.Remove(pawn.thingIDNumber);

        public static void ClearAll() => _sessions.Clear();
    }
}
