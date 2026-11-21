using System;
using Newtonsoft.Json.Linq;
using RimMind.Presentation.Api;

namespace RimMind.Dialogue.Core
{
    /// <summary>
    /// Legacy compatibility forwarder kept for binary/test compatibility.
    /// Delegates JSON extraction and repair to Core <see cref="RimMindAPI.Json"/>.
    /// </summary>
    [Obsolete("Use DialogueToolParser instead. RimMind mainline dialogue is now unified under native express_dialogue tool calls.")]
    public static class ResponseJsonParser
    {
        private static readonly string[] ReplyFieldNames =
        {
            "reply", "narration", "dialogue", "speech", "text", "content", "message"
        };

        public static void TryParseResponseJson(string? rawResponse, bool isMonologue,
            ref string replyText, ref string? thoughtTag, ref string? thoughtDesc, ref int relationDelta)
        {
            if (string.IsNullOrWhiteSpace(rawResponse))
                return;

            if (!RimMindAPI.Json.TryParseJObjectWithRepair(rawResponse, out var obj) || obj == null)
                return;

            foreach (var field in ReplyFieldNames)
            {
                if (obj.TryGetValue(field, StringComparison.OrdinalIgnoreCase, out var token)
                    && token.Type == JTokenType.String)
                {
                    string? str = token.Value<string>();
                    if (!string.IsNullOrEmpty(str))
                    {
                        replyText = str!;
                        break;
                    }
                }
            }

            if (obj.TryGetValue("thought", StringComparison.OrdinalIgnoreCase, out var thoughtToken))
            {
                if (thoughtToken is JObject thoughtJObj)
                {
                    thoughtTag = thoughtJObj.Value<string>("tag");
                    thoughtDesc = thoughtJObj.Value<string>("description");
                }
                else if (thoughtToken.Type == JTokenType.String)
                {
                    string? tagStr = thoughtToken.Value<string>();
                    if (!string.IsNullOrEmpty(tagStr))
                        thoughtTag = tagStr;
                }
            }

            if (!isMonologue
                && obj.TryGetValue("relation_delta", StringComparison.OrdinalIgnoreCase, out var relToken)
                && relToken.Type == JTokenType.Integer)
            {
                relationDelta = relToken.Value<int>();
            }
        }
    }
}
