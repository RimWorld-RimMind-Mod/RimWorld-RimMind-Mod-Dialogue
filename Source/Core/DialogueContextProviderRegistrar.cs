using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        internal static void RegisterAll()
        {
            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_state", ContextLayer.L3_State, 0.2f,
                async (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return null;
                    var pawn = RimMindPawnLookup.FindPawnByNumber(ctx.PawnId);
                    if (pawn == null) return null;
                    RimWorld.Thought_Memory[] memories;
                    try
                    {
                        var raw = pawn.needs?.mood?.thoughts?.memories?.Memories;
                        if (raw == null || raw.Count == 0) return null;
                        memories = raw.ToArray();
                    }
                    catch
                    {
                        return null;
                    }

                    var sb = new StringBuilder(Tr("RimMind.Dialogue.Context.StateHeader"));
                    bool any = false;
                    foreach (var t in memories)
                    {
                        if (t?.def == null || t.def.defName != "RimMindDialogue_Thought") continue;

                        string desc = (t as Thought_RimMindDialogue)?.aiDescription ?? t.def.label;
                        float hours = (float)t.DurationTicks / RimMindTime.TicksPerHour;
                        sb.AppendLine(Tr("RimMind.Dialogue.Context.ThoughtRemaining", desc, $"{hours:F1}"));
                        any = true;
                    }
                    return any ? sb.ToString().TrimEnd() : null;
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 750, invalidationTriggers: new[] { "DialogueEvent" }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_relation", ContextLayer.L3_State, 0.15f,
                async (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return null;
                    var pawn = RimMindPawnLookup.FindPawnByNumber(ctx.PawnId);
                    if (pawn == null) return null;
                    var recipient = RimMindDialogueService.GetActiveRecipient(pawn);
                    if (recipient == null) return null;

                    var sb = new StringBuilder(Tr("RimMind.Dialogue.Context.RelationHeader", recipient.Name?.ToStringShort ?? recipient.LabelShort));

                    float opinion = 0f;
                    try
                    {
                        if (UnityData.IsInMainThread)
                        {
                            opinion = pawn.relations?.OpinionOf(recipient) ?? 0f;
                        }
                        else
                        {
                            var mems = pawn.needs?.mood?.thoughts?.memories?.Memories?.ToArray();
                            if (mems != null)
                            {
                                foreach (var m in mems)
                                {
                                    if (m is RimWorld.ISocialThought st && m.otherPawn == recipient)
                                    {
                                        opinion += st.OpinionOffset();
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        opinion = 0f;
                    }
                    string opinionLabel = opinion >= 20 ? Tr("RimMind.Dialogue.Context.Opinion.Friend")
                                        : opinion <= -20 ? Tr("RimMind.Dialogue.Context.Opinion.Enemy")
                                        : Tr("RimMind.Dialogue.Context.Opinion.Acquaintance");
                    sb.AppendLine(Tr("RimMind.Dialogue.Context.OpinionLabel", opinion.ToString("+0;-0"), opinionLabel));

                    float compat = 0.5f;
                    try
                    {
                        compat = pawn.relations?.CompatibilityWith(recipient) ?? 0.5f;
                    }
                    catch
                    {
                        compat = 0.5f;
                    }
                    string compatLabel = compat >= 0.6f ? Tr("RimMind.Dialogue.Context.Compat.High")
                                       : compat <= 0.3f ? Tr("RimMind.Dialogue.Context.Compat.Low")
                                       : Tr("RimMind.Dialogue.Context.Compat.Medium");
                    sb.AppendLine(Tr("RimMind.Dialogue.Context.CompatLabel", $"{compat:F2}", compatLabel));

                    float romance = 0f;
                    try
                    {
                        if (UnityData.IsInMainThread)
                            romance = pawn.relations?.SecondaryRomanceChanceFactor(recipient) ?? 0f;
                    }
                    catch
                    {
                        romance = 0f;
                    }
                    string romanceLabel = romance >= 0.5f ? Tr("RimMind.Dialogue.Context.Romance.High")
                                        : romance >= 0.15f ? Tr("RimMind.Dialogue.Context.Romance.Medium")
                                        : Tr("RimMind.Dialogue.Context.Romance.Low");
                    sb.AppendLine(Tr("RimMind.Dialogue.Context.RomanceLabel", $"{romance:F2}", romanceLabel, Tr("RimMind.Dialogue.Context.Romance.Unlikely")));

                    try
                    {
                        var directRel = pawn.relations?.DirectRelations?.ToArray()?.FirstOrDefault(r => r != null && r.otherPawn == recipient);
                        if (directRel?.def != null)
                            sb.AppendLine(Tr("RimMind.Dialogue.Context.DirectRelation", directRel.def.label));
                    }
                    catch
                    {
                        // Ignore concurrent modification
                    }

                    return sb.ToString().TrimEnd();
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 750, invalidationTriggers: new[] { "DialogueEvent" }));

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
                    subKeys.Add(isMonologue ? "ToolCallMonologue" : "ToolCallDialogue");
                    return RimMindAPI.Prompt.BuildTaskInstruction("RimMind.Dialogue.Prompt.TaskInstruction", null, subKeys.ToArray());
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 0, invalidationTriggers: new[] { "DialogueEvent" }));

            RimMindAPI.Context.ContextKeys.Register(new ContextProviderDef(
                "dialogue_history", ContextLayer.L4_History, 0.5f,
                async (ctx, ct) =>
                {
                    if (ctx.PawnId <= 0) return null;
                    var pawn = RimMindPawnLookup.FindPawnByNumber(ctx.PawnId);
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
                }, RimMindOwnerConsts.DialogueModId, stalenessTicks: 0, invalidationTriggers: new[] { "DialogueEvent" }));
        }
    }
}
