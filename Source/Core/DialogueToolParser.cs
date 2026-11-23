using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;

namespace RimMind.Dialogue.Core
{
    public sealed class DialogueOutput
    {
        public string Speech { get; set; } = string.Empty;
        public string? ThoughtTag { get; set; }
        public string? ThoughtDesc { get; set; }
        public int RelationDelta { get; set; }
    }

    public static class DialogueToolParser
    {
        private const int MaxRelationDelta = 5;
        private const int MinRelationDelta = -5;

        // Dialogue-specific malformed pseudo-call quirk: express_dialogue(speech)=(...)
        private static readonly Regex MalformedParenAssignRegex = new Regex(
            @"\b[a-z_][a-z0-9_]*\s*\(\s*(?<key>speech|reply|dialogue|content|text|narration)\s*\)\s*[:=]\s*\((?<val>[\s\S]+)\)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool TryParse(string? toolCallsJson, bool isMonologue, out DialogueOutput output)
        {
            output = new DialogueOutput();
            if (string.IsNullOrWhiteSpace(toolCallsJson))
                return false;

            try
            {
                string trimmed = toolCallsJson!.Trim();
                // Avoid running outer code-fence regex across already-framed JSON objects/arrays,
                // because tool arguments may legally contain escaped ```json ... ``` blocks inside string fields.
                bool startsLikeJson = trimmed.StartsWith("[", StringComparison.Ordinal)
                    || trimmed.StartsWith("{", StringComparison.Ordinal);
                string cleaned = startsLikeJson
                    ? trimmed
                    : RimMindAPI.Json.StripThinkingAndCodeFences(trimmed);
                if (string.IsNullOrWhiteSpace(cleaned))
                    return false;

                JObject? targetArgsObj = null;

                if (startsLikeJson)
                {
                    if (cleaned.StartsWith("[", StringComparison.Ordinal))
                    {
                        targetArgsObj = ExtractArgumentsFromJsonArray(cleaned);
                    }
                    else if (RimMindAPI.Json.TryParseJObjectWithRepair(cleaned, out var directObj) && directObj != null)
                    {
                        targetArgsObj = ExtractArguments(directObj);
                    }
                }

                if (targetArgsObj == null)
                {
                    string? payload = RimMindAPI.Json.ExtractJsonPayload(cleaned);
                    if (!string.IsNullOrWhiteSpace(payload))
                    {
                        if (payload!.StartsWith("[", StringComparison.Ordinal))
                        {
                            targetArgsObj = ExtractArgumentsFromJsonArray(payload);
                        }
                        else if (RimMindAPI.Json.TryParseJObjectWithRepair(payload, out var singleObj) && singleObj != null)
                        {
                            targetArgsObj = ExtractArguments(singleObj);
                        }
                    }
                }

                if (targetArgsObj == null && TryParsePseudoFunctionCall(cleaned, out var pseudoArgs))
                {
                    targetArgsObj = pseudoArgs;
                }

                if (targetArgsObj == null)
                    return false;

                string? speech = GetString(targetArgsObj, "speech", "reply", "dialogue", "content", "text", "narration");
                if (string.IsNullOrWhiteSpace(speech))
                    return false;

                output.Speech = speech!.Trim();
                if (output.Speech.StartsWith("{", StringComparison.Ordinal)
                    && output.Speech.EndsWith("}", StringComparison.Ordinal)
                    && RimMindAPI.Json.TryParseJObjectWithRepair(output.Speech, out var inner)
                    && inner != null)
                {
                    string? innerSpeech = GetString(inner, "speech", "reply", "dialogue", "content", "text", "narration");
                    if (!string.IsNullOrWhiteSpace(innerSpeech))
                    {
                        output.Speech = innerSpeech!.Trim();
                        if (inner.TryGetValue("thought", StringComparison.OrdinalIgnoreCase, out var thoughtToken) && thoughtToken is JObject thoughtObj)
                        {
                            output.ThoughtTag ??= GetString(thoughtObj, "tag", "thought_tag");
                            output.ThoughtDesc ??= GetString(thoughtObj, "description", "desc", "thought_desc");
                        }
                        output.ThoughtTag ??= GetString(inner, "thought_tag", "tag", "thought");
                        output.ThoughtDesc ??= GetString(inner, "thought_desc", "description", "desc");
                        if (!isMonologue && output.RelationDelta == 0)
                        {
                            int d = GetInt(inner, "relation_delta", "delta", "relation");
                            output.RelationDelta = Math.Clamp(d, MinRelationDelta, MaxRelationDelta);
                        }
                    }
                }

                output.ThoughtTag ??= GetString(targetArgsObj, "thought_tag", "tag", "thought");
                output.ThoughtDesc ??= GetString(targetArgsObj, "thought_desc", "description", "desc");

                if (!string.IsNullOrWhiteSpace(output.ThoughtTag))
                {
                    string rawTag = output.ThoughtTag!.Trim();
                    if (rawTag.Length > 15 || rawTag.Contains("，") || rawTag.Contains("。") || rawTag.Contains(",") || rawTag.Contains(" "))
                    {
                        if (string.IsNullOrWhiteSpace(output.ThoughtDesc))
                        {
                            output.ThoughtDesc = rawTag;
                        }
                        output.ThoughtTag = DialogueTextSanitizer.ClassifyThoughtTag(rawTag);
                    }
                }

                if (!isMonologue && output.RelationDelta == 0)
                {
                    int delta = GetInt(targetArgsObj, "relation_delta", "delta", "relation");
                    output.RelationDelta = Math.Clamp(delta, MinRelationDelta, MaxRelationDelta);
                }
                else if (isMonologue)
                {
                    output.RelationDelta = 0;
                }

                return true;
            }
            catch (Exception ex)
            {
                RimMindErrors.Warn($"[RimMind-Dialogue] DialogueToolParser.TryParse failed: {ex.Message}");
                return false;
            }
        }

        private static JObject? ExtractArgumentsFromJsonArray(string arrayJson)
        {
            JArray? arr = null;
            try
            {
                arr = JToken.Parse(arrayJson) as JArray;
            }
            catch
            {
                string? repaired = RimMindAPI.Json.TryRepairJson(arrayJson);
                if (!string.IsNullOrWhiteSpace(repaired))
                {
                    try { arr = JToken.Parse(repaired!) as JArray; }
                    catch { arr = null; }
                }
            }

            if (arr == null)
                return null;

            foreach (var item in arr)
            {
                if (item is JObject obj)
                {
                    string? name = GetToolName(obj);
                    if (string.Equals(name, DialogueToolDefinitions.ExpressDialogueToolName, StringComparison.OrdinalIgnoreCase))
                    {
                        var args = ExtractArguments(obj);
                        if (args != null) return args;
                    }
                }
            }

            foreach (var item in arr)
            {
                if (item is JObject obj)
                {
                    var args = ExtractArguments(obj);
                    if (args != null) return args;
                }
            }

            return null;
        }

        private static string? GetToolName(JObject obj)
        {
            if (obj.TryGetValue("name", StringComparison.OrdinalIgnoreCase, out var nameToken) && nameToken.Type == JTokenType.String)
                return nameToken.Value<string>();

            if (obj.TryGetValue("function", StringComparison.OrdinalIgnoreCase, out var fnToken) && fnToken is JObject fnObj)
            {
                if (fnObj.TryGetValue("name", StringComparison.OrdinalIgnoreCase, out var fnNameToken) && fnNameToken.Type == JTokenType.String)
                    return fnNameToken.Value<string>();
            }

            return null;
        }

        private static JObject? ExtractArguments(JObject obj)
        {
            JToken? argsToken = null;
            if (obj.TryGetValue("arguments", StringComparison.OrdinalIgnoreCase, out var directArgs))
            {
                argsToken = directArgs;
            }
            else if (obj.TryGetValue("parameters", StringComparison.OrdinalIgnoreCase, out var directParams))
            {
                argsToken = directParams;
            }
            else if (obj.TryGetValue("function", StringComparison.OrdinalIgnoreCase, out var fnToken) && fnToken is JObject fnObj)
            {
                if (fnObj.TryGetValue("arguments", StringComparison.OrdinalIgnoreCase, out var nestedArgs))
                {
                    argsToken = nestedArgs;
                }
            }

            if (argsToken == null)
            {
                if (obj.ContainsKey("speech") || obj.ContainsKey("reply") || obj.ContainsKey("narration") || obj.ContainsKey("dialogue"))
                    return obj;
                return null;
            }

            if (argsToken.Type == JTokenType.Object && argsToken is JObject argsJObj)
            {
                return argsJObj;
            }

            if (argsToken.Type == JTokenType.String)
            {
                string rawArgs = argsToken.Value<string>() ?? string.Empty;
                if (RimMindAPI.Json.TryParseJObjectWithRepair(rawArgs, out var parsed) && parsed != null)
                    return parsed;
            }

            return null;
        }

        private static string? GetString(JObject jobj, params string[] candidateKeys)
        {
            foreach (var key in candidateKeys)
            {
                if (jobj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token))
                {
                    if (token.Type == JTokenType.String)
                    {
                        string? val = token.Value<string>();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                    else if (token.Type == JTokenType.Object && token is JObject nestedObj)
                    {
                        string? nestedVal = GetString(nestedObj, candidateKeys);
                        if (!string.IsNullOrWhiteSpace(nestedVal)) return nestedVal;
                    }
                }
            }
            return null;
        }

        private static int GetInt(JObject jobj, params string[] candidateKeys)
        {
            foreach (var key in candidateKeys)
            {
                if (jobj.TryGetValue(key, StringComparison.OrdinalIgnoreCase, out var token))
                {
                    if (token.Type == JTokenType.Integer)
                    {
                        return token.Value<int>();
                    }
                    if (token.Type == JTokenType.String && int.TryParse(token.Value<string>(), out int parsed))
                    {
                        return parsed;
                    }
                }
            }
            return 0;
        }

        internal static bool TryParsePseudoFunctionCall(string text, out JObject? argsObj)
        {
            argsObj = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            // Primary path: delegate to Core's unified pseudo-function-call parser.
            var calls = RimMindAPI.Json.ParsePseudoFunctionCalls(text);
            if (calls != null && calls.Count > 0)
            {
                foreach (var call in calls)
                {
                    if (string.Equals(call.Name, DialogueToolDefinitions.ExpressDialogueToolName, StringComparison.OrdinalIgnoreCase)
                        && RimMindAPI.Json.TryParseJObjectWithRepair(call.ArgumentsJson, out var expressObj)
                        && expressObj != null)
                    {
                        argsObj = expressObj;
                        return true;
                    }
                }

                foreach (var call in calls)
                {
                    if (RimMindAPI.Json.TryParseJObjectWithRepair(call.ArgumentsJson, out var parsed)
                        && parsed != null
                        && GetString(parsed, "speech", "reply", "dialogue", "content", "text", "narration") != null)
                    {
                        argsObj = parsed;
                        return true;
                    }
                }
            }

            // Narrow Dialogue-specific quirk fallback: express_dialogue(speech)=(...)
            var malformed = MalformedParenAssignRegex.Match(text.Trim());
            if (malformed.Success)
            {
                argsObj = new JObject
                {
                    [malformed.Groups["key"].Value] = malformed.Groups["val"].Value.Trim()
                };
                return true;
            }

            return false;
        }
    }
}
