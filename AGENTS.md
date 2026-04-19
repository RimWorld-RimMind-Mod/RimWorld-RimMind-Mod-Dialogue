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
8. **角色约束** — 根据小人身份（囚犯/奴隶/敌人/访客）自动添加语气约束

**依赖关系**：
- 依赖 RimMind-Core 提供的 API（`RimMindAPI`、`StructuredPromptBuilder`、`PromptBudget`）和上下文构建
- 与 RimMind-Personality 协作（人格档案影响对话风格）
- 与 RimMind-Memory 松耦合（通过反射调用 `RimMindMemoryAPI.AddMemory`）

**构建信息**：
- Target: `net48`，LangVersion 9.0，Nullable enable
- Assembly: `RimMindDialogue`，RootNamespace: `RimMind.Dialogue`
- Harmony ID: `"mcocdaa.RimMindDialogueStandalone"`
- 条件编译：默认生成 `V1_6` 宏（`FloatMenuPatch` 使用 `#if V1_5` 适配 1.5 API）
- NuGet: `Krafs.Rimworld.Ref`、`Lib.Harmony.Ref`、`Newtonsoft.Json 13.0.*`
- 本地引用: `RimMindCore.dll`（`../../RimMind-Core/$(GameVersion)/Assemblies/`）

## 源码结构

```
Source/
├── RimMindDialogueMod.cs           Mod 入口，注册 Harmony，初始化设置，注册上下文 Provider
├── Core/
│   ├── RimMindDialogueService.cs   核心服务：事件处理、对话生成、日志管理、并发控制
│   ├── DialoguePromptBuilder.cs    Prompt 构建：使用 StructuredPromptBuilder + 翻译键
│   ├── DialogueService.cs          玩家对话服务（多轮，使用 RequestImmediate）
│   ├── DialogueSession.cs          单个小人的对话会话（历史记录）
│   ├── DialogueSessionManager.cs   会话管理器
│   └── MemoryBridge.cs            跨模组记忆桥接（反射调用 RimMindMemoryAPI）
├── Comps/
│   ├── CompRimMindDialogue.cs      ThingComp：自动对话触发 + Gizmo
│   └── CompProperties_RimMindDialogue.cs
├── UI/
│   ├── Window_Dialogue.cs          玩家对话窗口（聊天界面）
│   ├── Window_DialogueLog.cs       对话日志窗口（分类 + 双栏对话视图）
│   └── DialogueOverlay.cs          MapComponent + 内嵌 Harmony Patch，屏幕浮窗覆盖层
├── Thoughts/
│   ├── Thought_RimMindDialogue.cs  自定义独白 Thought（Thought_Memory 子类）
│   ├── Thought_RelationDialogue.cs 自定义关系 Thought（Thought_MemorySocial 子类）
│   └── ThoughtInjector.cs          Thought 注入工具（独白 + 关系）
├── Patches/
│   ├── BubblePatch.cs              Chitchat/DeepTalk 拦截
│   ├── HediffPatch.cs              健康变化监听
│   ├── SkillLearnPatch.cs          技能升级监听（Prefix 记录旧等级 + Postfix 检测提升）
│   ├── ThoughtPatch.cs             心情变化监听
│   ├── FloatMenuPatch.cs           右键菜单添加对话选项（V1_5/V1_6 条件编译）
│   ├── GameLoadPatch.cs            游戏加载/新游戏初始化
│   └── AddCompToHumanlikePatch.cs  为人形种族自动挂载 Comp
├── Settings/
│   └── RimMindDialogueSettings.cs  模组设置（全部可序列化）
└── Debug/
    └── DialogueDebugActions.cs     Dev 菜单调试动作
```

## 关键类与 API

### RimMindDialogueService

核心服务类（static），处理所有自动触发的对话：

```csharp
static class RimMindDialogueService
{
    // 核心入口
    static void HandleTrigger(
        Pawn pawn,
        string context,
        DialogueTriggerType type,
        Pawn? recipient = null,
        bool isReply = false,
        bool isImmediate = false
    );

    // 显示对话气泡
    static void DisplayInteraction(Pawn initiator, Pawn? recipient, string replyText);

    // 分类
    static DialogueCategory GetCategory(Pawn initiator, Pawn? recipient);

    // 日志
    static void AddPlayerDialogueLog(Pawn pawn, Pawn? recipient, string playerMsg, string reply, string? thoughtTag, string? thoughtDesc);
    static List<DialogueLogEntry> GetDialogueHistory(int pawnId, int recipientId, int maxRounds);
    static int GetDailyDialogueCount(int pawnId, int recipientId);
    static bool IsDialoguePending(int pawnId, int recipientId);

    // 事件
    static event Action? OnLogUpdated;

    // 状态
    static bool IsReady;
    static IReadOnlyList<DialogueLogEntry> LogEntries;
    static void ClearLog();
    static void NotifyGameLoaded();
}
```

**并发控制**：
- `_pendingPawns` HashSet<int> — 防止同一小人并发请求
- `_pendingDialoguePairs` HashSet<(int,int)> — 防止同一对话对并发请求

**冷却机制**：
- `_recentTriggers` List — 按类型记录独白冷却
- `_dailyDialogueCounts` Dictionary — 每日对话计数（按天清理）

### DialoguePromptBuilder

Prompt 构建器（static），使用 `StructuredPromptBuilder` + 翻译键构建结构化 Prompt：

```csharp
static class DialoguePromptBuilder
{
    // 自动对话 System Prompt（七段式：Role/Goal/Process/Constraint/Example/Output/Fallback）
    static string BuildAutoSystemPrompt(Pawn pawn, string triggerLabel, Pawn? recipient, string? customPrompt);

    // 玩家对话 System Prompt（八段式：同上 + InitiatorConstraint）
    static string BuildPlayerSystemPrompt(Pawn pawn, Pawn? initiator, string? customPrompt);

    // 自动对话 User Prompt（使用 PromptBudget(4000, 400) + RimMindAPI.BuildFullPawnSections）
    static string BuildAutoUserPrompt(Pawn pawn, string context, DialogueTriggerType type, Pawn? recipient);

    // 玩家对话 User Prompt
    static string BuildPlayerUserPrompt(Pawn pawn, Pawn? initiator);

    // 角色约束（Prisoner/Slave/Enemy/Visitor）
    static string GetRoleConstraint(Pawn pawn);
}
```

### DialogueService

玩家主动对话服务（static）：

```csharp
static class DialogueService
{
    static void RequestReply(
        DialogueSession session,
        string playerMessage,
        Pawn? initiator,        // 玩家 Pawn（用于构建 InitiatorConstraint）
        Action<string> onReply,
        Action<string> onError
    );
    // 内部使用 RimMindAPI.RequestImmediate（非异步队列，立即请求）
}
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
```

注意：`DialogueSession.MaxHistoryRounds` 是玩家对话会话的硬编码历史轮数限制，与设置项 `maxDailyDialogueRounds`（每日对话限制）无关。

static class DialogueSessionManager
{
    static DialogueSession GetOrCreate(Pawn pawn);
    static void Clear(Pawn pawn);
    static void ClearAll();
}
```

### MemoryBridge

跨模组记忆桥接（internal static），通过反射调用 `RimMindMemoryAPI.AddMemory`：

```csharp
internal static class MemoryBridge
{
    static void AddMemory(string content, string memoryType, int tick, float importance, string? pawnId = null);
}
// 反射目标：RimMind.Memory.RimMindMemoryAPI, RimMindMemory 程序集
// 首次调用时 Resolve，失败则静默跳过
```

### ThoughtInjector

将对话产生的心理影响注入游戏（static）：

```csharp
static class ThoughtInjector
{
    static string ThoughtDefName = "RimMindDialogue_Thought";
    static string RelationThoughtDefName = "RimMindDialogue_RelationThought";

    // 注入独白 Thought
    static void Inject(Pawn pawn, Pawn? recipient, string tag, string? description);

    // 注入关系 Thought（影响好感度）
    static void InjectRelationDelta(Pawn pawn, Pawn recipient, float delta);
    // delta clamp 在 [-5, +5]，映射为 opinionOffset

    // Thought 标签与心情值映射
    // ENCOURAGED  → +1  受到鼓励
    // HURT        → -1  感到受伤
    // VALUED      → +2  感到被重视
    // CONNECTED   → +2  感到亲近
    // STRESSED    → -2  感到压力
    // IRRITATED   → -1  感到烦躁
}
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
    bool IsMonologue;          // recipientId == 0
    string PairKey;            // 名字排序拼接的对话配对标识
    string TimeStr;            // 格式化时间
}
```

### CompRimMindDialogue

ThingComp，挂载到所有人形 Pawn：

```csharp
public class CompRimMindDialogue : ThingComp
{
    int _lastTriggerTick = -99999;  // 序列化到存档
    Pawn Pawn => (Pawn)parent;

    override void CompTick();                  // 每 1000 ticks 检查自动对话
    override IEnumerable<Gizmo> CompGetGizmosExtra();  // Gizmo 按钮
    override void PostExposeData();            // 存档序列化
    bool IsEligible();                         // 自由殖民者、未倒地、未征召等
}
```

## AI Prompt 结构

Prompt 通过 `DialoguePromptBuilder` 构建，文本全部定义在翻译键 XML 中（`Languages/*/Keyed/RimMind_Dialogue.xml`），支持多语言。

### 自动对话 Prompt（翻译键前缀 `RimMind.Dialogue.Prompt.System`）

七段式结构，由 `StructuredPromptBuilder.FromKeyPrefix` 自动拼接：

| 段落 | 翻译键 | 内容 |
|------|--------|------|
| Role | `.Role` | 角色定义：扮演 RimWorld 殖民者 |
| Goal | `.GoalMonologue` / `.GoalDialogue` | 目标：独白或对话（根据 recipient 是否为 null 选择） |
| Process | `.Process` | 流程：感知状态→结合触发原因→生成对话→评估影响 |
| Constraint | `.Constraint` | 约束：角色一致性、不打破第四面墙、字数限制 |
| Example | `.Example` | 示例：独白和对话各一个 |
| Output | `.Output` | 输出格式：JSON 模板 |
| Fallback | `.Fallback` | 兜底：无法生成时的默认响应 |

额外段落（按条件追加）：
- `.TriggerReason` — 触发原因描述
- `.RelationDelta` — relation_delta 使用说明（仅对话时追加）
- `.RecipientRole` — 对话对象角色约束（Prisoner/Slave/Enemy/Visitor）
- `.JsonTemplate` — JSON 模板（调试用）

User Prompt 由 `BuildAutoUserPrompt` 构建，使用 `PromptBudget(4000, 400)` + `RimMindAPI.BuildFullPawnSections` 生成上下文，追加触发原因和对话历史。

### 玩家对话 Prompt（翻译键前缀 `RimMind.Dialogue.Prompt.PlayerSystem`）

八段式结构（同上 + InitiatorConstraint）：

| 段落 | 翻译键 | 内容 |
|------|--------|------|
| Role | `.Role` | 角色定义：扮演殖民者 {name} |
| Goal | `.Goal` | 目标：回应玩家对话 |
| Process | `.Process` | 流程 |
| Constraint | `.Constraint` | 约束：20~60 字 |
| Example | `.Example` | 示例 |
| Output | `.Output` | 输出格式（无 relation_delta） |
| Fallback | `.Fallback` | 兜底 |
| InitiatorConstraint | `.InitiatorConstraint` | 对话发起者约束（{0} 为发起者名字） |

### 角色约束（翻译键前缀 `RimMind.Dialogue.Prompt.Role`）

| 角色 | 翻译键 | 语气 |
|------|--------|------|
| 囚犯 | `.Prisoner` | 警惕、犹豫、乞求 |
| 奴隶 | `.Slave` | 恐惧、顺从、称"主人" |
| 敌人 | `.Enemy` | 敌对、攻击性、威胁 |
| 访客 | `.Visitor` | 礼貌、好奇、恭敬 |

### 触发原因上下文（翻译键前缀 `RimMind.Dialogue.Prompt.Context`）

| 触发类型 | 翻译键 | 格式 |
|----------|--------|------|
| Chitchat | `.Chitchat` | [触发原因: 闲聊] {context} |
| Hediff | `.Hediff` | [触发原因: 健康变化] {context} |
| LevelUp | `.LevelUp` | [触发原因: 技能升级] {context} |
| Thought | `.Thought` | [触发原因: 情绪变化] {context} |
| Auto | `.Auto` | [触发原因: 自动触发] {context} |
| 对话对象 | `.Recipient` | [对象] {recipientName} |
| 对话历史 | `.DialogueHistory` | [之前的对话记录] |
| 历史压缩 | `.HistoryCompressed` | （更早的对话已省略） |

## 事件触发机制

### Patch 列表

| Patch | Harmony 目标 | 触发条件 | 备注 |
|-------|-------------|----------|------|
| BubblePatch | `Pawn_InteractionsTracker.TryInteractWith` | Chitchat/DeepTalk 成功 | 检测 RimTalk 兼容 |
| HediffPatch | `Pawn_HealthTracker.AddHediff` | 添加显著 Hediff（isBad/tendable/makesSickThought） | |
| SkillLearnPatch | `SkillRecord.Learn` | 技能等级提升 | Prefix 记旧等级，Postfix 检测提升 |
| ThoughtPatch | `MemoryThoughtHandler.TryGainMemory` | 心情变化绝对值 >= 阈值 | |
| FloatMenuPatch | `FloatMenuMakerMap.ChoicesAtFor`(V1_5) / `.GetOptions`(V1_6) | 右键点击附近人形 Pawn | isImmediate: true，条件编译 |
| GameLoadPatch | `GameComponentUtility.LoadedGame` / `Game.InitNewGame` | 游戏加载/新游戏 | 调用 NotifyGameLoaded |
| AddCompToHumanlikePatch | `ThingDef.ResolveReferences` | 人形种族 Def 解析 | 自动添加 CompProperties_RimMindDialogue |
| CompRimMindDialogue.CompTick | 每 1000 ticks | 自动触发 | 冷却控制 |

### 冷却与限制机制

```csharp
// 独白冷却（同类型）
monologueCooldownTicks = 36000;       // 默认 10 游戏小时

// 自动对话冷却
autoDialogueCooldownHours = 12;       // 游戏小时（×2500 = ticks）

// 每日对话限制
maxDailyDialogueRounds = 6;           // 每对殖民者每天最大对话轮数

// 对话过期
monologueExpireTicks = 15000;         // 独白 AI 请求过期
dialogueExpireTicks = 60000;          // 对话 AI 请求过期

// 游戏开始延迟
startDelayEnabled = true;
startDelaySeconds = 10;               // ×60 = ticks

// 对话上下文
dialogueContextRounds = 5;            // 自动对话携带的历史轮数（-1=全部）

// 对话回复
enableDialogueReply = true;           // 收到对话后自动生成回复

// 心情变化阈值
moodChangeThreshold = 3f;             // ThoughtPatch 触发阈值
```

## 数据流

```
游戏事件（Chitchat/Hediff/LevelUp/Thought）
    │
    ├── Patch 拦截
    │       ▼
    ├── 检查设置：该类型是否启用？
    │       ▼
    ├── 检查冷却：是否可触发？（_recentTriggers / _dailyDialogueCounts）
    │       ▼
    ├── 检查并发：_pendingPawns / _pendingDialoguePairs
    │       ▼
    ├── RimMindDialogueService.HandleTrigger()
    │       ▼
    ├── DialoguePromptBuilder.BuildAutoSystemPrompt() / BuildAutoUserPrompt()
    │       ▼
    ├── RimMindAPI.RequestAsync()（自动对话）/ RequestImmediate()（玩家对话）
    │       ▼
    ├── AI 生成回复
    │       ▼
    ├── 解析 JSON 响应（AutoDialogueResponse / PlayerDialogueResponse）
    │       ▼
    ├── DisplayInteraction()（MoteMaker.ThrowText 显示气泡）
    │       ▼
    ├── ThoughtInjector.Inject() / InjectRelationDelta()
    │       ▼
    ├── MemoryBridge.AddMemory()（推送记忆）
    │       ▼
    └── 记录日志（DialogueLogEntry，上限 500 条）
```

## Def 定义

### ThoughtDef: RimMindDialogue_Thought

- thoughtClass: `Thought_RimMindDialogue`
- durationDays: 0.04（约 1 游戏小时）
- stackLimit: 1
- baseMoodEffect: 0（由代码动态设置 aiMoodOffset）

### ThoughtDef: RimMindDialogue_RelationThought

- thoughtClass: `Thought_RelationDialogue`（Thought_MemorySocial 子类）
- durationDays: 10
- stackLimit: 10
- lerpOpinionToZeroAfterDurationPct: 0.7
- baseMoodEffect: 0, baseOpinionOffset: 0（由代码动态设置）

### InteractionDef: RimMindDialogue_AutoInteraction

- label: "AI Dialogue"
- logRulesInitiator: "PAWN_initiator had an AI dialogue."
- 注意：当前代码未引用此 InteractionDef

## 上下文注入

Dialogue 在 Mod 初始化时向 Core 注册两个 Pawn 上下文 Provider：

| Provider Key | 内容 |
|-------------|------|
| `dialogue_state` | 当前活跃的对话 Thought 列表 |
| `dialogue_relation` | 近期关系变化记录 |

## UI 层

### Window_Dialogue

聊天窗口，初始大小 480×540。支持滚动消息列表、输入框、发送按钮。消息按 User/Pawn 分色显示。

### Window_DialogueLog

对话日志窗口，初始大小 720×560。三栏布局：分类栏→标签列表→内容区。支持 5 种分类过滤，独白为列表视图，对话为双栏视图。

### DialogueOverlay

MapComponent，通过内嵌 Harmony Patch（`DialogueOverlayPatch`）在 `UIRoot_Play.UIRootOnGUI` 隔帧绘制。支持拖拽、缩放、位置持久化（保存到 Settings）。分类颜色映射区分独白/对话/玩家对话。

## 代码约定

### 命名空间

| 目录 | 命名空间 | 说明 |
|------|----------|------|
| Source/ | `RimMind.Dialogue` | Mod 入口、MemoryBridge、Thought 类 |
| Source/Core/ | `RimMind.Dialogue.Core` | 核心服务 |
| Source/UI/ | `RimMind.Dialogue.UI` | 窗口界面 |
| Source/UI/ | `RimMind.Dialogue.Overlay` | DialogueOverlay（与目录不对应） |
| Source/Comps/ | `RimMind.Dialogue.Comps` | ThingComp |
| Source/Patches/ | `RimMind.Dialogue.Patches` | Harmony 补丁 |
| Source/Settings/ | `RimMind.Dialogue.Settings` | 设置 |
| Source/Debug/ | `RimMind.Dialogue.Debug` | 调试动作 |

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
    // 无 relation_delta
}
```

### 全部静态服务

`RimMindDialogueService`、`DialogueService`、`DialogueSessionManager`、`DialoguePromptBuilder`、`ThoughtInjector`、`MemoryBridge` 均为 static 类，无依赖注入，全局唯一。

### 翻译键约定

所有用户可见文本和 Prompt 文本均通过翻译键定义在 `Languages/*/Keyed/RimMind_Dialogue.xml` 中。Prompt 翻译键使用 `RimMind.Dialogue.Prompt.{Type}.{Section}` 格式，由 `StructuredPromptBuilder.FromKeyPrefix` 自动拼接。

## 扩展指南

### 添加新的触发类型

1. **在 DialogueTriggerType 添加枚举值**
2. **创建新的 Patch 类**（在 `Patches/` 目录，命名空间 `RimMind.Dialogue.Patches`）
3. **在 RimMindDialogueSettings 添加开关字段**
4. **在翻译键 XML 添加 `RimMind.Dialogue.Prompt.Context.{Type}` 条目**
5. **在 DialoguePromptBuilder.BuildAutoUserPrompt 添加触发类型映射**
6. **调用 HandleTrigger**

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
3. **更新翻译键 XML 中所有 Prompt 的 tag 列表**（`.Output`、`.Example`、`.JsonTemplate`）
4. **更新 `RimMind.Dialogue.Prompt.System.Output` 和 `.PlayerSystem.Output` 翻译键**

### 自定义对话分类

在 DialogueCategory 添加新分类，在 GetCategory 方法中定义分类逻辑，在 Window_DialogueLog 的 CategoryKeys 中添加翻译键。

### 添加新的角色约束

1. **在 DialoguePromptBuilder.GetRoleConstraint 添加角色判断**
2. **在翻译键 XML 添加 `RimMind.Dialogue.Prompt.Role.{Role}` 条目**

## 调试

Dev 菜单（需开启开发模式）→ RimMind-Dialogue：

- **Force Chitchat** — 强制触发闲聊对话（选中 + 目标）
- **Force Hediff Trigger** — 强制触发健康变化对话（选中）
- **Show Active Thoughts** — 查看小人的 RimMindDialogue Thought
- **Open Dialogue Window** — 打开玩家对话窗口（选中）
- **Open Dialogue Log** — 打开对话日志窗口
- **Toggle Overlay** — 切换屏幕浮窗显示

## 注意事项

1. **线程安全**：所有游戏 API 调用在主线程执行，AI 回调通过 `LongEventHandler.ExecuteWhenEventAvailable` 延迟处理
2. **并发控制**：`_pendingPawns` + `_pendingDialoguePairs` 双 HashSet 防止同一小人/对话对并发请求；全局并发由 RimMind-Core 的 `maxConcurrentRequests` 控制
3. **RimTalk 兼容**：检测到 RimTalk 模组时自动禁用 Chitchat 拦截
4. **存档安全**：`_pendingPawns`、`_logEntries`、`_recentTriggers` 等运行时状态不序列化；仅 CompRimMindDialogue._lastTriggerTick 通过 PostExposeData 序列化
5. **性能考虑**：自动对话有冷却机制 + 每日计数限制，避免频繁触发 AI 请求；Overlay 隔帧绘制
6. **MemoryBridge**：通过反射松耦合调用 RimMindMemory，不产生编译期依赖；反射失败时静默跳过
7. **版本兼容**：FloatMenuPatch 使用 `#if V1_5` / `#else` 条件编译适配 1.5/1.6 API 差异
8. **日志上限**：_logEntries 上限 500 条，超出时移除最早条目
9. **代码重复**：`RimMindDialogueService.GetRoleConstraint()` 和 `DialoguePromptBuilder.GetRoleConstraint()` 逻辑相同，修改时需同步
