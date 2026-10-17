using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace RimMind.Dialogue.Core
{
    /// <summary>
    /// Sanitizes dialogue text to prevent LLM stage-direction mirroring and clean formatting glitches.
    /// </summary>
    public static class DialogueTextSanitizer
    {
        // Matches a parenthetical stage direction at the beginning of the text,
        // supporting ASCII () and full-width （）.
        private static readonly Regex LeadingStageDirectionRegex = new Regex(
            @"^\s*(\((?<inner>[^)]+)\)|（(?<inner>[^）]+)）)",
            RegexOptions.Compiled);

        // Matches any parenthetical stage direction in a string.
        private static readonly Regex AnyStageDirectionRegex = new Regex(
            @"(\((?<inner>[^)]+)\)|（(?<inner>[^）]+)）)",
            RegexOptions.Compiled);

        // Punctuation and whitespace to trim after stripping a leading stage direction.
        private static readonly char[] LeadingSeparators = new[]
        {
            ' ', '\t', '\r', '\n', ',', '，', ':', '：', ';', '；', '-', '—', '·', '。', '.'
        };

        /// <summary>
        /// Sanitizes reply text by:
        /// 1. Stripping leading parenthetical stage directions if they mirror the immediately preceding dialogue.
        /// 2. Cleaning trailing formatting glitches (unmatched markdown, dangling quotes, trailing commas, etc.).
        /// </summary>
        public static string SanitizeReply(string replyText, string? precedingDialogueText)
        {
            if (string.IsNullOrWhiteSpace(replyText))
                return string.Empty;

            string text = CleanFormattingGlitches(replyText);

            // 1. Check for leading parenthetical stage direction
            var leadMatch = LeadingStageDirectionRegex.Match(text);
            if (leadMatch.Success && !string.IsNullOrWhiteSpace(precedingDialogueText))
            {
                string leadInner = leadMatch.Groups["inner"].Value;
                var prevMatch = AnyStageDirectionRegex.Match(precedingDialogueText!);
                if (prevMatch.Success)
                {
                    string prevInner = prevMatch.Groups["inner"].Value;
                    if (IsMirroredStageDirection(leadInner, prevInner))
                    {
                        string remainder = text.Substring(leadMatch.Index + leadMatch.Length);
                        remainder = remainder.TrimStart(LeadingSeparators).Trim();

                        if (!string.IsNullOrWhiteSpace(remainder))
                        {
                            if (char.IsLower(remainder[0]))
                            {
                                remainder = char.ToUpperInvariant(remainder[0]) + remainder.Substring(1);
                            }
                            text = remainder;
                        }
                    }
                }
            }

            // 2. Clean trailing formatting glitches that may remain or have been exposed
            text = CleanFormattingGlitches(text);

            return text;
        }

        /// <summary>
        /// Determines whether current stage direction mirrors the preceding stage direction.
        /// </summary>
        public static bool IsMirroredStageDirection(string currentInner, string prevInner)
        {
            if (string.IsNullOrWhiteSpace(currentInner) || string.IsNullOrWhiteSpace(prevInner))
                return false;

            string curr = currentInner.Trim();
            string prev = prevInner.Trim();

            if (curr.Equals(prev, StringComparison.OrdinalIgnoreCase))
                return true;

            string normCurr = Normalize(curr);
            string normPrev = Normalize(prev);

            if (normCurr.Length > 0 && normCurr.Equals(normPrev, StringComparison.OrdinalIgnoreCase))
                return true;

            if (normCurr.Length >= 2 && normPrev.Length >= 2)
            {
                if (normCurr.Contains(normPrev, StringComparison.OrdinalIgnoreCase) ||
                    normPrev.Contains(normCurr, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Normalize(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            string s = input.ToLowerInvariant();

            // Remove punctuation inside parenthesis: include full-width comma, period, exclamation, question, colon, semicolon, brackets
            s = Regex.Replace(s, @"[.,!?:;~…\-_\uff0c\u3002\uff01\uff1f\uff1a\uff1b\uff08\uff09\(\)]", "");

            // For Chinese: strip common aspect/modal/filler particles: \u4e86(le), \u4e00(yi), \u53e3(kou), \u4e2a(ge), \u7740(zhe), \u4e5f(ye), \u53c8(you), \u7684(de), \u5730(de), \u5f97(de), \u58f0(sheng), \u5fae(wei), \u8f7b(qing), \u6df1(shen), \u9635(zhen)
            s = Regex.Replace(s, @"[\u4e86\u4e00\u53e3\u4e2a\u7740\u4e5f\u53c8\u7684\u5730\u5f97\u58f0\u5fae\u8f7b\u6df1\u9635]", "");

            // For English: strip common verbal suffixes: ing, ed, es, s
            if (s.EndsWith("ing") && s.Length > 4)
                s = s.Substring(0, s.Length - 3);
            else if (s.EndsWith("ed") && s.Length > 3)
                s = s.Substring(0, s.Length - 2);
            else if (s.EndsWith("es") && s.Length > 3)
                s = s.Substring(0, s.Length - 2);
            else if (s.EndsWith("s") && s.Length > 2)
                s = s.Substring(0, s.Length - 1);

            return s.Trim();
        }

        /// <summary>
        /// Cleans common LLM formatting artifacts:
        /// - Strips wrapping quotes or dangling quotes/backticks
        /// - Strips markdown code blocks (```)
        /// - Replaces or trims trailing comma/colon glitches
        /// </summary>
        public static string CleanFormattingGlitches(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            string result = text.Trim();

            // Strip trailing code blocks
            while (result.EndsWith("```"))
            {
                result = result.Substring(0, result.Length - 3).TrimEnd();
            }

            // Strip leading code blocks
            if (result.StartsWith("```"))
            {
                int firstNewline = result.IndexOf('\n');
                if (firstNewline >= 0)
                    result = result.Substring(firstNewline + 1).TrimStart();
                else
                    result = result.TrimStart('`').TrimStart();
            }

            // Strip <thinking>...</thinking> blocks and residual XML wrapper tags (<speech>, </speech>, <thought>, <dialogue>, <response>, <tool_call>)
            if (result.IndexOf("<thinking>", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result = Regex.Replace(result, @"<thinking>[\s\S]*?</thinking>", string.Empty, RegexOptions.IgnoreCase).Trim();
            }
            if (result.IndexOf('<') >= 0 && result.IndexOf('>') >= 0)
            {
                result = Regex.Replace(
                    result,
                    @"</?(?:tool_call|speech|thought|dialogue|response|monologue|reply|thinking)\b[^>]*>",
                    string.Empty,
                    RegexOptions.IgnoreCase).Trim();
            }

            // Defensively extract inner speech if text contains or is wrapped in raw JSON object
            if (result.Contains("{") && result.Contains("}"))
            {
                try
                {
                    int start = result.IndexOf('{');
                    int end = result.LastIndexOf('}');
                    if (start >= 0 && end > start)
                    {
                        string jsonCandidate = result.Substring(start, end - start + 1);
                        while (jsonCandidate.StartsWith("{{") && jsonCandidate.EndsWith("}}"))
                        {
                            jsonCandidate = jsonCandidate.Substring(1, jsonCandidate.Length - 2).Trim();
                        }
                        try
                        {
                            var obj = JObject.Parse(jsonCandidate);
                            var paramsObj = obj["parameters"] as JObject ?? obj["arguments"] as JObject;
                            string? innerSpeech = obj["speech"]?.ToString()
                                               ?? obj["reply"]?.ToString()
                                               ?? obj["dialogue"]?.ToString()
                                               ?? obj["content"]?.ToString()
                                               ?? obj["text"]?.ToString()
                                               ?? paramsObj?["speech"]?.ToString()
                                               ?? paramsObj?["reply"]?.ToString()
                                               ?? paramsObj?["dialogue"]?.ToString()
                                               ?? paramsObj?["content"]?.ToString()
                                               ?? paramsObj?["text"]?.ToString();
                            if (!string.IsNullOrWhiteSpace(innerSpeech))
                            {
                                result = innerSpeech!.Trim();
                            }
                        }
                        catch
                        {
                            var m = Regex.Match(jsonCandidate, @"""(?:speech|reply|dialogue|content|text)""\s*:\s*""(?<val>(?:\\.|[^""\\])*)""");
                            if (m.Success)
                            {
                                string unescaped = Regex.Unescape(m.Groups["val"].Value);
                                if (!string.IsNullOrWhiteSpace(unescaped))
                                {
                                    result = unescaped.Trim();
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // Defensively extract inner speech if text is wrapped in pseudo-function-call syntax: express_dialogue(speech=..., thought_tag=..., ...)
            if (DialogueToolParser.TryParsePseudoFunctionCall(result, out var pseudoObj) && pseudoObj != null)
            {
                string? pseudoSpeech = pseudoObj["speech"]?.ToString()
                                    ?? pseudoObj["reply"]?.ToString()
                                    ?? pseudoObj["dialogue"]?.ToString()
                                    ?? pseudoObj["content"]?.ToString()
                                    ?? pseudoObj["text"]?.ToString();
                if (!string.IsNullOrWhiteSpace(pseudoSpeech))
                {
                    result = pseudoSpeech!.Trim();
                }
            }

            // Strip outer wrapping quotes
            if ((result.StartsWith("\"") && result.EndsWith("\"") && result.Length >= 2) ||
                (result.StartsWith("“") && result.EndsWith("”") && result.Length >= 2) ||
                (result.StartsWith("'") && result.EndsWith("'") && result.Length >= 2) ||
                (result.StartsWith("‘") && result.EndsWith("’") && result.Length >= 2))
            {
                result = result.Substring(1, result.Length - 2).Trim();
            }

            // Remove dangling trailing quotes or backticks if unmatched
            result = result.TrimEnd('`', '\\');

            if (result.EndsWith("\"") && !result.StartsWith("\""))
                result = result.Substring(0, result.Length - 1).TrimEnd();
            if (result.EndsWith("”") && !result.StartsWith("“"))
                result = result.Substring(0, result.Length - 1).TrimEnd();
            if (result.EndsWith("'") && !result.StartsWith("'"))
                result = result.Substring(0, result.Length - 1).TrimEnd();
            if (result.EndsWith("’") && !result.StartsWith("‘"))
                result = result.Substring(0, result.Length - 1).TrimEnd();

            // Clean trailing comma/colon artifacts
            if (result.EndsWith("，"))
                result = result.Substring(0, result.Length - 1).TrimEnd() + "。";
            else if (result.EndsWith(","))
                result = result.Substring(0, result.Length - 1).TrimEnd() + ".";
            else if (result.EndsWith("：") || result.EndsWith(":") || result.EndsWith("；") || result.EndsWith(";"))
                result = result.Substring(0, result.Length - 1).TrimEnd();

            return result.Trim();
        }

        /// <summary>
        /// Formats dialogue text for overhead map motes (<c>MoteMaker.ThrowText</c>) by stripping
        /// formatting glitches, capping total length, and inserting a line break near the midpoint
        /// so long lines never stretch horizontally across the entire screen.
        /// </summary>
        public static string FormatOverheadMoteText(string? text, int maxLineLength = 26, int maxTotalLength = 52)
        {
            string cleaned = CleanFormattingGlitches(text ?? string.Empty);
            if (string.IsNullOrWhiteSpace(cleaned))
                return string.Empty;

            cleaned = cleaned.Replace("\r", "").Replace("\n", " ").Trim();
            if (cleaned.Length > maxTotalLength)
            {
                cleaned = cleaned.Substring(0, maxTotalLength).TrimEnd('，', '。', ',', '.', '、', ' ') + "...";
            }

            if (cleaned.Length <= maxLineLength)
                return cleaned;

            int splitPos = -1;
            int searchStart = Math.Max(10, maxLineLength - 8);
            int searchEnd = Math.Min(cleaned.Length - 4, maxLineLength + 6);
            char[] breakChars = { '，', '。', '！', '？', '；', ',', '.', '!', '?', ';', ' ' };
            for (int i = searchEnd; i >= searchStart; i--)
            {
                if (Array.IndexOf(breakChars, cleaned[i]) >= 0)
                {
                    splitPos = i + 1;
                    break;
                }
            }

            if (splitPos <= 0 || splitPos >= cleaned.Length)
                splitPos = maxLineLength;

            return cleaned.Substring(0, splitPos).TrimEnd() + "\n" + cleaned.Substring(splitPos).TrimStart();
        }

        /// <summary>
        /// Sanitizes and classifies a raw thought_tag into a clean, concise emotion/reaction tag,
        /// filtering out leaked chain-of-thought (CoT) narrative sentences.
        /// </summary>
        public static string ClassifyThoughtTag(string? rawTag)
        {
            if (string.IsNullOrWhiteSpace(rawTag))
                return "FRIENDLY";

            string tag = rawTag!.Trim().Trim('"', '\'', '`', '[', ']');
            if (tag.Length <= 15 && !tag.Contains("\uFF0C") && !tag.Contains("\u3002") && !tag.Contains(",") && !tag.Contains(".") && !tag.Contains(" ") && !tag.Contains("\u4E00\u8FB9") && !tag.Contains("\u5FC3\u91CC"))
            {
                return tag.ToUpperInvariant();
            }

            string lower = tag.ToLowerInvariant();
            if (lower.Contains("grief") || lower.Contains("\u60B2") || lower.Contains("\u4F24") || lower.Contains("\u6B7B") || lower.Contains("mourn"))
                return "GRIEF";
            if (lower.Contains("anxi") || lower.Contains("\u614C") || lower.Contains("\u6025") || lower.Contains("\u7D27") || lower.Contains("\u6101"))
                return "ANXIOUS";
            if (lower.Contains("tense") || lower.Contains("\u6012") || lower.Contains("\u706B") || lower.Contains("\u6C14") || lower.Contains("conflict"))
                return "TENSE";
            if (lower.Contains("weary") || lower.Contains("\u7D2F") || lower.Contains("\u4E4F") || lower.Contains("exhaust"))
                return "EXHAUSTED";
            if (lower.Contains("concer") || lower.Contains("\u5173") || lower.Contains("\u5FE7") || lower.Contains("\u8003\u8651"))
                return "CONCERNED";

            return "REFLECTIVE";
        }
    }
}
