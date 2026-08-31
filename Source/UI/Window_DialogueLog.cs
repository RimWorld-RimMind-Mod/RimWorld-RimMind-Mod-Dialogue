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

                // 标签键基于 ID，但显示文本仍为小人名字
                string displayLabel = GetTabDisplayLabel(tab, entries);
                if (Widgets.ButtonText(tabRect.ContractedBy(2f), displayLabel))
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
            // _selectedTab 是基于 ID 的 PairKey（如 "101|202"），解析为左右 ID
            string[] parts = _selectedTab!.Split('|');
            int leftId = parts.Length > 0 && int.TryParse(parts[0], out var l) ? l : -1;
            int rightId = parts.Length > 1 && int.TryParse(parts[1], out var r) ? r : -1;

            // 表头仍显示名字（通过 ID 查表），保持用户可读性
            string leftName = leftId >= 0 ? GetPawnNameForId(entries, leftId) : "";
            string rightName = rightId >= 0 ? GetPawnNameForId(entries, rightId) : "";

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
                // 用 ID 判定左右列，避免重名导致列归属错误
                bool isLeft = entry.initiatorId == leftId;

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
                    .Where(e => e.trigger == "PlayerInput")
                    .ToList();
            }
            return RimMindDialogueService.LogEntries
                .Where(e => e.category == category && e.trigger != "PlayerInput")
                .ToList();
        }

        private List<string> GetTabs(List<DialogueLogEntry> entries)
        {
            if (_selectedCategory == DialogueCategory.ColonistDialogue
                || _selectedCategory == DialogueCategory.NonColonistDialogue)
            {
                // 对话：PairKey 已基于 ID
                return entries.Select(e => e.PairKey).Distinct().ToList();
            }
            // 独白：用 initiatorId 作为标签键，避免重名小人合并到同一标签
            return entries.Select(e => e.initiatorId.ToString()).Distinct().ToList();
        }

        private List<DialogueLogEntry> GetFilteredEntries(List<DialogueLogEntry> entries, string tab)
        {
            if (_selectedCategory == DialogueCategory.ColonistDialogue
                || _selectedCategory == DialogueCategory.NonColonistDialogue)
            {
                return entries.Where(e => e.PairKey == tab).ToList();
            }
            // 独白：按 initiatorId 过滤
            if (int.TryParse(tab, out int id))
                return entries.Where(e => e.initiatorId == id).ToList();
            return new List<DialogueLogEntry>();
        }

        /// <summary>
        /// 将基于 ID 的标签键转换为用户可读的名字标签。
        /// 对话标签键形如 "101|202"，独白标签键形如 "303"。
        /// </summary>
        private string GetTabDisplayLabel(string tabKey, List<DialogueLogEntry> entries)
        {
            if (_selectedCategory == DialogueCategory.ColonistDialogue
                || _selectedCategory == DialogueCategory.NonColonistDialogue)
            {
                string[] parts = tabKey.Split('|');
                if (parts.Length >= 2
                    && int.TryParse(parts[0], out int leftId)
                    && int.TryParse(parts[1], out int rightId))
                {
                    string leftName = GetPawnNameForId(entries, leftId);
                    string rightName = GetPawnNameForId(entries, rightId);
                    return $"{leftName} | {rightName}";
                }
                return tabKey;
            }
            // 独白：tabKey 即 initiatorId
            if (int.TryParse(tabKey, out int monoId))
                return GetPawnNameForId(entries, monoId);
            return tabKey;
        }

        /// <summary>
        /// 从日志条目中按小人 ID 反查显示名字。
        /// 优先匹配 initiatorId，其次 recipientId；找不到则回退为 ID 字符串。
        /// </summary>
        private static string GetPawnNameForId(List<DialogueLogEntry> entries, int id)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.initiatorId == id) return e.initiatorName;
                if (e.recipientId == id && e.recipientName != null) return e.recipientName;
            }
            return id.ToString();
        }

        private static string FormatEntry(DialogueLogEntry entry)
        {
            string triggerLabel = TranslateTrigger(entry.trigger);
            string result = $"[{entry.TimeStr}] ({triggerLabel}) {entry.reply}";
            if (entry.thoughtTag != "NONE")
                result += $" [{entry.thoughtTag}]";
            return result;
        }

        private static string TranslateTrigger(string triggerKey)
        {
            if (RimMindDialogueService.RegisteredTriggerLabels.TryGetValue(triggerKey, out var labelKey))
                return labelKey.Translate();

            return triggerKey switch
            {
                "Chitchat" => "RimMind.Dialogue.Trigger.Chitchat".Translate(),
                "Hediff" => "RimMind.Dialogue.Trigger.Hediff".Translate(),
                "LevelUp" => "RimMind.Dialogue.Trigger.LevelUp".Translate(),
                "Thought" => "RimMind.Dialogue.Trigger.Thought".Translate(),
                "Auto" => "RimMind.Dialogue.Trigger.Auto".Translate(),
                "PlayerInput" => "RimMind.Dialogue.Trigger.PlayerInput".Translate(),
                _ => triggerKey,
            };
        }

        private static Color GetTriggerColor(string trigger)
        {
            return trigger switch
            {
                "Chitchat" => new Color(0.7f, 0.85f, 1f),
                "Hediff" => new Color(1f, 0.6f, 0.6f),
                "LevelUp" => new Color(0.6f, 1f, 0.6f),
                "Thought" => new Color(1f, 0.95f, 0.6f),
                "Auto" => new Color(0.8f, 0.8f, 0.8f),
                "PlayerInput" => new Color(0.85f, 0.7f, 1f),
                _ => Color.white,
            };
        }
    }
}
