using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimMind.Core;
using RimMind.Core.Prompt;
using RimMind.Dialogue.Settings;
using RimWorld;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class DialoguePromptBuilder
    {
        public static string BuildAutoSystemPrompt(Pawn pawn, string triggerLabel, Pawn? recipient, string? customPrompt)
        {
            var builder = StructuredPromptBuilder.FromKeyPrefix("RimMind.Dialogue.Prompt.System");

            builder.Goal(recipient != null
                ? "RimMind.Dialogue.Prompt.System.GoalDialogue".Translate(recipient.Name.ToStringShort)
                : "RimMind.Dialogue.Prompt.System.GoalMonologue".Translate());

            builder.Constraint("RimMind.Dialogue.Prompt.System.TriggerReason".Translate(triggerLabel));

            if (recipient != null)
                builder.Constraint("RimMind.Dialogue.Prompt.System.RelationDelta".Translate());

            string roleConstraint = GetRoleConstraint(pawn);
            if (!string.IsNullOrEmpty(roleConstraint))
                builder.Constraint(roleConstraint);

            if (recipient != null)
            {
                string recipientRoleConstraint = GetRoleConstraint(recipient);
                if (!string.IsNullOrEmpty(recipientRoleConstraint))
                    builder.Constraint("RimMind.Dialogue.Prompt.System.RecipientRole".Translate(recipientRoleConstraint));
            }

            builder.WithCustom(customPrompt);

            return builder.Build();
        }

        public static string BuildPlayerSystemPrompt(Pawn pawn, Pawn? initiator, string? customPrompt)
        {
            var builder = StructuredPromptBuilder.FromKeyPrefix("RimMind.Dialogue.Prompt.PlayerSystem")
                .Role("RimMind.Dialogue.Prompt.PlayerSystem.Role".Translate(pawn.Name.ToStringShort));

            if (initiator != null)
            {
                builder.Constraint("RimMind.Dialogue.Prompt.PlayerSystem.InitiatorConstraint".Translate(
                    initiator.Name.ToStringShort));
                string initiatorRole = GetRoleConstraint(initiator);
                if (!string.IsNullOrEmpty(initiatorRole))
                    builder.Constraint(initiatorRole);
            }

            builder.WithCustom(customPrompt);

            return builder.Build();
        }

        public static string BuildAutoUserPrompt(Pawn pawn, string context, DialogueTriggerType type, Pawn? recipient)
        {
            var sections = new List<PromptSection>();

            var pawnSections = RimMindAPI.BuildFullPawnSections(pawn);
            sections.AddRange(pawnSections);

            var sb = new StringBuilder();
            switch (type)
            {
                case DialogueTriggerType.Chitchat:
                    sb.AppendLine("RimMind.Dialogue.Prompt.Context.Chitchat".Translate(context));
                    if (recipient != null)
                        sb.AppendLine("RimMind.Dialogue.Prompt.Context.Recipient".Translate(recipient.Name.ToStringShort));
                    break;
                case DialogueTriggerType.Hediff:
                    sb.AppendLine("RimMind.Dialogue.Prompt.Context.Hediff".Translate(context));
                    break;
                case DialogueTriggerType.LevelUp:
                    sb.AppendLine("RimMind.Dialogue.Prompt.Context.LevelUp".Translate(context));
                    break;
                case DialogueTriggerType.Thought:
                    sb.AppendLine("RimMind.Dialogue.Prompt.Context.Thought".Translate(context));
                    break;
                case DialogueTriggerType.Auto:
                    sb.AppendLine("RimMind.Dialogue.Prompt.Context.Auto".Translate(context));
                    break;
            }

            if (recipient != null)
            {
                int contextRounds = RimMindDialogueSettings.Get().dialogueContextRounds;
                var history = RimMindDialogueService.GetDialogueHistory(pawn.thingIDNumber, recipient.thingIDNumber, contextRounds);
                if (history.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("RimMind.Dialogue.Prompt.Context.DialogueHistory".Translate());
                    string historyText = string.Join("\n", history.Select(e => $"{e.initiatorName}: {e.reply}"));
                    sb.AppendLine(ContextComposer.CompressHistory(historyText, 6,
                        "RimMind.Dialogue.Prompt.Context.HistoryCompressed".Translate()));
                }
            }

            if (sb.Length > 0)
                sections.Add(new PromptSection("trigger_context",
                    sb.ToString().TrimEnd(),
                    PromptSection.PriorityCurrentInput));

            var budget = new PromptBudget(4000, 400);
            return budget.ComposeToString(sections);
        }

        public static string BuildPlayerUserPrompt(Pawn pawn, Pawn? initiator)
        {
            var sections = RimMindAPI.BuildFullPawnSections(pawn);

            if (initiator != null)
            {
                var initiatorSections = RimMindAPI.BuildFullPawnSections(initiator);
                for (int i = 0; i < initiatorSections.Count; i++)
                    initiatorSections[i] = new PromptSection(
                        "initiator_" + initiatorSections[i].Tag,
                        initiatorSections[i].Content,
                        initiatorSections[i].Priority - 50);
                sections.AddRange(initiatorSections);
            }

            var budget = new PromptBudget(4000, 400);
            return budget.ComposeToString(sections);
        }

        private static string GetRoleConstraint(Pawn pawn)
        {
            if (pawn.IsPrisoner)
                return "RimMind.Dialogue.Prompt.Role.Prisoner".Translate();
            if (pawn.IsSlave)
                return "RimMind.Dialogue.Prompt.Role.Slave".Translate();
            if (pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer))
                return "RimMind.Dialogue.Prompt.Role.Enemy".Translate();
            if (!pawn.IsColonist && pawn.Faction != null && !pawn.Faction.HostileTo(Faction.OfPlayer))
                return "RimMind.Dialogue.Prompt.Role.Visitor".Translate();
            return string.Empty;
        }
    }
}
