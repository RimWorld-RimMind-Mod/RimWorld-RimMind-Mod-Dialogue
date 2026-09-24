using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Dialogue.Settings;
using RimMind.Presentation.Api;
using UnityEngine;

namespace RimMind.Dialogue
{
    internal sealed class DialogueModCooldown : IModCooldown
    {
        public string Id => "Dialogue";
        public string OwnerModId => "RimMind.Dialogue";
        public int CooldownTicks
        {
            get
            {
                float scale = RimMindAPI.Settings.ActivityFrequencyScale;
                float multiplier = scale > 0.01f ? (1.0f / scale) : 1.0f;
                multiplier = Mathf.Clamp(multiplier, 0.35f, 3.5f);
                return Mathf.RoundToInt(1500 * multiplier);
            }
        }
    }
}
