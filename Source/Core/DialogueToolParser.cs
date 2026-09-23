using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimMind.Domain.Llm;
using RimMind.Domain.ValueObjects;

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

        public static bool TryParse(string? toolCallsJson, bool isMonologue, out DialogueOutput output)
        {
            output = new DialogueOutput();
            if (string.IsNullOrWhiteSpace(toolCallsJson))
                return false;

            try
            {
                string cleaned = toolCallsJson!.Trim();
                if (cleaned.StartsWith("```"))
                {
                    int firstNewline = cleaned.IndexOf('\n');
                    if (firstNewline >= 0)
                        cleaned = cleaned.Substring(firstNewline + 1);
                    if (cleaned.EndsWith("```"))
                        cleaned = cleaned.Substring(0, cleaned.Length - 3);
                    cleaned = cleaned.Trim();
                }

                JToken token = JToken.Parse(cleaned);
                JObject? targetArgsObj = null;

                if (token is JArray arr)
                {
                    foreach (var item in arr)
                    {
                        if (item is JObject obj)
                        {
                            string? name = GetToolName(obj);
                            if (string.Equals(name, DialogueToolDefinitions.ExpressDialogueToolName, StringComparison.OrdinalIgnoreCase))
                            {
                                targetArgsObj = ExtractArguments(obj);
                                if (targetArgsObj != null) break;
                            }
                        }
                    }

                    if (targetArgsObj == null)
                    {
                        foreach (var item in arr)
                        {
                            if (item is JObject obj)
                            {
                                targetArgsObj = ExtractArguments(obj);
                                if (targetArgsObj != null) break;
                            }
                        }
                    }
                }
                else if (token is JObject singleObj)
                {
                    targetArgsObj = ExtractArguments(singleObj);
                }

                if (targetArgsObj == null)
                    return false;

                string? speech = GetString(targetArgsObj, "speech", "reply", "dialogue", "content", "text", "narration");
                if (string.IsNullOrWhiteSpace(speech))
                    return false;

                output.Speech = speech!.Trim();
                if (output.Speech.StartsWith("{") && output.Speech.EndsWith("}"))
                {
                    try
                    {
                        var inner = JObject.Parse(output.Speech);
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
                    catch { }
                }

                output.ThoughtTag ??= GetString(targetArgsObj, "thought_tag", "tag", "thought");
                output.ThoughtDesc ??= GetString(targetArgsObj, "thought_desc", "description", "desc");

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
            else if (obj.TryGetValue("function", StringComparison.OrdinalIgnoreCase, out var fnToken) && fnToken is JObject fnObj)
            {
                if (fnObj.TryGetValue("arguments", StringComparison.OrdinalIgnoreCase, out var nestedArgs))
                {
                    argsToken = nestedArgs;
                }
            }

            if (argsToken == null)
            {
                if (obj.ContainsKey("speech") || obj.ContainsKey("reply") || obj.ContainsKey("narration"))
                    return obj;
                return null;
            }

            if (argsToken.Type == JTokenType.Object && argsToken is JObject argsJObj)
            {
                return argsJObj;
            }

            if (argsToken.Type == JTokenType.String)
            {
                string rawArgs = argsToken.Value<string>()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rawArgs)) return null;
                if (rawArgs.StartsWith("```"))
                {
                    int firstNewline = rawArgs.IndexOf('\n');
                    if (firstNewline >= 0)
                        rawArgs = rawArgs.Substring(firstNewline + 1);
                    if (rawArgs.EndsWith("```"))
                        rawArgs = rawArgs.Substring(0, rawArgs.Length - 3);
                    rawArgs = rawArgs.Trim();
                }
                return JObject.Parse(rawArgs);
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
    }
}
