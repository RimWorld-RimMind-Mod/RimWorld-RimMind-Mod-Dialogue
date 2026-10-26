<div align="center">

# RimMind-Dialogue 💬
### Contextual Social Dialogue, Native Spoken ToolCalls & Overhead Bubbles for RimWorld 1.6 & 1.7

**English** | [简体中文](README_zh.md)

<p>
  <a href="https://rimworldgame.com/"><img src="https://img.shields.io/badge/RimWorld-1.6%20%7C%201.7-brightgreen.svg" alt="RimWorld 1.6 | 1.7"></a>
  <a href="https://github.com/RimWorld-RimMind-Mod/RimWorld-RimMind-Mod-Core"><img src="https://img.shields.io/badge/Dependency-RimMind--Core-blue.svg" alt="Dependency: RimMind-Core"></a>
  <a href="#"><img src="https://img.shields.io/badge/Unit%20Tests-67%2B%20Passing-success.svg" alt="Unit Tests"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
</p>

<p><em>Imbue your colonists with spontaneous, context-aware conversations, natural banter, and evolving relationships.</em></p>

</div>

---

## 📖 Overview

**RimMind-Dialogue** brings RimWorld colonists to life through organic, natural spoken interactions. Powered by the native `express_dialogue` function call architecture, colonists talk to each other while eating in dining halls, laboring at craft benches, or resting by campfires.

### Core Capabilities
- **Native `express_dialogue` ToolCall (`DialogueToolDefinitions` & `DialogueToolParser`)**: Transmits spoken dialogue (`reply`), internal psychological subtext (`thought`), and relationship delta modifiers (`relation_delta: [-5, 5]`) as structured function parameters via `DialogueToolDefinitions.ExpressDialogue`, parsed deterministically by `DialogueToolParser` with dual-mode tool call and fallback support.
- **Dialogue Bubble Anti-Pollution & Sanitization Pipeline (`DialogueTextSanitizer`)**: Multi-stage sanitization strips `<think>` reasoning blocks, markdown code fences, raw JSON/XML fragments, and prompt metadata before any bubble or dialogue window renders, strictly blocking unparsed LLM artifacts from appearing over colonists' heads.
- **Contextual Spoken Triggers & Liveness Gating**: Conversational opportunities trigger organically based on physical proximity (radius 6.9 tiles), shared activities, and mutual social opinions, strictly guarded by `RimMindPawnLookup` liveness and consciousness checks so downed, sleeping, or off-map pawns never speak.
- **Dynamic Relationship Evolution**: Conversations dynamically impact mutual pawn opinions, fostering deep romances, bitter rivalries, or lasting camaraderie.
- **Smooth Overhead Speech Bubbles**: Renders text motes directly above colonists' heads with word wrapping and gentle fading animations.

---

## 🎮 In-Game Showcase

![RimMind-Dialogue Showcase](docs/images/showcase.jpg)
*Overhead spoken dialogue in the dining hall: Colonists share natural banter over a meal, with speech bubbles and live opinion adjustments.*

---

## 🏛️ Architecture & Dialogue ToolCall Flow

```mermaid
flowchart TD
    NearbyPawns["Nearby Colonists (Radius < 6.9, Awake & Active)"] --> Trigger["Social Context Trigger (Dining / Crafting)"]
    Trigger --> CoreReq["Submit Request via RimMind-Core (DialogueToolDefinitions)"]
    CoreReq --> ToolCall["LLM Returns express_dialogue ToolCall"]
    ToolCall --> Parser["DialogueToolParser & DialogueTextSanitizer"]
    Parser --> Bubble["Overhead Speech Bubble Rendering"]
    Parser --> Social["Update Pawn Opinion / Relationship"]
    Parser --> Thought["Inject Dialogue Thought into Mind"]
```

---

## 🛠️ Installation & Load Order

```text
1. Harmony
2. Core (Vanilla RimWorld)
3. RimMind-Core
4. RimMind-Dialogue
```

---

## 🧪 Developer Guide & Testing

Run unit tests covering `express_dialogue` tool call parsing, `DialogueTextSanitizer` anti-pollution filtering, and opinion delta boundaries (113 test cases):

```powershell
dotnet test RimMind-Dialogue/Tests/RimMindDialogue.Tests.csproj -c Release
```

---

## 📜 License

Licensed under the [MIT License](LICENSE).
