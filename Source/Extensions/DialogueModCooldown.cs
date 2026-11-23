using RimMind.Application.Common.Constants;
using RimMind.Application.Common.Helpers;
using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Presentation.Api;

namespace RimMind.Dialogue
{
    internal sealed class DialogueModCooldown : IModCooldown
    {
        public string Id => "RimMind.Dialogue";
        public string OwnerModId => RimMindOwnerConsts.DialogueModId;
        public int CooldownTicks => ActivityCooldownScaler.Scale(1500, RimMindAPI.Settings.ActivityFrequencyScale);
    }
}
