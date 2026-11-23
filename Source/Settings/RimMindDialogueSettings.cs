using UnityEngine;
using Verse;
using RimMind.Presentation.Api;
using RimMind.Presentation.UI;

namespace RimMind.Dialogue.Settings
{
    public class RimMindDialogueSettings : ModSettings
    {
        public bool enabled = true;

        public bool chitchatEnabled = true;
        public bool hediffEnabled = true;
        public bool levelUpEnabled = true;
        public bool thoughtEnabled = true;
        public bool autoDialogueEnabled = true;
        public bool playerDialogueEnabled = true;

        public float moodChangeThreshold = 3f;
        public int autoDialogueCooldownHours = 12;
        public int maxDailyDialogueRounds = 6;
        public int globalConcurrency = 3;
        public bool showThoughtNotification = false;

        public float targetDialogueCadenceDays = 3.0f;
        public int maxStoredDialogueHistory = 200;

        public bool enableDialogueReply = true;
        public int maxDailyReplyRounds = 12;

        public int monologueCooldownTicks = 36000;

        // State-transition social encounter triggers & probabilities
        public bool enableRecreationSocial = true;
        public float recreationSocialChance = 0.35f;

        public bool enableDiningSocial = true;
        public float diningSocialChance = 0.30f;

        public bool enableCoworkerSocial = true;
        public float coworkerSocialChance = 0.25f;

        public bool startDelayEnabled = true;
        public int startDelaySeconds = 10;
        public int startDelayTicks => startDelaySeconds * 60;

        public bool overlayEnabled = true;
        public float overlayOpacity = 0.75f;
        public int overlayMaxMessages = 8;
        public float overlayX = 20f;
        public float overlayY = 320f;
        public float overlayW = 420f;
        public float overlayH = 220f;

        public int AutoDialogueCooldownTicks => autoDialogueCooldownHours * 2500;

        private static RimMindDialogueSettings? _instance;
        public static RimMindDialogueSettings Get() => _instance ?? new RimMindDialogueSettings();

        public RimMindDialogueSettings()
        {
            _instance = this;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enabled, "enabled", true);

            Scribe_Values.Look(ref chitchatEnabled, "chitchatEnabled", true);
            Scribe_Values.Look(ref hediffEnabled, "hediffEnabled", true);
            Scribe_Values.Look(ref levelUpEnabled, "levelUpEnabled", true);
            Scribe_Values.Look(ref thoughtEnabled, "thoughtEnabled", true);
            Scribe_Values.Look(ref autoDialogueEnabled, "autoDialogueEnabled", true);
            Scribe_Values.Look(ref playerDialogueEnabled, "playerDialogueEnabled", true);

            Scribe_Values.Look(ref moodChangeThreshold, "moodChangeThreshold", 3f);
            Scribe_Values.Look(ref autoDialogueCooldownHours, "autoDialogueCooldownHours", 12);
            Scribe_Values.Look(ref maxDailyDialogueRounds, "maxDailyDialogueRounds", 6);
            Scribe_Values.Look(ref globalConcurrency, "globalConcurrency", 3);
            Scribe_Values.Look(ref showThoughtNotification, "showThoughtNotification", false);

            Scribe_Values.Look(ref targetDialogueCadenceDays, "targetDialogueCadenceDays", 3.0f);
            Scribe_Values.Look(ref maxStoredDialogueHistory, "maxStoredDialogueHistory", 200);

            Scribe_Values.Look(ref enableDialogueReply, "enableDialogueReply", true);
            Scribe_Values.Look(ref maxDailyReplyRounds, "maxDailyReplyRounds", 12);

            Scribe_Values.Look(ref monologueCooldownTicks, "monologueCooldownTicks", 36000);
            Scribe_Values.Look(ref enableRecreationSocial, "enableRecreationSocial", true);
            Scribe_Values.Look(ref recreationSocialChance, "recreationSocialChance", 0.35f);
            Scribe_Values.Look(ref enableDiningSocial, "enableDiningSocial", true);
            Scribe_Values.Look(ref diningSocialChance, "diningSocialChance", 0.30f);
            Scribe_Values.Look(ref enableCoworkerSocial, "enableCoworkerSocial", true);
            Scribe_Values.Look(ref coworkerSocialChance, "coworkerSocialChance", 0.25f);
            Scribe_Values.Look(ref startDelayEnabled, "startDelayEnabled", true);
            Scribe_Values.Look(ref startDelaySeconds, "startDelaySeconds", 10);
            Scribe_Values.Look(ref overlayEnabled, "overlayEnabled", true);
            Scribe_Values.Look(ref overlayOpacity, "overlayOpacity", 0.75f);
            Scribe_Values.Look(ref overlayMaxMessages, "overlayMaxMessages", 8);
            Scribe_Values.Look(ref overlayX, "overlayX", 20f);
            Scribe_Values.Look(ref overlayY, "overlayY", 320f);
            Scribe_Values.Look(ref overlayW, "overlayW", 420f);
            Scribe_Values.Look(ref overlayH, "overlayH", 220f);
        }

        private static Vector2 _settingsScrollPos = Vector2.zero;
        private static float _measuredSettingsHeight = 640f;

        private static string Tr(string key, string fallback, params object[] args)
            => RimMindAPI.Text.TrOr(key, fallback, args);

        public static void DrawSettingsContent(UnityEngine.Rect inRect)
        {
            var s = Get();

            SettingsUIDrawer.DrawScrollableSettingsPage(
                inRect,
                ref _settingsScrollPos,
                ref _measuredSettingsHeight,
                listing =>
                {
                    listing.CheckboxLabeled(
                        Tr("RimMind.Dialogue.Settings.Enable", "Enable AI Dialogue System"),
                        ref s.enabled,
                        Tr("RimMind.Dialogue.Settings.Enable.Desc", "When enabled, colonists will generate dialogues and monologues based on game events and their state."));

                    SettingsUIDrawer.DrawSectionHeader(listing, Tr("RimMind.Dialogue.Settings.TriggerSources", "Trigger Sources"));
                    listing.CheckboxLabeled(
                        Tr("RimMind.Dialogue.Settings.ChitchatIntercept", "Intercept Chitchat"),
                        ref s.chitchatEnabled,
                        Tr("RimMind.Dialogue.Settings.ChitchatIntercept.Desc", "Replace vanilla Chitchat/DeepTalk interactions with AI-generated dialogue."));
                    listing.CheckboxLabeled(
                        Tr("RimMind.Dialogue.Settings.HediffReaction", "Health Change Reaction"),
                        ref s.hediffEnabled,
                        Tr("RimMind.Dialogue.Settings.HediffReaction.Desc", "When a colonist is injured or falls ill, AI generates a dialogue reaction."));
                    listing.CheckboxLabeled(
                        Tr("RimMind.Dialogue.Settings.LevelUpReaction", "Skill Level Up Reaction"),
                        ref s.levelUpEnabled,
                        Tr("RimMind.Dialogue.Settings.LevelUpReaction.Desc", "When a colonist levels up a skill, AI generates a dialogue reaction."));
                    listing.CheckboxLabeled(
                        Tr("RimMind.Dialogue.Settings.ThoughtReaction", "Mood Change Reaction"),
                        ref s.thoughtEnabled,
                        Tr("RimMind.Dialogue.Settings.ThoughtReaction.Desc", "When a colonist's mood changes significantly, AI generates a dialogue reaction."));
                    if (s.thoughtEnabled)
                    {
                        listing.Label("  " + Tr("RimMind.Dialogue.Settings.MoodThreshold", "Mood change threshold: {0}", $"{s.moodChangeThreshold:F0}"));
                        s.moodChangeThreshold = listing.Slider(s.moodChangeThreshold, 1f, 5f);
                    }
                    listing.CheckboxLabeled(
                        Tr("RimMind.Dialogue.Settings.AutoMonologue", "Auto Monologue on Idle"),
                        ref s.autoDialogueEnabled,
                        Tr("RimMind.Dialogue.Settings.AutoMonologue.Desc", "When a colonist is idle, automatically trigger an inner monologue."));
                    if (s.autoDialogueEnabled)
                    {
                        listing.LabelWithTooltip(
                            "  " + Tr("RimMind.Dialogue.Settings.AutoInterval", "Auto monologue interval: {0} game hours", s.autoDialogueCooldownHours),
                            Tr("RimMind.Dialogue.Settings.AutoMonologueDesc", "Minimum game hours between auto-monologues when a colonist is idle."));
                        s.autoDialogueCooldownHours = (int)listing.Slider(s.autoDialogueCooldownHours, 1f, 24f);
                    }
                    listing.CheckboxLabeled(
                        Tr("RimMind.Dialogue.Settings.PlayerDialogueGizmo", "Player Initiated Dialogue"),
                        ref s.playerDialogueEnabled,
                        Tr("RimMind.Dialogue.Settings.PlayerDialogueGizmo.Desc", "Show a chat button on colonists and a right-click dialogue option."));

                    SettingsUIDrawer.DrawSectionHeader(listing, Tr("RimMind.Dialogue.Settings.Section.Behavior", "Behavior Settings"));
                    listing.LabelWithTooltip(
                        Tr("RimMind.Dialogue.Settings.MonologueCooldown", "Monologue Cooldown: {0} game hours ({1} ticks)", $"{s.monologueCooldownTicks / 2500f:F1}", $"{s.monologueCooldownTicks}"),
                        Tr("RimMind.Dialogue.Settings.MonologueCooldown.Desc", "Minimum interval between same-type monologues for the same colonist."));
                    s.monologueCooldownTicks = (int)listing.Slider(s.monologueCooldownTicks, 3600f, 72000f);
                    s.monologueCooldownTicks = (s.monologueCooldownTicks / 600) * 600;

                    listing.LabelWithTooltip(
                        Tr("RimMind.Dialogue.Settings.MaxDailyDialogues", "Max daily dialogue rounds per pair: {0}", s.maxDailyDialogueRounds),
                        Tr("RimMind.Dialogue.Settings.MaxDailyDialogues.Desc", "Maximum dialogue rounds per colonist pair per day."));
                    s.maxDailyDialogueRounds = (int)listing.Slider(s.maxDailyDialogueRounds, 1f, 20f);

                    listing.LabelWithTooltip(
                        Tr("RimMind.Dialogue.Settings.GlobalConcurrency", "Global dialogue concurrency limit: {0}", s.globalConcurrency),
                        Tr("RimMind.Dialogue.Settings.GlobalConcurrency.Desc", "Maximum number of simultaneous AI dialogue requests."));
                    s.globalConcurrency = (int)listing.Slider(s.globalConcurrency, 1f, 10f);

                    listing.CheckboxLabeled(
                        Tr("RimMind.Dialogue.Settings.EnableDialogueReply", "Enable Dialogue Reply"),
                        ref s.enableDialogueReply,
                        Tr("RimMind.Dialogue.Settings.EnableDialogueReply.Desc", "Automatically generate a reply when receiving dialogue."));
                    if (s.enableDialogueReply)
                    {
                        listing.LabelWithTooltip(
                            Tr("RimMind.Dialogue.Settings.MaxDailyReplies", "Maximum automatic replies per pair each day: {0}", s.maxDailyReplyRounds),
                            Tr("RimMind.Dialogue.Settings.MaxDailyReplies.Desc", "Rate-limits continuous two-way conversations."));
                        s.maxDailyReplyRounds = (int)listing.Slider(s.maxDailyReplyRounds, 1f, 50f);
                    }

                    listing.CheckboxLabeled(
                        Tr("RimMind.Dialogue.Settings.StartDelay", "Game start delay"),
                        ref s.startDelayEnabled,
                        Tr("RimMind.Dialogue.Settings.StartDelay.Desc", "No dialogue is triggered for a period after loading a save or starting a new game."));
                    if (s.startDelayEnabled)
                    {
                        listing.Label("  " + Tr("RimMind.Dialogue.Settings.StartDelayValue", "Start delay: {0}s", s.startDelaySeconds));
                        s.startDelaySeconds = (int)listing.Slider(s.startDelaySeconds, 1f, 60f);
                    }

                    SettingsUIDrawer.DrawSectionHeader(listing, Tr("RimMind.Dialogue.Settings.Section.Display", "Display"));
                    listing.CheckboxLabeled(
                        Tr("RimMind.Dialogue.Settings.ShowNotification", "Show notification when Thought is injected"),
                        ref s.showThoughtNotification,
                        Tr("RimMind.Dialogue.Settings.ShowNotification.Desc", "Display a notification when the dialogue system injects a mood Thought."));
                    listing.CheckboxLabeled(
                        Tr("RimMind.Dialogue.Settings.ShowOverlay", "Show dialogue overlay"),
                        ref s.overlayEnabled,
                        Tr("RimMind.Dialogue.Settings.ShowOverlay.Desc", "Display a floating panel on screen showing recent dialogue content."));
                    listing.LabelWithTooltip(
                        Tr("RimMind.Dialogue.Settings.OverlayOpacity", "Overlay opacity: {0}", $"{s.overlayOpacity:P0}"),
                        Tr("RimMind.Dialogue.Settings.OverlayOpacity.Desc", "Opacity of the dialogue overlay."));
                    s.overlayOpacity = listing.Slider(s.overlayOpacity, 0.1f, 1f);
                    listing.LabelWithTooltip(
                        Tr("RimMind.Dialogue.Settings.OverlayMaxMessages", "Overlay max messages: {0}", s.overlayMaxMessages),
                        Tr("RimMind.Dialogue.Settings.OverlayMaxMessages.Desc", "Maximum number of dialogue lines shown simultaneously in the overlay."));
                    s.overlayMaxMessages = (int)listing.Slider(s.overlayMaxMessages, 3f, 20f);
                },
                onReset: () => ResetToDefaults(s),
                onDirtySave: () => s.Write());
        }

        private static void ResetToDefaults(RimMindDialogueSettings s)
        {
            s.enabled = true;
            s.chitchatEnabled = true;
            s.hediffEnabled = true;
            s.levelUpEnabled = true;
            s.thoughtEnabled = true;
            s.autoDialogueEnabled = true;
            s.playerDialogueEnabled = true;
            s.moodChangeThreshold = 3f;
            s.autoDialogueCooldownHours = 12;
            s.maxDailyDialogueRounds = 6;
            s.globalConcurrency = 3;
            s.showThoughtNotification = false;
            s.targetDialogueCadenceDays = 3.0f;
            s.maxStoredDialogueHistory = 200;
            s.enableDialogueReply = true;
            s.maxDailyReplyRounds = 12;
            s.monologueCooldownTicks = 36000;
            s.enableRecreationSocial = true;
            s.recreationSocialChance = 0.35f;
            s.enableDiningSocial = true;
            s.diningSocialChance = 0.30f;
            s.enableCoworkerSocial = true;
            s.coworkerSocialChance = 0.25f;
            s.startDelayEnabled = true;
            s.startDelaySeconds = 10;
            s.overlayEnabled = true;
            s.overlayOpacity = 0.75f;
            s.overlayMaxMessages = 8;
            s.overlayX = 20f;
            s.overlayY = 320f;
            s.overlayW = 420f;
            s.overlayH = 220f;
        }
    }
}
