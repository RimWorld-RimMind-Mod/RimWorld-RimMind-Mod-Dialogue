using System.Collections.Generic;
using RimMind.Dialogue.Core;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.UI
{
    public class Window_Dialogue : Window
    {
        private readonly Pawn _pawn;
        private readonly Pawn? _initiator;
        private readonly List<(string role, string content)> _messages = new List<(string, string)>();
        private string _inputText = string.Empty;
        private bool _isWaiting;
        private Vector2 _scrollPosition;
        private bool _autoScroll = true;
        private const float InputHeight = 36f;
        private const float SendButtonWidth = 80f;
        private const float Padding = 8f;
        private const float HeaderHeight = 28f;
        private const float StatusHeight = 22f;
        private const float ScrollbarWidth = 16f;
        private static readonly Color UserColor = new Color(0.7f, 0.85f, 1f);
        private static readonly Color PawnColor = new Color(1f, 0.95f, 0.8f);
        private static readonly Color ChatBgColor = new Color(0.08f, 0.08f, 0.12f, 0.5f);
        private static readonly Color StatusColor = new Color(1f, 1f, 0.5f);

        public override Vector2 InitialSize => new Vector2(480f, 540f);

        public Window_Dialogue(Pawn pawn, Pawn? initiator = null) : base()
        {
            _pawn = pawn;
            _initiator = initiator;
            RimMindDialogueService.SetActiveRecipient(pawn, initiator);
            doCloseX = true;
            closeOnAccept = false;
            forcePause = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            draggable = true;
        }

        public override void PostClose()
        {
            RimMindDialogueService.SetActiveRecipient(_pawn, null);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (_pawn.Dead || _pawn.Destroyed)
            {
                Close();
                return;
            }

            Text.Font = GameFont.Small;

            float statusH = _isWaiting ? StatusHeight + Padding : 0f;
            float chatHeight = inRect.height - HeaderHeight - statusH - InputHeight - Padding * 3;
            float inputY = inRect.yMax - InputHeight;

            Rect headerRect = new Rect(inRect.x, inRect.y, inRect.width, HeaderHeight);
            Rect chatRect = new Rect(inRect.x, inRect.y + HeaderHeight + Padding, inRect.width, chatHeight);

            GUI.color = new Color(0.8f, 0.95f, 1f);
            Text.Anchor = TextAnchor.MiddleCenter;
            string title = _initiator != null
                ? "RimMind.Dialogue.UI.ChatTitleWithInitiator".Translate(_initiator.Name.ToStringShort, _pawn.Name.ToStringShort)
                : "RimMind.Dialogue.UI.ChatTitle".Translate(_pawn.Name.ToStringShort);
            Widgets.Label(headerRect, title);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            Widgets.DrawBoxSolid(chatRect, ChatBgColor);
            DrawChatHistory(chatRect);

            if (_isWaiting)
            {
                Rect statusRect = new Rect(inRect.x, inputY - StatusHeight - Padding, inRect.width, StatusHeight);
                GUI.color = StatusColor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(statusRect, "RimMind.Dialogue.UI.Thinking".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
            }

            Rect inputRect = new Rect(inRect.x, inputY, inRect.width - SendButtonWidth - Padding, InputHeight);
            Rect sendRect = new Rect(inRect.xMax - SendButtonWidth, inputY, SendButtonWidth, InputHeight);

            _inputText = Widgets.TextField(inputRect, _inputText);

            if (!_isWaiting)
            {
                if (Widgets.ButtonText(sendRect, "RimMind.Dialogue.UI.Send".Translate()))
                {
                    if (!_inputText.NullOrEmpty())
                        SendMessage();
                }
            }
            else
            {
                Widgets.ButtonText(sendRect, "RimMind.Dialogue.UI.Send".Translate());
            }

            if (!_isWaiting && Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Return && !_inputText.NullOrEmpty())
            {
                SendMessage();
                Event.current.Use();
            }
        }

        private void DrawChatHistory(Rect rect)
        {
            if (_messages.Count == 0) return;

            float contentWidth = rect.width - ScrollbarWidth;
            float contentHeight = CalcMessagesHeight(_messages, contentWidth - Padding * 2);
            Rect viewRect = new Rect(0f, 0f, contentWidth, contentHeight);

            float prevScrollY = _scrollPosition.y;
            Widgets.BeginScrollView(rect, ref _scrollPosition, viewRect);

            float y = 0f;
            int index = 0;
            foreach (var (role, content) in _messages)
            {
                string prefix = role == "user"
                    ? (_initiator != null
                        ? "RimMind.Dialogue.UI.PawnPrefix".Translate(_initiator.Name.ToStringShort)
                        : "RimMind.Dialogue.UI.PlayerPrefix".Translate())
                    : "RimMind.Dialogue.UI.PawnPrefix".Translate(_pawn.Name.ToStringShort);
                string line = prefix + content;
                float lineH = Text.CalcHeight(line, contentWidth - Padding * 2) + Padding;

                if (index % 2 == 0)
                {
                    Widgets.DrawBoxSolid(new Rect(0f, y, contentWidth, lineH),
                        new Color(1f, 1f, 1f, 0.03f));
                }

                Color color = role == "user" ? UserColor : PawnColor;
                GUI.color = color;
                Widgets.Label(new Rect(Padding, y, contentWidth - Padding * 2, lineH), line);
                GUI.color = Color.white;

                y += lineH;
                index++;
            }

            Widgets.EndScrollView();

            float maxScroll = Mathf.Max(0f, contentHeight - rect.height);
            if (_autoScroll && maxScroll > 0f)
                _scrollPosition.y = maxScroll;

            if (Mathf.Abs(prevScrollY - _scrollPosition.y) > 1f && _scrollPosition.y < maxScroll - 1f)
                _autoScroll = false;

            if (_scrollPosition.y >= maxScroll - 2f)
                _autoScroll = true;
        }

        private float CalcMessagesHeight(List<(string role, string content)> messages, float width)
        {
            float h = 0f;
            foreach (var (role, content) in messages)
            {
                string prefix = role == "user"
                    ? (_initiator != null
                        ? "RimMind.Dialogue.UI.PawnPrefix".Translate(_initiator.Name.ToStringShort)
                        : "RimMind.Dialogue.UI.PlayerPrefix".Translate())
                    : "RimMind.Dialogue.UI.PawnPrefix".Translate(_pawn.Name.ToStringShort);
                h += Text.CalcHeight(prefix + content, width) + Padding;
            }
            return h + Padding;
        }

        private void SendMessage()
        {
            string message = _inputText.Trim();
            _inputText = string.Empty;
            _isWaiting = true;
            _autoScroll = true;

            // 本地记录用户消息用于显示
            _messages.Add(("user", message));

            DialogueService.RequestReply(_pawn, message, _initiator,
                onReply: reply =>
                {
                    _messages.Add(("assistant", reply));
                    _isWaiting = false;
                    _autoScroll = true;
                },
                onError: error =>
                {
                    _isWaiting = false;
                    _autoScroll = true;
                    Log.Warning($"[RimMind-Dialogue] Player dialogue error: {error}");
                    Messages.Message(
                        "RimMind.Dialogue.UI.FloatMenu.RequestFailed".Translate(_pawn.Name.ToStringShort),
                        MessageTypeDefOf.RejectInput, false);
                });
        }
    }
}
