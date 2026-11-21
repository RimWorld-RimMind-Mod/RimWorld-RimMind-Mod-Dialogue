using System.Collections.Generic;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Presentation.Api;
using RimWorld.Planet;
using Verse;

namespace RimMind.Dialogue.Data
{
    public class RimMindDialogueWorldComponent : WorldComponent
    {
        private static readonly WorldScopedComponentCache<RimMindDialogueWorldComponent> _instanceCache =
            new WorldScopedComponentCache<RimMindDialogueWorldComponent>();

        private List<DialogueLogEntry> _savedEntries = new List<DialogueLogEntry>();

        public static RimMindDialogueWorldComponent? Instance => _instanceCache.Get();

        internal static void ResetInstanceCache() => _instanceCache.Reset();

        public RimMindDialogueWorldComponent(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();

            var settings = RimMindDialogueSettings.Get();
            int maxStored = settings.maxStoredDialogueHistory > 0 ? settings.maxStoredDialogueHistory : 200;

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _savedEntries = RimMindDialogueService.ExportLogEntries(maxStored);
            }

            Scribe_Collections.Look(ref _savedEntries, "dialogueEntries", LookMode.Deep);
            _savedEntries ??= new List<DialogueLogEntry>();

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                RimMindDialogueService.ImportLogEntries(_savedEntries, maxStored);
            }
        }
    }
}
