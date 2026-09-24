using Verse;

namespace Verse
{
    // Pawn 存根，仅提供编译所需的最小属性链
    public class Pawn : Thing
    {
        public bool IsColonist;
        public bool Dead;
        public bool Destroyed;
        public RimWorld.Pawn_NeedsTracker? needs;
        public RimWorld.Pawn_RelationsTracker? relations;
        public NameTriple Name => new NameTriple();
        public bool IsPrisoner;
        public bool IsSlave;
        public bool Downed;
        public bool IsFreeNonSlaveColonist = true;
        public RimWorld.Pawn_DraftController? drafter;
        public Map? Map;
        public UnityEngine.Vector3 DrawPos;
        public IntVec3 Position;
        public bool Awake() => true;
        public RimWorld.Job? CurJob;
        public RimWorld.JobDef? CurJobDef;
        public bool IsHashIntervalTick(int interval) => true;
    }

    public class NameTriple
    {
        public string ToStringShort => "TestPawn";
    }
}

namespace RimWorld
{
    public class Pawn_RelationsTracker
    {
        public int OpinionOf(Verse.Pawn other) => 0;
    }

    public static class LovePartnerRelationUtility
    {
        public static bool LovePartnerRelationExists(Verse.Pawn a, Verse.Pawn b) => false;
    }

    public class Job
    {
        public JobDef? def;
    }

    public class JobDef : Verse.Def
    {
        public object? joyKind;
        public string? label;
    }

    public static class JobDefOf
    {
        public static JobDef Ingest = new JobDef { defName = "Ingest" };
        public static JobDef Skygaze = new JobDef { defName = "Skygaze" };
    }

    public class Pawn_DraftController { public bool Drafted; }

    public class Pawn_NeedsTracker
    {
        public Need_Mood? mood;
    }

    public class Need_Mood
    {
        public float CurLevel = 0.5f;
        public ThoughtHandler? thoughts;
    }

    public class ThoughtHandler
    {
        public MemoryThoughtHandler? memories;
    }

    public class MemoryThoughtHandler
    {
        public void TryGainMemory(Thought_Memory thought) { }
    }

    // ThoughtDef 存根
    public class ThoughtDef : Verse.Def
    {
    }

    public class Thought : Verse.IExposable
    {
        public ThoughtDef? def;
        public virtual string LabelCap => "";
        public virtual string Description => "";
        public virtual float MoodOffset() => 0f;
        public virtual bool GroupsWith(Thought other) => false;
        public virtual void ExposeData() { }
    }

    // Thought_Memory 存根
    public class Thought_Memory : Thought
    {
    }

    // Thought_MemorySocial 存根
    public class Thought_MemorySocial : Thought_Memory
    {
        public float opinionOffset;
        public Pawn? otherPawn;
    }

    // ThoughtMaker 存根
    public static class ThoughtMaker
    {
        public static Thought_Memory MakeThought(ThoughtDef def) => new Thought_Memory();
    }
}
