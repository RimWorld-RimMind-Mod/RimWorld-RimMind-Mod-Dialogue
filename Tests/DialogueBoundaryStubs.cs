using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Linq;
using RimMind.Application.Common.Models.Memory;
using RimMind.Application.Features.Context;
using RimMind.Application.Features.Json;
using RimMind.Application.Features.Llm;
using RimMind.Application.Features.Prompt;
using RimMind.Domain.Llm;
using RimMind.Domain.ValueObjects;
using UnityEngine;
using Verse;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RimMind.Presentation.Api
{
    // The production coordinator, envelope builder and response handler run unchanged.
    // Only the external Core facade is replaced, so tests control admission and delivery.
    public static class RimMindAPI
    {
        public static bool Configured = true;
        public static bool Skip;
        public static Exception? SendException;
        public static Result<LlmResponse, RimMindError>? ImmediateResult;
        public static readonly List<(LlmRequestEnvelope Envelope,
            Action<Result<LlmResponse, RimMindError>> Complete)> Sent = new();
        public static readonly List<(int PawnId, string Text)> Perceptions = new();
        public static bool IsConfigured() => Configured;
        public static bool ShouldSkipDialogue(Pawn pawn, string type) => Skip;
        public static void PublishPerception(int pawnId, string type, string text, float salience)
            => Perceptions.Add((pawnId, text));
        public static class Settings
        {
            public static bool IsConfigured() => Configured;
            public static float ActivityFrequencyScale { get; set; } = 1.0f;
            public static int ScaleCooldown(int baseCooldownTicks, int minFloorTicks = 0)
                => RimMind.Application.Common.Helpers.ActivityCooldownScaler.Scale(baseCooldownTicks, ActivityFrequencyScale, minFloorTicks);
        }
        public static class Ext
        {
            public static bool ShouldSkipDialogue(Pawn pawn, string type) => Skip;
            public static bool ShouldSkipFloatMenu() => Skip;
        }
        public static class Text
        {
            public static string TrOr(string key, string fallback, params object[] args)
                => RimMindText.TrOr(key, fallback, args);
            public static string Tr(string key, params object[] args)
                => RimMindText.Tr(key, args);
            public static string WrapForMote(string? text, int maxCharsPerLine = MoteTextFormatter.DefaultMaxCharsPerLine)
                => MoteTextFormatter.WrapForMote(text, maxCharsPerLine);
        }
        public static class Json
        {
            public static string StripThinkingAndCodeFences(string? raw)
                => LlmPayloadParser.StripThinkingAndCodeFences(raw);
            public static string? ExtractJsonPayload(string? raw)
                => LlmPayloadParser.ExtractJsonPayload(raw);
            public static bool TryParseJObjectWithRepair(string? raw, out JObject? obj)
                => LlmPayloadParser.TryParseJObjectWithRepair(raw, out obj);
            public static bool TryDeserializeWithRepair<T>(string? raw, out T? value) where T : class
                => LlmPayloadParser.TryDeserializeWithRepair<T>(raw, out value);
            public static IReadOnlyList<ParsedPseudoFunctionCall> ParsePseudoFunctionCalls(string? raw)
                => LlmPayloadParser.ParsePseudoFunctionCalls(raw);
            public static string? TryRepairJson(string input)
                => JsonRepairer.TryRepair(input);
            public static string? TryRepairTruncatedJson(string input)
                => JsonRepairer.TryRepairTruncatedJson(input);
        }
        public static class Thoughts
        {
            public static string FormatCleanLabel(string rawTagOrKey, string defaultPrefixToStrip = "RimMind.")
            {
                if (string.IsNullOrEmpty(rawTagOrKey)) return string.Empty;
                if (UnityData.IsInMainThread && rawTagOrKey.CanTranslate())
                    return rawTagOrKey.Translate().ToString();
                return RimMind.Application.Common.Models.Psychology.ThoughtSpec.FormatCleanLabelFallback(rawTagOrKey, defaultPrefixToStrip);
            }
        }
        public static class Request
        {
            public static void Send(LlmRequestEnvelope envelope,
                Action<Result<LlmResponse, RimMindError>> onComplete)
            {
                if (SendException != null) throw SendException;
                Sent.Add((envelope, onComplete));
                if (ImmediateResult.HasValue) onComplete(ImmediateResult.Value);
            }
        }
        public static class Memory
        {
            public static readonly List<string?> Pawns = new();
            public static bool AddPawnMemory(string content, MemoryKind kind, int tick,
                float importance, string? pawnId = null)
            {
                Pawns.Add(pawnId);
                return true;
            }
        }
        public static class Bus
        {
            public static void RegisterEventType<T>(string name) where T : RimMind.Domain.Events.AgentBusEvent
                => RimMind.Application.Features.AgentBus.AgentBusImpl.RegisterKnownEventType(name, typeof(T));
            public static void PublishOnMainThread<T>(T evt) where T : RimMind.Domain.Events.AgentBusEvent { }
            public static void PublishPerception(int pawnId, string type, string text, float salience = 0.5f)
                => Perceptions.Add((pawnId, text));
        }
        public static class MainThreadSnapshot
        {
            public static RimMind.Application.Common.Threading.MainThreadReader Reader { get; set; } =
                new RimMind.Application.Common.Threading.MainThreadReader(a => a(), () => true);

            public static System.Threading.Tasks.Task<T> GetAsync<T>(
                Func<T> readOnMainThread,
                T fallback = default!,
                TimeSpan? timeout = null,
                System.Threading.CancellationToken ct = default)
                => Reader.ReadAsync(
                    readOnMainThread,
                    fallback,
                    timeout ?? RimMind.Application.Common.Threading.MainThreadReader.DefaultTimeout,
                    ct);
        }
        public static class Context
        {
            public static string ScenarioDialogue => RimMind.Application.Common.Models.Context.ScenarioIds.Dialogue;
            public static string ScenarioCrisisDialogue => RimMind.Application.Common.Models.Context.ScenarioIds.CrisisDialogue;
            public static TestContextRegistry ContextKeys { get; } = new();
            public static void RegisterScenario(ScenarioDescriptor descriptor)
                => ScenarioRegistry.Register(descriptor);
        }
        public sealed class TestContextRegistry
        {
            public List<RimMind.Application.Common.Interfaces.Context.ContextProviderDef> Definitions { get; } = new();
            public void Register(RimMind.Application.Common.Interfaces.Context.ContextProviderDef definition)
            {
                Definitions.RemoveAll(d => d.Key == definition.Key);
                Definitions.Add(definition);
            }
        }
        public static class Prompt
        {
            public static string BuildTaskInstruction(string prefix, object? args, params string[] subKeys)
                => string.Join("\n", System.Linq.Enumerable.Select(subKeys, key => prefix + "." + key));
        }
    }

    public static class RimMindPawnLookup
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, Pawn> _cache = new();
        public static void CachePawn(Pawn? pawn)
        {
            if (pawn != null && pawn.thingIDNumber > 0) _cache[pawn.thingIDNumber] = pawn;
        }
        public static Pawn? FindPawnByNumber(int thingIDNumber)
            => _cache.TryGetValue(thingIDNumber, out var p) ? p : null;
        public static void ClearCache() => _cache.Clear();

        public static bool IsActiveSpawnedColonist(Pawn? pawn)
        {
            if (pawn == null) return false;
            if (pawn.Dead || pawn.Destroyed) return false;
            if (!pawn.Spawned || pawn.Map == null) return false;
            if (!pawn.IsColonist || pawn.IsPrisoner || pawn.IsSlave) return false;
            if (pawn.Faction != RimWorld.Faction.OfPlayer) return false;
            if (pawn.IsKidnapped()) return false;
            return true;
        }

        public static bool IsEligibleColonist(Pawn? pawn)
        {
            if (!IsActiveSpawnedColonist(pawn)) return false;
            if (pawn!.Downed || pawn.InMentalState) return false;
            return true;
        }

        public static IReadOnlyList<Pawn> GetSpawnedColonistsSafe(Map? map)
            => map?.mapPawns?.FreeColonistsSpawned ?? (IReadOnlyList<Pawn>)Array.Empty<Pawn>();
    }
}

namespace Verse
{
    public static class UnityData
    {
        public static bool IsInMainThread = true;
    }

    public class Thing
    {
        public int thingIDNumber;
        public RimWorld.Faction? Faction = RimWorld.Faction.OfPlayer;
        public bool Spawned = true;
        public string LabelShort => "TestPawn";
        public string ThingID => "Thing_" + thingIDNumber;
    }
    public class Map { public MapPawns mapPawns = new(); }
    public class MapPawns { public List<Pawn> AllPawns = new(); public List<Pawn> FreeColonistsSpawned = new(); }
    public class WorldPawns { public List<Pawn> AllPawnsAlive = new(); }
    public class TickManager { public int TicksGame; }
    public static class Find
    {
        public static TickManager TickManager = new();
        public static List<Map> Maps = new();
        public static WorldPawns WorldPawns = new();
        public static WindowStack WindowStack = new();
    }
    public static class ModsConfig
    {
        public static bool MemoryEnabled;
        public static bool IsActive(string id) => MemoryEnabled;
    }
    public static class Messages
    {
        public static readonly List<string> Shown = new();
        public static void Message(string text, RimWorld.MessageTypeDef type, bool historical)
            => Shown.Add(text);
        public static void Message(string text, Pawn pawn, RimWorld.MessageTypeDef type, bool historical)
            => Shown.Add(text);
    }
    public static class MoteMaker
    {
        public static readonly List<(Vector3 Position, Map Map, string Text)> Thrown = new();
        public static void ThrowText(Vector3 position, Map map, string text, Color color, float duration)
            => Thrown.Add((position, map, text));
    }
    public class ModSettings
    {
        public int WriteCount;
        public virtual void ExposeData() { }
        public void Write() => WriteCount++;
    }
    public class ThingComp
    {
        public Thing parent = null!;
        public virtual void CompTick() { }
        public virtual IEnumerable<Gizmo> CompGetGizmosExtra() { yield break; }
        public virtual void PostExposeData() { }
    }
    public class Gizmo { }
    public class Command_Action : Gizmo
    {
        public string defaultLabel = "";
        public Texture2D? icon;
        public Action action = null!;
    }
    public static class ContentFinder<T> where T : new()
    {
        public static T Get(string path, bool reportFailure) => new();
    }
    public class Window
    {
        public bool doCloseButton, doCloseX, closeOnAccept, forcePause, closeOnClickedOutside,
            absorbInputAroundWindow, draggable;
        public virtual Vector2 InitialSize => default;
        public virtual void PostClose() { }
        public virtual void DoWindowContents(Rect rect) { }
        public void Close() => PostClose();
    }
    public class WindowStack
    {
        public readonly List<Window> Windows = new();
        public void Add(Window window) => Windows.Add(window);
    }
    public enum GameFont { Small }
    public static class Text
    {
        public static GameFont Font;
        public static TextAnchor Anchor;
        public static float CalcHeight(string text, float width) => 20;
    }
    public static class Widgets
    {
        public static string? NextText;
        public static bool ClickSend;
        public static readonly List<string> Labels = new();
        public static void Label(Rect rect, string text) => Labels.Add(text);
        public static string TextField(Rect rect, string text)
        {
            string result = NextText ?? text;
            NextText = null;
            return result;
        }
        public static bool ButtonText(Rect rect, string text)
        {
            bool click = ClickSend;
            ClickSend = false;
            return click;
        }
        public static void DrawBoxSolid(Rect rect, Color color) { }
        public static void BeginScrollView(Rect rect, ref Vector2 position, Rect view) { }
        public static void EndScrollView() { }
    }
    public class Listing_Standard
    {
        public void Begin(Rect rect) { }
        public void End() { }
        public void Label(string text) { }
        public void CheckboxLabeled(string text, ref bool value, string tip) { }
        public float Slider(float value, float min, float max) => value;
    }
}

namespace RimWorld
{
    public class Faction
    {
        public bool IsPlayer;
        public bool HostileTo(Faction other) => false;
        public static Faction OfPlayer = new() { IsPlayer = true };
    }
    public class MessageTypeDef { }
    public static class MessageTypeDefOf
    {
        public static MessageTypeDef RejectInput = new();
        public static MessageTypeDef SilentInput = new();
    }
}

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => default;
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    }
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height)
        { this.x = x; this.y = y; this.width = width; this.height = height; }
        public float xMax => x + width;
        public float yMax => y + height;
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }
        public static Color white => new Color(1f, 1f, 1f, 1f);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1f);
    }
    public class Texture2D { }
    public enum TextAnchor { MiddleCenter, UpperLeft }
    public enum EventType { KeyDown, Repaint }
    public enum KeyCode { Return, KeypadEnter }
    public class Event
    {
        public static Event current = new() { type = EventType.Repaint };
        public EventType type;
        public KeyCode keyCode;
        public void Use() { }
    }
    public static class GUI { public static Color color; public static bool enabled = true; public static bool changed; }
}

namespace RimMind.Presentation.UI
{
    public readonly struct ChatWindowMessage
    {
        public ChatWindowMessage(string role, string content, string? senderPrefix = null)
        {
            Role = role ?? string.Empty;
            Content = content ?? string.Empty;
            SenderPrefix = senderPrefix;
        }
        public string Role { get; }
        public string Content { get; }
        public string? SenderPrefix { get; }
    }

    public abstract class RimMindChatWindowBase : Window
    {
        private readonly List<ChatWindowMessage> _messages = new();
        private readonly ConcurrentQueue<Action> _mainThreadUpdates = new();
        private readonly CancellationTokenSource _lifetime = new();
        private string _inputText = string.Empty;
        private bool _isWaiting;
        private bool _closed;

        protected RimMindChatWindowBase()
        {
            doCloseButton = false;
            doCloseX = true;
            closeOnAccept = false;
            forcePause = false;
            closeOnClickedOutside = false;
            absorbInputAroundWindow = false;
            draggable = true;
        }

        public override Vector2 InitialSize => new Vector2(480f, 540f);
        protected CancellationToken LifetimeToken => _lifetime.Token;
        protected bool IsClosed => _closed;
        protected bool IsWaiting => _isWaiting;
        protected IReadOnlyList<ChatWindowMessage> Messages => _messages;

        protected virtual Color HeaderColor => new Color(0.8f, 0.95f, 1f);
        protected abstract string GetTitle();
        protected abstract string GetWaitingText();
        protected abstract string GetSendButtonLabel();
        protected abstract string GetPrefixForRole(string role);
        protected abstract void OnSubmitUserMessage(string trimmedInput);
        protected virtual bool ValidateCanStayOpen() => true;
        protected virtual void OnMessageAppended(ChatWindowMessage message) { }

        protected void AppendMessage(string role, string content, string? senderPrefix = null, bool notifyHook = true)
        {
            if (string.IsNullOrEmpty(content)) return;
            var msg = new ChatWindowMessage(role, content, senderPrefix);
            _messages.Add(msg);
            if (notifyHook) OnMessageAppended(msg);
        }

        protected void PostReply(string role, string content, bool clearWaiting = true, string? senderPrefix = null)
        {
            if (_closed) return;
            _mainThreadUpdates.Enqueue(() =>
            {
                if (_closed) return;
                if (clearWaiting) _isWaiting = false;
                if (!string.IsNullOrEmpty(content)) AppendMessage(role, content, senderPrefix);
            });
        }

        protected void SetWaiting(bool waiting) => _isWaiting = waiting;

        public override void PostClose()
        {
            if (!_closed)
            {
                _closed = true;
                _isWaiting = false;
                try { _lifetime.Cancel(); } catch (ObjectDisposedException) { }
                try { _lifetime.Dispose(); } catch (ObjectDisposedException) { }
            }
            base.PostClose();
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (!ValidateCanStayOpen())
            {
                Close();
                return;
            }
            while (_mainThreadUpdates.TryDequeue(out var update))
                update?.Invoke();

            Widgets.Label(inRect, GetTitle());
            if (_isWaiting) Widgets.Label(inRect, GetWaitingText());
            _inputText = Widgets.TextField(inRect, _inputText ?? string.Empty);
            bool sendClicked = Widgets.ButtonText(inRect, GetSendButtonLabel());
            if (sendClicked && !_isWaiting && !string.IsNullOrWhiteSpace(_inputText))
            {
                string trimmed = _inputText.Trim();
                _inputText = string.Empty;
                OnSubmitUserMessage(trimmed);
            }
        }
    }

    public static class SettingsUIDrawer
    {
        public static Rect SplitContentArea(Rect rect) => rect;
        public static Rect SplitBottomBar(Rect rect) => rect;
        public static void DrawSectionHeader(Listing_Standard listing, string text) { }
        public static void DrawBottomBar(Rect rect, Action reset) { }
        public static void LabelWithTooltip(this Verse.Listing_Standard listing, string label, string? tooltip, UnityEngine.Color? textColor = null) { }
        public static void DrawScrollableSettingsPage(
            Rect inRect,
            ref Vector2 scrollPos,
            ref float measuredHeight,
            Action<Listing_Standard> drawContent,
            Action? onReset = null,
            Action? onDirtySave = null)
        {
            bool prevChanged = GUI.changed;
            GUI.changed = false;
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            drawContent(listing);
            listing.End();
            bool dirty = GUI.changed;
            GUI.changed = prevChanged || dirty;
            if (dirty) onDirtySave?.Invoke();
        }
    }
}
