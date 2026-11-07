using System;
using RimMind.Domain.Events;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;

namespace RimMind.Dialogue.Core
{
    /// <summary>
    /// Published on the Core bus when a pawn's dialogue partner changes or a dialogue reply was
    /// applied (thoughts/relation updated). The cached <c>dialogue_state</c> and
    /// <c>dialogue_relation</c> context keys name <see cref="EventName"/> as their invalidation
    /// trigger, so the prompt never describes the previous recipient or stale dialogue thoughts.
    /// </summary>
    public sealed class DialogueContextChangedEvent : AgentBusEvent
    {
        /// <summary>Bus event name used by context-key invalidation triggers.</summary>
        public const string EventName = "DialogueEvent";

        public DialogueContextChangedEvent(int pawnId)
        {
            PawnId = pawnId;
            NpcId = pawnId > 0 ? "NPC-" + pawnId : string.Empty;
        }

        /// <summary>Registers <see cref="EventName"/> with the Core bus. Call before registering context keys.</summary>
        internal static void Register()
        {
            try { RimMindAPI.Bus.RegisterEventType<DialogueContextChangedEvent>(EventName); }
            catch (Exception ex) { RimMindErrors.Warn($"[RimMind-Dialogue] Event type registration failed: {ex.Message}"); }
        }

        /// <summary>Publishes from any thread; never throws.</summary>
        internal static void Publish(int pawnId)
        {
            try { RimMindAPI.Bus.PublishOnMainThread(new DialogueContextChangedEvent(pawnId)); }
            catch (Exception ex) { RimMindErrors.Warn($"[RimMind-Dialogue] Dialogue change notification failed: {ex.Message}"); }
        }
    }
}
