using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimMind.Application.Common.Interfaces.Context;
using RimMind.Application.Common.Interfaces.Extension;
using RimMind.Application.Common.Models.Context;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation;
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
            RimMindAPI.Context.RegisterContextKey("dialogue_state", ContextLayer.L3_State, 0.2f,
                pawnObj =>
                {
                    var pawn = pawnObj as Pawn; if (pawn == null) return new List<ContextEntry>();
                    var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
                    if (memories == null) return new List<ContextEntry>();

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
                    return any ? new List<ContextEntry> { new ContextEntry(sb.ToString().TrimEnd()) } : new List<ContextEntry>();
                }, "RimMind.Dialogue");

            RimMindAPI.Context.RegisterContextKey("dialogue_relation", ContextLayer.L3_State, 0.15f,
                pawnObj =>
                {
                    var pawn = pawnObj as Pawn; if (pawn == null) return new List<ContextEntry>();
                    var recipient = RimMindDialogueService.GetActiveRecipient(pawn);
                    if (recipient == null) return new List<ContextEntry>();

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

                    return new List<ContextEntry> { new ContextEntry(sb.ToString().TrimEnd()) };
                }, "RimMind.Dialogue");

            RimMindAPI.Context.RegisterContextKey("dialogue_task", ContextLayer.L0_Static, 0.95f,
                pawnObj =>
                {
                    var pawn = pawnObj as Pawn; if (pawn == null) return new List<ContextEntry>();
                    if (RimMindAPI.Context.CurrentScenario != RimMindAPI.Context.ScenarioDialogue) return new List<ContextEntry>();
                    if (!string.IsNullOrEmpty(RimMindAPI.Context.CurrentSpeakerName)) return new List<ContextEntry>();
                    bool isMonologue = RimMindAPI.Context.CurrentIsMonologue;
                    var subKeys = new List<string> { "Role", "Process", "Constraint", "Fallback", "ThoughtRules" };
                    subKeys.Add(isMonologue ? "GoalMonologue" : "GoalDialogue");
                    subKeys.Add(isMonologue ? "ExampleMonologue" : "ExampleDialogue");
                    subKeys.Add(isMonologue ? "OutputMonologue" : "OutputDialogue");
                    if (!isMonologue) subKeys.Add("RelationDelta");
                    return new List<ContextEntry> { new ContextEntry(RimMindAPI.Prompt.BuildTaskInstruction("RimMind.Dialogue.Prompt.TaskInstruction", null, subKeys.ToArray())) };
                }, "RimMind.Dialogue");

            RimMindAPI.Context.RegisterContextKey("player_dialogue_task", ContextLayer.L0_Static, 0.95f,
                pawnObj =>
                {
                    var pawn = pawnObj as Pawn; if (pawn == null) return new List<ContextEntry>();
                    if (RimMindAPI.Context.CurrentScenario != RimMindAPI.Context.ScenarioDialogue) return new List<ContextEntry>();
                    if (string.IsNullOrEmpty(RimMindAPI.Context.CurrentSpeakerName)) return new List<ContextEntry>();
                    return new List<ContextEntry> { new ContextEntry(RimMindAPI.Prompt.BuildTaskInstruction("RimMind.Dialogue.Prompt.PlayerTaskInstruction", null,
                        "Role", "Goal", "Process", "Constraint", "Example", "Output", "Fallback", "InitiatorConstraint")) };
                }, "RimMind.Dialogue");
        }

        public override string SettingsCategory() => "RimMind - Dialogue";

        public override void DoSettingsWindowContents(UnityEngine.Rect rect)
        {
            RimMindDialogueSettings.DrawSettingsContent(rect);
        }
    }
}
