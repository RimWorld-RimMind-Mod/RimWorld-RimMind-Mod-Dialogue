namespace RimMind.Dialogue.Core
{
    /// <summary>
    /// Pure dialogue lifecycle rules shared by trigger admission and response handling.
    /// </summary>
    public static class DialogueFlowPolicy
    {
        public static bool IsMonologue(DialogueTriggerType type, bool hasRecipient)
            => !hasRecipient && type != DialogueTriggerType.PlayerInput;

        public static bool UsesDailyQuota(
            DialogueTriggerType type,
            bool hasRecipient,
            bool isReply)
            => !IsMonologue(type, hasRecipient) && hasRecipient && !isReply;

        public static bool UsesMonologueCooldown(
            DialogueTriggerType type,
            bool hasRecipient)
            => IsMonologue(type, hasRecipient);

        public static bool ShouldAutoReply(
            DialogueTriggerType type,
            bool hasRecipient,
            bool isReply,
            bool repliesEnabled)
            => repliesEnabled
               && !IsMonologue(type, hasRecipient)
               && type != DialogueTriggerType.PlayerInput;
    }
}
