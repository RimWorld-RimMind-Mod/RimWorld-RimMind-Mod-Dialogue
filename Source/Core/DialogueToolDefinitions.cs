using System.Collections.Generic;
using RimMind.Domain.Llm;

namespace RimMind.Dialogue.Core
{
    public static class DialogueToolDefinitions
    {
        public const string ExpressDialogueToolName = "express_dialogue";

        public static readonly StructuredTool ExpressDialogueTool = new StructuredTool
        {
            Name = ExpressDialogueToolName,
            Description = "Express the colonist's spoken dialogue or internal monologue, along with their emotional thought tag and relation change.",
            Parameters = "{\"type\":\"object\",\"properties\":{\"speech\":{\"type\":\"string\",\"description\":\"The spoken words or inner monologue content of the character.\"},\"thought_tag\":{\"type\":\"string\",\"description\":\"Emotional/mental tag (e.g. ENCOURAGED, HURT, VALUED, CONNECTED, STRESSED, IRRITATED, TIRED, CALM, NONE).\"},\"thought_desc\":{\"type\":\"string\",\"description\":\"Brief description of why the character feels this way (<=15 words).\"},\"relation_delta\":{\"type\":\"integer\",\"description\":\"Relationship opinion change towards the conversation partner (-5 to +5). 0 for monologue or no change.\"}},\"required\":[\"speech\"]}",
            ToolChoice = "required"
        };

        public static List<StructuredTool> AsList() => new List<StructuredTool> { ExpressDialogueTool };
    }
}
