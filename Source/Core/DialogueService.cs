using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RimMind.Core;
using RimMind.Core.Client;
using RimMind.Core.Prompt;
using RimMind.Dialogue.Settings;
using Verse;

namespace RimMind.Dialogue.Core
{
    public static class DialogueService
    {
        public static void RequestReply(DialogueSession session, string playerMessage,
                                        Pawn? initiator,
                                        Action<string> onReply, Action<string> onError)
        {
            session.AddUserMessage(playerMessage);

            string? custom = RimMindDialogueSettings.Get().dialogueCustomPrompt?.Trim();
            string systemPrompt = DialoguePromptBuilder.BuildPlayerSystemPrompt(session.Pawn, initiator, custom);
            string pawnContext = DialoguePromptBuilder.BuildPlayerUserPrompt(session.Pawn, initiator);

            var request = new AIRequest
            {
                SystemPrompt = systemPrompt,
                Messages = BuildMessages(systemPrompt, pawnContext, session.GetContextMessages()),
                MaxTokens = 300,
                Temperature = 0.85f,
                UseJsonMode = true,
                RequestId = $"RimMindDialogue_Player_{session.Pawn.thingIDNumber}_{Find.TickManager.TicksGame}",
                ModId = "Dialogue",
                ExpireAtTicks = Find.TickManager.TicksGame + RimMindDialogueSettings.Get().dialogueExpireTicks
            };

            RimMindAPI.RequestImmediate(request, response =>
            {
                if (!response.Success) { onError(response.Error); return; }

                PlayerDialogueResponse? result = null;
                try { result = JsonConvert.DeserializeObject<PlayerDialogueResponse>(response.Content); }
                catch (Exception ex) { onError($"JSON parse failed: {ex.Message}"); return; }

                string replyText = result?.reply ?? string.Empty;
                if (replyText.NullOrEmpty()) { onError("Empty reply"); return; }

                session.AddAssistantMessage(replyText);

                if (result?.thought?.tag is string tag && tag != "NONE" && !tag.NullOrEmpty())
                    ThoughtInjector.Inject(session.Pawn, null, tag, result.thought.description);

                RimMindDialogueService.AddPlayerDialogueLog(session.Pawn, playerMessage, replyText,
                    result?.thought?.tag, result?.thought?.description);

                onReply(replyText);
            });
        }

        private static List<ChatMessage> BuildMessages(string systemPrompt, string pawnContext,
            List<(string role, string content)> history)
        {
            var msgs = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = pawnContext }
            };

            foreach (var (role, content) in history)
                msgs.Add(new ChatMessage { Role = role, Content = content });

            return msgs;
        }
    }

    public class PlayerDialogueResponse
    {
        public string? reply;
        public ThoughtPart? thought;
    }
}
