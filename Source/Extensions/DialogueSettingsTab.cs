using RimMind.Application.Common.Constants;
using RimMind.Dialogue.Settings;
using RimMind.Presentation.Api;
using RimMind.Presentation.Settings;
using UnityEngine;

namespace RimMind.Dialogue
{
    internal sealed class DialogueSettingsTab : ISettingsTab
    {
        public string Id => "dialogue";
        public string OwnerModId => RimMindOwnerConsts.DialogueModId;
        public string Label => RimMindAPI.Text.TrOr("RimMind.Dialogue.Settings.TabLabel", "Dialogue");
        public void Draw(Rect rect) => RimMindDialogueSettings.DrawSettingsContent(rect);
    }
}
