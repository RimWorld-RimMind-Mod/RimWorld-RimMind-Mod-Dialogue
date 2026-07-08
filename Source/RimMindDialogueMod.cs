using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimMind.Application.Common.Interfaces.Context;
using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Domain.ValueObjects;
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

            RegisterContextProviders();
            RimMindAPI.Extensions<ISettingsTab>().Register(new DialogueSettingsTab());
            RimMindAPI.Extensions<IModCooldown>().Register(new DialogueModCooldown());
            RimMindAPI.Extensions<IToggleBehavior>().Register(new DialogueOverlayToggleBehavior());
            RimMindAPI.Extensions<IDialogueTrigger>().Register(new DialogueTriggerAdapter());
            RimMindAPI.Extensions<ISkipCheck>().Register(new DialogueSkipCheck());

            Log.Message("[RimMind-Dialogue] Initialized.");
        }

        private static void RegisterContextProviders()
        {
            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_state", ContextLayer.L3_State, 0.2f,
                async (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return null;
                    var pawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId)
                        ?? Find.CurrentMap?.mapPawns?.FreeColonists.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId);
                    if (pawn == null) return null;
                    var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
                    if (memories == null) return null;

                    var sb = new StringBuilder("RimMind.Dialogue.Context.StateHeader".Translate());
                    bool any = false;
                    foreach (var t in memories)
                    {
                        if (t.def.defName != "RimMindDialogue_Thought") continue;

                        string desc = (t as Thought_RimMindDialogue)?.aiDescription ?? t.def.label;
                        float hours = t.DurationTicks / 2500f;
                        sb.AppendLine("RimMind.Dialogue.Context.ThoughtRemaining".Translate(desc, $"{hours:F1}"));
                        any = true;
                    }
                    return any ? sb.ToString().TrimEnd() : null;
                }, "RimMind.Dialogue", stalenessTicks: 750, invalidationTriggers: new[] { "DialogueEvent" }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_relation", ContextLayer.L3_State, 0.15f,
                async (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return null;
                    var pawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId)
                        ?? Find.CurrentMap?.mapPawns?.FreeColonists.FirstOrDefault(p => p.thingIDNumber == ctx.PawnId);
                    if (pawn == null) return null;
                    var recipient = RimMindDialogueService.GetActiveRecipient(pawn);
                    if (recipient == null) return null;

                    var sb = new StringBuilder("RimMind.Dialogue.Context.RelationHeader".Translate(recipient.Name.ToStringShort));

                    float opinion = pawn.relations?.OpinionOf(recipient) ?? 0f;
                    string opinionLabel = opinion >= 20 ? "RimMind.Dialogue.Context.Opinion.Friend".Translate()
                                        : opinion <= -20 ? "RimMind.Dialogue.Context.Opinion.Enemy".Translate()
                                        : "RimMind.Dialogue.Context.Opinion.Acquaintance".Translate();
                    sb.AppendLine("RimMind.Dialogue.Context.OpinionLabel".Translate(opinion.ToString("+0;-0"), opinionLabel));

                    float compat = pawn.relations?.CompatibilityWith(recipient) ?? 0.5f;
                    string compatLabel = compat >= 0.6f ? "RimMind.Dialogue.Context.Compat.High".Translate()
                                        : compat <= 0.3f ? "RimMind.Dialogue.Context.Compat.Low".Translate()
                                        : "RimMind.Dialogue.Context.Compat.Medium".Translate();
                    sb.AppendLine("RimMind.Dialogue.Context.CompatLabel".Translate($"{compat:F2}", compatLabel));

                    float romance = pawn.relations?.SecondaryRomanceChanceFactor(recipient) ?? 0f;
                    string romanceLabel = romance >= 0.5f ? "RimMind.Dialogue.Context.Romance.High".Translate()
                                         : romance >= 0.15f ? "RimMind.Dialogue.Context.Romance.Medium".Translate()
                                         : "RimMind.Dialogue.Context.Romance.Low".Translate();
                    sb.AppendLine("RimMind.Dialogue.Context.RomanceLabel".Translate($"{romance:F2}", romanceLabel, "RimMind.Dialogue.Context.Romance.Unlikely".Translate()));

                    var directRel = pawn.relations?.DirectRelations?.FirstOrDefault(r => r.otherPawn == recipient);
                    if (directRel != null)
                        sb.AppendLine("RimMind.Dialogue.Context.DirectRelation".Translate(directRel.def.label));

                    return sb.ToString().TrimEnd();
                }, "RimMind.Dialogue", stalenessTicks: 750, invalidationTriggers: new[] { "DialogueEvent" }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_task", ContextLayer.L0_Static, 0.95f,
                async (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return null;
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioDialogue) return null;
                    string? speakerName = ctx.Hints?.TryGetValue("SpeakerName", out var sn) == true ? sn?.ToString() : null;
                    if (!string.IsNullOrEmpty(speakerName)) return null;
                    bool isMonologue = ctx.Hints?.TryGetValue("IsMonologue", out var im) == true && im is bool b && b;
                    var subKeys = new List<string> { "Role", "Process", "Constraint", "Fallback", "ThoughtRules" };
                    subKeys.Add(isMonologue ? "GoalMonologue" : "GoalDialogue");
                    subKeys.Add(isMonologue ? "ExampleMonologue" : "ExampleDialogue");
                    subKeys.Add(isMonologue ? "OutputMonologue" : "OutputDialogue");
                    if (!isMonologue) subKeys.Add("RelationDelta");
                    return RimMindAPI.Prompt.BuildTaskInstruction("RimMind.Dialogue.Prompt.TaskInstruction", null, subKeys.ToArray());
                }, "RimMind.Dialogue", stalenessTicks: 0, invalidationTriggers: new[] { "DialogueEvent" }));
        }

        public override string SettingsCategory() => "RimMind - Dialogue";

        public override void DoSettingsWindowContents(UnityEngine.Rect rect)
        {
            RimMindDialogueSettings.DrawSettingsContent(rect);
        }
    }
}
