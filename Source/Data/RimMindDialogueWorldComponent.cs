using System.Collections.Generic;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimWorld.Planet;
using Verse;

namespace RimMind.Dialogue.Data
{
    public class RimMindDialogueWorldComponent : WorldComponent
    {
        private List<DialogueLogEntry> _savedEntries = new List<DialogueLogEntry>();

        private static RimMindDialogueWorldComponent? _instance;
        public static RimMindDialogueWorldComponent? Instance => _instance;

        public RimMindDialogueWorldComponent(World world) : base(world)
        {
            _instance = this;
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
