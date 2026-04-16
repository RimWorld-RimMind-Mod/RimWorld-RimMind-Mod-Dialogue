using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.Dialogue.Core;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.UI
{
    public class Window_DialogueLog : Window
    {
        private DialogueCategory _selectedCategory = DialogueCategory.ColonistMonologue;
        private string? _selectedTab;
        private Vector2 _categoryScrollPos;
        private Vector2 _contentScrollPos;
        private const float TabWidth = 160f;
        private const float Padding = 6f;
        private const float TabHeight = 28f;

        private static readonly (DialogueCategory cat, string key)[] CategoryKeys = new[]
        {
            (DialogueCategory.ColonistMonologue, "RimMind.Dialogue.UI.Log.ColonistMonologue"),
            (DialogueCategory.ColonistDialogue, "RimMind.Dialogue.UI.Log.ColonistDialogue"),
            (DialogueCategory.PlayerDialogue, "RimMind.Dialogue.UI.Log.PlayerDialogue"),
            (DialogueCategory.NonColonistMonologue, "RimMind.Dialogue.UI.Log.NonColonistMonologue"),
            (DialogueCategory.NonColonistDialogue, "RimMind.Dialogue.UI.Log.NonColonistDialogue"),
        };

        public override Vector2 InitialSize => new Vector2(720f, 560f);

        public Window_DialogueLog()
        {
            forcePause = false;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Small;

            float headerHeight = 30f;
            float categoryBarHeight = 32f;
            float contentY = inRect.y + headerHeight + Padding;
            float contentHeight = inRect.height - headerHeight - Padding;

            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, headerHeight);
            Rect categoryBarRect = new Rect(inRect.x, contentY, inRect.width, categoryBarHeight);
            contentY += categoryBarHeight + Padding;
            contentHeight -= categoryBarHeight + Padding;

            Widgets.Label(headerRect, "RimMind.Dialogue.UI.Log.Title".Translate());

            DrawCategoryBar(categoryBarRect);

            Rect leftRect = new Rect(inRect.x, contentY, TabWidth, contentHeight);
            Rect rightRect = new Rect(inRect.x + TabWidth + Padding, contentY,
                inRect.width - TabWidth - Padding, contentHeight);

            DrawTabList(leftRect);
            DrawContent(rightRect);
        }

        private void DrawCategoryBar(Rect rect)
        {
            float x = rect.x;
            foreach (var (cat, key) in CategoryKeys)
            {
                string label = key.Translate();
                float width = Text.CalcSize(label).x + 20f;
                var btnRect = new Rect(x, rect.y, width, rect.height);

                bool selected = _selectedCategory == cat;
                if (selected)
                    Widgets.DrawBoxSolid(btnRect, new Color(0.3f, 0.4f, 0.6f, 0.5f));

                if (Widgets.ButtonText(btnRect, label))
                {
                    _selectedCategory = cat;
                    _selectedTab = null;
                }

                x += width + 4f;
            }
        }

        private void DrawTabList(Rect rect)
        {
            var entries = GetEntriesForCategory(_selectedCategory);
            var tabs = GetTabs(entries);

            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.15f, 0.5f));

            if (tabs.Count == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.grey;
                Widgets.Label(rect, "RimMind.Dialogue.UI.Log.NoRecords".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            float contentHeight = tabs.Count * TabHeight;
            Rect viewRect = new Rect(rect.x, rect.y, rect.width - 16f, contentHeight);
            Widgets.BeginScrollView(rect, ref _categoryScrollPos, viewRect);

            float y = rect.y;
            foreach (var tab in tabs)
            {
                var tabRect = new Rect(viewRect.x, y, viewRect.width, TabHeight);
                bool selected = _selectedTab == tab;

                if (selected)
                    Widgets.DrawBoxSolid(tabRect, new Color(0.25f, 0.35f, 0.55f, 0.6f));

                if (Widgets.ButtonText(tabRect.ContractedBy(2f), tab))
                    _selectedTab = tab;

                y += TabHeight;
            }

            Widgets.EndScrollView();
        }

        private void DrawContent(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.08f, 0.12f, 0.4f));

            if (_selectedTab == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.grey;
                Widgets.Label(rect, "RimMind.Dialogue.UI.Log.SelectTab".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            var entries = GetEntriesForCategory(_selectedCategory);
            var filtered = GetFilteredEntries(entries, _selectedTab);

            if (_selectedCategory == DialogueCategory.ColonistDialogue
                || _selectedCategory == DialogueCategory.NonColonistDialogue)
            {
                DrawDialogueContent(rect, filtered);
            }
            else
            {
                DrawMonologueContent(rect, filtered);
            }
        }

        private void DrawMonologueContent(Rect rect, List<DialogueLogEntry> entries)
        {
            float contentHeight = 0f;
            float[] heights = new float[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                heights[i] = Text.CalcHeight(FormatEntry(entries[i]), rect.width - 32f) + Padding;
                contentHeight += heights[i];
            }

            Rect viewRect = new Rect(rect.x, rect.y, rect.width - 16f, contentHeight);
            Widgets.BeginScrollView(rect, ref _contentScrollPos, viewRect);

            float y = rect.y;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                string line = FormatEntry(entry);

                Color bgColor = i % 2 == 0
                    ? new Color(1f, 1f, 1f, 0.02f)
                    : new Color(0f, 0f, 0f, 0.02f);
                Widgets.DrawBoxSolid(new Rect(viewRect.x, y, viewRect.width, heights[i]), bgColor);

                GUI.color = GetTriggerColor(entry.trigger);
                Widgets.Label(new Rect(viewRect.x + Padding, y, viewRect.width - Padding * 2, heights[i]), line);
                GUI.color = Color.white;

                y += heights[i];
            }

            Widgets.EndScrollView();
        }

        private void DrawDialogueContent(Rect rect, List<DialogueLogEntry> entries)
        {
            string[] names = _selectedTab!.Split('|');
            string leftName = names.Length > 0 ? names[0] : "";
            string rightName = names.Length > 1 ? names[1] : "";

            float halfWidth = (rect.width - 16f - Padding) / 2f;

            float headerH = 24f;
            var leftHeaderRect = new Rect(rect.x, rect.y, halfWidth, headerH);
            var rightHeaderRect = new Rect(rect.x + halfWidth + Padding, rect.y, halfWidth, headerH);

            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.7f, 0.85f, 1f);
            Widgets.Label(leftHeaderRect, leftName);
            GUI.color = new Color(1f, 0.95f, 0.8f);
            Widgets.Label(rightHeaderRect, rightName);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            Rect contentRect = new Rect(rect.x, rect.y + headerH + Padding,
                rect.width, rect.height - headerH - Padding);

            float contentHeight = 0f;
            float[] heights = new float[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                heights[i] = Text.CalcHeight(entries[i].reply, halfWidth - Padding * 2) + Padding * 2;
                contentHeight += heights[i];
            }

            Rect viewRect = new Rect(contentRect.x, contentRect.y, contentRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(contentRect, ref _contentScrollPos, viewRect);

            float y = contentRect.y;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                bool isLeft = entry.initiatorName == leftName;

                string timeStr = entry.TimeStr;
                float entryHeight = heights[i];

                Color bgColor = i % 2 == 0
                    ? new Color(1f, 1f, 1f, 0.02f)
                    : new Color(0f, 0f, 0f, 0.02f);
                Widgets.DrawBoxSolid(new Rect(viewRect.x, y, viewRect.width, entryHeight), bgColor);

                if (isLeft)
                {
                    var leftRect = new Rect(viewRect.x + Padding, y + Padding, halfWidth - Padding * 2, entryHeight - Padding * 2);
                    GUI.color = new Color(0.7f, 0.85f, 1f);
                    Widgets.Label(leftRect, $"[{timeStr}] {entry.reply}");
                    GUI.color = Color.white;
                }
                else
                {
                    var rightRect = new Rect(viewRect.x + halfWidth + Padding, y + Padding, halfWidth - Padding * 2, entryHeight - Padding * 2);
                    GUI.color = new Color(1f, 0.95f, 0.8f);
                    Widgets.Label(rightRect, $"[{timeStr}] {entry.reply}");
                    GUI.color = Color.white;
                }

                y += entryHeight;
            }

            Widgets.EndScrollView();
        }

        private List<DialogueLogEntry> GetEntriesForCategory(DialogueCategory category)
        {
            if (category == DialogueCategory.PlayerDialogue)
            {
                return RimMindDialogueService.LogEntries
                    .Where(e => e.trigger == "RimMind.Dialogue.Trigger.PlayerInput".Translate())
                    .ToList();
            }
            return RimMindDialogueService.LogEntries
                .Where(e => e.category == category && e.trigger != "RimMind.Dialogue.Trigger.PlayerInput".Translate())
                .ToList();
        }

        private List<string> GetTabs(List<DialogueLogEntry> entries)
        {
            if (_selectedCategory == DialogueCategory.ColonistDialogue
                || _selectedCategory == DialogueCategory.NonColonistDialogue)
            {
                return entries.Select(e => e.PairKey).Distinct().ToList();
            }
            return entries.Select(e => e.initiatorName).Distinct().ToList();
        }

        private List<DialogueLogEntry> GetFilteredEntries(List<DialogueLogEntry> entries, string tab)
        {
            if (_selectedCategory == DialogueCategory.ColonistDialogue
                || _selectedCategory == DialogueCategory.NonColonistDialogue)
            {
                return entries.Where(e => e.PairKey == tab).ToList();
            }
            return entries.Where(e => e.initiatorName == tab).ToList();
        }

        private static string FormatEntry(DialogueLogEntry entry)
        {
            string result = $"[{entry.TimeStr}] ({entry.trigger}) {entry.reply}";
            if (entry.thoughtTag != "NONE")
                result += $" [{entry.thoughtTag}]";
            return result;
        }

        private static Color GetTriggerColor(string trigger)
        {
            if (trigger == "RimMind.Dialogue.Trigger.Chitchat".Translate()) return new Color(0.7f, 0.85f, 1f);
            if (trigger == "RimMind.Dialogue.Trigger.Hediff".Translate()) return new Color(1f, 0.6f, 0.6f);
            if (trigger == "RimMind.Dialogue.Trigger.LevelUp".Translate()) return new Color(0.6f, 1f, 0.6f);
            if (trigger == "RimMind.Dialogue.Trigger.Thought".Translate()) return new Color(1f, 0.95f, 0.6f);
            if (trigger == "RimMind.Dialogue.Trigger.Auto".Translate()) return new Color(0.8f, 0.8f, 0.8f);
            if (trigger == "RimMind.Dialogue.Trigger.PlayerInput".Translate()) return new Color(0.85f, 0.7f, 1f);
            return Color.white;
        }
    }
}
