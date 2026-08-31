using HarmonyLib;
using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Presentation.Api;
using RimMind.Presentation.Settings;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using Verse;

namespace RimMind.Dialogue
{
    public class RimMindDialogueMod : Mod
    {
        public static RimMindDialogueSettings Settings = null!;

        public RimMindDialogueMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimMindDialogueSettings>();
            new Harmony("mcocdaa.RimMindDialogueStandalone").PatchAll();

            DialogueContextProviderRegistrar.RegisterAll();
            RimMindAPI.Extensions<ISettingsTab>().Register(new DialogueSettingsTab());
            RimMindAPI.Extensions<IModCooldown>().Register(new DialogueModCooldown());
            RimMindAPI.Extensions<IToggleBehavior>().Register(new DialogueOverlayToggleBehavior());
            RimMindAPI.Extensions<IDialogueTrigger>().Register(new DialogueTriggerAdapter());
            RimMindAPI.Extensions<ISkipCheck>().Register(new DialogueSkipCheck());

            Log.Message("[RimMind-Dialogue] Initialized.");
        }

        public override string SettingsCategory() => "RimMind - Dialogue";

        public override void DoSettingsWindowContents(UnityEngine.Rect rect)
        {
            RimMindDialogueSettings.DrawSettingsContent(rect);
        }
    }
}
