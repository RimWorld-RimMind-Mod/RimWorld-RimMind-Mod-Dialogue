using UnityEngine;
using Verse;
using RimMind.Core.UI;

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
        public bool showThoughtNotification = false;

        public int dialogueContextRounds = 5;
        public bool enableDialogueReply = true;

        public int monologueCooldownTicks = 36000;

        public bool startDelayEnabled = true;
        public int startDelaySeconds = 10;
        public int startDelayTicks => startDelaySeconds * 60;

        public bool overlayEnabled = true;
        public float overlayOpacity = 0.75f;
        public int overlayMaxMessages = 8;
        public float overlayX = 20f;
        public float overlayY = 20f;
        public float overlayW = 420f;
        public float overlayH = 220f;

        public string dialogueCustomPrompt = "";

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
            Scribe_Values.Look(ref showThoughtNotification, "showThoughtNotification", false);

            Scribe_Values.Look(ref dialogueContextRounds, "dialogueContextRounds", 5);
            Scribe_Values.Look(ref enableDialogueReply, "enableDialogueReply", true);

            Scribe_Values.Look(ref monologueCooldownTicks, "monologueCooldownTicks", 36000);
            Scribe_Values.Look(ref startDelayEnabled, "startDelayEnabled", true);
            Scribe_Values.Look(ref startDelaySeconds, "startDelaySeconds", 10);
            Scribe_Values.Look(ref overlayEnabled, "overlayEnabled", true);
            Scribe_Values.Look(ref overlayOpacity, "overlayOpacity", 0.75f);
            Scribe_Values.Look(ref overlayMaxMessages, "overlayMaxMessages", 8);
            Scribe_Values.Look(ref overlayX, "overlayX", 20f);
            Scribe_Values.Look(ref overlayY, "overlayY", 20f);
            Scribe_Values.Look(ref overlayW, "overlayW", 420f);
            Scribe_Values.Look(ref overlayH, "overlayH", 220f);
            Scribe_Values.Look(ref dialogueCustomPrompt, "dialogueCustomPrompt", "");
        }

        private static Vector2 _settingsScrollPos = Vector2.zero;

        public static void DrawSettingsContent(UnityEngine.Rect inRect)
        {
            var s = Get();

            Rect contentArea = SettingsUIHelper.SplitContentArea(inRect);
            Rect bottomBar = SettingsUIHelper.SplitBottomBar(inRect);

            float contentH = EstimateSettingsHeight();
            Rect viewRect = new Rect(0f, 0f, contentArea.width - 16f, contentH);
            Widgets.BeginScrollView(contentArea, ref _settingsScrollPos, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            listing.CheckboxLabeled("RimMind.Dialogue.Settings.Enable".Translate(), ref s.enabled,
                "RimMind.Dialogue.Settings.Enable.Desc".Translate());

            SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Dialogue.Settings.TriggerSources".Translate());
            listing.CheckboxLabeled("RimMind.Dialogue.Settings.ChitchatIntercept".Translate(), ref s.chitchatEnabled,
                "RimMind.Dialogue.Settings.ChitchatIntercept.Desc".Translate());
            listing.CheckboxLabeled("RimMind.Dialogue.Settings.HediffReaction".Translate(), ref s.hediffEnabled,
                "RimMind.Dialogue.Settings.HediffReaction.Desc".Translate());
            listing.CheckboxLabeled("RimMind.Dialogue.Settings.LevelUpReaction".Translate(), ref s.levelUpEnabled,
                "RimMind.Dialogue.Settings.LevelUpReaction.Desc".Translate());
            listing.CheckboxLabeled("RimMind.Dialogue.Settings.ThoughtReaction".Translate(), ref s.thoughtEnabled,
                "RimMind.Dialogue.Settings.ThoughtReaction.Desc".Translate());
            if (s.thoughtEnabled)
            {
                listing.Label("  " + "RimMind.Dialogue.Settings.MoodThreshold".Translate($"{s.moodChangeThreshold:F0}"));
                s.moodChangeThreshold = listing.Slider(s.moodChangeThreshold, 1f, 5f);
            }
            listing.CheckboxLabeled("RimMind.Dialogue.Settings.AutoMonologue".Translate(), ref s.autoDialogueEnabled,
                "RimMind.Dialogue.Settings.AutoMonologue.Desc".Translate());
            if (s.autoDialogueEnabled)
            {
                GUI.color = Color.gray;
                listing.Label("  " + "RimMind.Dialogue.Settings.AutoMonologueDesc".Translate());
                GUI.color = Color.white;
                listing.Label("  " + "RimMind.Dialogue.Settings.AutoInterval".Translate(s.autoDialogueCooldownHours));
                s.autoDialogueCooldownHours = (int)listing.Slider(s.autoDialogueCooldownHours, 1f, 24f);
            }
            listing.CheckboxLabeled("RimMind.Dialogue.Settings.PlayerDialogueGizmo".Translate(), ref s.playerDialogueEnabled,
                "RimMind.Dialogue.Settings.PlayerDialogueGizmo.Desc".Translate());

            SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Dialogue.Settings.Section.Behavior".Translate());
            listing.Label("RimMind.Dialogue.Settings.MonologueCooldown".Translate($"{s.monologueCooldownTicks / 2500f:F1}", $"{s.monologueCooldownTicks}"));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Dialogue.Settings.MonologueCooldown.Desc".Translate());
            GUI.color = Color.white;
            s.monologueCooldownTicks = (int)listing.Slider(s.monologueCooldownTicks, 3600f, 72000f);
            s.monologueCooldownTicks = (s.monologueCooldownTicks / 600) * 600;

            listing.Label("RimMind.Dialogue.Settings.MaxDailyDialogues".Translate(s.maxDailyDialogueRounds));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Dialogue.Settings.MaxDailyDialogues.Desc".Translate());
            GUI.color = Color.white;
            s.maxDailyDialogueRounds = (int)listing.Slider(s.maxDailyDialogueRounds, 1f, 20f);

            listing.Label("RimMind.Dialogue.Settings.DialogueContextRounds".Translate(s.dialogueContextRounds));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Dialogue.Settings.DialogueContextRounds.Desc".Translate());
            GUI.color = Color.white;
            s.dialogueContextRounds = (int)listing.Slider(s.dialogueContextRounds, -1f, 20f);

            listing.CheckboxLabeled("RimMind.Dialogue.Settings.EnableDialogueReply".Translate(), ref s.enableDialogueReply,
                "RimMind.Dialogue.Settings.EnableDialogueReply.Desc".Translate());

            listing.CheckboxLabeled("RimMind.Dialogue.Settings.StartDelay".Translate(), ref s.startDelayEnabled,
                "RimMind.Dialogue.Settings.StartDelay.Desc".Translate());
            if (s.startDelayEnabled)
            {
                listing.Label("  " + "RimMind.Dialogue.Settings.StartDelayValue".Translate(s.startDelaySeconds));
                s.startDelaySeconds = (int)listing.Slider(s.startDelaySeconds, 1f, 60f);
            }

            SettingsUIHelper.DrawCustomPromptSection(listing,
                "RimMind.Dialogue.Settings.CustomPrompt".Translate(),
                ref s.dialogueCustomPrompt);

            SettingsUIHelper.DrawSectionHeader(listing, "RimMind.Dialogue.Settings.Section.Display".Translate());
            listing.CheckboxLabeled("RimMind.Dialogue.Settings.ShowNotification".Translate(), ref s.showThoughtNotification,
                "RimMind.Dialogue.Settings.ShowNotification.Desc".Translate());
            listing.CheckboxLabeled("RimMind.Dialogue.Settings.ShowOverlay".Translate(), ref s.overlayEnabled,
                "RimMind.Dialogue.Settings.ShowOverlay.Desc".Translate());
            listing.Label("RimMind.Dialogue.Settings.OverlayOpacity".Translate($"{s.overlayOpacity:P0}"));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Dialogue.Settings.OverlayOpacity.Desc".Translate());
            GUI.color = Color.white;
            s.overlayOpacity = listing.Slider(s.overlayOpacity, 0.1f, 1f);
            listing.Label("RimMind.Dialogue.Settings.OverlayMaxMessages".Translate(s.overlayMaxMessages));
            GUI.color = Color.gray;
            listing.Label("  " + "RimMind.Dialogue.Settings.OverlayMaxMessages.Desc".Translate());
            GUI.color = Color.white;
            s.overlayMaxMessages = (int)listing.Slider(s.overlayMaxMessages, 3f, 20f);

            listing.End();
            Widgets.EndScrollView();

            SettingsUIHelper.DrawBottomBar(bottomBar, () =>
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
                s.showThoughtNotification = false;
                s.dialogueContextRounds = 5;
                s.enableDialogueReply = true;
                s.monologueCooldownTicks = 36000;
                s.startDelayEnabled = true;
                s.startDelaySeconds = 10;
                s.overlayEnabled = true;
                s.overlayOpacity = 0.75f;
                s.overlayMaxMessages = 8;
                s.dialogueCustomPrompt = "";
                s.overlayX = 20f;
                s.overlayY = 20f;
                s.overlayW = 420f;
                s.overlayH = 220f;
            });

            Get().Write();
        }

        private static float EstimateSettingsHeight()
        {
            var s = Get();
            float h = 30f;
            h += 24f;
            h += 24f + 24f * 6;
            if (s.thoughtEnabled)
                h += 24f + 32f;
            if (s.autoDialogueEnabled)
                h += 24f + 24f + 32f;
            h += 24f + 24f + 32f + 24f + 32f + 24f + 32f + 24f + 24f;
            if (s.startDelayEnabled)
                h += 24f + 32f;
            h += 24f + 80f;
            h += 24f + 24f + 24f + 24f + 32f + 24f + 32f;
            return h + 40f;
        }
    }
}
