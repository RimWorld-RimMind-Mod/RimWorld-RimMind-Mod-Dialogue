using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using RimMind.Application.Common.Constants;
using RimMind.Presentation.Api;
using UnityEngine;

namespace RimMind.Dialogue.Core
{
    internal sealed class DialogueTriggerDescriptor
    {
        public DialogueTriggerType Type { get; }
        public string TypeId { get; }
        public string TriggerKind { get; }
        public string LabelKey { get; }
        public string LabelFallback { get; }
        public string? PromptFormatKey { get; }
        public string? PromptFallback { get; }
        public Color LogColor { get; }

        public DialogueTriggerDescriptor(
            DialogueTriggerType type,
            string triggerKind,
            string labelKey,
            string labelFallback,
            string? promptFormatKey,
            string? promptFallback,
            Color logColor)
        {
            Type = type;
            TypeId = type.ToString();
            TriggerKind = triggerKind;
            LabelKey = labelKey;
            LabelFallback = labelFallback;
            PromptFormatKey = promptFormatKey;
            PromptFallback = promptFallback;
            LogColor = logColor;
        }
    }

    /// <summary>
    /// Single source of truth for DialogueTriggerType metadata (trigger kind, localized label,
    /// prompt format key, and log display color), plus extensible custom trigger labels.
    /// </summary>
    internal static class DialogueTriggerRegistry
    {
        private static readonly Dictionary<DialogueTriggerType, DialogueTriggerDescriptor> BuiltInByType =
            new Dictionary<DialogueTriggerType, DialogueTriggerDescriptor>
            {
                [DialogueTriggerType.Chitchat] = new DialogueTriggerDescriptor(
                    DialogueTriggerType.Chitchat,
                    DialogueTriggerKinds.Chitchat,
                    "RimMind.Dialogue.Trigger.Chitchat",
                    "Chitchat",
                    "RimMind.Dialogue.Prompt.Context.Chitchat",
                    "[Chitchat: {0}]",
                    new Color(0.7f, 0.85f, 1f)),
                [DialogueTriggerType.Hediff] = new DialogueTriggerDescriptor(
                    DialogueTriggerType.Hediff,
                    DialogueTriggerKinds.Hediff,
                    "RimMind.Dialogue.Trigger.Hediff",
                    "Health Change",
                    "RimMind.Dialogue.Prompt.Context.Hediff",
                    "[Health condition: {0}]",
                    new Color(1f, 0.6f, 0.6f)),
                [DialogueTriggerType.LevelUp] = new DialogueTriggerDescriptor(
                    DialogueTriggerType.LevelUp,
                    DialogueTriggerKinds.LevelUp,
                    "RimMind.Dialogue.Trigger.LevelUp",
                    "Skill Level Up",
                    "RimMind.Dialogue.Prompt.Context.LevelUp",
                    "[Skill level up: {0}]",
                    new Color(0.6f, 1f, 0.6f)),
                [DialogueTriggerType.Thought] = new DialogueTriggerDescriptor(
                    DialogueTriggerType.Thought,
                    DialogueTriggerKinds.Thought,
                    "RimMind.Dialogue.Trigger.Thought",
                    "Mood Change",
                    "RimMind.Dialogue.Prompt.Context.Thought",
                    "[Recent thought: {0}]",
                    new Color(1f, 0.95f, 0.6f)),
                [DialogueTriggerType.Auto] = new DialogueTriggerDescriptor(
                    DialogueTriggerType.Auto,
                    DialogueTriggerKinds.Auto,
                    "RimMind.Dialogue.Trigger.Auto",
                    "Auto Trigger",
                    "RimMind.Dialogue.Prompt.Context.Auto",
                    "{0}",
                    new Color(0.8f, 0.8f, 0.8f)),
                [DialogueTriggerType.PlayerInput] = new DialogueTriggerDescriptor(
                    DialogueTriggerType.PlayerInput,
                    DialogueTriggerKinds.PlayerInput,
                    "RimMind.Dialogue.Trigger.PlayerInput",
                    "Player Dialogue",
                    null,
                    null,
                    new Color(0.85f, 0.7f, 1f)),
            };

        private static readonly Dictionary<string, DialogueTriggerDescriptor> BuiltInByName =
            BuildByNameMap();

        internal static readonly ConcurrentDictionary<string, string> CustomLabels =
            new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private static Dictionary<string, DialogueTriggerDescriptor> BuildByNameMap()
        {
            var map = new Dictionary<string, DialogueTriggerDescriptor>(StringComparer.Ordinal);
            foreach (var descriptor in BuiltInByType.Values)
            {
                map[descriptor.TypeId] = descriptor;
            }
            return map;
        }

        public static void RegisterLabel(string typeId, string labelKey)
        {
            if (string.IsNullOrWhiteSpace(typeId)) return;
            CustomLabels[typeId] = labelKey;
        }

        public static string GetTriggerKind(DialogueTriggerType type)
            => BuiltInByType.TryGetValue(type, out var desc) ? desc.TriggerKind : type.ToString();

        public static string GetLabel(DialogueTriggerType type)
            => GetLabel(type.ToString());

        public static string GetLabel(string triggerKey)
        {
            if (string.IsNullOrEmpty(triggerKey)) return string.Empty;
            if (CustomLabels.TryGetValue(triggerKey, out var customKey))
                return RimMindAPI.Text.TrOr(customKey, customKey);
            if (BuiltInByName.TryGetValue(triggerKey, out var desc))
                return RimMindAPI.Text.TrOr(desc.LabelKey, desc.LabelFallback);
            return triggerKey;
        }

        public static string FormatContext(DialogueTriggerType type, string context)
        {
            if (BuiltInByType.TryGetValue(type, out var desc)
                && !string.IsNullOrEmpty(desc.PromptFormatKey)
                && desc.PromptFallback != null)
            {
                return RimMindAPI.Text.TrOr(desc.PromptFormatKey!, desc.PromptFallback, context);
            }
            return context;
        }

        public static Color GetLogColor(string triggerKey)
            => !string.IsNullOrEmpty(triggerKey) && BuiltInByName.TryGetValue(triggerKey, out var desc)
                ? desc.LogColor
                : Color.white;
    }
}
