using System.Collections.Generic;
using RimMind.Application.Common.Helpers;
using RimMind.Presentation.Api;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Dialogue.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimMind.Dialogue.Comps
{
    public class CompRimMindDialogue : ThingComp
    {
        private static Texture2D? _cachedChatIcon;
        private static bool _chatIconLoaded;
        private int _lastTriggerTick = -99999;

        private Pawn? PawnOrNull => parent as Pawn;

        public override void CompTick()
        {
            base.CompTick();
            if (parent is not Pawn pawn || !pawn.IsColonist) return;
            var settings = RimMindDialogueSettings.Get();
            if (!settings.enabled || !settings.autoDialogueEnabled) return;
            // Sample natural encounters every 250 ticks (~4 seconds)
            if (!pawn.IsHashIntervalTick(250)) return;
            if (!RimMindAPI.Settings.IsConfigured()) return;
            if (!IsEligible()) return;

            int currentTick = Find.TickManager.TicksGame;
            // Cooldown guardrail: single pawn anti-spam safety
            if (currentTick - _lastTriggerTick < settings.monologueCooldownTicks) return;

            // 1. Check for nearby awake colonist companion
            Pawn? nearbyCompanion = FindNearbyCompanion();

            float activityScale = RimMindAPI.Settings.ActivityFrequencyScale;

            // 2. State-transition social encounters with dynamic probabilities
            if (nearbyCompanion != null)
            {
                // Dining Encounter (Shared Meal)
                if (pawn.CurJob?.def == JobDefOf.Ingest && nearbyCompanion.CurJob?.def == JobDefOf.Ingest)
                {
                    float dynamicChance = CalculateDynamicSocialChance(settings.diningSocialChance, pawn, nearbyCompanion, activityScale);
                    if (settings.enableDiningSocial && Rand.Value < dynamicChance)
                    {
                        string contextMsg = RimMindAPI.Text.TrOr(
                            "RimMind.Dialogue.Context.DiningEncounter",
                            "Dining together with {0}, enjoying a shared meal",
                            nearbyCompanion.LabelShort);
                        TryDispatchTrigger(pawn, contextMsg, DialogueTriggerType.Chitchat, nearbyCompanion, currentTick, ref _lastTriggerTick);
                        return;
                    }
                }

                // Recreation Encounter (Shared Play / Joy)
                if (pawn.CurJob?.def?.joyKind != null && nearbyCompanion.CurJob?.def?.joyKind != null)
                {
                    float dynamicChance = CalculateDynamicSocialChance(settings.recreationSocialChance, pawn, nearbyCompanion, activityScale);
                    if (settings.enableRecreationSocial && Rand.Value < dynamicChance)
                    {
                        string contextMsg = RimMindAPI.Text.TrOr(
                            "RimMind.Dialogue.Context.RecreationEncounter",
                            "Engaging in recreation with {0} in a relaxed atmosphere",
                            nearbyCompanion.LabelShort);
                        TryDispatchTrigger(pawn, contextMsg, DialogueTriggerType.Chitchat, nearbyCompanion, currentTick, ref _lastTriggerTick);
                        return;
                    }
                }

                // Co-working Encounter (Working side-by-side)
                if (pawn.CurJob != null && nearbyCompanion.CurJob != null)
                {
                    if (!IsIdleOrResting(pawn.CurJob.def) && !IsIdleOrResting(nearbyCompanion.CurJob.def)
                        && pawn.CurJob.def != JobDefOf.Ingest && nearbyCompanion.CurJob.def != JobDefOf.Ingest
                        && pawn.CurJob.def?.joyKind == null && nearbyCompanion.CurJob.def?.joyKind == null)
                    {
                        float dynamicChance = CalculateDynamicSocialChance(settings.coworkerSocialChance, pawn, nearbyCompanion, activityScale);
                        if (settings.enableCoworkerSocial && Rand.Value < dynamicChance)
                        {
                            string contextMsg = RimMindAPI.Text.TrOr(
                                "RimMind.Dialogue.Context.CoworkerEncounter",
                                "Working side-by-side near {0}",
                                nearbyCompanion.LabelShort);
                            TryDispatchTrigger(pawn, contextMsg, DialogueTriggerType.Chitchat, nearbyCompanion, currentTick, ref _lastTriggerTick);
                            return;
                        }
                    }
                }
            }
            else
            {
                float mood = pawn.needs?.mood?.CurLevel ?? 0.5f;
                float jitter = Rand.Range(0.85f, 1.15f);

                // Solitary contemplation & recreation monologue
                if (pawn.CurJobDef?.defName == "Skygaze" || pawn.CurJobDef?.defName == "Meditate")
                {
                    float monologueChance = 0.35f * (mood > 0.8f || mood < 0.3f ? 1.25f : 1.0f) * jitter * activityScale;
                    if (settings.autoDialogueEnabled && Rand.Value < monologueChance)
                    {
                        string contextMsg = RimMindAPI.Text.TrOr(
                            "RimMind.Dialogue.Context.ContemplationMonologue",
                            "Silently gazing at the horizon and contemplating life");
                        TryDispatchTrigger(pawn, contextMsg, DialogueTriggerType.Auto, null, currentTick, ref _lastTriggerTick);
                        return;
                    }
                }
                else if (pawn.CurJob?.def?.joyKind != null)
                {
                    float monologueChance = (settings.recreationSocialChance * 0.4f) * jitter * activityScale;
                    if (settings.autoDialogueEnabled && Rand.Value < monologueChance)
                    {
                        string contextMsg = RimMindAPI.Text.TrOr(
                            "RimMind.Dialogue.Context.RecreationMonologue",
                            "Enjoying leisure time and relaxing moments");
                        TryDispatchTrigger(pawn, contextMsg, DialogueTriggerType.Auto, null, currentTick, ref _lastTriggerTick);
                        return;
                    }
                }
            }

            // 3. Fallback idle monologue
            int idleCooldown = ActivityCooldownScaler.Scale(settings.AutoDialogueCooldownTicks, activityScale);
            if (settings.autoDialogueEnabled && (currentTick - _lastTriggerTick >= idleCooldown))
            {
                string contextMsg = RimMindAPI.Text.TrOr(
                    "RimMind.Dialogue.Context.IdleState",
                    "The colonist is currently idle");
                TryDispatchTrigger(pawn, contextMsg, DialogueTriggerType.Auto, null, currentTick, ref _lastTriggerTick);
            }
        }

        public static float CalculateDynamicSocialChance(float baseChance, Pawn speaker, Pawn listener, float activityScale = 1.0f)
        {
            float chance = baseChance * activityScale;

            if (speaker.relations != null && listener != null)
            {
                int opinion = speaker.relations.OpinionOf(listener);
                float opinionMod = 1.0f + (opinion / 200f);
                chance *= Mathf.Clamp(opinionMod, 0.30f, 1.60f);

                if (LovePartnerRelationUtility.LovePartnerRelationExists(speaker, listener))
                {
                    chance *= 1.4f;
                }
            }

            if (speaker.needs?.mood != null)
            {
                float mood = speaker.needs.mood.CurLevel;
                if (mood > 0.75f) chance *= 1.20f;
                else if (mood < 0.25f) chance *= 0.65f;
            }

            float jitter = Rand.Range(0.85f, 1.15f);
            chance *= jitter;

            return Mathf.Clamp(chance, 0.01f, 0.98f);
        }

        private static bool IsIdleOrResting(JobDef? def)
        {
            if (def == null) return true;
            string name = def.defName;
            return name.Contains("Wait") || name.Contains("Wander") || name == "LayDown";
        }

        internal static bool TryDispatchTrigger(
            Pawn pawn,
            string contextMsg,
            DialogueTriggerType triggerType,
            Pawn? recipient,
            int currentTick,
            ref int lastTriggerTick)
        {
            bool inCall = true;
            bool accepted = true;
            RimMindDialogueService.HandleTrigger(
                pawn,
                contextMsg,
                triggerType,
                recipient,
                onError: error =>
                {
                    if (inCall)
                    {
                        accepted = false;
                    }
                    else if (error != "Dialogue request cancelled.")
                    {
                        RimMind.Domain.ValueObjects.RimMindErrors.Warn($"[RimMind-Dialogue] Request failed: {error}");
                    }
                });
            inCall = false;

            if (accepted)
            {
                lastTriggerTick = currentTick;
            }
            return accepted;
        }

        private Pawn? FindNearbyCompanion()
        {
            if (PawnOrNull is not Pawn pawn) return null;
            var spawnedColonists = RimMindPawnLookup.GetSpawnedColonistsSafe(pawn.Map);

            for (int i = 0; i < spawnedColonists.Count; i++)
            {
                Pawn other = spawnedColonists[i];
                if (other != null && other != pawn && RimMindPawnLookup.IsEligibleColonist(other) && RestUtility.Awake(other) && pawn.Position.InHorDistOf(other.Position, 6.9f))
                {
                    return other;
                }
            }
            return null;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (PawnOrNull is not Pawn pawn) yield break;
            if (!IsEligible()) yield break;
            if (!RimMindDialogueSettings.Get().enabled) yield break;
            if (!RimMindDialogueSettings.Get().playerDialogueEnabled) yield break;

            if (!_chatIconLoaded)
            {
                _chatIconLoaded = true;
                _cachedChatIcon = ContentFinder<Texture2D>.Get("UI/RimMindDialogue_Icon", reportFailure: false);
            }

            yield return new Command_Action
            {
                defaultLabel = RimMindAPI.Text.TrOr("RimMind.Dialogue.UI.Gizmo.ChatWith", "Chat with {0}", parent.LabelShort),
                icon = _cachedChatIcon,
                action = () => Find.WindowStack.Add(new Window_Dialogue(pawn))
            };
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref _lastTriggerTick, "lastTriggerTick", -99999);
        }

        private bool IsEligible()
        {
            return PawnOrNull is Pawn pawn
                && RimMindPawnLookup.IsEligibleColonist(pawn)
                && RestUtility.Awake(pawn)
                && !(pawn.drafter?.Drafted ?? false)
                && pawn.needs?.mood != null;
        }
    }
}
