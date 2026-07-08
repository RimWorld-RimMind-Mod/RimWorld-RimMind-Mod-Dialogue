using Verse;

namespace RimMind.Dialogue.Core
{
    public class DialogueLogEntry
    {
        public int tick;
        public string initiatorName = string.Empty;
        public int initiatorId;
        public bool initiatorIsColonist;
        public string? recipientName;
        public int recipientId;
        public bool recipientIsColonist;
        public DialogueCategory category;
        public string trigger = string.Empty;
        public string context = string.Empty;
        public string reply = string.Empty;
        public string thoughtTag = "NONE";
        public string thoughtDesc = string.Empty;

        public bool IsMonologue => category == DialogueCategory.ColonistMonologue || category == DialogueCategory.NonColonistMonologue;

        public string PairKey
        {
            get
            {
                if (recipientName == null) return initiatorName;
                return string.CompareOrdinal(initiatorName, recipientName) < 0
                    ? $"{initiatorName}|{recipientName}"
                    : $"{recipientName}|{initiatorName}";
            }
        }

        public string TimeStr
        {
            get
            {
                float hours = tick / 2500f;
                int days = (int)(hours / 24f);
                float remHours = hours % 24f;
                return "RimMind.Dialogue.UI.TimeFormat".Translate((days + 1).ToString(), $"{remHours:F1}");
            }
        }
    }
}
