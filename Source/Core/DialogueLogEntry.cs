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
                // 基于小人 ID 而非名字生成配对键，避免重名导致的冲突与列归属错误。
                // recipientId <= 0 视为独白（AddLogEntry 用 -1 表示无接收者；
                // 0 是 int 默认值，真实小人 thingIDNumber 始终为正数）。
                if (recipientId <= 0) return initiatorId.ToString();
                return initiatorId < recipientId
                    ? $"{initiatorId}|{recipientId}"
                    : $"{recipientId}|{initiatorId}";
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
