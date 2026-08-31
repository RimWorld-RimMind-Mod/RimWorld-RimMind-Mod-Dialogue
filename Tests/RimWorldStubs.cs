namespace RimWorld
{
    // Pawn 存根，仅提供编译所需的最小属性链
    public class Pawn
    {
        public int thingIDNumber;
        public bool IsColonist;
        public bool Dead;
        public bool Destroyed;
        public Pawn_NeedsTracker? needs;
        public NameTriple Name => new NameTriple();
        public string LabelShort => "TestPawn";
        public string ThingID => "Thing_" + thingIDNumber;
    }

    public class NameTriple
    {
        public string ToStringShort => "TestPawn";
    }

    public class Pawn_NeedsTracker
    {
        public Need_Mood? mood;
    }

    public class Need_Mood
    {
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

    // Thought_Memory 存根
    public class Thought_Memory : Verse.IExposable
    {
        public virtual string LabelCap => "";
        public virtual string Description => "";
        public virtual float MoodOffset() => 0f;
        public virtual void ExposeData() { }
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
