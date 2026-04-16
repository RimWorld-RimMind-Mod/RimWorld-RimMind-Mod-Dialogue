using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimMind.Dialogue.Core
{
    public class DialogueSession
    {
        public Pawn Pawn;
        public Pawn? Recipient;
        public int MaxHistoryRounds = 6;
        public List<(string role, string content)> Messages = new List<(string, string)>();

        public DialogueSession(Pawn pawn)
        {
            Pawn = pawn;
        }

        public void AddUserMessage(string text) => Messages.Add(("user", text));
        public void AddAssistantMessage(string text) => Messages.Add(("assistant", text));

        public List<(string role, string content)> GetContextMessages()
            => Messages.TakeLast(MaxHistoryRounds * 2).ToList();
    }
}
