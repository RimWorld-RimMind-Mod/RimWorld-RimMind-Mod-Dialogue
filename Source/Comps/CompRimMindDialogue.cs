using System.Collections.Generic;
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
        private int _lastTriggerTick = -99999;

        private Pawn Pawn => (Pawn)parent;

        public override void CompTick()
        {
            base.CompTick();
            if (!Pawn.IsColonist) return;
            var settings = RimMindDialogueSettings.Get();
            if (!settings.enabled || !settings.autoDialogueEnabled) return;
            // Sample natural encounters every 250 ticks (≈4 seconds)
            if (!Pawn.IsHashIntervalTick(250)) return;
            if (!RimMindAPI.IsConfigured()) return;
            if (!IsEligible()) return;

            int currentTick = Find.TickManager.TicksGame;
            // Cooldown guardrail: single pawn anti-spam safety
            if (currentTick - _lastTriggerTick < settings.monologueCooldownTicks) return;

            // 1. Check for nearby awake colonist companion
            Pawn? nearbyCompanion = null;
            Map? map = Pawn.Map;
            if (map != null)
            {
                var spawnedColonists = map.mapPawns.FreeColonistsSpawned;
                for (int i = 0; i < spawnedColonists.Count; i++)
                {
                    Pawn other = spawnedColonists[i];
                    if (other != Pawn && !other.Dead && !other.Downed && other.Awake() && Pawn.Position.InHorDistOf(other.Position, 6.9f))
                    {
                        nearbyCompanion = other;
                        break;
                    }
                }
            }

            float activityScale = RimMindAPI.Settings.ActivityFrequencyScale;

            // 2. State-transition social encounters with dynamic probabilities
            if (nearbyCompanion != null)
            {
                // Dining Encounter (Shared Meal)
                if (Pawn.CurJob?.def == JobDefOf.Ingest && nearbyCompanion.CurJob?.def == JobDefOf.Ingest)
                {
                    float dynamicChance = CalculateDynamicSocialChance(settings.diningSocialChance, Pawn, nearbyCompanion);
                    float dynamicChance = CalculateDynamicSocialChance(settings.diningSocialChance, Pawn, nearbyCompanion, activityScale);
                    if (settings.enableDiningSocial && Rand.Value < dynamicChance)
                    {
                        _lastTriggerTick = currentTick;
                        string contextMsg = "RimMind.Dialogue.Context.DiningEncounter".Translate(nearbyCompanion.LabelShort);
                        RimMindDialogueService.HandleTrigger(Pawn, contextMsg, DialogueTriggerType.Chitchat, nearbyCompanion);
                        return;
                    }
                }

                // Recreation Encounter (Shared Play / Joy)
                if (Pawn.CurJob?.def?.joyKind != null && nearbyCompanion.CurJob?.def?.joyKind != null)
                {
                    float dynamicChance = CalculateDynamicSocialChance(settings.recreationSocialChance, Pawn, nearbyCompanion);
                    float dynamicChance = CalculateDynamicSocialChance(settings.recreationSocialChance, Pawn, nearbyCompanion, activityScale);
                    if (settings.enableRecreationSocial && Rand.Value < dynamicChance)
                    {
                        _lastTriggerTick = currentTick;
                        string contextMsg = "RimMind.Dialogue.Context.RecreationEncounter".Translate(nearbyCompanion.LabelShort);
                        RimMindDialogueService.HandleTrigger(Pawn, contextMsg, DialogueTriggerType.Chitchat, nearbyCompanion);
                        return;
                    }
                }

                // Co-working Encounter (Working side-by-side)
                if (Pawn.CurJob != null && nearbyCompanion.CurJob != null)
                {
                    if (!IsIdleOrResting(Pawn.CurJob.def) && !IsIdleOrResting(nearbyCompanion.CurJob.def)
                        && Pawn.CurJob.def != JobDefOf.Ingest && nearbyCompanion.CurJob.def != JobDefOf.Ingest
                        && Pawn.CurJob.def?.joyKind == null && nearbyCompanion.CurJob.def?.joyKind == null)
                    {
                        float dynamicChance = CalculateDynamicSocialChance(settings.coworkerSocialChance, Pawn, nearbyCompanion);
                        float dynamicChance = CalculateDynamicSocialChance(settings.coworkerSocialChance, Pawn, nearbyCompanion, activityScale);
                        if (settings.enableCoworkerSocial && Rand.Value < dynamicChance)
                        {
                            _lastTriggerTick = currentTick;
                            string contextMsg = "RimMind.Dialogue.Context.CoworkerEncounter".Translate(nearbyCompanion.LabelShort);
                            RimMindDialogueService.HandleTrigger(Pawn, contextMsg, DialogueTriggerType.Chitchat, nearbyCompanion);
                            return;
                        }
                    }
                }
            }
            else
            {
                float mood = Pawn.needs?.mood?.CurLevel ?? 0.5f;
                float jitter = Rand.Range(0.85f, 1.15f);

                // Solitary contemplation & recreation monologue
                if (Pawn.CurJobDef?.defName == "Skygaze" || Pawn.CurJobDef?.defName == "Meditate")
                {
                    float monologueChance = 0.35f * (mood > 0.8f || mood < 0.3f ? 1.25f : 1.0f) * jitter;
                    float monologueChance = 0.35f * (mood > 0.8f || mood < 0.3f ? 1.25f : 1.0f) * jitter * activityScale;
                    if (settings.autoDialogueEnabled && Rand.Value < monologueChance)
                    {
                        _lastTriggerTick = currentTick;
                        string contextMsg = "RimMind.Dialogue.Context.ContemplationMonologue".Translate();
                        RimMindDialogueService.HandleTrigger(Pawn, contextMsg, DialogueTriggerType.Auto, null);
                        return;
                    }
                }
                else if (Pawn.CurJob?.def?.joyKind != null)
                {
                    float monologueChance = (settings.recreationSocialChance * 0.4f) * jitter;
                    float monologueChance = (settings.recreationSocialChance * 0.4f) * jitter * activityScale;
                    if (settings.autoDialogueEnabled && Rand.Value < monologueChance)
                    {
                        _lastTriggerTick = currentTick;
                        string contextMsg = "RimMind.Dialogue.Context.RecreationMonologue".Translate();
                        RimMindDialogueService.HandleTrigger(Pawn, contextMsg, DialogueTriggerType.Auto, null);
                        return;
                    }
                }
            }

            // 3. Fallback idle monologue
            if (settings.autoDialogueEnabled && (currentTick - _lastTriggerTick >= settings.AutoDialogueCooldownTicks))
            float idleCdMult = activityScale > 0.01f ? (1.0f / activityScale) : 1.0f;
            int idleCooldown = Mathf.RoundToInt(settings.AutoDialogueCooldownTicks * Mathf.Clamp(idleCdMult, 0.35f, 3.5f));
            if (settings.autoDialogueEnabled && (currentTick - _lastTriggerTick >= idleCooldown))
            {
                _lastTriggerTick = currentTick;
                string contextMsg = "RimMind.Dialogue.Context.IdleState".Translate();
                RimMindDialogueService.HandleTrigger(Pawn, contextMsg, DialogueTriggerType.Auto, null);
            }
        }

        /// <summary>
        /// Dynamically adjusts base social encounter chance according to:
        /// 1. Inter-pawn social opinion (-100 to +100).
        /// 2. Romantic relationship status (lovers/spouses communicate significantly more often).
        /// 3. Speaker's emotional state (cheerfulness vs deep depression).
        /// 4. Organic ±15% natural daily jitter.
        /// 5. Global ActivityFrequencyScale setting multiplier.
        /// </summary>
        public static float CalculateDynamicSocialChance(float baseChance, Pawn speaker, Pawn listener)
        public static float CalculateDynamicSocialChance(float baseChance, Pawn speaker, Pawn listener, float activityScale = 1.0f)
        {
            float chance = baseChance;
            float chance = baseChance * activityScale;

            // 1. Social relations & opinion modifier (-100 to +100)
            if (speaker.relations != null && listener != null)
            {
                int opinion = speaker.relations.OpinionOf(listener);
                float opinionMod = 1.0f + (opinion / 200f);
                chance *= Mathf.Clamp(opinionMod, 0.30f, 1.60f);

                // Romantic partner boost (+40%)
                if (LovePartnerRelationUtility.LovePartnerRelationExists(speaker, listener))
                {
                    chance *= 1.4f;
                }
            }

            // 2. Speaker mood modifier (happy colonists are more communicative)
            if (speaker.needs?.mood != null)
            {
                float mood = speaker.needs.mood.CurLevel;
                if (mood > 0.75f) chance *= 1.20f;
                else if (mood < 0.25f) chance *= 0.65f;
            }

            // 3. Organic jitter (±15% natural variance)
            float jitter = Rand.Range(0.85f, 1.15f);
            chance *= jitter;

            return Mathf.Clamp(chance, 0.05f, 0.95f);
            return Mathf.Clamp(chance, 0.01f, 0.98f);
        }

        private static bool IsIdleOrResting(JobDef? def)
        {
            if (def == null) return true;
            string name = def.defName;
            return name.Contains("Wait") || name.Contains("Wander") || name == "LayDown";
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!(parent.Faction?.IsPlayer ?? false)) yield break;
            if (!RimMindDialogueSettings.Get().enabled) yield break;
            if (!RimMindDialogueSettings.Get().playerDialogueEnabled) yield break;

            yield return new Command_Action
            {
                defaultLabel = "RimMind.Dialogue.UI.Gizmo.ChatWith".Translate(parent.LabelShort),
                icon = ContentFinder<Texture2D>.Get("UI/RimMindDialogue_Icon", reportFailure: false),
                action = () => Find.WindowStack.Add(new Window_Dialogue((Pawn)parent))
            };
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref _lastTriggerTick, "lastTriggerTick", -99999);
        }

        private bool IsEligible()
        {
            return Pawn.IsFreeNonSlaveColonist
                && !Pawn.Dead
                && !Pawn.Downed
                && !(Pawn.drafter?.Drafted ?? false)
                && Pawn.Map != null
                && Pawn.needs?.mood != null;
        }
    }
}
