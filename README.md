# RimMind - Dialogue

AI 驱动的对话系统，拦截游戏事件生成上下文 AI 对话，支持玩家主动发起对话，让殖民者真正"开口说话"。

## RimMind 是什么

RimMind 是一套 AI 驱动的 RimWorld 模组套件，通过接入大语言模型（LLM），让殖民者拥有人格、记忆、对话和自主决策能力。

## 子模组列表与依赖关系

| 模组 | 职责 | 依赖 |
|------|------|------|
| RimMind-Core | API 客户端、请求调度、上下文打包 | Harmony |
| RimMind-Actions | AI 控制小人的动作执行库 | Core |
| RimMind-Advisor | AI 扮演小人做出工作决策 | Core, Actions |
| **RimMind-Dialogue** | **AI 驱动的对话系统** | Core |
| RimMind-Memory | 记忆采集与上下文注入 | Core |
| RimMind-Personality | AI 生成人格与想法 | Core |
| RimMind-Storyteller | AI 叙事者，智能选择事件 | Core |

```
Core ── Actions ── Advisor
  ├── Dialogue
  ├── Memory
  ├── Personality
  └── Storyteller
```

## 安装步骤

1. 安装 [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) 前置模组
2. 安装 RimMind-Core
3. 安装 RimMind-Dialogue
4. 在模组管理器中确保加载顺序：Harmony → Core → Dialogue

<!-- ![安装步骤](images/install-steps.png) -->

## 快速开始

### 填写 API Key

1. 启动游戏，进入主菜单
2. 点击 **选项 → 模组设置 → RimMind-Core**
3. 填写你的 **API Key** 和 **API 端点**
4. 填写 **模型名称**（如 `gpt-4o-mini`）
5. 点击 **测试连接**，确认显示"连接成功"

### 体验对话

- **自动对话**：殖民者受伤、技能升级、心情变化时会自动生成内心独白
- **主动对话**：右键点击其他殖民者，选择"对话"选项
- **对话浮窗**：屏幕角落实时显示最近对话

<!-- ![对话窗口](images/screenshot-dialogue-window.png) -->

## 截图展示

<!-- ![对话浮窗](images/screenshot-dialogue-overlay.png) -->
<!-- ![对话日志](images/screenshot-dialogue-log.png) -->
<!-- ![Thought注入](images/screenshot-dialogue-thought.png) -->

## 核心功能

### 事件驱动的 AI 对话

通过拦截游戏事件，触发 AI 生成上下文感知的对话：

| 触发来源 | 说明 |
|----------|------|
| 社交互动 | 拦截原生 Chitchat/DeepTalk，替换为 AI 对话 |
| 健康变化 | 受伤或生病时的反应 |
| 技能升级 | 技能提升时的内心独白 |
| 心情波动 | 显著心情变化时的表达 |
| 自动独白 | 空闲时随机触发的独白 |

### Thought 注入系统

对话产生的心理影响转化为游戏内 Thought，实际影响殖民者心情：

| 标签 | 心情值 | 说明 |
|------|--------|------|
| ENCOURAGED | +1 | 受到鼓励 |
| HURT | -1 | 感到受伤 |
| VALUED | +2 | 感到被重视 |
| CONNECTED | +2 | 感到亲近 |
| STRESSED | -2 | 感到压力 |
| IRRITATED | -1 | 感到烦躁 |

### 玩家主动对话

- 右键点击其他殖民者选择"对话"
- 支持多轮对话，保留历史记录
- 对话回复链：A 对话后自动触发 B 的回复

### 对话日志

- 分类查看：殖民者独白/对话、非殖民者独白/对话、玩家对话
- 时间戳显示游戏内时间
- 浮窗覆盖：屏幕角落实时显示最近对话

## 设置项

| 设置 | 默认值 | 说明 |
|------|--------|------|
| 启用对话系统 | 开启 | 总开关 |
| 社交互动触发 | 开启 | 拦截 Chitchat/DeepTalk |
| 健康变化触发 | 开启 | 受伤/生病时触发 |
| 技能升级触发 | 开启 | 技能提升时触发 |
| 心情变化触发 | 开启 | 心情波动时触发 |
| 自动独白触发 | 开启 | 空闲时随机独白 |
| 玩家主动对话 | 开启 | 右键菜单对话选项 |
| 自动独白冷却 | 6 游戏小时 | 独白最小间隔 |
| 心情变化阈值 | 3 | 触发对话的最小心情变化 |
| 最大对话历史轮数 | 6 | 玩家对话保留的历史轮数 |
| 全局并发上限 | 3 | 同时进行的 AI 请求数上限 |
| 显示对话浮窗 | 开启 | 屏幕角落实时对话 |
| 浮窗透明度 | 75% | 浮窗透明度 |

## 常见问题

**Q: 对话会影响游戏性能吗？**
A: 不会。所有 AI 请求异步执行，并发数有上限控制，各类触发均有冷却机制。

**Q: 配合 Memory 模组效果更好吗？**
A: 是的。安装 RimMind-Memory 后，对话内容会自动写入记忆系统，让 AI 在后续对话中参考历史。

**Q: 可以只开启部分触发类型吗？**
A: 可以。在模组设置中可单独开关每种触发类型。

**Q: 对话浮窗可以移动位置吗？**
A: 可以在模组设置中调整浮窗的位置和大小。

---

# RimMind - Dialogue (English)

An AI-driven dialogue system that intercepts game events to generate contextual AI conversations, supports player-initiated dialogue, and makes colonists truly "speak".

## What is RimMind

RimMind is an AI-driven RimWorld mod suite that connects to Large Language Models (LLMs), giving colonists personality, memory, dialogue, and autonomous decision-making.

## Sub-Modules & Dependencies

| Module | Role | Depends On |
|--------|------|------------|
| RimMind-Core | API client, request dispatch, context packaging | Harmony |
| RimMind-Actions | AI-controlled pawn action execution | Core |
| RimMind-Advisor | AI role-plays colonists for work decisions | Core, Actions |
| **RimMind-Dialogue** | **AI-driven dialogue system** | Core |
| RimMind-Memory | Memory collection & context injection | Core |
| RimMind-Personality | AI-generated personality & thoughts | Core |
| RimMind-Storyteller | AI storyteller, smart event selection | Core |

## Installation

1. Install [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
2. Install RimMind-Core
3. Install RimMind-Dialogue
4. Ensure load order: Harmony → Core → Dialogue

## Quick Start

### API Key Setup

1. Launch the game, go to main menu
2. Click **Options → Mod Settings → RimMind-Core**
3. Enter your **API Key** and **API Endpoint**
4. Enter your **Model Name** (e.g., `gpt-4o-mini`)
5. Click **Test Connection** to confirm

### Experience Dialogue

- **Auto Dialogue**: Colonists generate inner monologues when injured, leveling skills, or experiencing mood changes
- **Player Dialogue**: Right-click another colonist and select "Dialogue"
- **Dialogue Overlay**: Real-time dialogue display in screen corner

## Key Features

- **Event-Driven AI Dialogue**: Intercepts game events (social, health, skill, mood) to trigger contextual AI conversations
- **Thought Injection**: Dialogue impacts are injected as in-game Thoughts, actually affecting colonist mood
- **Player-Initiated Dialogue**: Right-click colonists to start conversations with multi-turn history
- **Dialogue Log**: Categorized log with timestamps and real-time overlay

## FAQ

**Q: Will dialogue affect game performance?**
A: No. All AI requests are async with concurrency limits and cooldown mechanisms.

**Q: Does it work better with Memory?**
A: Yes. With RimMind-Memory installed, dialogue content is automatically saved to the memory system for AI to reference in future conversations.

**Q: Can I enable only certain trigger types?**
A: Yes. Each trigger type can be toggled individually in mod settings.

**Q: Can I move the dialogue overlay?**
A: Yes. Overlay position and size can be adjusted in mod settings.
