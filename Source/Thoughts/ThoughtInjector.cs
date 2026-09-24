using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using RimMind.Domain.ValueObjects;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue
{
    public static class ThoughtInjector
    {
        private const string ThoughtDefName = "RimMindDialogue_Thought";
        private const string RelationThoughtDefName = "RimMindDialogue_RelationThought";

        // 使用 ConcurrentDictionary：RegisterThoughtTag 是公开 API，鼓励外部 mod 调用，
        // 并发注册 + 读取时 Dictionary 会抛 InvalidOperationException（集合已修改）。
        private static readonly ConcurrentDictionary<string, int> MoodOffsetMap = new ConcurrentDictionary<string, int>
        {
            ["ENCOURAGED"] = 1,
            ["HURT"] = -1,
            ["VALUED"] = 2,
            ["CONNECTED"] = 2,
            ["STRESSED"] = -2,
            ["IRRITATED"] = -1,
        };

        private static readonly ConcurrentDictionary<string, string> LabelMap = new ConcurrentDictionary<string, string>
        {
            ["ENCOURAGED"] = "RimMind.Dialogue.Thought.ENCOURAGED",
            ["HURT"] = "RimMind.Dialogue.Thought.HURT",
            ["VALUED"] = "RimMind.Dialogue.Thought.VALUED",
            ["CONNECTED"] = "RimMind.Dialogue.Thought.CONNECTED",
            ["STRESSED"] = "RimMind.Dialogue.Thought.STRESSED",
            ["IRRITATED"] = "RimMind.Dialogue.Thought.IRRITATED",
        };

        public static void Inject(Pawn pawn, Pawn? recipient, string tag, string? description)
        {
            if (tag.NullOrEmpty() || tag == "NONE") return;

            var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(ThoughtDefName);
            if (thoughtDef == null)
            {
                RimMindErrors.Warn($"[RimMind-Dialogue] ThoughtDef '{ThoughtDefName}' not found.");
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

        public static int MapTagToMoodOffset(string tag)
        {
            return MoodOffsetMap.TryGetValue(tag.ToUpperInvariant(), out int v) ? v : 0;
        }

        public static string MapTagToLabel(string tag)
        {
            return LabelMap.TryGetValue(tag.ToUpperInvariant(), out string? v) ? v.Translate() : tag;
        }

        public static void RegisterThoughtTag(string tag, int moodOffset, string labelKey)
        {
            MoodOffsetMap[tag.ToUpperInvariant()] = moodOffset;
            LabelMap[tag.ToUpperInvariant()] = labelKey;
        }

        public static void InjectRelationDelta(Pawn pawn, Pawn recipient, float delta)
        {
            if (recipient == null) return;
            if (Mathf.Abs(delta) < 0.01f) return;

            delta = Mathf.Clamp(delta, -5f, +5f);

            var thoughtDef = DefDatabase<ThoughtDef>.GetNamedSilentFail(RelationThoughtDefName);
            if (thoughtDef == null)
            {
                RimMindErrors.Warn($"[RimMind-Dialogue] ThoughtDef '{RelationThoughtDefName}' not found.");
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
