using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Dialogue.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Overlay
{
    public class DialogueOverlay : MapComponent
    {
        private bool _isDragging;
        private bool _isResizing;
        private bool _positionDirty;
        private Vector2 _dragStartOffset;
        private Rect _windowRect;
        private bool _cacheDirty = true;
        private bool _temporarilyClosed;
        private bool _lastEnabledState;
        private List<DialogueLogEntry> _cachedEntries = new List<DialogueLogEntry>();

        private const float OptionsBarHeight = 24f;
        private const float ResizeHandleSize = 24f;
        private const float TextPadding = 4f;
        private const float MinWidth = 300f;
        private const float MinHeight = 100f;

        public DialogueOverlay(Map map) : base(map)
        {
            RimMindDialogueService.OnLogUpdated += () => _cacheDirty = true;
            LoadPositionFromSettings();
        }

        public override void MapComponentOnGUI()
        {
            if (Current.ProgramState != ProgramState.Playing) return;

            var settings = RimMindDialogueSettings.Get();
            bool currentlyEnabled = settings.overlayEnabled;
            if (currentlyEnabled && !_lastEnabledState)
                _temporarilyClosed = false;
            _lastEnabledState = currentlyEnabled;

            if (!currentlyEnabled || _temporarilyClosed) return;

            HandleInput();

            bool isMouseOver = Mouse.IsOver(_windowRect);

            GUI.BeginGroup(_windowRect);
            var inRect = new Rect(Vector2.zero, _windowRect.size);

            Widgets.DrawBoxSolid(inRect, new Color(0.08f, 0.08f, 0.12f, settings.overlayOpacity));

            DrawMessages(inRect);

            if (isMouseOver)
            {
                DrawOptionsBar(inRect);

                var resizeRect = new Rect(inRect.width - ResizeHandleSize, inRect.height - ResizeHandleSize,
                    ResizeHandleSize, ResizeHandleSize);
                GUI.DrawTexture(resizeRect, TexUI.WinExpandWidget);
                TooltipHandler.TipRegion(resizeRect, "RimMind.Dialogue.UI.Overlay.DragResize".Translate());
            }

            GUI.EndGroup();

            if (_positionDirty)
            {
                SavePositionToSettings();
                _positionDirty = false;
            }
        }

        private void DrawMessages(Rect inRect)
        {
            if (_cacheDirty)
            {
                _cachedEntries = RimMindDialogueService.LogEntries
                    .TakeLast(RimMindDialogueSettings.Get().overlayMaxMessages)
                    .ToList();
                _cacheDirty = false;
            }

            if (_cachedEntries.Count == 0) return;

            var contentRect = inRect.ContractedBy(TextPadding);
            contentRect.yMin += OptionsBarHeight;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            float y = contentRect.y;
            for (int i = 0; i < _cachedEntries.Count; i++)
            {
                var entry = _cachedEntries[i];
                string name = entry.initiatorName;
                string label = entry.IsMonologue ? name : $"{name}→{entry.recipientName}";
                string formattedLabel = $"[{label}]";

                float labelWidth = Text.CalcSize(formattedLabel).x;
                float availableWidth = contentRect.width - labelWidth - TextPadding;
                if (availableWidth < 50f) availableWidth = 50f;

                float lineHeight = Mathf.Max(
                    Text.CalcHeight(formattedLabel, labelWidth),
                    Text.CalcHeight(entry.reply, availableWidth)) + 2f;

                if (y + lineHeight > contentRect.yMax) break;

                var labelRect = new Rect(contentRect.x, y, labelWidth, lineHeight);
                var replyRect = new Rect(contentRect.x + labelWidth + TextPadding, y, availableWidth, lineHeight);

                Color nameColor = GetCategoryColor(entry.category);
                GUI.color = nameColor;
                Widgets.Label(labelRect, formattedLabel);
                GUI.color = Color.white;
                Widgets.Label(replyRect, entry.reply);

                y += lineHeight;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawOptionsBar(Rect inRect)
        {
            var barRect = new Rect(inRect.x, inRect.y, inRect.width, OptionsBarHeight);
            Widgets.DrawBoxSolid(barRect, new Color(0.05f, 0.05f, 0.08f, 0.8f));

            var titleRect = new Rect(barRect.x + 4f, barRect.y, 100f, barRect.height);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.7f, 0.8f, 1f);
            Widgets.Label(titleRect, "RimMind.Dialogue.UI.Overlay.Title".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            var openBtnRect = new Rect(barRect.xMax - 60f, barRect.y + 2f, 56f, barRect.height - 4f);
            var closeBtnRect = new Rect(barRect.xMax - 82f, barRect.y + 2f, 20f, barRect.height - 4f);
            if (Widgets.ButtonText(closeBtnRect, "X"))
            {
                _temporarilyClosed = true;
            }
            if (Widgets.ButtonText(openBtnRect, "RimMind.Dialogue.UI.Overlay.Details".Translate()))
            {
                Find.WindowStack.Add(new Window_DialogueLog());
            }
        }

        private void HandleInput()
        {
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                var openBtnScreenRect = new Rect(
                    _windowRect.xMax - 60f, _windowRect.y + 2f, 56f, OptionsBarHeight - 4f);

                var closeBtnScreenRect = new Rect(
                    _windowRect.xMax - 82f, _windowRect.y + 2f, 20f, OptionsBarHeight - 4f);

                var resizeScreenRect = new Rect(
                    _windowRect.xMax - ResizeHandleSize, _windowRect.yMax - ResizeHandleSize,
                    ResizeHandleSize, ResizeHandleSize);

                if (resizeScreenRect.Contains(currentEvent.mousePosition))
                {
                    _isResizing = true;
                    currentEvent.Use();
                }
                else if (!openBtnScreenRect.Contains(currentEvent.mousePosition)
                    && !closeBtnScreenRect.Contains(currentEvent.mousePosition))
                {
                    var dragRect = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, OptionsBarHeight);
                    if (dragRect.Contains(currentEvent.mousePosition))
                    {
                        _isDragging = true;
                        _dragStartOffset = currentEvent.mousePosition - _windowRect.position;
                        currentEvent.Use();
                    }
                }
            }
            else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            {
                if (_isDragging || _isResizing)
                    _positionDirty = true;
                _isDragging = false;
                _isResizing = false;
            }
            else if (currentEvent.type == EventType.MouseDrag)
            {
                if (_isResizing)
                {
                    float desiredWidth = currentEvent.mousePosition.x - _windowRect.x;
                    float desiredHeight = currentEvent.mousePosition.y - _windowRect.y;

                    float maxWidth = Verse.UI.screenWidth - _windowRect.x;
                    float maxHeight = Verse.UI.screenHeight - _windowRect.y;

                    _windowRect.width = Mathf.Clamp(desiredWidth, MinWidth, maxWidth);
                    _windowRect.height = Mathf.Clamp(desiredHeight, MinHeight, maxHeight);
                    currentEvent.Use();
                }
                else if (_isDragging)
                {
                    _windowRect.position = currentEvent.mousePosition - _dragStartOffset;
                    _windowRect.x = Mathf.Clamp(_windowRect.x, 0, Verse.UI.screenWidth - _windowRect.width);
                    _windowRect.y = Mathf.Clamp(_windowRect.y, 0, Verse.UI.screenHeight - _windowRect.height);
                    currentEvent.Use();
                }
            }
        }

        private static Color GetCategoryColor(DialogueCategory category)
        {
            return category switch
            {
                DialogueCategory.ColonistMonologue => new Color(0.6f, 1f, 0.6f),
                DialogueCategory.ColonistDialogue => new Color(0.7f, 0.85f, 1f),
                DialogueCategory.PlayerDialogue => new Color(0.85f, 0.7f, 1f),
                DialogueCategory.NonColonistMonologue => new Color(1f, 0.8f, 0.6f),
                DialogueCategory.NonColonistDialogue => new Color(1f, 0.6f, 0.6f),
                _ => Color.white
            };
        }

        private void LoadPositionFromSettings()
        {
            var s = RimMindDialogueSettings.Get();
            _windowRect = new Rect(s.overlayX, s.overlayY, s.overlayW, s.overlayH);
        }

        private void SavePositionToSettings()
        {
            var s = RimMindDialogueSettings.Get();
            s.overlayX = _windowRect.x;
            s.overlayY = _windowRect.y;
            s.overlayW = _windowRect.width;
            s.overlayH = _windowRect.height;
        }
    }

    [HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
    public static class DialogueOverlayPatch
    {
        private static bool _skip;

        private static void DrawOverlay()
        {
            _skip = !_skip;
            if (_skip) return;
            if (Current.ProgramState != ProgramState.Playing) return;

            var mapComp = Find.CurrentMap?.GetComponent<DialogueOverlay>();
            mapComp?.MapComponentOnGUI();
        }

        [HarmonyPrefix]
        public static void Prefix() => DrawOverlay();

        [HarmonyPostfix]
        public static void Postfix() => DrawOverlay();
    }
}
