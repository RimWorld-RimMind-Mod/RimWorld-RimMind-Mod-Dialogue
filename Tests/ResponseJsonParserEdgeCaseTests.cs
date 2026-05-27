using RimMind.Dialogue.Core;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    /// <summary>
    /// ResponseJsonParser 边界情况和特殊输入测试
    /// </summary>
    public class ResponseJsonParserEdgeCaseTests
    {
        [Fact]
        public void TryParse_JSON前有空白字符_仍能解析()
        {
            // JSON 字符串前有空白，TrimStart 后以 { 开头
            var json = "   {\"reply\":\"你好\",\"thought\":{\"tag\":\"ENCOURAGED\",\"description\":\"受到鼓励\"}}";

            string replyText = "";
            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(json, false, ref replyText, ref thoughtTag, ref thoughtDesc, ref relationDelta);

            Assert.Equal("你好", replyText);
            Assert.Equal("ENCOURAGED", thoughtTag);
        }

        [Fact]
        public void TryParse_Thought对象缺少description_tag仍能解析()
        {
            var json = "{\"reply\":\"嗯嗯\",\"thought\":{\"tag\":\"HURT\"}}";

            string replyText = "";
            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(json, false, ref replyText, ref thoughtTag, ref thoughtDesc, ref relationDelta);

            Assert.Equal("嗯嗯", replyText);
            Assert.Equal("HURT", thoughtTag);
            Assert.Null(thoughtDesc);
        }

        [Fact]
        public void TryParse_JSON包含未知字段_不影响已知字段解析()
        {
            var json = "{\"reply\":\"好的\",\"thought\":{\"tag\":\"VALUED\",\"description\":\"被重视\"},\"relation_delta\":2,\"unknown_field\":123}";

            string replyText = "";
            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(json, false, ref replyText, ref thoughtTag, ref thoughtDesc, ref relationDelta);

            Assert.Equal("好的", replyText);
            Assert.Equal("VALUED", thoughtTag);
            Assert.Equal("被重视", thoughtDesc);
            Assert.Equal(2, relationDelta);
        }

        [Fact]
        public void TryParse_RelationDelta为浮点数_不解析为int()
        {
            // Newtonsoft.Json 将浮点数反序列化为 double，不匹配 long 或 int
            var json = "{\"reply\":\"嗯\",\"relation_delta\":1.5}";

            string replyText = "";
            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(json, false, ref replyText, ref thoughtTag, ref thoughtDesc, ref relationDelta);

            Assert.Equal("嗯", replyText);
            // 1.5 反序列化为 double，不匹配 long/int，relationDelta 保持 0
            Assert.Equal(0, relationDelta);
        }

        [Fact]
        public void TryParse_Reply包含Unicode字符_正确解析()
        {
            var json = "{\"reply\":\"你好世界！🎮\",\"thought\":{\"tag\":\"CONNECTED\",\"description\":\"亲近感\"}}";

            string replyText = "";
            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(json, false, ref replyText, ref thoughtTag, ref thoughtDesc, ref relationDelta);

            Assert.Equal("你好世界！🎮", replyText);
            Assert.Equal("CONNECTED", thoughtTag);
            Assert.Equal("亲近感", thoughtDesc);
        }

        [Fact]
        public void TryParse_超大RelationDelta值_正确解析()
        {
            var json = "{\"reply\":\"极端情况\",\"relation_delta\":999}";

            string replyText = "";
            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(json, false, ref replyText, ref thoughtTag, ref thoughtDesc, ref relationDelta);

            Assert.Equal(999, relationDelta);
        }

        [Fact]
        public void TryParse_只有Thought没有Reply_保留原始ReplyText()
        {
            var json = "{\"thought\":{\"tag\":\"STRESSED\",\"description\":\"压力\"}}";

            string replyText = "原始文本";
            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(json, false, ref replyText, ref thoughtTag, ref thoughtDesc, ref relationDelta);

            // reply 字段不存在，replyText 保持原值
            Assert.Equal("原始文本", replyText);
            Assert.Equal("STRESSED", thoughtTag);
            Assert.Equal("压力", thoughtDesc);
        }

        [Fact]
        public void TryParse_ThoughtTag为空字符串_不覆盖ThoughtTag()
        {
            // thought.tag 为空字符串，TryGetValue 返回空字符串，但空字符串非 null
            var json = "{\"reply\":\"嗯\",\"thought\":{\"tag\":\"\",\"description\":\"空标签\"}}";

            string replyText = "";
            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(json, false, ref replyText, ref thoughtTag, ref thoughtDesc, ref relationDelta);

            Assert.Equal("嗯", replyText);
            // JObject.Value<string>("tag") 对空字符串返回 ""
            Assert.Equal("", thoughtTag);
            Assert.Equal("空标签", thoughtDesc);
        }

        [Fact]
        public void TryParse_ThoughtDesc为null_正确处理()
        {
            var json = "{\"reply\":\"嗯\",\"thought\":{\"tag\":\"IRRITATED\"}}";

            string replyText = "";
            string? thoughtTag = null;
            string? thoughtDesc = null;
            int relationDelta = 0;

            ResponseJsonParser.TryParseResponseJson(json, false, ref replyText, ref thoughtTag, ref thoughtDesc, ref relationDelta);

            Assert.Equal("IRRITATED", thoughtTag);
            // description 字段不存在，thoughtDesc 保持 null
            Assert.Null(thoughtDesc);
        }

        [Fact]
        public void TryParse_连续调用_状态独立不污染()
        {
            // 第一次解析
            var json1 = "{\"reply\":\"第一次\",\"thought\":{\"tag\":\"ENCOURAGED\",\"description\":\"鼓励\"}}";
            string replyText1 = "";
            string? thoughtTag1 = null;
            string? thoughtDesc1 = null;
            int relationDelta1 = 0;
            ResponseJsonParser.TryParseResponseJson(json1, false, ref replyText1, ref thoughtTag1, ref thoughtDesc1, ref relationDelta1);

            // 第二次解析
            var json2 = "{\"reply\":\"第二次\",\"thought\":{\"tag\":\"HURT\",\"description\":\"受伤\"},\"relation_delta\":-2}";
            string replyText2 = "";
            string? thoughtTag2 = null;
            string? thoughtDesc2 = null;
            int relationDelta2 = 0;
            ResponseJsonParser.TryParseResponseJson(json2, false, ref replyText2, ref thoughtTag2, ref thoughtDesc2, ref relationDelta2);

            // 两次调用结果独立
            Assert.Equal("第一次", replyText1);
            Assert.Equal("ENCOURAGED", thoughtTag1);
            Assert.Equal(0, relationDelta1);

            Assert.Equal("第二次", replyText2);
            Assert.Equal("HURT", thoughtTag2);
            Assert.Equal(-2, relationDelta2);
        }
    }
}
