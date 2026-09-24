namespace RimMind.Dialogue.Core
{
    public static class DialogueClassifier
    {
        public static (int, int) MakePairKey(int idA, int idB)
        {
            return idA < idB ? (idA, idB) : (idB, idA);
        }

        public static DialogueCategory Classify(bool initiatorColonist, bool? recipientColonist, DialogueTriggerType triggerType)
        {
            if (triggerType == DialogueTriggerType.PlayerInput)
                return DialogueCategory.PlayerDialogue;
            if (recipientColonist == null)
                return initiatorColonist ? DialogueCategory.ColonistMonologue : DialogueCategory.NonColonistMonologue;
            if (!initiatorColonist || !recipientColonist.Value)
                return DialogueCategory.NonColonistDialogue;
            return DialogueCategory.ColonistDialogue;
        }
    }
}
