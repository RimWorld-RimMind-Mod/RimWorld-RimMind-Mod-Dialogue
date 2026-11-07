using RimMind.Application.Common.Helpers;
using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Presentation.Api;

namespace RimMind.Dialogue
{
    internal sealed class DialogueModCooldown : IModCooldown
    {
        public string Id => "Dialogue";
        public string OwnerModId => "RimMind.Dialogue";
        public int CooldownTicks => ActivityCooldownScaler.Scale(1500, RimMindAPI.Settings.ActivityFrequencyScale);
    }
}
