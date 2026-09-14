# Dialogue lifecycle

Start with `RimMindDialogueService.cs` for automatic triggers, or
`DialogueService.RequestReply` for player input from `Window_Dialogue`.
Both use the same `DialogueRequestCoordinator` instance.

## Module composition

`../RimMindDialogueMod.cs` installs Harmony patches and Core extensions, then
calls `DialogueContextProviderRegistrar.RegisterAll`. Open the registrar only
when changing `dialogue_state`, `dialogue_relation`, or `dialogue_task` context.

## Request flow

`RimMindDialogueService.HandleTrigger` / `DialogueService.RequestReply`
→ `DialogueRequestCoordinator`
→ Core `RimMindAPI.Request.Send`
→ Core's main-thread terminal callback
→ `NpcResponseHandler`

## Responsibilities

- `DialogueRequestCoordinator.cs`: shared admission, reservations, request construction,
  cancellation and terminal cleanup. There is one Core request-send site.
- `DialogueActivityState.cs`: readiness, cooldowns, quotas, recipients, Pawn lookup.
- `DialogueLogStore.cs`: bounded log storage and snapshots.
- `NpcResponseHandler.cs`: game-side effects after a successful response, including
  optional memory writes through the Core public `RimMindAPI.Memory` API.
- `DialogueFlowPolicy.cs`: pure monologue, quota, and reply rules.

## Invariants

- Enabled/configured/startup/skip gates, Pawn/pair reservations and Dialogue's global
  capacity apply to both entry paths; player input also respects `playerDialogueEnabled`.
- Player input does not consume automatic daily quotas or monologue cooldowns.
- Admission rejection, dispatch exceptions and response failures terminate player waiters.
- Window closure cancels its request; loading/starting a game cancels all in-flight
  requests. Late or duplicate callbacks cannot run response effects or release newer leases.
- Request entry, reset and caller cancellation run on the main thread. Core delivers
  completion there too; Dialogue does not add a second `LongEventHandler` hop.
- Replies use their own pair limiter and do not consume the normal dialogue quota.
- Reservations are released before response handling so A-B automatic replies may continue.
- Debug code reads supported diagnostics and never reflects private lifecycle fields.

## Smallest verification

`dotnet test Tests/RimMindDialogue.Tests.csproj -c Release`

`dotnet build Source/RimMindDialogue.csproj -c Release`

`Tests/Contracts/DialogueRequestLifecycleContracts.cs` executes the production player
entry, coordinator, activity state, response handler, window and Gizmo. Only external
Verse/Unity and Core facade calls are replaced; request envelopes use the real Core builder.
