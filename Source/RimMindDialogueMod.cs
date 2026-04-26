using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimMind.Core;
using RimMind.Core.Context;
using RimMind.Core.Prompt;
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
            RimMindAPI.RegisterSettingsTab("dialogue", () => "RimMind.Dialogue.Settings.TabLabel".Translate(), RimMindDialogueSettings.DrawSettingsContent);
            RimMindAPI.RegisterModCooldown("Dialogue", () => RimMindDialogueSettings.Get().monologueCooldownTicks);

            RimMindAPI.RegisterToggleBehavior("dialogue_overlay",
                () => RimMindDialogueSettings.Get().overlayEnabled,
                () =>
                {
                    var s = RimMindDialogueSettings.Get();
                    s.overlayEnabled = !s.overlayEnabled;
                    s.Write();
                });

            RimMindAPI.RegisterDialogueTrigger((pawn, context, recipient) =>
            {
                RimMindDialogueService.HandleTrigger(pawn, context, DialogueTriggerType.Chitchat, recipient);
            });

            Log.Message("[RimMind-Dialogue] Initialized.");
        }

        private static void RegisterContextProviders()
        {
            RimMindAPI.RegisterPawnContextProvider("dialogue_state", pawn =>
            {
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
            });

            RimMindAPI.RegisterPawnContextProvider("dialogue_relation", pawn =>
            {
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
            });

            ContextKeyRegistry.Register("dialogue_task", ContextLayer.L0_Static, 0.95f,
                pawn =>
                {
                    if (ContextKeyRegistry.CurrentScenario != ScenarioIds.Dialogue) return new List<ContextEntry>();
                    return new List<ContextEntry> { new ContextEntry(TaskInstructionBuilder.Build("RimMind.Dialogue.Prompt.TaskInstruction",
                        "Role", "Goal", "Process", "Constraint", "Example", "Output", "Fallback",
                        "GoalDialogue", "GoalMonologue", "JsonTemplate", "TriggerReason", "RelationDelta")) };
                }, "RimMind.Dialogue");

            ContextKeyRegistry.Register("player_dialogue_task", ContextLayer.L0_Static, 0.95f,
                pawn =>
                {
                    if (ContextKeyRegistry.CurrentScenario != ScenarioIds.Dialogue) return new List<ContextEntry>();
                    return new List<ContextEntry> { new ContextEntry(TaskInstructionBuilder.Build("RimMind.Dialogue.Prompt.PlayerTaskInstruction",
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
