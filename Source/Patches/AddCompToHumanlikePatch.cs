using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimMind.Dialogue.Comps;
using RimWorld;
using Verse;

namespace RimMind.Dialogue.Patches
{
    [HarmonyPatch(typeof(ThingDef), nameof(ThingDef.ResolveReferences))]
    public static class AddCompToHumanlikePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ThingDef __instance)
        {
            if (__instance.race?.intelligence != Intelligence.Humanlike) return;

            __instance.comps ??= new List<CompProperties>();

            if (__instance.comps.Any(c => c is CompProperties_RimMindDialogue)) return;

            __instance.comps.Add(new CompProperties_RimMindDialogue());
        }
    }
}
