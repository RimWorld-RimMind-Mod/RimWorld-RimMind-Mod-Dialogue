using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue
{
    public static class ThoughtInjector
    {
        private const string ThoughtDefName = "RimMindDialogue_Thought";
        private const string RelationThoughtDefName = "RimMindDialogue_RelationThought";

        public static void Inject(Pawn pawn, Pawn? recipient, string tag, string? description)
        {
            if (tag.NullOrEmpty() || tag == "NONE") return;

            var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(ThoughtDefName);
            if (thoughtDef == null)
            {
                Log.Warning($"[RimMind-Dialogue] ThoughtDef '{ThoughtDefName}' not found.");
                return;
            }

            float moodOffset = MapTagToMoodOffset(tag);
            string label = MapTagToLabel(tag);

            var thought = (Thought_RimMindDialogue)ThoughtMaker.MakeThought(thoughtDef);
            thought.aiLabel = label;
            thought.aiDescription = description ?? string.Empty;
            thought.aiMoodOffset = moodOffset;

            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
        }

        private static float MapTagToMoodOffset(string tag)
        {
            switch (tag.ToUpperInvariant())
            {
                case "ENCOURAGED": return +1f;
                case "HURT": return -1f;
                case "VALUED": return +2f;
                case "CONNECTED": return +2f;
                case "STRESSED": return -2f;
                case "IRRITATED": return -1f;
                default: return 0f;
            }
        }

        private static string MapTagToLabel(string tag)
        {
            switch (tag.ToUpperInvariant())
            {
                case "ENCOURAGED": return "RimMind.Dialogue.Thought.ENCOURAGED".Translate();
                case "HURT": return "RimMind.Dialogue.Thought.HURT".Translate();
                case "VALUED": return "RimMind.Dialogue.Thought.VALUED".Translate();
                case "CONNECTED": return "RimMind.Dialogue.Thought.CONNECTED".Translate();
                case "STRESSED": return "RimMind.Dialogue.Thought.STRESSED".Translate();
                case "IRRITATED": return "RimMind.Dialogue.Thought.IRRITATED".Translate();
                default: return tag;
            }
        }

        public static void InjectRelationDelta(Pawn pawn, Pawn recipient, float delta)
        {
            if (recipient == null) return;
            if (Mathf.Abs(delta) < 0.01f) return;

            delta = Mathf.Clamp(delta, -5f, +5f);

            var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(RelationThoughtDefName);
            if (thoughtDef == null)
            {
                Log.Warning($"[RimMind-Dialogue] ThoughtDef '{RelationThoughtDefName}' not found.");
                return;
            }

            var thought = (Thought_RelationDialogue)ThoughtMaker.MakeThought(thoughtDef);
            thought.otherPawn = recipient;
            thought.opinionOffset = delta;
            thought.aiLabel = delta > 0
                ? "RimMind.Dialogue.Relation.Positive".Translate(recipient.Name.ToStringShort)
                : "RimMind.Dialogue.Relation.Negative".Translate(recipient.Name.ToStringShort);

            pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(thought);
        }
    }
}
