using System;
using System.IO;
using System.Text.RegularExpressions;
using RimMind.Dialogue.Core;
using RimMind.Domain.Llm;
using Verse;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    public class DialogueAntiHallucinationAndMirroringTests
    {
        private static string FindRepoRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "RimMind-Dialogue")))
                    return current.FullName;
                current = current.Parent;
            }
            throw new DirectoryNotFoundException("Could not find repository root containing RimMind-Dialogue.");
        }

        [Fact]
        public void EnglishPromptConstraint_IncludesDeceasedAntiHallucination_And_StageDirectionAntiMirroring()
        {
            string repoRoot = FindRepoRoot();
            string xmlPath = Path.Combine(repoRoot, "RimMind-Dialogue", "Languages", "English", "Keyed", "RimMind_Dialogue.xml");
            Assert.True(File.Exists(xmlPath), $"File not found: {xmlPath}");

            string content = File.ReadAllText(xmlPath);
            var match = Regex.Match(content, @"<RimMind\.Dialogue\.Prompt\.TaskInstruction\.Constraint>(.*?)</RimMind\.Dialogue\.Prompt\.TaskInstruction\.Constraint>", RegexOptions.Singleline);
            Assert.True(match.Success, "Could not find RimMind.Dialogue.Prompt.TaskInstruction.Constraint in English XML");

            string constraint = match.Groups[1].Value.Trim();
            Assert.Contains("CRITICAL: Deceased or lost colonists", constraint);
            Assert.Contains("[deceased] or [lost]", constraint);
            Assert.Contains("must NOT be spoken to or treated as physically present", constraint);
            Assert.Contains("Do NOT replicate parenthetical stage directions or physical actions", constraint);
            Assert.Contains("THREE RIGID CONSTRAINTS", constraint);
            Assert.Contains("Environmental physical anchors (no temperature hallucination)", constraint);
            Assert.Contains("Reject universal polite positivity, present raw wasteland emotions", constraint);
            Assert.Equal("Maintain character consistency; do not break the fourth wall; dialogue 15~40 chars, monologue 15~40 chars. CRITICAL: Deceased or lost colonists (marked as [deceased] or [lost]) must NOT be spoken to or treated as physically present; they can only be remembered or mourned in past tense. Do NOT replicate parenthetical stage directions or physical actions from previous lines; output fresh and natural dialogue. THREE RIGID CONSTRAINTS: 1. Environmental physical anchors (no temperature hallucination): Strictly adhere to current ambient temperature and season. In freezing winter weather (&lt;0°C), NEVER mention heat relief, heatwaves, or sandstorms; never fabricate sandstorms indoors. 2. Reject universal polite positivity, present raw wasteland emotions: When suffering from freezing cold (Chilly/Cold), darkness, tattered apparel, severe pain, extreme hunger, insults, or low mood, strictly forbid false positive energy, out-of-place flattery, or overly polite good-boy pleasantries; express natural complaints, cursing terrible weather, cynical retorts, irritable outbursts, fatalistic sighs, or cynical weariness. 3. Anti-repetition: Strictly forbid canned template clichés such as \"stew is delicious/better than yesterday\" or \"organizing building materials\"; lines must be improvised based on the immediate real situation.", constraint);
        }

        [Fact]
        public void ChinesePromptConstraint_IncludesDeceasedAntiHallucination_And_StageDirectionAntiMirroring()
        {
            string repoRoot = FindRepoRoot();
            string xmlPath = Path.Combine(repoRoot, "RimMind-Dialogue", "Languages", "ChineseSimplified", "Keyed", "RimMind_Dialogue.xml");
            Assert.True(File.Exists(xmlPath), $"File not found: {xmlPath}");

            string content = File.ReadAllText(xmlPath);
            var match = Regex.Match(content, @"<RimMind\.Dialogue\.Prompt\.TaskInstruction\.Constraint>(.*?)</RimMind\.Dialogue\.Prompt\.TaskInstruction\.Constraint>", RegexOptions.Singleline);
            Assert.True(match.Success, "Could not find RimMind.Dialogue.Prompt.TaskInstruction.Constraint in Chinese XML");

            string constraint = match.Groups[1].Value.Trim();
            Assert.Contains("重要准则：已故或离场角色", constraint);
            Assert.Contains("[deceased]或[lost]", constraint);
            Assert.Contains("严禁作为现场活人进行互动或当面对话", constraint);
            Assert.Contains("严禁模仿前文的括号动作描写或身体动作", constraint);
            Assert.Contains("三大刚性约束", constraint);
            Assert.Contains("环境客观物理锚点（严禁气温错乱）", constraint);
            Assert.Contains("拒绝全员乖宝宝，呈现废土真实情绪", constraint);
            Assert.Equal("保持人设一致；不要打破第四面墙；对话15~40字，独白15~40字。重要准则：已故或离场角色（标注有[deceased]或[lost]）严禁作为现场活人进行互动或当面对话，只能作为过往回忆或哀思进行单向怀念。严禁模仿前文的括号动作描写或身体动作，请输出独创的自然对话。三大刚性约束：1. 环境客观物理锚点（严禁气温错乱）：必须严格遵循当前环境气温与季节。在严冬寒冷天气（低于0°C）严禁提及“防暑”、“热浪”、“风沙”等反差概念；在室内严禁凭空编造风沙。2. 拒绝全员乖宝宝，呈现废土真实情绪：当角色遭遇严寒（Chilly/Cold）、黑暗、衣衫破烂（Tattered apparel）、剧痛、极饿、被侮辱等负面状态，或心情低落时，严禁使用虚假正能量、违背情境的夸赞或过度礼貌的乖宝宝客套话；应当自然表达抱怨、对恶劣天气的咒骂、冷言冷语、暴躁发泄、宿命感叹或厌世烦躁。3. 去重防复读：严禁出现“炖菜好喝/比昨天好”、“整理建材”等跨人跨天的固定模板套话，台词必须针对当前的真实情境即兴展开。", constraint);
        }

        [Theory]
        [InlineData("（叹了口气）今天真冷。", "（叹了口气）是啊，天气太恶劣了。", "今天真冷。")]
        [InlineData("（叹气）真累。", "（叹了口气）我快撑不住了。", "真累。")]
        [InlineData("（也叹了口气）确实挺麻烦。", "（叹气）这该怎么办。", "确实挺麻烦。")]
        [InlineData("（深深地叹了口气）我们得想个办法。", "（叹了口气）唉。", "我们得想个办法。")]
        [InlineData("（摇了摇头）我不清楚。", "（摇头）这事可真怪。", "我不清楚。")]
        [InlineData("（皱了皱眉）怎么会这样？", "（皱眉）不对劲。", "怎么会这样？")]
        [InlineData("(sighs) I cannot go on.", "(sighs) What a day.", "I cannot go on.")]
        [InlineData("(sighing) What now?", "(sighs) We are out of food.", "What now?")]
        [InlineData("(sighs heavily) We will manage.", "(sighs) It is hard.", "We will manage.")]
        [InlineData("(frowns) Why did you do that?", "(frowning) Look at this.", "Why did you do that?")]
        [InlineData("(sighs) OK.", "（sighs）Take care.", "OK.")]
        [InlineData("（叹气）知道了。", "(叹气) 快点做完。", "知道了。")]
        public void MirroredStageDirections_AreStripped(string reply, string preceding, string expected)
        {
            string actual = DialogueTextSanitizer.SanitizeReply(reply, preceding);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("（笑了笑）但我很有信心。", "（叹了口气）今天真累。", "（笑了笑）但我很有信心。")]
        [InlineData("(smiles) We will be fine.", "(sighs) It's tough.", "(smiles) We will be fine.")]
        [InlineData("（点头）明白了。", "（叹气）这事很重。", "（点头）明白了。")]
        [InlineData("(nods) Understood.", "(frowns) Watch out.", "(nods) Understood.")]
        public void DistinctStageDirections_ArePreserved(string reply, string preceding, string expected)
        {
            string actual = DialogueTextSanitizer.SanitizeReply(reply, preceding);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void PrecedingWithoutStageDirection_PreservesCurrentStageDirection()
        {
            string reply = "（叹了口气）今天真冷。";
            string preceding = "今天我们去采矿吧。";
            string actual = DialogueTextSanitizer.SanitizeReply(reply, preceding);
            Assert.Equal("（叹了口气）今天真冷。", actual);

            string enReply = "(sighs) Tough work.";
            string enPreceding = "Let's finish the field.";
            Assert.Equal("(sighs) Tough work.", DialogueTextSanitizer.SanitizeReply(enReply, enPreceding));
        }

        [Fact]
        public void NullOrEmptyPreceding_PreservesCurrentStageDirection()
        {
            Assert.Equal("（叹气）今天真累。", DialogueTextSanitizer.SanitizeReply("（叹气）今天真累。", null));
            Assert.Equal("(sighs) Tough day.", DialogueTextSanitizer.SanitizeReply("(sighs) Tough day.", ""));
        }

        [Theory]
        [InlineData("(sighs) We will survive.```", "(sighs) Hard times.", "We will survive.")]
        [InlineData("\"确实如此。\"", null, "确实如此。")]
        [InlineData("“明天继续加油吧。”", null, "明天继续加油吧。")]
        [InlineData("今天真冷，", null, "今天真冷。")]
        [InlineData("Hello there,", null, "Hello there.")]
        [InlineData("```json\n(sighs) Done.```", "(sighs) Working.", "Done.")]
        public void FormattingGlitches_AreCleaned(string reply, string? preceding, string expected)
        {
            string actual = DialogueTextSanitizer.SanitizeReply(reply, preceding);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NpcResponseHandler_Handle_StripsMirroredStageDirection_EndToEnd()
        {
            var map = new Verse.Map();
            var colonistA = new Pawn { thingIDNumber = 101, IsColonist = true, Faction = RimWorld.Faction.OfPlayer, Map = map, Spawned = true };
            var colonistB = new Pawn { thingIDNumber = 102, IsColonist = true, Faction = RimWorld.Faction.OfPlayer, Map = map, Spawned = true };

            // Colonist A speaks and logs: "（叹了口气）今天真累啊。"
            RimMindDialogueService.AddLogEntry(colonistA, colonistB, DialogueTriggerType.Chitchat, "work", "（叹了口气）今天真累啊。", "STRESSED", "Tired");

            // Colonist B replies with mirrored stage direction: "（叹了口气）是啊，采矿太繁重了。"
            var response = new LlmResponse
            {
                ToolCallsJson = "[{\"name\":\"express_dialogue\",\"arguments\":{\"speech\":\"（叹了口气）是啊，采矿太繁重了。\",\"thought_tag\":\"VALUED\",\"thought_desc\":\"Understood\"}}]"
            };

            string? result = NpcResponseHandler.Handle(response, "NPC-102", colonistB, colonistA, "chat", DialogueTriggerType.Chitchat, isReply: true);

            Assert.NotNull(result);
            Assert.Equal("是啊，采矿太繁重了。", result);

            // Verify logged entry for colonist B is also sanitized
            var history = RimMindDialogueService.GetDialogueHistory(colonistB.thingIDNumber, 5);
            var latestB = history[0];
            Assert.Equal(colonistB.thingIDNumber, latestB.initiatorId);
            Assert.Equal("是啊，采矿太繁重了。", latestB.reply);
        }

        [Fact]
        public void NpcResponseHandler_Handle_FallbackToContext_WhenHistoryEmpty()
        {
            var map = new Verse.Map();
            var colonistA = new Pawn { thingIDNumber = 201, IsColonist = true, Faction = RimWorld.Faction.OfPlayer, Map = map, Spawned = true };
            var colonistB = new Pawn { thingIDNumber = 202, IsColonist = true, Faction = RimWorld.Faction.OfPlayer, Map = map, Spawned = true };

            // When history has no entry between 201 and 202, but context contains the reply trigger string
            string replyContext = "Alex 对你说：（叹了口气）今天真冷。";
            var response = new LlmResponse
            {
                Content = "（叹了口气）确实，外面都在结冰。"
            };

            string? result = NpcResponseHandler.Handle(response, "NPC-202", colonistB, colonistA, replyContext, DialogueTriggerType.Chitchat, isReply: true);

            Assert.NotNull(result);
            Assert.Equal("确实，外面都在结冰。", result);
        }

        [Fact]
        public void CleanFormattingGlitches_UnwrapsRawJsonEnvelope_ToPureSpeech()
        {
            string rawJson = "{\"speech\": \"Hooke，这件工件的毛边我锉到一半，手心那道口子又被铁屑蹭开了。\", \"thought_tag\": \"AnxiousCare\", \"relation_delta\": 2}";
            string cleaned = DialogueTextSanitizer.CleanFormattingGlitches(rawJson);
            Assert.Equal("Hooke，这件工件的毛边我锉到一半，手心那道口子又被铁屑蹭开了。", cleaned);
        }

        [Fact]
        public void CleanFormattingGlitches_UnwrapsFunctionParametersEnvelope_ToPureSpeech()
        {
            string rawFuncJson = "{\"name\": \"express_dialogue\", \"parameters\": {\"speech\": \"Damn, that leg still hurts.\", \"thought_tag\": \"TIRED\"}}";
            string cleaned = DialogueTextSanitizer.CleanFormattingGlitches(rawFuncJson);
            Assert.Equal("Damn, that leg still hurts.", cleaned);
        }

        [Fact]
        public void CleanFormattingGlitches_UnwrapsDoubleBracedJson_ToPureSpeech()
        {
            string doubleBraced = "{{\"speech\":\"这件工件内侧还有一圈浅锤印，我想换细砂再磨两遍。\",\"thought_tag\":\"caring\",\"relation_delta\":3}}";
            string cleaned = DialogueTextSanitizer.CleanFormattingGlitches(doubleBraced);
            Assert.Equal("这件工件内侧还有一圈浅锤印，我想换细砂再磨两遍。", cleaned);
        }

        [Fact]
        public void NpcResponseHandler_Handle_StripsRawJsonWrapper_Defensively()
        {
            var map = new Verse.Map();
            var colonistA = new Pawn { thingIDNumber = 301, IsColonist = true, Faction = RimWorld.Faction.OfPlayer, Map = map, Spawned = true };
            var colonistB = new Pawn { thingIDNumber = 302, IsColonist = true, Faction = RimWorld.Faction.OfPlayer, Map = map, Spawned = true };

            var response = new LlmResponse
            {
                Content = "{\"speech\": \"Cleme, 热汤这一口下去, 胸口那块冷意总算化开了。\", \"thought_tag\": \"SharedWarmth\", \"relation_delta\": 3}"
            };

            string? result = NpcResponseHandler.Handle(response, "NPC-302", colonistB, colonistA, "dinner", DialogueTriggerType.Chitchat, isReply: false);

            Assert.NotNull(result);
            Assert.Equal("Cleme, 热汤这一口下去, 胸口那块冷意总算化开了。", result);
        }

        [Theory]
        [InlineData("再不生火取暖、找点吃的，我们迟早得交代在这鬼地方！</speech>", "再不生火取暖、找点吃的，我们迟早得交代在这鬼地方！")]
        [InlineData("<speech>今天先把木头搬进棚子里。</speech>", "今天先把木头搬进棚子里。")]
        [InlineData("<thinking>Analyze colonist mood first.</thinking><dialogue>外面零下二十度，别乱跑。</dialogue>", "外面零下二十度，别乱跑。")]
        [InlineData("express_dialogue(speech)=(Stanley，外面天寒地冻的，但在这亮堂堂的屋里吃口现成的饭还算舒坦。)", "Stanley，外面天寒地冻的，但在这亮堂堂的屋里吃口现成的饭还算舒坦。")]
        [InlineData("<tool_call>express_dialogue(speech: \"Stanley, let's get inside before frostbite hits.\", thought_tag: \"FRIENDLY\")</tool_call>", "Stanley, let's get inside before frostbite hits.")]
        public void CleanFormattingGlitches_StripsResidualModelXmlTags_AndThinkingBlocks(string raw, string expected)
        {
            string cleaned = DialogueTextSanitizer.CleanFormattingGlitches(raw);
            Assert.Equal(expected, cleaned);
        }
    }
}
