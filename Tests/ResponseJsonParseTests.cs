using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace RimMind.Dialogue.Tests
{
    public class ResponseJsonParseTests
    {
        private static void ParseJson(string json, bool isMonologue,
            out string reply, out string? thoughtTag, out string? thoughtDesc, out int relationDelta)
        {
            reply = "";
            thoughtTag = null;
            thoughtDesc = null;
            relationDelta = 0;

            if (string.IsNullOrEmpty(json) || !json.TrimStart().StartsWith("{")) return;

            var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (obj == null) return;

            if (obj.TryGetValue("reply", out var replyObj) && replyObj is string replyStr && !string.IsNullOrEmpty(replyStr))
                reply = replyStr;

            if (obj.TryGetValue("thought", out var thoughtObj) && thoughtObj is JObject thoughtJObj)
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

        [Fact]
        public void DialogueResponse_ParsesAllThreeFields()
        {
            var json = "{\"reply\":\"你好呀\",\"thought\":{\"tag\":\"CONNECTED\",\"description\":\"想与同伴亲近\"},\"relation_delta\":1}";

            ParseJson(json, false, out var reply, out var tag, out var desc, out var delta);

            Assert.Equal("你好呀", reply);
            Assert.Equal("CONNECTED", tag);
            Assert.Equal("想与同伴亲近", desc);
            Assert.Equal(1, delta);
        }

        [Fact]
        public void MonologueResponse_IgnoresRelationDelta()
        {
            var json = "{\"reply\":\"好累啊……\",\"thought\":{\"tag\":\"STRESSED\",\"description\":\"感到疲惫\"},\"relation_delta\":1}";

            ParseJson(json, true, out var reply, out var tag, out var desc, out var delta);

            Assert.Equal("好累啊……", reply);
            Assert.Equal("STRESSED", tag);
            Assert.Equal("感到疲惫", desc);
            Assert.Equal(0, delta); // ignored in monologue
        }

        [Fact]
        public void MonologueResponse_NoRelationDeltaField()
        {
            var json = "{\"reply\":\"这矿洞真冷……\",\"thought\":{\"tag\":\"STRESSED\",\"description\":\"感到疲惫\"}}";

            ParseJson(json, true, out var reply, out var tag, out var desc, out var delta);

            Assert.Equal("这矿洞真冷……", reply);
            Assert.Equal("STRESSED", tag);
            Assert.Equal(0, delta);
        }

        [Fact]
        public void ResponseNoThought()
        {
            var json = "{\"reply\":\"嗯。\"}";

            ParseJson(json, false, out var reply, out var tag, out var desc, out var delta);

            Assert.Equal("嗯。", reply);
            Assert.Null(tag);
            Assert.Null(desc);
            Assert.Equal(0, delta);
        }

        [Fact]
        public void ResponseThoughtNone()
        {
            var json = "{\"reply\":\"早啊，有什么事吗？\",\"thought\":{\"tag\":\"NONE\",\"description\":\"平淡问候\"}}";

            ParseJson(json, false, out var reply, out var tag, out var desc, out var delta);

            Assert.Equal("早啊，有什么事吗？", reply);
            Assert.Equal("NONE", tag);
            Assert.Equal("平淡问候", desc);
            Assert.Equal(0, delta);
        }

        [Fact]
        public void NonJsonString_ReturnsDefaults()
        {
            ParseJson("just plain text", false, out var reply, out var tag, out var desc, out var delta);

            Assert.Equal("", reply);
            Assert.Null(tag);
            Assert.Null(desc);
            Assert.Equal(0, delta);
        }

        [Fact]
        public void EmptyAndNull_ReturnsDefaults()
        {
            ParseJson("", false, out var reply, out var tag, out var desc, out var delta);
            Assert.Equal("", reply);
            Assert.Null(tag);

            ParseJson(null, false, out reply, out tag, out desc, out delta);
            Assert.Equal("", reply);
            Assert.Null(tag);
        }

        [Fact]
        public void DialogueNegativeRelationDelta()
        {
            var json = "{\"reply\":\"滚开！\",\"thought\":{\"tag\":\"IRRITATED\",\"description\":\"对方让你烦躁\"},\"relation_delta\":-3}";

            ParseJson(json, false, out var reply, out var tag, out var desc, out var delta);

            Assert.Equal("滚开！", reply);
            Assert.Equal("IRRITATED", tag);
            Assert.Equal(-3, delta);
        }
    }
}
