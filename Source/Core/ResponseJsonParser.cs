using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RimMind.Domain.ValueObjects;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class ResponseJsonParser
    {
        public static void TryParseResponseJson(string? rawResponse, bool isMonologue,
            ref string replyText, ref string? thoughtTag, ref string? thoughtDesc, ref int relationDelta)
        {
            if (rawResponse is null || rawResponse.Length == 0 || !rawResponse.TrimStart().StartsWith("{")) return;
            try
            {
                var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(rawResponse);
                if (obj == null) return;

                if (obj.TryGetValue("reply", out var replyObj) && replyObj is string replyStr && !string.IsNullOrEmpty(replyStr))
                    replyText = replyStr;

                if (obj.TryGetValue("thought", out var thoughtObj) && thoughtObj is Newtonsoft.Json.Linq.JObject thoughtJObj)
                {
                    thoughtTag = thoughtJObj.Value<string>("tag");
                    thoughtDesc = thoughtJObj.Value<string>("description");
                }

                if (!isMonologue && obj.TryGetValue("relation_delta", out var relObj))
                {
                    if (relObj is long relLong) relationDelta = (int)relLong;
                    else if (relObj is int relInt) relationDelta = relInt;
                }
            }
            catch (Exception ex)
            {
                RimMindErrors.Warn($"[RimMind] TryParseResponseJson failed: {ex.Message}");
            }
        }
    }
}
