using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimMind.Application.Common.Constants;
using RimMind.Application.Common.Interfaces.Context;
using RimMind.Application.Common.Models.Context;
using RimMind.Application.Features.Context;
using RimMind.Domain.Common;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;
using Verse;

namespace RimMind.Dialogue.Core
{
    internal static class DialogueContextProviderRegistrar
    {
        private static string Tr(string key, params object[] args)
            => RimMindText.TrOr(key, key ?? string.Empty, args);

        /// <summary>
        /// Context providers run on pipeline thread-pool threads; pawn memories, relations, dialogue
        /// history and translations are read inside this main-thread snapshot.
        /// </summary>
        private static Task<string?> OnMainThread(Func<string?> build, CancellationToken ct)
            => RimMindAPI.MainThreadSnapshot.GetAsync(build, null, ct: ct);

        internal static void RegisterAll()
        {
            // Register the typed event before any key names it, so the trigger resolves
            // to a real event (unknown names never fall back to the base bus event).
            DialogueContextChangedEvent.Register();
            RegisterScenarios();

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_state", ContextLayer.L3_State, 0.2f,
                (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return Task.FromResult<string?>(null);
                    int pawnId = ctx.PawnId;
                    return OnMainThread(() => BuildStateContext(pawnId), ct);
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 750, invalidationTriggers: new[] { DialogueContextChangedEvent.EventName }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_relation", ContextLayer.L3_State, 0.15f,
                (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return Task.FromResult<string?>(null);
                    int pawnId = ctx.PawnId;
                    return OnMainThread(() => BuildRelationContext(pawnId), ct);
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 750, invalidationTriggers: new[] { DialogueContextChangedEvent.EventName }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_task", ContextLayer.L0_Static, 0.95f,
                (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return Task.FromResult<string?>(null);
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioDialogue) return Task.FromResult<string?>(null);
                    string? speakerName = ctx.Hints?.TryGetValue("SpeakerName", out var sn) == true ? sn?.ToString() : null;
                    bool isMonologue = string.IsNullOrEmpty(speakerName)
                        && ctx.Hints?.TryGetValue("IsMonologue", out var im) == true
                        && ((im is bool b && b) || (im is string imStr && bool.TryParse(imStr, out var parsed) && parsed));
                    var subKeys = new List<string> { "Role", "Process", "Constraint", "Fallback", "ThoughtRules" };
                    subKeys.Add(isMonologue ? "GoalMonologue" : "GoalDialogue");
                    subKeys.Add(isMonologue ? "ExampleMonologue" : "ExampleDialogue");
                    subKeys.Add(isMonologue ? "OutputMonologue" : "OutputDialogue");
                    if (!isMonologue) subKeys.Add("RelationDelta");
                    subKeys.Add(isMonologue ? "ToolCallMonologue" : "ToolCallDialogue");
                    string[] keys = subKeys.ToArray();
                    return OnMainThread(
                        () => RimMindAPI.Prompt.BuildTaskInstruction("RimMind.Dialogue.Prompt.TaskInstruction", null, keys),
                        ct);
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 0));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_history", ContextLayer.L4_History, 0.5f,
                (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return Task.FromResult<string?>(null);
                    int pawnId = ctx.PawnId;
                    return OnMainThread(() => BuildHistoryContext(pawnId), ct);
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 0));
        }

        private static void RegisterScenarios()
        {
            ScenarioRegistry.Register(new ScenarioDescriptor
            {
                Id = ScenarioIds.Dialogue,
                DefaultBaseRounds = 10,
                Description = "RimMind.Application.Scenario.Dialogue",
                DefaultBudget = 0.6f,
                L4Mode = L4Mode.BudgetControlled,
                DefaultExcludeKeys = new[] { "combat_status", "task_progress" },
                OwnerModId = RimMindOwnerConsts.DialogueModId,
                RelevanceWeights = new Dictionary<string, float>(StringComparer.Ordinal)
                {
                    ["health"] = 0.6f,
                    ["mood"] = 0.9f,
                    ["current_job"] = 0.5f,
                    ["combat_status"] = 0.4f,
                    ["target_info"] = 0.3f,
                    ["task_progress"] = 0.3f,
                    ["nearby_pawns"] = 0.8f,
                    ["colony_status"] = 0.5f,
                    ["current_area"] = 0.6f,
                    ["weather"] = 0.3f,
                    ["time_of_day"] = 0.4f,
                    ["season"] = 0.2f,
                    ["map_structure"] = 0.2f,
                    ["pawn_base_info"] = 0.7f,
                    ["fixed_relations"] = 0.85f,
                    ["ideology"] = 0.6f,
                    ["skills_summary"] = 0.4f,
                    ["memory_pawn"] = 0.7f,
                    ["working_memory"] = 0.6f,
                    ["memory_narrator"] = 0.3f,
                    ["dialogue_state"] = 0.9f,
                    ["dialogue_relation"] = 0.95f,
                    ["dialogue_history"] = 0.85f,
                    ["dialogue_task"] = 0.95f
                }
            });

            ScenarioRegistry.Register(new ScenarioDescriptor
            {
                Id = ScenarioIds.CrisisDialogue,
                DefaultBaseRounds = 8,
                Description = "RimMind.Application.Scenario.CrisisDialogue",
                DefaultBudget = 0.7f,
                L4Mode = L4Mode.BudgetControlled,
                DefaultExcludeKeys = new[] { "task_progress" },
                OwnerModId = RimMindOwnerConsts.DialogueModId,
                RelevanceWeights = new Dictionary<string, float>(StringComparer.Ordinal)
                {
                    ["health"] = 0.9f,
                    ["mood"] = 1.0f,
                    ["current_job"] = 0.6f,
                    ["combat_status"] = 0.8f,
                    ["target_info"] = 0.4f,
                    ["task_progress"] = 0.2f,
                    ["nearby_pawns"] = 0.85f,
                    ["colony_status"] = 0.6f,
                    ["current_area"] = 0.6f,
                    ["weather"] = 0.4f,
                    ["time_of_day"] = 0.3f,
                    ["season"] = 0.2f,
                    ["map_structure"] = 0.3f,
                    ["pawn_base_info"] = 0.8f,
                    ["fixed_relations"] = 0.9f,
                    ["ideology"] = 0.7f,
                    ["skills_summary"] = 0.4f,
                    ["memory_pawn"] = 0.8f,
                    ["working_memory"] = 0.85f,
                    ["memory_narrator"] = 0.3f,
                    ["dialogue_state"] = 0.95f,
                    ["dialogue_relation"] = 0.85f,
                    ["dialogue_history"] = 0.5f,
                    ["dialogue_task"] = 0.95f
                }
            });
        }

        /// <summary>Main thread only.</summary>
        private static string? BuildStateContext(int pawnId)
        {
            var pawn = RimMindPawnLookup.FindPawnByNumber(pawnId);
            var memories = pawn?.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null || memories.Count == 0) return null;

            var sb = new StringBuilder();
            sb.AppendLine(Tr("RimMind.Dialogue.Context.StateHeader"));
            bool any = false;
            for (int i = 0; i < memories.Count; i++)
            {
                var t = memories[i];
                if (t?.def == null || t.def.defName != "RimMindDialogue_Thought") continue;

                string desc = (t as Thought_RimMindDialogue)?.aiDescription ?? t.def.label;
                float hours = (float)t.DurationTicks / RimMindTime.TicksPerHour;
                sb.AppendLine(Tr("RimMind.Dialogue.Context.ThoughtRemaining", desc, $"{hours:F1}"));
                any = true;
            }
            return any ? sb.ToString().TrimEnd() : null;
        }

        /// <summary>Main thread only.</summary>
        private static string? BuildRelationContext(int pawnId)
        {
            var pawn = RimMindPawnLookup.FindPawnByNumber(pawnId);
            if (pawn == null) return null;
            var recipient = RimMindDialogueService.GetActiveRecipient(pawn);
            if (recipient == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine(Tr("RimMind.Dialogue.Context.RelationHeader", recipient.Name?.ToStringShort ?? recipient.LabelShort));

            float opinion = pawn.relations?.OpinionOf(recipient) ?? 0f;
            string opinionLabel = opinion >= 20 ? Tr("RimMind.Dialogue.Context.Opinion.Friend")
                                : opinion <= -20 ? Tr("RimMind.Dialogue.Context.Opinion.Enemy")
                                : Tr("RimMind.Dialogue.Context.Opinion.Acquaintance");
            sb.AppendLine(Tr("RimMind.Dialogue.Context.OpinionLabel", opinion.ToString("+0;-0"), opinionLabel));

            float compat = pawn.relations?.CompatibilityWith(recipient) ?? 0.5f;
            string compatLabel = compat >= 0.6f ? Tr("RimMind.Dialogue.Context.Compat.High")
                               : compat <= 0.3f ? Tr("RimMind.Dialogue.Context.Compat.Low")
                               : Tr("RimMind.Dialogue.Context.Compat.Medium");
            sb.AppendLine(Tr("RimMind.Dialogue.Context.CompatLabel", $"{compat:F2}", compatLabel));

            float romance = pawn.relations?.SecondaryRomanceChanceFactor(recipient) ?? 0f;
            string romanceLabel = romance >= 0.5f ? Tr("RimMind.Dialogue.Context.Romance.High")
                                : romance >= 0.15f ? Tr("RimMind.Dialogue.Context.Romance.Medium")
                                : Tr("RimMind.Dialogue.Context.Romance.Low");
            sb.AppendLine(Tr("RimMind.Dialogue.Context.RomanceLabel", $"{romance:F2}", romanceLabel, Tr("RimMind.Dialogue.Context.Romance.Unlikely")));

            var directRel = pawn.relations?.DirectRelations?.FirstOrDefault(r => r != null && r.otherPawn == recipient);
            if (directRel?.def != null)
                sb.AppendLine(Tr("RimMind.Dialogue.Context.DirectRelation", directRel.def.label));

            return sb.ToString().TrimEnd();
        }

        /// <summary>Main thread only.</summary>
        private static string? BuildHistoryContext(int pawnId)
        {
            var pawn = RimMindPawnLookup.FindPawnByNumber(pawnId);
            if (pawn == null) return null;

            var recipient = RimMindDialogueService.GetActiveRecipient(pawn);
            int? recipientId = recipient?.thingIDNumber;

            var history = RimMindDialogueService.GetDialogueHistory(pawn.thingIDNumber, 10);
            if (history == null || history.Count == 0) return null;

            List<DialogueLogEntry> relevant;
            if (recipientId.HasValue && recipientId.Value > 0)
            {
                relevant = history
                    .Where(e => (e.initiatorId == pawn.thingIDNumber && e.recipientId == recipientId.Value) ||
                                (e.initiatorId == recipientId.Value && e.recipientId == pawn.thingIDNumber))
                    .Take(3)
                    .ToList();
                if (relevant.Count == 0)
                {
                    relevant = history.Take(2).ToList();
                }
            }
            else
            {
                relevant = history.Take(3).ToList();
            }

            if (relevant.Count == 0) return null;

            relevant.Reverse();

            var sb = new StringBuilder();
            sb.AppendLine(RimMindText.TrOr(
                "RimMind.Dialogue.Context.HistoryHeader",
                "【Recent Dialogue History】(Strictly forbidden to mechanically repeat the same complaint or phrase. Expand on new work details or mutual care):"));

            foreach (var entry in relevant)
            {
                string toWho = !string.IsNullOrEmpty(entry.recipientName) ? $" -> {entry.recipientName}" : "";
                sb.AppendLine($"- {entry.initiatorName}{toWho}: \"{entry.reply}\"");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
