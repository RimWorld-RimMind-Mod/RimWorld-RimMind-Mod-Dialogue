# RimMind Dialogue contract tests

The project now compiles only the active behavior contracts and their explicitly
listed production seams. Legacy files remain on disk but are excluded.

## Active contract manifest

| Contract | Stable boundaries | Discovered facts |
|---|---|---:|
| `Contracts/DialoguePipelineContracts.cs` | classification, pair keys, monologue/relation semantics, response parsing | 1 |
| `Contracts/DialogueThoughtInjectionContracts.cs` | thought tags, continuous reply rate limit, active-dialogue quota policy, Thought save fields | 1 |
| `Contracts/DialogueGateErrorContracts.cs` | atomic Pawn/pair/capacity reservation, lifecycle reset fencing and ownership-aware cleanup | 1 |

Current discovery count: **3 Facts**, **0 Theories** (budget: <= 40).
Each Fact uses `ContractCaseRunner` to report its named scenarios independently.

## Active project entry

The active project entry includes:

```xml
<PropertyGroup>
  <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
</PropertyGroup>
<ItemGroup>
  <Compile Include="Contracts\**\*.cs" />
  <Compile Include="..\..\RimMind-Core\TestSupport\ContractCaseRunner.cs"
           Link="Support\ContractCaseRunner.cs" />
  <Compile Include="RimWorldStubs.cs" />
  <Compile Include="UnityEngineStubs.cs" />
  <Compile Include="VerseStubs.cs" />
  <Compile Include="..\Source\Core\ResponseJsonParser.cs" LinkBase="Core" />
  <Compile Include="..\Source\Core\DialogueTypes.cs" LinkBase="Core" />
  <Compile Include="..\Source\Core\DialogueClassifier.cs" LinkBase="Core" />
  <Compile Include="..\Source\Core\DialogueLogEntry.cs" LinkBase="Core" />
  <Compile Include="..\Source\Core\DialogueFlowPolicy.cs" LinkBase="Core" />
  <Compile Include="..\Source\Core\DialogueRequestReservations.cs" LinkBase="Core" />
  <Compile Include="..\Source\Core\DialogueActiveRecipientRegistry.cs" LinkBase="Core" />
  <Compile Include="..\Source\Core\DialoguePairRateLimiter.cs" LinkBase="Core" />
  <Compile Include="..\Source\Thoughts\ThoughtInjector.cs" LinkBase="Thoughts" />
  <Compile Include="..\Source\Thoughts\Thought_RimMindDialogue.cs" LinkBase="Thoughts" />
  <Compile Include="..\Source\Thoughts\Thought_RelationDialogue.cs" LinkBase="Thoughts" />
</ItemGroup>
```

Legacy compile categories superseded by these contracts are:

- classifier, parser, and parser edge-case tests;
- monologue, quota, lifecycle, log-entry, and pair-key tests;
- Thought mapping, key convention, concurrency, and injection tests.

## Retired legacy tests

Files outside `Contracts/` are retained on disk but excluded from compilation.
Their behavior mapping is recorded in the root contract mapping document.
Deletion requires explicit owner approval for each exact file path; directories are never deleted.
