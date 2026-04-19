# RimMind - Dialogue

让殖民者开口说话，为游戏事件注入生动的 AI 生成对话。

## 核心能力

**事件响应对话** - 当殖民者受伤、技能升级、心情显著变化时，自动生成符合情境的内心独白或对话。拦截原生 Chitchat/DeepTalk 交互，替换为 AI 生成的对话。

**玩家主动对话** - 通过殖民者身上的 Gizmo 按钮或右键菜单发起对话，支持多轮聊天，了解他们的想法和感受。

**对话回复链** - A 对话后自动触发 B 的回复，形成双向对话，让社交互动自然流动。

**对话上下文记忆** - 自动对话携带历史上下文（默认最近 5 轮），玩家对话保留会话历史（默认最近 6 轮），让对话有连贯性。

**思想注入系统** - AI 生成的对话以 Thought 形式注入游戏，影响殖民者心情；对话还可改变小人间好感度，让对话有实际游戏效果。

**角色约束** - 根据小人身份（囚犯/奴隶/敌人/访客）自动添加语气约束，让不同角色的对话风格各异。

**对话浮窗** - 屏幕角落实时显示最近对话，支持拖拽移动和缩放，不错过任何精彩瞬间。

**对话日志** - 分类查看所有对话记录（殖民者独白/对话、非殖民者独白/对话、玩家对话），对话以双栏视图展示。

## 触发场景

| 触发来源 | 说明 | 类型 |
|----------|------|------|
| 社交互动 | 拦截原生 Chitchat/DeepTalk | 对话（有对象） |
| 健康变化 | 受伤或患病时 | 独白 |
| 技能升级 | 技能等级提升时 | 独白 |
| 心情波动 | 心情变化超过阈值时 | 独白 |
| 空闲自动 | 空闲时按间隔触发 | 独白 |
| 玩家主动 | Gizmo 按钮或右键菜单 | 对话（有对象） |

## 设置项

| 设置 | 默认值 | 说明 |
|------|--------|------|
| 启用对话系统 | 开启 | 总开关 |
| 拦截原生闲聊 | 开启 | 将 Chitchat/DeepTalk 替换为 AI 对话 |
| 健康变化反应 | 开启 | 受伤/生病时触发独白 |
| 技能升级反应 | 开启 | 技能提升时触发独白 |
| 心情变化反应 | 开启 | 心情波动超过阈值时触发独白 |
| 心情变化阈值 | 3 | 触发独白的最小心情变化绝对值 |
| 空闲自动独白 | 开启 | 空闲时按间隔触发独白 |
| 空闲自动独白间隔 | 12 游戏小时 | 自动独白的最小间隔 |
| 玩家主动对话 | 开启 | Gizmo 按钮 + 右键菜单对话选项 |
| 独白冷却 | 10 游戏小时 | 同一小人同类型独白的最小间隔 |
| 每日每对最大对话轮数 | 6 | 每对殖民者每天最多对话轮数 |
| 对话上下文轮数 | 5 | 发送给 AI 的历史对话轮数（-1=全部） |
| 启用对话回复 | 开启 | 收到对话后自动生成回复 |
| 游戏开始延迟 | 10 秒 | 加载存档后暂不触发对话 |
| 自定义对话 Prompt | 空 | 追加在系统 Prompt 末尾 |
| 注入 Thought 时显示通知 | 关闭 | 注入心情 Thought 时屏幕通知 |
| 显示对话浮窗 | 开启 | 屏幕角落实时对话 |
| 浮窗透明度 | 75% | 浮窗不透明度 |
| 浮窗最大消息数 | 8 | 浮窗同时显示的最大对话条数 |
| 独白请求过期 | 0.25 游戏天 | 独白请求超时自动取消 |
| 对话请求过期 | 1 游戏天 | 对话请求超时自动取消 |

## 建议配图

1. 对话窗口截图（展示玩家与殖民者的聊天）
2. 游戏内对话浮窗截图
3. 思想面板中 AI 生成 Thought 的展示

---

# RimMind - Dialogue (English)

Make your colonists speak, injecting vivid AI-generated dialogue into game events.

## Key Features

**Event-Responsive Dialogue** - Automatically generates context-appropriate inner monologues or conversations when colonists are injured, level up skills, or experience significant mood changes. Intercepts vanilla Chitchat/DeepTalk interactions with AI-generated dialogue.

**Player-Initiated Dialogue** - Start conversations via Gizmo button or right-click context menu. Supports multi-turn chat to learn colonists' thoughts and feelings.

**Dialogue Reply Chain** - After A speaks, B automatically responds, creating two-way conversations that flow naturally.

**Dialogue Context Memory** - Auto-dialogue carries history context (default: last 5 rounds); player dialogue retains session history (default: last 6 rounds), ensuring coherent conversations.

**Thought Injection System** - AI-generated dialogue is injected as Thoughts, affecting colonist mood. Dialogue can also change opinion between pawns, giving dialogue actual gameplay impact.

**Role Constraints** - Automatically adds tone constraints based on pawn identity (Prisoner/Slave/Enemy/Visitor), making different characters speak in distinct styles.

**Dialogue Overlay** - Real-time dialogue display in screen corner. Supports drag-to-move and resize. Never miss a moment.

**Dialogue Log** - Categorized view of all dialogue records (Colonist Monologue/Dialogue, Non-Colonist Monologue/Dialogue, Player Dialogue). Dialogues displayed in dual-column view.

## Trigger Scenarios

| Trigger | Description | Type |
|---------|-------------|------|
| Social Interaction | Intercepts Chitchat/DeepTalk | Dialogue (with recipient) |
| Health Change | When injured or ill | Monologue |
| Skill Level Up | When skill level increases | Monologue |
| Mood Change | When mood change exceeds threshold | Monologue |
| Auto on Idle | Triggered at intervals when idle | Monologue |
| Player Initiated | Gizmo button or right-click menu | Dialogue (with recipient) |

## Suggested Screenshots

1. Dialogue window (showing player chatting with colonist)
2. In-game dialogue overlay
3. Thought panel showing AI-generated thoughts
