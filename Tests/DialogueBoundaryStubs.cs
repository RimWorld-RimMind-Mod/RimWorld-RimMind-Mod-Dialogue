using System;
using System.Collections.Generic;
using RimMind.Application.Common.Models.Memory;
using RimMind.Application.Features.Llm;
using RimMind.Domain.Llm;
using RimMind.Domain.ValueObjects;
using UnityEngine;
using Verse;

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
    }
}

namespace Verse
{
    public class Thing
    {
        public int thingIDNumber;
        public RimWorld.Faction? Faction;
        public string LabelShort => "TestPawn";
        public string ThingID => "Thing_" + thingIDNumber;
    }
    public class Map { public MapPawns mapPawns = new(); }
    public class MapPawns { public List<Pawn> AllPawns = new(); }
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
        public static void ThrowText(Vector3 position, Map map, string text, Color color, float duration) { }
    }
    public class ModSettings
    {
        public virtual void ExposeData() { }
        public void Write() { }
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
        public bool doCloseX, closeOnAccept, forcePause, closeOnClickedOutside,
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
    public struct Vector3 { }
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
        public Color(float r, float g, float b, float a = 1) { }
        public static Color white => default;
        public static Color gray => default;
    }
    public class Texture2D { }
    public enum TextAnchor { MiddleCenter, UpperLeft }
    public enum EventType { KeyDown, Repaint }
    public enum KeyCode { Return }
    public class Event
    {
        public static Event current = new() { type = EventType.Repaint };
        public EventType type;
        public KeyCode keyCode;
        public void Use() { }
    }
    public static class GUI { public static Color color; }
}

namespace RimMind.Presentation.UI
{
    public static class SettingsUIDrawer
    {
        public static Rect SplitContentArea(Rect rect) => rect;
        public static Rect SplitBottomBar(Rect rect) => rect;
        public static void DrawSectionHeader(Listing_Standard listing, string text) { }
        public static void DrawBottomBar(Rect rect, Action reset) { }
    }
}
