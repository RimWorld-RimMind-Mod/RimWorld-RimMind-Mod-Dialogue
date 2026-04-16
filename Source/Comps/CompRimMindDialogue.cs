using System.Collections.Generic;
using RimMind.Core;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Dialogue.UI;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Comps
{
    public class CompRimMindDialogue : ThingComp
    {
        private int _lastTriggerTick = -99999;

        private Pawn Pawn => (Pawn)parent;

        public override void CompTick()
        {
            if (!Pawn.IsHashIntervalTick(1000)) return;
            if (!RimMindDialogueSettings.Get().autoDialogueEnabled) return;
            if (!RimMindAPI.IsConfigured()) return;
            if (!IsEligible()) return;

            int minInterval = RimMindDialogueSettings.Get().AutoDialogueCooldownTicks;
            if (Find.TickManager.TicksGame - _lastTriggerTick < minInterval) return;

            _lastTriggerTick = Find.TickManager.TicksGame;

            string context = "RimMind.Dialogue.Context.IdleState".Translate();
            RimMindDialogueService.HandleTrigger(Pawn, context, DialogueTriggerType.Auto, null);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!(parent.Faction?.IsPlayer ?? false)) yield break;
            if (!RimMindDialogueSettings.Get().playerDialogueEnabled) yield break;

            yield return new Command_Action
            {
                defaultLabel = "RimMind.Dialogue.UI.Gizmo.ChatWith".Translate(parent.LabelShort),
                icon = ContentFinder<Texture2D>.Get("UI/RimMindDialogue_Icon", reportFailure: false),
                action = () => Find.WindowStack.Add(new Window_Dialogue((Pawn)parent))
            };
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref _lastTriggerTick, "lastTriggerTick", -99999);
        }

        private bool IsEligible()
        {
            return Pawn.IsFreeNonSlaveColonist
                && !Pawn.Dead
                && !Pawn.Downed
                && !(Pawn.drafter?.Drafted ?? false)
                && Pawn.Map != null
                && Pawn.needs?.mood != null;
        }
    }
}
