using System;
using System.Linq;
using System.Threading;
using RimMind.Dialogue.Comps;
using RimMind.Dialogue.Core;
using RimMind.Dialogue.Settings;
using RimMind.Dialogue.UI;
using RimMind.Domain.Llm;
using RimMind.Domain.ValueObjects;
using RimMind.Presentation.Api;
using RimWorld;
using UnityEngine;
using Verse;
using Xunit;

namespace RimMind.Dialogue.Tests.Contracts
{
    public sealed class DialogueRequestLifecycleContracts : IDisposable
    {
        private readonly RimMindDialogueSettings _settings;
        private readonly Pawn _pawn;
        private readonly Pawn _recipient;

        public DialogueRequestLifecycleContracts()
        {
            RimMindAPI.Configured = true;
            RimMindAPI.Skip = false;
            RimMindAPI.SendException = null;
            RimMindAPI.ImmediateResult = null;
            RimMindAPI.Sent.Clear();
            RimMindAPI.Perceptions.Clear();
            RimMindAPI.Memory.Pawns.Clear();
            Messages.Shown.Clear();
            ModsConfig.MemoryEnabled = false;
            Widgets.Labels.Clear();
            Widgets.NextText = null;
            Widgets.ClickSend = false;
            _settings = new RimMindDialogueSettings
            {
                startDelayEnabled = false,
                enableDialogueReply = false
            };
            Find.TickManager.TicksGame = 1000;
            var map = new Map();
            _pawn = new Pawn { thingIDNumber = 1, IsColonist = true, Faction = Faction.OfPlayer, Map = map };
            _recipient = new Pawn { thingIDNumber = 2, IsColonist = true, Map = map };
            map.mapPawns.AllPawns.AddRange(new[] { _pawn, _recipient });
            Find.Maps.Clear();
            Find.Maps.Add(map);
            RimMindDialogueService.NotifyGameLoaded();
        }

        public void Dispose() => RimMindDialogueService.NotifyGameLoaded();

        [Theory]
        [InlineData("disabled")]
        [InlineData("player disabled")]
        [InlineData("unconfigured")]
        [InlineData("startup delay")]
        [InlineData("skip policy")]
        [InlineData("dead pawn")]
        public void Player_entry_rejects_shared_gates_and_completes_the_waiter(string gate)
        {
            switch (gate)
            {
                case "disabled": _settings.enabled = false; break;
                case "player disabled": _settings.playerDialogueEnabled = false; break;
                case "unconfigured": RimMindAPI.Configured = false; break;
                case "startup delay": _settings.startDelayEnabled = true; break;
                case "skip policy": RimMindAPI.Skip = true; break;
                case "dead pawn": _pawn.Dead = true; break;
            }
            int replies = 0, errors = 0;
            DialogueService.RequestReply(_pawn, "hello", _recipient, _ => replies++, _ => errors++);
            Assert.Empty(RimMindAPI.Sent);
            Assert.Equal(0, replies);
            Assert.Equal(1, errors);
            Assert.Equal(0, RimMindDialogueService.ActiveRequestCount);
        }

        [Fact]
        public void Player_and_automatic_entries_share_pawn_pair_and_global_capacity()
        {
            _settings.globalConcurrency = 1;
            DialogueService.RequestReply(_pawn, "hello", _recipient, _ => { }, _ => { });
            Assert.Equal(1, RimMindDialogueService.ActiveRequestCount);
            Assert.Equal(1, RimMindDialogueService.ActivePairCount);
            Assert.Same(_recipient, RimMindDialogueService.GetActiveRecipient(_pawn));
            RimMindDialogueService.HandleTrigger(_pawn, "same pawn", DialogueTriggerType.Thought, null);
            RimMindDialogueService.HandleTrigger(_recipient, "reversed pair", DialogueTriggerType.Chitchat, _pawn);
            RimMindDialogueService.HandleTrigger(new Pawn { thingIDNumber = 3 }, "capacity", DialogueTriggerType.Auto, null);
            Assert.Single(RimMindAPI.Sent);
            Complete(0, "reply");
            Assert.Equal(0, RimMindDialogueService.ActiveRequestCount);
            Assert.Equal(0, RimMindDialogueService.ActivePairCount);
            Assert.Null(RimMindDialogueService.GetActiveRecipient(_pawn));
        }

        [Theory]
        [InlineData(true, "pawn")]
        [InlineData(false, "pawn")]
        [InlineData(true, "pair")]
        [InlineData(false, "pair")]
        [InlineData(true, "capacity")]
        [InlineData(false, "capacity")]
        public void Each_reservation_blocks_both_entry_directions(bool playerFirst, string conflict)
        {
            _settings.globalConcurrency = conflict == "capacity" ? 1 : 10;
            if (playerFirst)
                DialogueService.RequestReply(_pawn, "first", _recipient, _ => { }, _ => { });
            else
                RimMindDialogueService.HandleTrigger(_pawn, "first", DialogueTriggerType.Chitchat, _recipient);

            Pawn nextPawn = conflict == "pawn" ? _pawn
                : conflict == "pair" ? _recipient : new Pawn { thingIDNumber = 3 };
            Pawn? nextRecipient = conflict == "pair" ? _pawn : null;
            int errors = 0;
            if (playerFirst)
                RimMindDialogueService.HandleTrigger(nextPawn, "blocked", DialogueTriggerType.Chitchat, nextRecipient);
            else
                DialogueService.RequestReply(nextPawn, "blocked", nextRecipient, _ => { }, _ => errors++);

            Assert.Single(RimMindAPI.Sent);
            Assert.Equal(playerFirst ? 0 : 1, errors);
            Assert.Equal(1, RimMindDialogueService.ActiveRequestCount);
            Assert.Equal(1, RimMindDialogueService.ActivePairCount);
        }

        [Fact]
        public void Player_turns_bypass_automatic_quota_and_do_not_consume_it()
        {
            _settings.maxDailyDialogueRounds = 0;
            int replies = 0;
            DialogueService.RequestReply(_pawn, "first", _recipient, _ => replies++, _ => { });
            Complete(0, "first reply");
            DialogueService.RequestReply(_pawn, "second", _recipient, _ => replies++, _ => { });
            Complete(1, "second reply");
            Assert.Equal(2, replies);
            Assert.Equal(0, RimMindDialogueService.GetDailyDialogueCount(1, 2));
            RimMindDialogueService.HandleTrigger(_pawn, "automatic", DialogueTriggerType.Chitchat, _recipient);
            Assert.Equal(2, RimMindAPI.Sent.Count);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Shared_builder_preserves_player_input_and_automatic_request_options(bool player)
        {
            if (player)
                DialogueService.RequestReply(_pawn, "player input", _recipient, _ => { }, _ => { });
            else
                RimMindDialogueService.HandleTrigger(_pawn, "trigger", DialogueTriggerType.Auto, null);

            var envelope = Assert.Single(RimMindAPI.Sent).Envelope;
            Assert.Equal("NPC-1", envelope.NpcId);
            Assert.Equal("RimMind.Dialogue", envelope.ModId);
            Assert.Equal(RimMind.Application.Common.Models.Context.ScenarioIds.Dialogue, envelope.ScenarioId);
            Assert.Equal(400, envelope.MaxTokens);
            Assert.Equal(player ? 0.85f : 0.8f, envelope.Temperature);
            Assert.Contains(player ? "<dialogue_input>" : "<dialogue_trigger>", envelope.GameStateInfo!.ToString());
            Assert.Contains(player ? "player input" : "RimMind.Dialogue.Prompt.AutoTrigger", envelope.GameStateInfo.ToString());
            Assert.True(envelope.Ct.CanBeCanceled);
        }

        [Fact]
        public void Automatic_reply_chain_continues_A_B_until_its_own_limiter()
        {
            _settings.enableDialogueReply = true;
            _settings.maxDailyDialogueRounds = 1;
            _settings.maxDailyReplyRounds = 2;
            RimMindDialogueService.HandleTrigger(_pawn, "start", DialogueTriggerType.Chitchat, _recipient);
            Complete(0, "A");
            Complete(1, "B");
            Complete(2, "A again");
            Assert.Equal(new[] { "NPC-1", "NPC-2", "NPC-1" }, RimMindAPI.Sent.Select(x => x.Envelope.NpcId));
            Assert.Equal(1, RimMindDialogueService.GetDailyDialogueCount(1, 2));
            Assert.Equal(0, RimMindDialogueService.ActiveRequestCount);
        }

        [Fact]
        public void Synchronous_dispatch_throw_clears_reservations_and_notifies_once()
        {
            RimMindAPI.SendException = new InvalidOperationException("dispatch failed");
            int errors = 0;
            DialogueService.RequestReply(_pawn, "hello", _recipient, _ => { }, _ => errors++);
            Assert.Equal(1, errors);
            Assert.Equal(0, RimMindDialogueService.ActiveRequestCount);
            Assert.Null(RimMindDialogueService.GetActiveRecipient(_pawn));
        }

        [Theory]
        [InlineData("error")]
        [InlineData("empty")]
        [InlineData("dead pawn")]
        [InlineData("dead recipient")]
        public void Unsuccessful_response_notifies_once_and_leaves_no_side_effects(string failure)
        {
            int replies = 0, errors = 0;
            DialogueService.RequestReply(_pawn, "hello", _recipient, _ => replies++, _ => errors++);
            if (failure == "dead pawn") _pawn.Dead = true;
            if (failure == "dead recipient") _recipient.Dead = true;
            if (failure == "error")
                RimMindAPI.Sent[0].Complete(Result<LlmResponse, RimMindError>.Err(
                    RimMindErrors.PipelineShortCircuited("provider unavailable")));
            else
                Complete(0, failure == "empty" ? "  " : "reply");
            Complete(0, "late duplicate");
            Assert.Equal(0, replies);
            Assert.Equal(1, errors);
            Assert.Equal(0, RimMindDialogueService.ActiveRequestCount);
            Assert.Equal(0, RimMindDialogueService.ActivePairCount);
            Assert.Null(RimMindDialogueService.GetActiveRecipient(_pawn));
            Assert.Empty(RimMindDialogueService.LogEntries);
            Assert.Empty(RimMindAPI.Perceptions);
        }

        [Fact]
        public void Response_subscriber_throw_still_terminates_waiter_and_allows_the_next_turn()
        {
            int replies = 0, errors = 0;
            Action<Pawn, Pawn?, string, string?> subscriber = (_, _, _, _) => throw new InvalidOperationException("subscriber failed");
            RimMindDialogueService.OnDialogueCompleted += subscriber;
            try
            {
                DialogueService.RequestReply(_pawn, "hello", _recipient, _ => replies++, _ => errors++);
                Complete(0, "reply");
                Assert.Equal(0, replies);
                Assert.Equal(1, errors);
                Assert.Equal(0, RimMindDialogueService.ActiveRequestCount);
            }
            finally { RimMindDialogueService.OnDialogueCompleted -= subscriber; }

            DialogueService.RequestReply(_pawn, "next", _recipient, _ => replies++, _ => errors++);
            Complete(1, "next reply");
            Assert.Equal(1, replies);
            Assert.Equal(1, errors);
        }

        [Fact]
        public void Already_cancelled_request_is_rejected_before_dispatch()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            int errors = 0;
            DialogueService.RequestReply(_pawn, "hello", _recipient, _ => { }, _ => errors++, cancellation.Token);
            Assert.Empty(RimMindAPI.Sent);
            Assert.Equal(1, errors);
            Assert.Equal(0, RimMindDialogueService.ActiveRequestCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Inline_Core_completion_has_the_same_single_terminal_cleanup(bool success)
        {
            RimMindAPI.ImmediateResult = success
                ? Result<LlmResponse, RimMindError>.Ok(new LlmResponse { Content = "inline" })
                : Result<LlmResponse, RimMindError>.Err(RimMindErrors.PipelineShortCircuited("unavailable"));
            int replies = 0, errors = 0;
            DialogueService.RequestReply(_pawn, "hello", _recipient, _ => replies++, _ => errors++);
            Assert.Equal(success ? 1 : 0, replies);
            Assert.Equal(success ? 0 : 1, errors);
            Assert.Equal(0, RimMindDialogueService.ActiveRequestCount);
            Assert.Null(RimMindDialogueService.GetActiveRecipient(_pawn));
            Complete(0, "duplicate");
            Assert.Equal(success ? 1 : 0, replies);
            Assert.Equal(success ? 0 : 1, errors);
        }

        [Fact]
        public void Reset_terminates_waiter_and_stale_callback_cannot_touch_new_request()
        {
            int replies = 0, errors = 0;
            DialogueService.RequestReply(_pawn, "old", _recipient, _ => replies++, _ => errors++);
            RimMindDialogueService.NotifyGameLoaded();
            Assert.Equal(1, errors);
            DialogueService.RequestReply(_pawn, "new", _recipient, _ => replies++, _ => errors++);
            Complete(0, "stale");
            Assert.Equal(0, replies);
            Assert.Empty(RimMindDialogueService.LogEntries);
            Assert.Empty(RimMindAPI.Perceptions);
            Assert.Equal(1, RimMindDialogueService.ActiveRequestCount);
            Assert.Same(_recipient, RimMindDialogueService.GetActiveRecipient(_pawn));
            Complete(1, "current");
            Assert.Equal(1, replies);
            Assert.Equal("current", Assert.Single(RimMindDialogueService.LogEntries).reply);
        }

        [Fact]
        public void Duplicate_callback_is_ignored_after_successful_response()
        {
            int replies = 0;
            ModsConfig.MemoryEnabled = true;
            DialogueService.RequestReply(_pawn, "hello", _recipient, _ => replies++, _ => { });
            Complete(0, "{\"reply\":\"hi\",\"thought\":{\"tag\":\"NONE\"}}");
            Complete(0, "duplicate");
            Assert.Equal(1, replies);
            Assert.Equal("hi", Assert.Single(RimMindDialogueService.LogEntries).reply);
            Assert.Equal(DialogueCategory.PlayerDialogue, RimMindDialogueService.LogEntries[0].category);
            Assert.Single(RimMindAPI.Perceptions);
            Assert.Equal(new[] { "Thing_1", "Thing_2" }, RimMindAPI.Memory.Pawns);
        }

        [Fact]
        public void Closed_window_cancels_request_and_does_not_process_late_reply()
        {
            var window = new Window_Dialogue(_pawn, _recipient);
            SendFromWindow(window, "hello");
            Assert.Single(RimMindAPI.Sent);
            window.PostClose();
            Assert.Equal(0, RimMindDialogueService.ActiveRequestCount);
            Assert.True(RimMindAPI.Sent[0].Envelope.Ct.IsCancellationRequested);
            Complete(0, "late");
            Assert.Empty(RimMindDialogueService.LogEntries);
            Assert.Empty(RimMindAPI.Perceptions);
            Assert.Empty(Messages.Shown);
        }

        [Fact]
        public void Window_can_send_again_after_rejection_or_dispatch_exception()
        {
            var window = new Window_Dialogue(_pawn);
            _settings.enabled = false;
            SendFromWindow(window, "rejected");
            Assert.Empty(RimMindAPI.Sent);
            _settings.enabled = true;
            RimMindAPI.SendException = new InvalidOperationException("failed");
            SendFromWindow(window, "failed");
            RimMindAPI.SendException = null;
            SendFromWindow(window, "accepted");
            Assert.Single(RimMindAPI.Sent);
            window.PostClose();
        }

        [Fact]
        public void Disabled_mod_does_not_offer_player_gizmo()
        {
            var comp = new CompRimMindDialogue { parent = _pawn };
            Assert.Single(comp.CompGetGizmosExtra());
            _settings.enabled = false;
            Assert.Empty(comp.CompGetGizmosExtra());
        }

        [Fact]
        public void GetActiveRecipient_works_safely_off_main_thread_without_map_access()
        {
            var window = new Window_Dialogue(_pawn, _recipient);
            SendFromWindow(window, "test message");
            Assert.Single(RimMindAPI.Sent);

            // Switch to background thread (off-main-thread)
            UnityData.IsInMainThread = false;

            // Empty the map's pawns to prove it does not touch mapPawns off-thread
            var map = Find.Maps[0];
            var savedPawns = map.mapPawns.AllPawns.ToList();
            map.mapPawns.AllPawns.Clear();

            // Calling GetActiveRecipient off-thread must succeed via cached recipient!
            var recipient = RimMindDialogueService.GetActiveRecipient(_pawn);
            Assert.Same(_recipient, recipient);

            // Restore
            map.mapPawns.AllPawns.AddRange(savedPawns);
            UnityData.IsInMainThread = true;
            window.PostClose();
        }

        private static void Complete(int index, string content)
        {
            string? toolCalls = string.IsNullOrWhiteSpace(content)
                ? null
                : "[{\"name\":\"express_dialogue\",\"arguments\":{\"speech\":\"" + content.Replace("\"", "\\\"") + "\"}}]";
            RimMindAPI.Sent[index].Complete(Result<LlmResponse, RimMindError>.Ok(new LlmResponse
            {
                Content = content,
                ToolCallsJson = toolCalls
            }));
        }

        private static void SendFromWindow(Window_Dialogue window, string text)
        {
            Widgets.NextText = text;
            Widgets.ClickSend = true;
            window.DoWindowContents(new Rect(0, 0, 480, 540));
        }
    }
}
