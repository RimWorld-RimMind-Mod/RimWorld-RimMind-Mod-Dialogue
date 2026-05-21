using RimMind.Application.Common.Interfaces.Extension;

namespace RimMind.Dialogue
{
    internal sealed class DialogueSkipCheck : ISkipCheck
    {
        public string Id => "dialogue.skip";
        public string OwnerModId => "RimMindDialogue";
        public SkipCheckKind Kind => SkipCheckKind.Dialogue;
        public bool ShouldSkip(in SkipCheckArgs args) => false;
    }
}
