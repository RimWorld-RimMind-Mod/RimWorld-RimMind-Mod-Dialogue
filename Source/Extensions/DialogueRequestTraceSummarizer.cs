using System;
using System.Text.RegularExpressions;
using RimMind.Application.Common.Constants;
using RimMind.Application.Common.Interfaces.Diagnostics;
using RimMind.Application.Common.Models.Debug;
using RimMind.Dialogue.Core;
using RimMind.Presentation.Api;

namespace RimMind.Dialogue
{
    /// <summary>
    /// Summarizes Dialogue tool calls, prompts, and responses for Core's trace UI
    /// without Core hardcoding Dialogue tool names or translation keys.
    /// </summary>
    internal sealed class DialogueRequestTraceSummarizer : IRequestTraceSummarizer
    {
        private const string DialogueModId = "RimMind.Dialogue";
        private const string AutoTriggerFallback = "Based on your current state and surroundings, respond naturally.";

        private static readonly string[] SuppressedTools =
        {
            DialogueToolDefinitions.ExpressDialogueToolName
        };

        private static readonly Regex LayerTagRegex = new Regex(@"\[L\d+\]\s*", RegexOptions.Compiled);
        private static readonly Regex XmlTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

        public string Id => "dialogue.trace";
        public string OwnerModId => RimMindOwnerConsts.DialogueModId;
        public int Order => 100;

        public bool ShouldSuppressToolCall(string toolName)
            => string.Equals(toolName, DialogueToolDefinitions.ExpressDialogueToolName, StringComparison.OrdinalIgnoreCase);

        public bool TrySummarizeResponse(AIRequestTraceEntry entry, out RequestTraceSummary? summary)
        {
            summary = null;
            if (entry == null)
                return false;

            AIRequestToolCallTrace? expressCall = FindExpressDialogueCall(entry);
            bool isDialogueRequest = expressCall != null
                || string.Equals(entry.ModId, DialogueModId, StringComparison.OrdinalIgnoreCase);
            if (!isDialogueRequest)
                return false;

            DialogueOutput? parsed = null;
            if (expressCall != null && !string.IsNullOrWhiteSpace(expressCall.ArgumentsJson)
                && DialogueToolParser.TryParse(expressCall.ArgumentsJson, isMonologue: false, out var fromArgs))
            {
                parsed = fromArgs;
            }
            else if (!string.IsNullOrWhiteSpace(entry.Response)
                && DialogueToolParser.TryParse(entry.Response, isMonologue: false, out var fromResponse))
            {
                parsed = fromResponse;
            }

            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Speech))
                return false;

            string speech = "\"" + ToSingleLine(parsed.Speech) + "\"";
            if (!string.IsNullOrWhiteSpace(parsed.ThoughtDesc))
            {
                speech += " (" + ToSingleLine(parsed.ThoughtDesc) + ")";
            }
            else if (!string.IsNullOrWhiteSpace(parsed.ThoughtTag)
                && !string.Equals(parsed.ThoughtTag, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                string label = ThoughtInjector.MapTagToLabel(parsed.ThoughtTag!);
                if (!string.IsNullOrWhiteSpace(label))
                    speech += " (" + ToSingleLine(label) + ")";
            }

            summary = new RequestTraceSummary(speech, SuppressedTools);
            return true;
        }

        public bool TrySummarizePrompt(string rawPrompt, out string? summary)
        {
            summary = null;
            if (string.IsNullOrWhiteSpace(rawPrompt))
                return false;

            string localizedAutoTrigger = RimMindAPI.Text.TrOr(
                "RimMind.Dialogue.Prompt.AutoTrigger",
                AutoTriggerFallback);

            bool isDialogueTrigger = rawPrompt.IndexOf("dialogue_trigger", StringComparison.OrdinalIgnoreCase) >= 0
                || rawPrompt.IndexOf(AutoTriggerFallback, StringComparison.OrdinalIgnoreCase) >= 0
                || (!string.IsNullOrEmpty(localizedAutoTrigger)
                    && rawPrompt.IndexOf(localizedAutoTrigger, StringComparison.Ordinal) >= 0);

            if (!isDialogueTrigger)
                return false;

            string cleaned = LayerTagRegex.Replace(rawPrompt, string.Empty);
            cleaned = XmlTagRegex.Replace(cleaned, string.Empty);
            if (!string.IsNullOrEmpty(localizedAutoTrigger))
                cleaned = cleaned.Replace(localizedAutoTrigger, string.Empty);
            cleaned = cleaned.Replace(AutoTriggerFallback, string.Empty);
            cleaned = ToSingleLine(cleaned);

            summary = string.IsNullOrWhiteSpace(cleaned)
                ? RimMindAPI.Text.TrOr("RimMind.UI.AgentsPage.Trace.DailyReaction", "Daily reaction")
                : cleaned;
            return true;
        }

        private AIRequestToolCallTrace? FindExpressDialogueCall(AIRequestTraceEntry entry)
        {
            for (int i = 0; i < entry.ToolCalls.Count; i++)
            {
                var call = entry.ToolCalls[i];
                if (call != null && ShouldSuppressToolCall(call.ToolName))
                    return call;
            }
            return null;
        }

        private static string ToSingleLine(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return WhitespaceRegex.Replace(text!, " ").Trim();
        }
    }
}
