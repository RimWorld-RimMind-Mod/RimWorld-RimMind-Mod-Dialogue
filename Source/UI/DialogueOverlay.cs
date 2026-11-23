using System;
using System.Collections.Generic;
using System.Linq;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Dialogue.UI;
using RimMind.Presentation.Api;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Overlay
{
    public class DialogueOverlay : MapComponent
    {
        private bool _isDragging;
        private bool _isResizing;
        private bool _positionDirty;
        private bool _wasHovered;
        private Vector2 _dragStartOffset;
        private Rect _windowRect;
        private bool _cacheDirty = true;
        private int _cachedLogRevision = -1;
        private int _cachedMaxMessages = -1;
        private bool _temporarilyClosed;
        private bool _lastEnabledState;
        private List<DialogueLogEntry> _cachedEntries = new List<DialogueLogEntry>();
        private string[] _cachedLabels = Array.Empty<string>();
        private string[] _cachedDisplayReplies = Array.Empty<string>();
        private float[] _cachedLabelWidths = Array.Empty<float>();
        private float[] _cachedLineHeights = Array.Empty<float>();
        private float _cachedLayoutWidth = -1f;
        private readonly HashSet<int> _liveColonistIds = new HashSet<int>();
        private int _lastLiveColonistRefreshTick = -99999;
        private int _cachedLiveColonistsHash;
        private int _nextEntryExpiryTick = -1;

        private const int LiveColonistCacheIntervalTicks = 60;
        private const int MaxEntryAgeTicks = 60000; // 1 in-game day
        private const float OptionsBarHeight = 24f;
        private const float ResizeHandleSize = 24f;
        private const float TextPadding = 6f;
        private const float MinWidth = 300f;
        private const float MinHeight = 100f;
        private const float MinAutoFitHeight = 34f;
        private const float MinHoveredHeight = 76f;
        private const float AlertsMargin = 245f;
        private const float TopColonistBarMargin = 96f;

        public DialogueOverlay(Map map) : base(map)
        {
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

            NormalizeWindowRect();
            EnsureCachedEntries(settings);
            EnsureLayoutMetrics(_windowRect.width - TextPadding * 2f);

            float maxAllowedRenderH = Mathf.Min(
                _windowRect.height,
                Mathf.Max(MinAutoFitHeight, Verse.UI.screenHeight - _windowRect.y));
            float idleHeight = DialogueOverlayLayout.ComputeRenderHeight(
                _cachedLineHeights,
                maxAllowedRenderH,
                MinAutoFitHeight,
                TextPadding * 2f,
                forceExpandToMax: false);
            float hoveredHeight = DialogueOverlayLayout.ComputeRenderHeight(
                _cachedLineHeights,
                Mathf.Max(maxAllowedRenderH, MinHoveredHeight),
                MinHoveredHeight,
                OptionsBarHeight + TextPadding * 2f,
                forceExpandToMax: _isResizing);

            float hoverTestHeight = _cachedEntries.Count == 0
                ? MinHoveredHeight
                : (_wasHovered ? hoveredHeight : idleHeight);
            var hoverHitRect = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, hoverTestHeight);
            bool isMouseOver = Mouse.IsOver(hoverHitRect);
            bool showChrome = isMouseOver || _isDragging || _isResizing;
            _wasHovered = showChrome;

            var renderRect = new Rect(
                _windowRect.x,
                _windowRect.y,
                _windowRect.width,
                showChrome ? hoveredHeight : idleHeight);

            HandleInput(renderRect);
            NormalizeWindowRect();

            if (_isDragging || _isResizing)
            {
                EnsureLayoutMetrics(_windowRect.width - TextPadding * 2f);
                float updatedMaxH = Mathf.Min(
                    _windowRect.height,
                    Mathf.Max(MinHoveredHeight, Verse.UI.screenHeight - _windowRect.y));
                float updatedHeight = DialogueOverlayLayout.ComputeRenderHeight(
                    _cachedLineHeights,
                    updatedMaxH,
                    MinHoveredHeight,
                    OptionsBarHeight + TextPadding * 2f,
                    forceExpandToMax: _isResizing);
                renderRect = new Rect(_windowRect.x, _windowRect.y, _windowRect.width, updatedHeight);
            }

            if (!DialogueOverlayLayout.ShouldRender(_cachedEntries.Count, isMouseOver, _isDragging, _isResizing))
            {
                if (_positionDirty)
                {
                    SavePositionToSettings();
                    _positionDirty = false;
                }
                return;
            }

            Color previousColor = GUI.color;
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            GUI.BeginGroup(renderRect);
            try
            {
                var inRect = new Rect(Vector2.zero, renderRect.size);

                Widgets.DrawBoxSolid(inRect, new Color(0.08f, 0.08f, 0.12f, settings.overlayOpacity));

                DrawMessages(inRect, reserveTopChrome: showChrome);

                if (showChrome)
                {
                    DrawOptionsBar(inRect);

                    var resizeRect = new Rect(inRect.width - ResizeHandleSize, inRect.height - ResizeHandleSize,
                        ResizeHandleSize, ResizeHandleSize);
                    GUI.DrawTexture(resizeRect, TexUI.WinExpandWidget);
                    TooltipHandler.TipRegion(resizeRect, RimMindAPI.Text.TrOr("RimMind.Dialogue.UI.Overlay.DragResize", "Drag to resize"));
                }
            }
            finally
            {
                GUI.EndGroup();
                GUI.color = previousColor;
                Text.Font = previousFont;
                Text.Anchor = previousAnchor;
            }

            if (_positionDirty)
            {
                SavePositionToSettings();
                _positionDirty = false;
            }
        }

        private void EnsureCachedEntries(RimMindDialogueSettings settings)
        {
            int maxMessages = Math.Max(0, settings.overlayMaxMessages);
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            int currentLogRevision = DialogueLogStore.CurrentRevision;

            if (_lastLiveColonistRefreshTick < 0
                || nowTick < _lastLiveColonistRefreshTick
                || (nowTick - _lastLiveColonistRefreshTick) >= LiveColonistCacheIntervalTicks)
            {
                RefreshLiveColonistCache(nowTick);
            }

            if (_nextEntryExpiryTick > 0 && nowTick > _nextEntryExpiryTick)
            {
                _cacheDirty = true;
            }

            if (_cacheDirty || _cachedLogRevision != currentLogRevision || _cachedMaxMessages != maxMessages)
            {
                bool IsPawnActiveOnCurrentMap(int pawnId, bool isColonist)
                {
                    if (isColonist)
                        return _liveColonistIds.Contains(pawnId);

                    var p = RimMindPawnLookup.FindPawnByNumber(pawnId);
                    return p != null && !p.Dead && !p.Destroyed && p.Spawned && p.Map == map;
                }

                _cachedEntries = RimMindDialogueService.LogEntries
                    .Where(entry =>
                        (nowTick <= 0 || entry.tick <= 0 || (nowTick - entry.tick) <= MaxEntryAgeTicks)
                        && IsPawnActiveOnCurrentMap(entry.initiatorId, entry.initiatorIsColonist)
                        && (entry.recipientId <= 0 || IsPawnActiveOnCurrentMap(entry.recipientId, entry.recipientIsColonist)))
                    .TakeLast(maxMessages)
                    .ToList();

                int count = _cachedEntries.Count;
                _cachedLabels = count == 0 ? Array.Empty<string>() : new string[count];
                _cachedDisplayReplies = count == 0 ? Array.Empty<string>() : new string[count];
                _cachedLabelWidths = count == 0 ? Array.Empty<float>() : new float[count];
                _cachedLineHeights = count == 0 ? Array.Empty<float>() : new float[count];
                int nextExpiry = -1;
                for (int i = 0; i < count; i++)
                {
                    DialogueLogEntry entry = _cachedEntries[i];
                    if (entry.tick > 0)
                    {
                        int expiry = entry.tick + MaxEntryAgeTicks;
                        if (nextExpiry < 0 || expiry < nextExpiry)
                            nextExpiry = expiry;
                    }
                    string name = entry.initiatorName ?? string.Empty;
                    _cachedLabels[i] = entry.IsMonologue
                        ? $"[{name}]"
                        : $"[{name}→{entry.recipientName ?? string.Empty}]";
                    _cachedDisplayReplies[i] = FormatCompactOverlayText(entry.reply);
                }

                _nextEntryExpiryTick = nextExpiry;
                _cachedLayoutWidth = -1f;
                _cachedLogRevision = currentLogRevision;
                _cachedMaxMessages = maxMessages;
                _cacheDirty = false;
            }
        }

        private void RefreshLiveColonistCache(int nowTick)
        {
            _lastLiveColonistRefreshTick = nowTick;
            _liveColonistIds.Clear();
            int liveHash = 0;
            var spawned = RimMindPawnLookup.GetSpawnedColonistsSafe(map);
            for (int i = 0; i < spawned.Count; i++)
            {
                var p = spawned[i];
                if (p != null && RimMindPawnLookup.IsActiveSpawnedColonist(p))
                {
                    _liveColonistIds.Add(p.thingIDNumber);
                    liveHash = unchecked(liveHash * 397 ^ p.thingIDNumber);
                }
            }

            if (liveHash != _cachedLiveColonistsHash)
            {
                _cachedLiveColonistsHash = liveHash;
                _cacheDirty = true;
            }
        }

        private void EnsureLayoutMetrics(float contentWidth)
        {
            if (_cachedEntries.Count == 0)
            {
                _cachedLayoutWidth = contentWidth;
                return;
            }

            float safeWidth = Mathf.Max(20f, contentWidth);
            if (Mathf.Abs(_cachedLayoutWidth - safeWidth) <= 0.5f)
                return;

            GameFont prevFont = Text.Font;
            Text.Font = GameFont.Small;
            try
            {
                float maxLabelWidth = Mathf.Max(60f, safeWidth * 0.4f);
                for (int i = 0; i < _cachedEntries.Count; i++)
                {
                    _cachedLabelWidths[i] = Mathf.Min(maxLabelWidth, Text.CalcSize(_cachedLabels[i]).x);
                    float replyWidth = Mathf.Max(1f, safeWidth - _cachedLabelWidths[i] - TextPadding);
                    _cachedLineHeights[i] = Mathf.Max(
                        Text.CalcHeight(_cachedLabels[i], Mathf.Max(1f, _cachedLabelWidths[i])),
                        Text.CalcHeight(_cachedDisplayReplies[i], replyWidth)) + 2f;
                }
                _cachedLayoutWidth = safeWidth;
            }
            finally
            {
                Text.Font = prevFont;
            }
        }

        private void DrawMessages(Rect inRect, bool reserveTopChrome)
        {
            if (_cachedEntries.Count == 0) return;

            var contentRect = inRect.ContractedBy(TextPadding);
            if (reserveTopChrome)
                contentRect.yMin += OptionsBarHeight;

            if (contentRect.height <= 0f || contentRect.width <= 0f)
                return;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            EnsureLayoutMetrics(contentRect.width);

            int firstVisible = DialogueOverlayLayout.FindFirstVisibleIndex(_cachedLineHeights, contentRect.height);
            float y = contentRect.y;
            for (int i = firstVisible; i < _cachedEntries.Count; i++)
            {
                var entry = _cachedEntries[i];
                float lineHeight = Mathf.Min(_cachedLineHeights[i], contentRect.yMax - y);
                if (lineHeight <= 0f)
                    break;

                float availableWidth = Mathf.Max(1f, contentRect.width - _cachedLabelWidths[i] - TextPadding);
                var labelRect = new Rect(contentRect.x, y, _cachedLabelWidths[i], lineHeight);
                var replyRect = new Rect(contentRect.x + _cachedLabelWidths[i] + TextPadding, y, availableWidth, lineHeight);

                Color nameColor = GetCategoryColor(entry.category);
                GUI.color = nameColor;
                Widgets.Label(labelRect, _cachedLabels[i]);
                GUI.color = Color.white;
                Widgets.Label(replyRect, _cachedDisplayReplies[i]);

                y += lineHeight;
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        internal static string FormatCompactOverlayText(string? rawText, int maxChars = 140)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return string.Empty;

            string normalized = rawText!
                .Replace("\r\n", " ")
                .Replace('\n', ' ')
                .Replace('\r', ' ')
                .Trim();

            if (normalized.Length <= maxChars)
                return normalized;

            return normalized.Substring(0, maxChars).TrimEnd() + "...";
        }

        private void NormalizeWindowRect()
        {
            OverlayBounds value = DialogueOverlayLayout.Normalize(
                new OverlayBounds(_windowRect.x, _windowRect.y, _windowRect.width, _windowRect.height),
                Verse.UI.screenWidth,
                Verse.UI.screenHeight,
                MinWidth,
                MinHeight,
                AlertsMargin,
                TopColonistBarMargin);
            Rect normalized = new Rect(value.X, value.Y, value.Width, value.Height);
            if (normalized.x == _windowRect.x && normalized.y == _windowRect.y &&
                normalized.width == _windowRect.width && normalized.height == _windowRect.height)
                return;

            _windowRect = normalized;
            _positionDirty = true;
        }

        private void DrawOptionsBar(Rect inRect)
        {
            var barRect = new Rect(inRect.x, inRect.y, inRect.width, OptionsBarHeight);
            Widgets.DrawBoxSolid(barRect, new Color(0.05f, 0.05f, 0.08f, 0.8f));

            var titleRect = new Rect(barRect.x + 4f, barRect.y, Mathf.Max(100f, barRect.width - 92f), barRect.height);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = new Color(0.7f, 0.8f, 1f);
            Widgets.Label(titleRect, RimMindAPI.Text.TrOr("RimMind.Dialogue.UI.Overlay.Title", "RimMind Dialogue"));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            var openBtnRect = new Rect(barRect.xMax - 60f, barRect.y + 2f, 56f, barRect.height - 4f);
            var closeBtnRect = new Rect(barRect.xMax - 82f, barRect.y + 2f, 20f, barRect.height - 4f);
            if (Widgets.ButtonText(closeBtnRect, "X"))
            {
                _temporarilyClosed = true;
            }
            if (Widgets.ButtonText(openBtnRect, RimMindAPI.Text.TrOr("RimMind.Dialogue.UI.Overlay.Details", "Details")))
            {
                Find.WindowStack.Add(new Window_DialogueLog());
            }
        }

        private void HandleInput(Rect activeRect)
        {
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
            {
                var openBtnScreenRect = new Rect(
                    activeRect.xMax - 60f, activeRect.y + 2f, 56f, OptionsBarHeight - 4f);

                var closeBtnScreenRect = new Rect(
                    activeRect.xMax - 82f, activeRect.y + 2f, 20f, OptionsBarHeight - 4f);

                var resizeScreenRect = new Rect(
                    activeRect.xMax - ResizeHandleSize, activeRect.yMax - ResizeHandleSize,
                    ResizeHandleSize, ResizeHandleSize);

                if (resizeScreenRect.Contains(currentEvent.mousePosition))
                {
                    _isResizing = true;
                    currentEvent.Use();
                }
                else if (!openBtnScreenRect.Contains(currentEvent.mousePosition)
                    && !closeBtnScreenRect.Contains(currentEvent.mousePosition))
                {
                    var dragRect = new Rect(activeRect.x, activeRect.y, activeRect.width, OptionsBarHeight);
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

                    float maxWidth = Mathf.Max(MinWidth, Verse.UI.screenWidth - _windowRect.x - AlertsMargin);
                    float maxHeight = Verse.UI.screenHeight - _windowRect.y;

                    _windowRect.width = Mathf.Clamp(desiredWidth, MinWidth, maxWidth);
                    _windowRect.height = Mathf.Clamp(desiredHeight, MinHeight, maxHeight);
                    currentEvent.Use();
                }
                else if (_isDragging)
                {
                    _windowRect.position = currentEvent.mousePosition - _dragStartOffset;
                    float maxX = Mathf.Max(0f, Verse.UI.screenWidth - _windowRect.width - AlertsMargin);
                    float minY = Mathf.Min(TopColonistBarMargin, Mathf.Max(0f, Verse.UI.screenHeight - _windowRect.height));
                    _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, maxX);
                    _windowRect.y = Mathf.Clamp(_windowRect.y, minY, Mathf.Max(minY, Verse.UI.screenHeight - _windowRect.height));
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
            s.Write();
        }
    }
}
