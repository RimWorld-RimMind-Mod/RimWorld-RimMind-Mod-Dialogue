using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RimMind.Domain.ValueObjects;

namespace RimMind.Dialogue.Core
{
    public static class ResponseJsonParser
    {
        private static readonly string[] ReplyFieldNames =
        {
            "reply", "narration", "dialogue", "speech", "text", "content", "message"
        };

        public static void TryParseResponseJson(string? rawResponse, bool isMonologue,
            ref string replyText, ref string? thoughtTag, ref string? thoughtDesc, ref int relationDelta)
        {
            if (string.IsNullOrWhiteSpace(rawResponse)) return;

            string cleaned = rawResponse!.Trim();
            if (cleaned.StartsWith("```"))
            {
                int firstNewline = cleaned.IndexOf('\n');
                if (firstNewline >= 0)
                    cleaned = cleaned.Substring(firstNewline + 1);
                if (cleaned.EndsWith("```"))
                    cleaned = cleaned.Substring(0, cleaned.Length - 3);
                cleaned = cleaned.Trim();
            }

            int firstBrace = cleaned.IndexOf('{');
            int lastBrace = cleaned.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
            }
            else
            {
                return;
            }

            bool extractedReply = false;
            try
            {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(cleaned);
                if (obj != null)
                {
                    foreach (var field in ReplyFieldNames)
                    {
                        if (obj.TryGetValue(field, out var val) && val is string str && !string.IsNullOrEmpty(str))
                        {
                            replyText = str;
                            extractedReply = true;
                            break;
                        }
                    }

                    if (obj.TryGetValue("thought", out var thoughtObj))
                    {
                        if (thoughtObj is JObject thoughtJObj)
                        {
                            thoughtTag = thoughtJObj.Value<string>("tag");
                            thoughtDesc = thoughtJObj.Value<string>("description");
                        }
                        else if (thoughtObj is string tagStr && !string.IsNullOrEmpty(tagStr))
                        {
                            thoughtTag = tagStr;
                        }
                    }

                    if (!isMonologue && obj.TryGetValue("relation_delta", out var relObj))
                    {
                        if (relObj is long relLong) relationDelta = (int)relLong;
                        else if (relObj is int relInt) relationDelta = relInt;
                    }
                }
            }
            catch (Exception ex)
            {
                RimMindErrors.Warn($"[RimMind] TryParseResponseJson failed: {ex.Message}");
            }

            // Fallback regex extraction if JSON parse or fields failed
            if (!extractedReply)
            {
                try
                {
                    var match = Regex.Match(
                        cleaned,
                        "\"(?:reply|narration|dialogue|speech|text|content|message)\"\\s*:\\s*\"((?:\\\\\"|[^\"])+)\"",
                        RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        replyText = Regex.Unescape(match.Groups[1].Value);
                    }
                }
                catch
                {
                    // Ignore regex errors
                }
            }
        }
    }
}
