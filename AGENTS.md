# AGENTS.md — RimMind-Dialogue

AI对话系统，拦截游戏事件生成上下文对话，注入Thought，支持玩家主动多轮对话。

## Start here

对话生命周期先读 `Source/Core/README.md`。通常只需继续打开请求协调器、活动状态、日志存储或响应处理器中的一个，不要从所有 Patch 开始搜索。

## 项目定位

通过Harmony Patch监听Chitchat/Hediff/技能升级/心情变化 → `RimMindDialogueService.HandleTrigger` → `DialogueRequestCoordinator` → `RimMindAPI.Request.Send` → `NpcResponseHandler.Handle` 解析JSON响应(reply/thought/relation_delta) → `ThoughtInjector` 注入独白/关系Thought。请求、活动状态、日志和响应副作用各有单一入口。

依赖: Core(编译期)。可选 Memory 集成使用 Core 公共 `RimMindAPI.Memory`，不引用 Memory 实现程序集。

## 构建

| 项 | 值 |
|----|-----|
| Target | net48, C#9.0, Nullable enable |
| Output | `../1.6/Assemblies/` |
| Assembly | RimMindDialogue, RootNS: RimMind.Dialogue |
| Harmony ID | mcocdaa.RimMindDialogueStandalone |
| 依赖 | RimMindCore.dll, Krafs.Rimworld.Ref, Lib.Harmony.Ref, Newtonsoft.Json |

## 源码结构

```
Source/
├── RimMindDialogueMod.cs            组合入口、Harmony与Core扩展注册
├── Core/
│   ├── DialogueContextProviderRegistrar.cs  三个Core上下文Provider的唯一注册入口
│   ├── README.md                     对话生命周期入口地图
│   ├── RimMindDialogueService.cs    公共兼容门面与轻量事件边界
│   ├── DialogueRequestCoordinator.cs 请求门控、派发与完成清理
│   ├── DialogueActivityState.cs     冷却、配额、接收者与Pawn查询
│   ├── DialogueLogStore.cs          有界日志与只读快照
│   ├── DialogueService.cs           玩家兼容入口，直接进入同一请求协调器
│   ├── NpcResponseHandler.cs        统一响应处理(自动+玩家对话共用,含感知发布)
├── Comps/CompRimMindDialogue.cs     ThingComp(非殖民者首行return, 1000tick检查)
├── UI/Window_Dialogue.cs / Window_DialogueLog.cs / DialogueOverlay.cs
├── Thoughts/ThoughtInjector.cs + Thought_RimMindDialogue.cs + Thought_RelationDialogue.cs
├── Patches/                         7个Patch(Bubble/Hediff/SkillLearn/Thought/FloatMenu/GameLoad/AddComp)
├── Debug/DialogueDebugActions.cs    调试操作(Force触发/状态查看/冷却清除)
└── Settings/RimMindDialogueSettings.cs
```

## 统一请求生命周期

自动触发通过 `RimMindDialogueService.HandleTrigger`，玩家输入通过 `DialogueService.RequestReply`，两者直接进入同一个 `DialogueRequestCoordinator`：总开关（玩家另查玩家开关）→ 参与者有效性 → API配置 → IsReady → Pawn pending → ShouldSkipDialogue → 自动冷却/配额策略 → 原子 Pawn/Pair/全局容量预约 → 唯一 `RimMindAPI.Request.Send`。

玩家多轮输入不消耗自动每日配额或独白冷却；自动 A-B 回复仍连续进行，由独立 pair reply limiter 限制。

入口、窗口取消与游戏重置均在主线程调用。Core 已在主线程交付完成回调，不再通过 `LongEventHandler` 重复调度。拒绝、同步抛错、失败、取消均结束玩家等待；关闭窗口或游戏重置会取消在途请求，旧/重复回调不执行响应副作用。清理先释放预约再调用 `NpcResponseHandler`，允许下一轮 A-B 回复获取同一 pair。

## Thought标签与心情映射

| 标签 | 心情 | 翻译键 | 说明 |
|------|------|--------|------|
| ENCOURAGED | +1 | RimMind.Dialogue.Thought.ENCOURAGED | 受到鼓励 |
| HURT | -1 | RimMind.Dialogue.Thought.HURT | 感到受伤 |
| VALUED | +2 | RimMind.Dialogue.Thought.VALUED | 感到被重视 |
| CONNECTED | +2 | RimMind.Dialogue.Thought.CONNECTED | 感到亲近 |
| STRESSED | -2 | RimMind.Dialogue.Thought.STRESSED | 感到压力 |
| IRRITATED | -1 | RimMind.Dialogue.Thought.IRRITATED | 感到烦躁 |

外部mod可通过 `ThoughtInjector.RegisterThoughtTag(tag, moodOffset, labelKey)` 注册自定义标签。

## 上下文注入

- `dialogue_state` / `dialogue_relation`: ContextKeyRegistry(L3_State)
- `dialogue_task`: ContextKeyRegistry(L0_Static, 仅ScenarioIds.Dialogue, CurrentSpeakerName为空时触发 → 当前仅独白)

> 注：原 `player_dialogue_task` provider 已于 2026-07-08 审查删除——Core 的 `ContextOrchestrator` 在所有 `BuildContext` 构造路径中将 `SpeakerName` 硬编码为 null，该 provider 触发条件（SpeakerName 非空）永不满足，属不可达死代码。归档于 `Refs/backup/RimMind-Dialogue/RimMindDialogueMod.player_dialogue_task.cs`。

## 公共API（供其他mod调用）

| API | 说明 |
|-----|------|
| `RimMindDialogueService.OnDialogueCompleted` | 对话完成事件 `(Pawn, Pawn?, string, string?)` |
| `RimMindDialogueService.GetDialogueHistory(pawnId, maxCount)` | 查询指定小人的对话历史 |
| `RimMindDialogueService.RegisterTriggerType(typeId, labelKey)` | 注册自定义触发类型标签 |
| `ThoughtInjector.RegisterThoughtTag(tag, moodOffset, labelKey)` | 注册自定义Thought标签 |

## 代码约定

- 全部静态服务，全局唯一
- 翻译键前缀: `RimMind.Dialogue.*`
- 翻译键大小写: Thought标签翻译键使用全大写（如 `RimMind.Dialogue.Thought.ENCOURAGED`），与XML保持一致
- Memory 写入只走 Core 公共 `RimMindAPI.Memory`；可选模块未启用时跳过，不新增反射链
- 日志上限500条(ConcurrentBag + 脏标记缓存)
- `isMonologue` 判断: `recipient == null && type != PlayerInput`（`HandleTrigger` 与 `NpcResponseHandler.Handle` 统一使用此公式）
- `reply` 触发: `TryTriggerReply` 用 `isReply: true` 绕过每日限额检查，`NpcResponseHandler.Handle` 对 `isReply=true` 不调用 `RecordDailyDialogue`（reply 是对话链的自然延续，不额外消耗每日额度）
- `ThoughtInjector.MoodOffsetMap`/`LabelMap` 和 `RimMindDialogueService.RegisteredTriggerLabels` 使用 `ConcurrentDictionary` 保证外部 mod 并发注册安全

## 操作边界

### ✅ 必须做
- 新触发类型在 `DialogueTriggerType` 添加值 + `GetTriggerLabel` 映射 + `RegisterTriggerType` 注册标签
- 新Thought标签通过 `ThoughtInjector.RegisterThoughtTag` 注册（勿直接修改MoodOffsetMap/LabelMap）
- AI响应通过 `NpcResponseHandler.Handle` 统一处理
- 翻译键大小写必须与XML一致

### ⚠️ 先询问
- 修改并发控制(`_pendingPawns`/`_pendingDialoguePairs`)
- 修改冷却机制
- 新增对RimTalk/RimChat直接编译期依赖
- 修改 `dialogue_task` provider 的触发条件

### 🚫 绝对禁止
- 对 Memory/Actions 实现程序集新增编译期引用；访问 Core Internal
- 后台线程调用 `MoteMaker.ThrowText`/`ThoughtInjector.Inject`
- Gizmo按钮对话忽略 `initiator` 参数(导致玩家对话被当作独白)
- LabelMap翻译键使用与XML不一致的大小写
