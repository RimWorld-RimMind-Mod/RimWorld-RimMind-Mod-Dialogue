using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RimMind.Application.Common.Constants;
using RimMind.Application.Common.Interfaces.Context;
using RimMind.Domain.Common;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;
using Verse;

namespace RimMind.Dialogue.Core
{
    internal static class DialogueContextProviderRegistrar
    {
        private static string Tr(string key, params object[] args)
        {
            try
            {
                if (string.IsNullOrEmpty(key)) return string.Empty;
                string template = key.CanTranslate() ? key.TranslateSimple() : key;
                return (args == null || args.Length == 0) ? template : string.Format(template, args);
            }
            catch { return key ?? string.Empty; }
        }

        /// <summary>
        /// Context providers run on pipeline thread-pool threads; pawn memories, relations, dialogue
        /// history and translations are read inside this main-thread snapshot.
        /// </summary>
        private static Task<string?> OnMainThread(Func<string?> build, CancellationToken ct)
            => RimMindAPI.MainThreadSnapshot.GetAsync(build, null, ct: ct);

        internal static void RegisterAll()
        {
            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_state", ContextLayer.L3_State, 0.2f,
                (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return Task.FromResult<string?>(null);
                    int pawnId = ctx.PawnId;
                    return OnMainThread(() => BuildStateContext(pawnId), ct);
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 750, invalidationTriggers: new[] { "DialogueEvent" }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_relation", ContextLayer.L3_State, 0.15f,
                (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return Task.FromResult<string?>(null);
                    int pawnId = ctx.PawnId;
                    return OnMainThread(() => BuildRelationContext(pawnId), ct);
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 750, invalidationTriggers: new[] { "DialogueEvent" }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_task", ContextLayer.L0_Static, 0.95f,
                (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return Task.FromResult<string?>(null);
                    if (ctx.Scenario != RimMindAPI.Context.ScenarioDialogue) return Task.FromResult<string?>(null);
                    string? speakerName = ctx.Hints?.TryGetValue("SpeakerName", out var sn) == true ? sn?.ToString() : null;
                    if (!string.IsNullOrEmpty(speakerName)) return Task.FromResult<string?>(null);
                    bool isMonologue = ctx.Hints?.TryGetValue("IsMonologue", out var im) == true && im is bool b && b;
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
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 0, invalidationTriggers: new[] { "DialogueEvent" }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_history", ContextLayer.L4_History, 0.5f,
                (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return Task.FromResult<string?>(null);
                    int pawnId = ctx.PawnId;
                    return OnMainThread(() => BuildHistoryContext(pawnId), ct);
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 0, invalidationTriggers: new[] { "DialogueEvent" }));
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
            sb.AppendLine("RimMind.Dialogue.Context.HistoryHeader".CanTranslate()
                ? "RimMind.Dialogue.Context.HistoryHeader".Translate().ToString()
                : "【Recent Dialogue History】(Strictly forbidden to mechanically repeat the same complaint or phrase. Expand on new work details or mutual care):");

            foreach (var entry in relevant)
            {
                string toWho = !string.IsNullOrEmpty(entry.recipientName) ? $" -> {entry.recipientName}" : "";
                sb.AppendLine($"- {entry.initiatorName}{toWho}: \"{entry.reply}\"");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
