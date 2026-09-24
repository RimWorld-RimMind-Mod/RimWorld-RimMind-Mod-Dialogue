# RimMind Dialogue contract tests

The project compiles production lifecycle and policy code directly. Core Domain and
Application are project references, including the real request-envelope builder.

## Active contract manifest

| Contract | Behavior | Discovered cases |
|---|---|---:|
| `Contracts/DialoguePipelineContracts.cs` | classification, pair keys, parser, bounded log snapshots, activity cooldown/quota, overlay geometry | 8 |
| `Contracts/DialogueThoughtInjectionContracts.cs` | thought tags, reply rate limit, quota policy, Thought save fields | 5 |
| `Contracts/DialogueGateErrorContracts.cs` | atomic Pawn/pair/capacity reservations, lifecycle fencing and ownership cleanup | 7 |
| `Contracts/DialogueRequestLifecycleContracts.cs` | real player/automatic entries, shared admission and envelopes, response effects, cancellation, failure and window/Gizmo lifecycle | 31 |

Current discovery count: **51 cases**, including every Theory row (module budget:
strictly below 1000). Unrelated scenarios are independently discovered, not hidden
inside a budget-reducing umbrella Fact.

## Active project entry

`RimMindDialogue.Tests.csproj` explicitly lists production files. Lifecycle tests
execute `DialogueService`, `RimMindDialogueService`, `DialogueRequestCoordinator`,
`DialogueActivityState`, `NpcResponseHandler`, `Window_Dialogue`, the settings class
and `CompRimMindDialogue`; they do not copy the admission or completion algorithm.

`DialogueBoundaryStubs.cs`, `VerseStubs.cs`, `RimWorldStubs.cs` and
`UnityEngineStubs.cs` replace only external Core facade and game-engine dependencies.
The request boundary captures real envelopes and delivers controlled terminal
results. GUI input is driven through `DoWindowContents`, with no private-field
reflection. These tests do not validate real Unity rendering or a loaded game.

From the repository root:

```powershell
dotnet test RimMind-Dialogue/Tests/RimMindDialogue.Tests.csproj -c Release
dotnet build RimMind-Dialogue/Source/RimMindDialogue.csproj -c Release
```

## Retired legacy tests

Files outside `Contracts/` are retained on disk but excluded from compilation.
Their behavior mapping is recorded in the root contract mapping document.
Deletion requires explicit owner approval for each exact file path; directories are never deleted.
