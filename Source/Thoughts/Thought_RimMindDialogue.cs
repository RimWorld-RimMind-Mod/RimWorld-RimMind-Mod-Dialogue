using RimWorld;
using Verse;

namespace RimMind.Dialogue
{
    public class Thought_RimMindDialogue : Thought_Memory
    {
        public string aiLabel = string.Empty;
        public string aiDescription = string.Empty;
        public float aiMoodOffset;

        public override string LabelCap
        {
            get
            {
                if (aiLabel.NullOrEmpty()) return base.LabelCap;
                string text;
                if (ThoughtInjector.TryGetLabelKey(aiLabel, out string? mappedKey) && !string.IsNullOrEmpty(mappedKey) && mappedKey.CanTranslate())
                {
                    text = mappedKey.Translate().ToString();
                }
                else if (aiLabel.CanTranslate())
                {
                    text = aiLabel.Translate().ToString();
                }
                else if (aiLabel.StartsWith("RimMind."))
                {
                    int lastDot = aiLabel.LastIndexOf('.');
                    text = lastDot >= 0 && lastDot < aiLabel.Length - 1
                        ? aiLabel.Substring(lastDot + 1)
                        : aiLabel;
                }
                else
                {
                    text = aiLabel;
                }
                return $"[RimMind] {text.CapitalizeFirst()}";
            }
        }

        public override string Description
            => aiDescription.NullOrEmpty() ? base.Description : aiDescription;

        public override float MoodOffset() => aiMoodOffset;

        public override bool GroupsWith(Thought other)
        {
            if (other is Thought_RimMindDialogue otherDialogue)
            {
                return def == otherDialogue.def && aiLabel == otherDialogue.aiLabel && aiMoodOffset == otherDialogue.aiMoodOffset;
            }
            return false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
#pragma warning disable CS8601
            Scribe_Values.Look(ref aiLabel, "aiLabel", string.Empty);
            Scribe_Values.Look(ref aiDescription, "aiDesc", string.Empty);
#pragma warning restore CS8601
            Scribe_Values.Look(ref aiMoodOffset, "aiMoodOffset", 0f);
        }
    }
}
