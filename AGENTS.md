# AGENTS.md — RimMind-Dialogue

AI对话系统，拦截游戏事件生成上下文对话，注入Thought，支持玩家主动多轮对话。

## 项目定位

通过Harmony Patch监听Chitchat/Hediff/技能升级/心情变化 → `RimMindDialogueService.HandleTrigger` → `RimMindAPI.Chat` → `NpcResponseHandler.Handle` 解析命令(express_emotion/change_relationship) → `ThoughtInjector` 注入独白/关系Thought → `MemoryBridge`(反射松耦合)推送记忆。含并发控制(ConcurrentDictionary)、独立冷却(独白/每日对话)、玩家对话Window、对话日志。

依赖: Core(编译期)，Memory/Actions(反射松耦合)。

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
├── RimMindDialogueMod.cs            Mod入口
├── Core/
│   ├── RimMindDialogueService.cs    核心服务(事件处理/并发控制/日志)
│   ├── DialogueService.cs           玩家对话服务(RimMindAPI.Chat)
│   ├── NpcResponseHandler.cs        统一响应处理(自动+玩家对话共用)
│   └── MemoryBridge.cs              反射桥接RimMindMemoryAPI
├── Comps/CompRimMindDialogue.cs     ThingComp(非殖民者首行return, 1000tick检查)
├── UI/Window_Dialogue.cs / Window_DialogueLog.cs / DialogueOverlay.cs
├── Thoughts/ThoughtInjector.cs + Thought_RimMindDialogue.cs + Thought_RelationDialogue.cs
├── Patches/                         7个Patch(Bubble/Hediff/SkillLearn/Thought/FloatMenu/GameLoad/AddComp)
└── Settings/RimMindDialogueSettings.cs
```

## HandleTrigger 检查流程

1. 总开关 → 2. API配置 → 3. IsReady → 4. _pendingPawns并发(同一小人) → 5. ShouldSkipDialogue → 6. _pendingDialoguePairs并发(同一对话对) → 7. 独白冷却 → 8. 每日对话限制 → 发送RimMindAPI.Chat

回调通过 `LongEventHandler.ExecuteWhenFinished` 调度到主线程。

## Thought标签与心情映射

| 标签 | 心情 | 说明 |
|------|------|------|
| ENCOURAGED | +1 | 受到鼓励 |
| HURT | -1 | 感到受伤 |
| VALUED | +2 | 感到被重视 |
| CONNECTED | +2 | 感到亲近 |
| STRESSED | -2 | 感到压力 |
| IRRITATED | -1 | 感到烦躁 |

## 上下文注入

- `dialogue_state` / `dialogue_relation`: PawnContextProvider
- `dialogue_task` / `player_dialogue_task`: ContextKeyRegistry(仅ScenarioIds.Dialogue)

## 代码约定

- 全部静态服务，全局唯一
- 翻译键前缀: `RimMind.Dialogue.*`
- Memory/Actions调用必须反射松耦合(检查 `ModsConfig.IsActive` 先)
- 日志上限500条(ConcurrentBag)

## 操作边界

### ✅ 必须做
- 新触发类型在 `DialogueTriggerType` 添加值 + `GetTriggerLabel`/`GetCategory` 映射
- 新Thought标签在 `ThoughtInjector.MapTagToMoodOffset` 添加映射
- AI响应通过 `NpcResponseHandler.Handle` 统一处理

### ⚠️ 先询问
- 修改并发控制(`_pendingPawns`/`_pendingDialoguePairs`)
- 修改冷却机制
- 新增对RimTalk/RimChat直接编译期依赖

### 🚫 绝对禁止
- MemoryBridge/跨模组调用使用编译期引用
- 后台线程调用 `MoteMaker.ThrowText`/`ThoughtInjector.Inject`
- Gizmo按钮对话忽略 `initiator` 参数(导致玩家对话被当作独白)
