# Dialogue lifecycle

Start with `RimMindDialogueService.cs`. It is the stable facade used by patches,
components, UI, and other RimMind extensions.

## Request flow

`RimMindDialogueService.HandleTrigger`
→ `DialogueRequestCoordinator`
→ Core `RimMindAPI.Request.Send`
→ main-thread completion
→ `NpcResponseHandler`

## Responsibilities

- `DialogueRequestCoordinator.cs`: gates, reservations, request construction, cleanup.
- `DialogueActivityState.cs`: readiness, cooldowns, quotas, recipients, Pawn lookup.
- `DialogueLogStore.cs`: bounded log storage and snapshots.
- `NpcResponseHandler.cs`: game-side effects after a successful response.
- `DialogueFlowPolicy.cs`: pure monologue, quota, and reply rules.

## Invariants

- Reservation ownership fences stale callbacks.
- Verse and Unity side effects run on the main thread.
- Replies use their own pair limiter and do not consume the normal dialogue quota.
- Debug code reads supported diagnostics and never reflects private lifecycle fields.

## Smallest verification

`dotnet test Tests/RimMindDialogue.Tests.csproj -c Release`

`dotnet build Source/RimMindDialogue.csproj -c Release`
