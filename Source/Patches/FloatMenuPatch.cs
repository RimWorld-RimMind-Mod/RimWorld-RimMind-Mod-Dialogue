using System.Collections.Generic;
using HarmonyLib;
using RimMind.Core;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Patches
{
#if V1_5
    [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.ChoicesAtFor))]
#else
    [HarmonyPatch(typeof(FloatMenuMakerMap), nameof(FloatMenuMakerMap.GetOptions))]
#endif
    public static class FloatMenuPatch
    {
        private const int ClickRadiusCells = 1;

#if V1_5
        [HarmonyPostfix]
        public static void Postfix(Vector3 clickPos, Pawn pawn, ref List<FloatMenuOption> __result)
        {
            TryAddTalkOption(__result, pawn, clickPos);
        }
#else
        [HarmonyPostfix]
        public static void Postfix(
            List<Pawn> selectedPawns,
            Vector3 clickPos,
            FloatMenuContext context,
            ref List<FloatMenuOption> __result)
        {
            Pawn? pawn = (selectedPawns is { Count: 1 }) ? selectedPawns[0] : null;
            TryAddTalkOption(__result, pawn!, clickPos);
        }
#endif

        private static void TryAddTalkOption(List<FloatMenuOption> result, Pawn selectedPawn, Vector3 clickPos)
        {
            if (result == null) return;
            if (!RimMindDialogueSettings.Get().enabled) return;
            if (!RimMindDialogueSettings.Get().playerDialogueEnabled) return;
            if (selectedPawn == null || selectedPawn.Drafted) return;
            if (!selectedPawn.Spawned || selectedPawn.Dead) return;

            Map map = selectedPawn.Map;
            if (map == null) return;
            IntVec3 clickCell = IntVec3.FromVector3(clickPos);

            HashSet<Pawn> processedPawns = new HashSet<Pawn>();

            for (int dx = -ClickRadiusCells; dx <= ClickRadiusCells; dx++)
            {
                for (int dz = -ClickRadiusCells; dz <= ClickRadiusCells; dz++)
                {
                    IntVec3 curCell = clickCell + new IntVec3(dx, 0, dz);
                    if (!curCell.InBounds(map)) continue;

                    List<Thing> thingList = map.thingGrid.ThingsListAt(curCell);
                    for (int i = 0; i < thingList.Count; i++)
                    {
                        if (thingList[i] is Pawn hitPawn)
                        {
                            if (!processedPawns.Add(hitPawn)) continue;
                            if (hitPawn == selectedPawn) continue;
                            if (!IsValidTarget(hitPawn)) continue;

                            AddTalkOption(result, selectedPawn, hitPawn);
                        }
                    }
                }
            }
        }

        private static bool IsValidTarget(Pawn target)
        {
            if (target == null) return false;
            if (!target.Spawned || target.Dead) return false;
            if (!(target.RaceProps?.Humanlike ?? false)) return false;
            return true;
        }

        private static void AddTalkOption(List<FloatMenuOption> result, Pawn initiator, Pawn target)
        {
            result.Add(new FloatMenuOption(
                "RimMind.Dialogue.UI.FloatMenu.ChatWith".Translate(target.LabelShortCap),
                delegate
                {
                    if (!RimMindAPI.IsConfigured())
                    {
                        Messages.Message("RimMind.Dialogue.UI.FloatMenu.APINotConfigured".Translate(),
                            MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    if (!RimMindDialogueService.IsReady)
                    {
                        Messages.Message("RimMind.Dialogue.UI.FloatMenu.NotReady".Translate(),
                            MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    if (RimMindDialogueService.IsDialoguePending(initiator.thingIDNumber, target.thingIDNumber))
                    {
                        Messages.Message("RimMind.Dialogue.UI.FloatMenu.Pending".Translate(),
                            MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    int count = RimMindDialogueService.GetDailyDialogueCount(initiator.thingIDNumber, target.thingIDNumber);
                    if (count >= RimMindDialogueSettings.Get().maxHistoryRounds)
                    {
                        Messages.Message("RimMind.Dialogue.UI.FloatMenu.DailyLimitReached".Translate(),
                            MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    string context = "RimMind.Dialogue.Context.PlayerInitiate".Translate(
                        initiator.Name.ToStringShort, target.Name.ToStringShort);
                    RimMindDialogueService.HandleTrigger(initiator, context, DialogueTriggerType.Chitchat, target, isImmediate: true);
                },
                MenuOptionPriority.Default,
                null,
                target
            ));
        }
    }
}
