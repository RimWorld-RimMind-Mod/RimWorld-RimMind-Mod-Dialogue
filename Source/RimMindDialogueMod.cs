using System.Linq;
using System.Text;
using HarmonyLib;
using RimMind.Core;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using Verse;

namespace RimMind.Dialogue
{
    public class RimMindDialogueMod : Mod
    {
        public static RimMindDialogueSettings Settings = null!;

        public RimMindDialogueMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimMindDialogueSettings>();
            new Harmony("mcocdaa.RimMindDialogueStandalone").PatchAll();

            RegisterContextProviders();
            RimMindAPI.RegisterSettingsTab("dialogue", () => "RimMind.Dialogue.Settings.TabLabel".Translate(), RimMindDialogueSettings.DrawSettingsContent);
            RimMindAPI.RegisterModCooldown("Dialogue", () => RimMindDialogueSettings.Get().monologueCooldownTicks);

            RimMindAPI.RegisterToggleBehavior("dialogue_overlay",
                () => RimMindDialogueSettings.Get().overlayEnabled,
                () =>
                {
                    var s = RimMindDialogueSettings.Get();
                    s.overlayEnabled = !s.overlayEnabled;
                    s.Write();
                });

            RimMindAPI.RegisterDialogueTrigger((pawn, context, recipient) =>
            {
                RimMindDialogueService.HandleTrigger(pawn, context, DialogueTriggerType.Chitchat, recipient, isImmediate: true);
            });

            Log.Message("[RimMind-Dialogue] Initialized.");
        }

        private static void RegisterContextProviders()
        {
            RimMindAPI.RegisterPawnContextProvider("dialogue_state", pawn =>
            {
                var memories = pawn.needs?.mood?.thoughts?.memories?.Memories;
                if (memories == null) return null;

                var sb = new StringBuilder("RimMind.Dialogue.Context.StateHeader".Translate());
                bool any = false;
                foreach (var t in memories)
                {
                    if (t.def.defName != "RimMindDialogue_Thought") continue;

                    string desc = (t as Thought_RimMindDialogue)?.aiDescription ?? t.def.label;
                    float hours = t.DurationTicks / 2500f;
                    sb.AppendLine("RimMind.Dialogue.Context.ThoughtRemaining".Translate(desc, $"{hours:F1}"));
                    any = true;
                }
                return any ? sb.ToString().TrimEnd() : null;
            });

            RimMindAPI.RegisterPawnContextProvider("dialogue_relation", pawn =>
            {
                var session = DialogueSessionManager.GetOrCreate(pawn);
                if (session?.Recipient == null) return null;

                var recipient = session.Recipient;
                var sb = new StringBuilder();
                sb.AppendLine($"[与 {recipient.Name.ToStringShort} 的关系]");

                float opinion = pawn.relations?.OpinionOf(recipient) ?? 0f;
                string opinionLabel = opinion >= 20 ? "朋友" : opinion <= -20 ? "仇敌" : "相识";
                sb.AppendLine($"好感度: {opinion:+0;-0} ({opinionLabel})");

                float compat = pawn.relations?.CompatibilityWith(recipient) ?? 0.5f;
                string compatLabel = compat >= 0.6f ? "高，容易产生愉快交流" : compat <= 0.3f ? "低，容易产生摩擦" : "中等";
                sb.AppendLine($"兼容性: {compat:F2} ({compatLabel})");

                float romance = pawn.relations?.SecondaryRomanceChanceFactor(recipient) ?? 0f;
                string romanceLabel = romance >= 0.5f ? "高" : romance >= 0.15f ? "中等" : "低";
                sb.AppendLine($"吸引力: {romance:F2} ({romanceLabel}，不太可能发展浪漫关系)");

                var directRel = pawn.relations?.DirectRelations?.FirstOrDefault(r => r.otherPawn == recipient);
                if (directRel != null)
                    sb.AppendLine($"关系: {directRel.def.label}");

                return sb.ToString().TrimEnd();
            });
        }

        public override string SettingsCategory() => "RimMind - Dialogue";

        public override void DoSettingsWindowContents(UnityEngine.Rect rect)
        {
            RimMindDialogueSettings.DrawSettingsContent(rect);
        }
    }
}
