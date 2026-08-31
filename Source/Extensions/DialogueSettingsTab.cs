using UnityEngine;
using Verse;
using RimMind.Presentation.Settings;
using RimMind.Dialogue.Settings;

namespace RimMind.Dialogue
{
    internal sealed class DialogueSettingsTab : ISettingsTab
    {
        public string Id => "dialogue";
        public string OwnerModId => "RimMindDialogue";
        public string Label => "RimMind.Dialogue.Settings.TabLabel".Translate();
        public void Draw(Rect rect) => RimMindDialogueSettings.DrawSettingsContent(rect);
    }
}
