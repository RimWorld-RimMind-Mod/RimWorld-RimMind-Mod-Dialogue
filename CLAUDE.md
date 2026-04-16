# AGENTS.md — RimMind-Dialogue

本文件供 AI 编码助手阅读，描述 RimMind-Dialogue 的架构、代码约定和扩展模式。

## 项目定位

RimMind-Dialogue 是 RimMind 套件的**AI 对话系统**。它通过拦截游戏事件触发 AI 生成的对话，为 RimWorld 小人注入动态人格表达。

**核心职责**：
1. **事件拦截** — 通过 Harmony Patch 监听 Chitchat、Hediff、技能升级、心情变化等事件
2. **AI 对话生成** — 调用 RimMind-Core API 生成上下文感知的对话内容
3. **Thought 注入** — 将对话产生的心理影响转化为游戏内 Thought（独白 + 关系）
4. **关系变化** — AI 可输出 `relation_delta` 影响小人间好感度
5. **玩家对话** — 提供主动与小人对话的 UI 界面（多轮对话）
6. **对话日志** — 记录所有对话历史，支持分类查看
7. **记忆桥接** — 通过 `MemoryBridge` 将对话推送到 RimMind-Memory（反射松耦合）

**依赖关系**：
- 依赖 RimMind-Core 提供的 API 和上下文构建
- 与 RimMind-Personality 协作（人格档案影响对话风格）
- 与 RimMind-Memory 松耦合（通过反射调用 `RimMindMemoryAPI.AddMemory`）

## 源码结构

```
Source/
├── RimMindDialogueMod.cs           Mod 入口，注册 Harmony，初始化设置
├── Core/
│   ├── RimMindDialogueService.cs   核心服务：事件处理、对话生成、日志管理
│   ├── DialogueService.cs          玩家对话服务（多轮）
│   ├── DialogueSession.cs          单个小人的对话会话（历史记录）
│   ├── DialogueSessionManager.cs   会话管理器
│   └── MemoryBridge.cs            跨模组记忆桥接（反射调用 RimMindMemoryAPI）
├── Comps/
│   ├── CompRimMindDialogue.cs      ThingComp：自动对话触发 + Gizmo
│   └── CompProperties_RimMindDialogue.cs
├── UI/
│   ├── Window_Dialogue.cs          玩家对话窗口
│   ├── Window_DialogueLog.cs       对话日志窗口（分类 + 双栏对话视图）
│   └── DialogueOverlay.cs          MapComponent，屏幕浮窗覆盖层
├── Thoughts/
│   ├── Thought_RimMindDialogue.cs  自定义独白 Thought 类型
│   ├── Thought_RelationDialogue.cs 自定义关系 Thought 类型
│   └── ThoughtInjector.cs          Thought 注入工具（独白 + 关系）
├── Patches/
│   ├── BubblePatch.cs              Chitchat/DeepTalk 拦截
│   ├── HediffPatch.cs              健康变化监听
│   ├── SkillLearnPatch.cs          技能升级监听
│   ├── ThoughtPatch.cs             心情变化监听
│   ├── FloatMenuPatch.cs           右键菜单添加对话选项
│   ├── GameLoadPatch.cs            游戏加载初始化
│   └── AddCompToHumanlikePatch.cs  为人形种族自动挂载 Comp
├── Settings/
│   └── RimMindDialogueSettings.cs  模组设置
└── Debug/
    └── DialogueDebugActions.cs     Dev 菜单调试动作
```

## 关键类与 API

### RimMindDialogueService

核心服务类，处理所有自动触发的对话：

```csharp
// 处理事件触发
RimMindDialogueService.HandleTrigger(
    pawn,                    // 触发者
    context,                 // 上下文描述
    DialogueTriggerType,     // 触发类型
    recipient,               // 对话对象（独白时为 null）
    isReply = false,         // 是否为回复
    isImmediate = false      // 是否立即触发（跳过冷却）
);

// 触发类型枚举
public enum DialogueTriggerType
{
    Chitchat,    // 社交闲聊
    Hediff,      // 健康变化
    LevelUp,     // 技能升级
    Thought,     // 心情变化
    Auto,        // 后台自动
    PlayerInput  // 玩家输入
}

// 对话分类
public enum DialogueCategory
{
    ColonistMonologue,      // 殖民者独白
    ColonistDialogue,       // 殖民者间对话
    PlayerDialogue,         // 玩家与殖民者对话
    NonColonistMonologue,   // 非殖民者独白
    NonColonistDialogue     // 非殖民者对话
}

// 事件
static event Action? OnLogUpdated;

// 状态
static bool IsReady           // 游戏启动延迟就绪
static IReadOnlyList<DialogueLogEntry> LogEntries
static void ClearLog()
static void NotifyGameLoaded()
```

### DialogueService

玩家主动对话服务：

```csharp
DialogueService.RequestReply(
    session,        // 对话会话
    playerMessage,  // 玩家输入
    onReply,        // 成功回调
    onError         // 错误回调
);
// 内部使用 RimMindAPI.RequestImmediate（非异步队列）
```

### DialogueSession / DialogueSessionManager

```csharp
public class DialogueSession
{
    public Pawn Pawn;
    public Pawn? Recipient;
    public int MaxHistoryRounds = 6;
    public List<(string role, string content)> Messages;

    void AddUserMessage(string text);
    void AddAssistantMessage(string text);
    List<(string role, string content)> GetContextMessages(); // 取最后 MaxHistoryRounds * 2 条
}

static class DialogueSessionManager
{
    static DialogueSession GetOrCreate(Pawn pawn);
    static void Clear(Pawn pawn);
    static void ClearAll();
}
```

### MemoryBridge

跨模组记忆桥接，通过反射调用 `RimMindMemoryAPI.AddMemory`：

```csharp
internal static class MemoryBridge
{
    static void AddMemory(string content, string memoryType, int tick, float importance, string? pawnId = null);
}
```

### ThoughtInjector

将对话产生的心理影响注入游戏：

```csharp
// 注入独白 Thought
ThoughtInjector.Inject(pawn, recipient, tag, description);

// 注入关系 Thought（影响好感度）
ThoughtInjector.InjectRelationDelta(pawn, recipient, float delta);
// delta clamp 在 [-5, +5]，映射为 opinionOffset

// Thought 标签与心情值映射
ENCOURAGED  → +1  受到鼓励
HURT        → -1  感到受伤
VALUED      → +2  感到被重视
CONNECTED   → +2  感到亲近
STRESSED    → -2  感到压力
IRRITATED   → -1  感到烦躁
```

### DialogueLogEntry

```csharp
public class DialogueLogEntry
{
    public int tick;
    public string initiatorName;
    public int initiatorId;
    public bool initiatorIsColonist;
    public string? recipientName;
    public int recipientId;
    public bool recipientIsColonist;
    public DialogueCategory category;
    public string trigger;
    public string context;
    public string reply;
    public string thoughtTag;
    public string thoughtDesc;

    // 计算属性
    bool IsMonologue;
    string PairKey;    // 对话配对标识
    string TimeStr;    // 格式化时间
}
```

## AI Prompt 结构

### 自动对话 Prompt

```
System:
你正在扮演 RimWorld 殖民者 {name}。
根据当前状态用第一人称说出一句内心独白（15~40字）。
触发原因：{triggerLabel}
只返回以下 json 格式：
{"reply": "对话/独白文本", "thought": {"tag": "ENCOURAGED|HURT|...|NONE", "description": "≤15字"}, "relation_delta": 0.0}

User:
{FullPawnContext}
[触发原因: {type}] {context}
```

### 玩家对话 Prompt

```
System:
你正在扮演 RimWorld 殖民者 {name}。
玩家正在与你对话，请用第一人称自然地回应。
保持角色一致性，根据当前状态和性格做出反应。
回复长度 20~60 字。
只返回 json 格式：
{"reply": "对话文本", "thought": {"tag": "...", "description": "..."}}

Messages:
[system] {systemPrompt}
[user] {pawnContext}
[assistant] 好的，我已了解当前情况，请开始对话。
[user] ...
[assistant] ...
```

## 事件触发机制

### Patch 列表

| Patch | 监听事件 | 触发条件 |
|-------|----------|----------|
| BubblePatch | Pawn_InteractionsTracker.TryInteractWith | Chitchat/DeepTalk 成功 |
| HediffPatch | Pawn_HealthTracker.AddHediff | 添加显著 Hediff（isBad/tendable/makesSickThought） |
| SkillLearnPatch | SkillRecord.Learn | 技能等级提升 |
| ThoughtPatch | MemoryThoughtHandler.TryGainMemory | 心情变化绝对值 >= 阈值 |
| FloatMenuPatch | FloatMenuMakerMap | 右键点击附近人形 Pawn（isImmediate: true） |
| CompRimMindDialogue.CompTick | 每 1000 ticks | 自动触发（冷却控制） |

### 冷却机制

```csharp
// 独白冷却（同类型）
monologueCooldownTicks = 36000;  // 默认 10 游戏小时

// 自动对话冷却
autoDialogueCooldownHours = 6;  // 游戏小时

// 对话过期
dialogueExpireTicks = 60000;

// 游戏开始延迟
startDelayEnabled = true;
startDelaySeconds = 10;
```

## 数据流

```
游戏事件（Chitchat/Hediff/LevelUp/Thought）
    │
    ├── Patch 拦截
    │       ▼
    ├── 检查设置：该类型是否启用？
    │       ▼
    ├── 检查冷却：是否可触发？
    │       ▼
    ├── 构建上下文
    │       ▼
    ├── RimMindDialogueService.HandleTrigger()
    │       ▼
    ├── 构建 Prompt
    │       ▼
    ├── RimMindAPI.RequestAsync()
    │       ▼
    ├── AI 生成回复
    │       ▼
    ├── 解析 JSON 响应
    │       ▼
    ├── 显示对话（MoteMaker.ThrowText）
    │       ▼
    ├── 注入 Thought（ThoughtInjector.Inject / InjectRelationDelta）
    │       ▼
    ├── 推送记忆（MemoryBridge.AddMemory）
    │       ▼
    └── 记录日志（DialogueLogEntry）
```

## 上下文注入

Dialogue 向 Core 注册两个 Provider：

| Provider | 内容 |
|---------|------|
| dialogue_state | 当前活跃的对话 Thought 列表 |
| dialogue_relation | 近期关系变化记录 |

## 代码约定

### 命名空间

- `RimMind.Dialogue` — 顶层（Mod 入口、MemoryBridge、Thought 类）
- `RimMind.Dialogue.Core` — 核心服务
- `RimMind.Dialogue.UI` — 界面
- `RimMind.Dialogue.Overlay` — 悬浮窗
- `RimMind.Dialogue.Comps` — ThingComp
- `RimMind.Dialogue.Patches` — Harmony 补丁
- `RimMind.Dialogue.Settings` — 设置
- `RimMind.Dialogue.Debug` — 调试动作

### JSON 响应格式

```csharp
// 自动对话
public class AutoDialogueResponse
{
    public string? reply;
    public ThoughtPart? thought;
    public float? relation_delta;  // 可选，影响对话对象好感度
}

public class ThoughtPart
{
    public string? tag;          // ENCOURAGED|HURT|VALUED|CONNECTED|STRESSED|IRRITATED|NONE
    public string? description;  // ≤15 字描述
}

// 玩家对话
public class PlayerDialogueResponse
{
    public string? reply;
    public ThoughtPart? thought;
}
```

### DialogueOverlay

`DialogueOverlay` 是 `MapComponent`，通过 Harmony 补丁 `UIRoot_Play.UIRootOnGUI` 每帧绘制。支持拖拽、缩放、位置持久化（保存到 Settings）。

## 扩展指南

### 添加新的触发类型

1. **在 DialogueTriggerType 添加枚举值**
2. **创建新的 Patch 类**
3. **调用 HandleTrigger**

```csharp
[HarmonyPatch(typeof(SomeClass), "SomeMethod")]
public static class MyTriggerPatch
{
    [HarmonyPostfix]
    public static void Postfix(Pawn __instance)
    {
        if (!RimMindDialogueSettings.Get().myTriggerEnabled) return;

        string context = "描述发生了什么";
        RimMindDialogueService.HandleTrigger(
            __instance, context, DialogueTriggerType.MyNewType, null);
    }
}
```

### 添加新的 Thought 标签

1. **在 ThoughtInjector.MapTagToMoodOffset 添加映射**
2. **在 ThoughtInjector.MapTagToLabel 添加标签**
3. **更新 System Prompt 中的标签列表**

### 自定义对话分类

在 DialogueCategory 添加新分类，并在 GetCategory 方法中定义分类逻辑。

## 调试

Dev 菜单（需开启开发模式）→ RimMind-Dialogue：

- **Force Chitchat** — 强制触发闲聊对话
- **Force Hediff Trigger** — 强制触发健康变化对话
- **Show Active Thoughts** — 查看小人的 RimMindDialogue Thought
- **Open Dialogue Window** — 打开玩家对话窗口
- **Open Dialogue Log** — 打开对话日志窗口
- **Toggle Overlay** — 切换屏幕浮窗显示

## 注意事项

1. **线程安全**：所有游戏 API 调用在主线程执行，AI 回调通过延迟机制处理
2. **并发控制**：使用 `_pendingPawns` HashSet 防止同一小人并发请求
3. **RimTalk 兼容**：检测到 RimTalk 模组时自动禁用 Chitchat 拦截
4. **存档安全**：PendingAction 中的 Pawn 引用不序列化，加载后队列自然清空
5. **性能考虑**：自动对话有冷却机制，避免频繁触发 AI 请求
6. **MemoryBridge**：通过反射松耦合调用 RimMindMemory，不产生编译期依赖
