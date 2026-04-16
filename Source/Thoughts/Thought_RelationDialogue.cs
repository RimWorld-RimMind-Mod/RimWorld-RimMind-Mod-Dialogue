using RimWorld;
using Verse;

namespace RimMind.Dialogue
{
    public class Thought_RelationDialogue : Thought_MemorySocial
    {
        public string aiLabel = string.Empty;
        public string aiDescription = string.Empty;

        public override string LabelCap
        {
            get
            {
                if (aiLabel.NullOrEmpty()) return base.LabelCap;
                return $"[RimMind] {aiLabel.CapitalizeFirst()}";
            }
        }

        public override string Description
            => aiDescription.NullOrEmpty() ? base.Description : aiDescription;

        public override void ExposeData()
        {
            base.ExposeData();
#pragma warning disable CS8601
            Scribe_Values.Look(ref aiLabel, "aiLabel", string.Empty);
            Scribe_Values.Look(ref aiDescription, "aiDesc", string.Empty);
#pragma warning restore CS8601
        }
    }
}
