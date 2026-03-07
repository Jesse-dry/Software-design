# 共识协议 (Consensus Protocol)
## 项目结构总览（已重构主菜单相关）

```
Assets/
├── Scripts/                    # 游戏核心脚本
│   ├── Core/                   # 核心框架层（Bootstrapper / GameManager / SceneController）
│   ├── Gameplay/               # 游戏玩法层（Player / Interaction / Memory 等）
│   ├── Data/                   # 数据定义层
│   ├── UI/                     # UI 交互层（新：EyeTracker / HoverRevealButton / MainMenuController）
│   ├── Animations/             # 动画 & 场景过渡层（AsyncSceneLoader 等）
│   ├── 接水管/                  # 接水管小游戏
│   └── CameraFollow.cs         # 相机跟随
│
├── LLMModule/                  # LLM 文本生成模块（独立）
│   ├── Data/                   # 章节策划配置
│   └── Example/                # 调用示例
│
├── Resources/                  # Unity 可加载资源（Config）
│   └── Config/
│       └── GameBootConfig.asset
│
├── Scenes/                     # 场景文件（含 MainMenu.Unity）
├── Prefabs/                    # 预制体（UISceneRoot Prefab 等）
├── Art/                        # 美术资源（主菜单素材位于 Art/UI/主菜单）
├── Audio/                      # 音频资源
├── Animations/                 # 动画资源
├── Materials/                  # 材质
└── Video/                      # 视频资源
```
│
├── LLMModule/                  # LLM 文本生成模块（独立）
│   ├── Data/                   # 章节策划配置
│   └── Example/                # 调用示例
│
├── Resources/                  # Unity 可加载资源
│   └── Config/
│       └── GameBootConfig.asset
│
├── Scenes/                     # 场景文件
├── Prefabs/                    # 预制体
├── Art/                        # 美术资源
├── Audio/                      # 音频资源
├── Animations/                 # 动画资源
├── Materials/                  # 材质
└── Video/                      # 视频资源
```

---

## 模块详细说明

### 1. Core — 核心框架层

> 路径：`Assets/Scripts/Core/`

| 文件 | 类型 | 职责 |
|------|------|------|
| `GameBootstrapper.cs` | 静态类 | **自动启动器**。通过 `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 在任何场景加载前自动创建所有 Manager，无需手动挂载 |
| `GameBootConfig.cs` | ScriptableObject | **启动配置**。定义各场景名称、起始阶段、调试开关。路径：`Resources/Config/GameBootConfig.asset` |
| `GamePhase.cs` | 枚举 | 定义游戏阶段：`Boot` / `MainMenu` / `Cutscene` / `Memory` / `Abyss` / `Court` / `Result` |
| `GameManager.cs` | 单例 MonoBehaviour | **游戏状态机**。管理阶段切换，提供 `OnPhaseChanged` 全局事件，所有阶段切换必须通过 `EnterPhase()` |
| `SceneController.cs` | 单例 MonoBehaviour | **场景控制器**。封装异步场景加载，防重复加载，支持过渡扩展 |
| `DataManager.cs` | 单例 MonoBehaviour | **数据管理器**。管理证据解锁状态、庭审话题说服值计算 |
| `MemoryNodeBase.cs` | MonoBehaviour 基类 | 记忆节点抽象基类，定义 `Interact()` 虚方法 |
| `MemoryUIManager.cs` | 单例 MonoBehaviour | 记忆面板 UI 管理，控制面板显示/隐藏和玩家冻结 |

#### 自动初始化流程

```
[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]
    │
    ├── 1. Resources.Load<GameBootConfig>("Config/GameBootConfig")
    │      ↓ 找不到则用默认配置
    ├── 2. 创建 [MANAGERS] 根 GameObject + DontDestroyOnLoad
    ├── 3. 创建 DataManager       （数据管理）
    ├── 4. 创建 UIManager         （+ 子系统组件）
    │      └─ InitializeGlobalUI()
    │           ├─ 创建 GlobalCanvas（Toast + Transition 层）
    │           ├─ TransitionSystem.Initialize()
    │           └─ ToastSystem.Initialize()
    ├── 5. 创建 SceneController   （注入场景名配置）
    └── 6. 创建 GameManager       （注入起始阶段）
            │
            └─ Start() → EnterPhase(startPhase)
                            │
                            ├─ Boot → 自动跳转 MainMenu
                            ├─ MainMenu → LoadMainMenu()
                            │     └─ UISceneRoot.Awake() → RegisterSceneRoot()
                            │           → HUD / Modal / Dialogue / ItemDisplay.Initialize()
                            ├─ Memory → LoadMemory()
                            │     └─ UISceneRoot.Awake() → RegisterSceneRoot() ...
                            └─ ... 其他阶段
```

---

### 2. Gameplay — 游戏玩法层

> 路径：`Assets/Scripts/Gameplay/`

| 文件 | 职责 |
|------|------|
| `MemorySceneSetup.cs` | **Memory 场景运行时装配器**。创建 Player、背景、4 个碎片节点与终点门，并在启动时修复缺失的 UI 层级 |
| `PlayerMovement.cs` | 2D 玩家移动（`W/S/方向键` 回退输入），支持冻结/解冻与纵轴锁定 |
| `PlayerInteraction.cs` | 玩家交互检测。通过 Trigger2D 检测 `MemoryNodeBase`，按 `E/F` 触发交互，并维护 `Free/Interacting` 状态 |
| `MemoryFragmentNode.cs` | 记忆碎片节点（继承 `MemoryNodeBase`）。弹窗阅读后收集碎片、Toast 反馈、销毁节点；已收集碎片会在场景重建时被跳过 |
| `MemoryParallaxBackground.cs` | 背景帧序列驱动。将玩家 Y 轴进度映射到背景帧，实现“景随人动” |
| `MemoryPerspectiveEffect.cs` | 碎片透视效果。按玩家与碎片逻辑距离动态调整位置/缩放/透明度 |
| `AbyssPortalNode.cs` | 终点门交互节点。显示“进入深渊/碎片不足”提示并触发传送门逻辑 |
| `AbyssPortal.cs` | **潜渊传送门**。校验碎片数量，确认弹窗后执行 `CrossFade` 场景切换到 Abyss |
| `CourtController.cs` | **庭审控制器**。管理庭审状态机 `Intro → Debate → Verdict → End` |
| `CourtState.cs` | 庭审状态枚举：`Intro` / `Debate` / `Verdict` / `End` |

#### 记忆探索流程

```
玩家进入 Memory 场景
    │
    ├── MemorySceneSetup.Start()
    │      ├── 兜底创建/修复 UI 层级（若 UIRoot 缺层）
    │      ├── 创建不可见 Player（Vertical Move + Trigger Interaction）
    │      ├── 创建 Parallax 背景与 4 个碎片逻辑节点 + Visual 子物体
    │      └── 创建 AbyssPortal + AbyssPortalNode
    │
    ├── PlayerInteraction: Trigger 检测节点，按 E/F 交互
    │      ↓
    ├── MemoryFragmentNode.Interact() → 冻结玩家 → Modal.ShowText
    │      ↓ 关闭弹窗
    ├── OnClosed() → AbyssPortal.CollectFragment() → Toast → 销毁碎片
    │
    └── 碎片满足条件后与门交互
           └── AbyssPortal.TryEnterAbyss() → Modal.ShowConfirm
                  └── 确认后 Transition.CrossFade → 加载 Abyss
```

补充说明：
- 记忆碎片采用唯一 `fragmentId`（如 `fragment_1`~`fragment_4`）记录已收集状态；场景重新初始化时会跳过已收集碎片，避免“收集后再次显示”。
- 交互与弹窗关闭均由统一输入路径处理（`PlayerInteraction` + `ModalSystem`），节点脚本不做 `Update` 键盘轮询。

#### 庭审流程

```
CourtController.Start()
    │
    ├── Intro:    开庭介绍（Ink 对话驱动）
    ├── Debate:   玩家提交证据 → DataManager.SubmitEvidenceToCourt()
    │                → 检查话题是否达成共识
    ├── Verdict:  评判结果
    └── End:      通知 GameManager 进入下一阶段
```

---

### 3. Data — 数据定义层

> 路径：`Assets/Scripts/Data/`

| 文件 | 类型 | 职责 |
|------|------|------|
| `EvidenceData.cs` | 序列化类 | 证据数据：id / 标题 / 描述 / 类型 / 关联话题 / 说服值 / 解锁状态 |
| `EvidenceType.cs` | 枚举 | 证据类型：`Memory`（记忆）/ `Testimony`（证词）/ `Object`（物品）/ `Record`（记录） |
| `CourtTopic.cs` | 序列化类 | 庭审话题：id / 标题 / 所需说服值阈值 |

---

### 4. UI — UI 交互层

> 路径：`Assets/Scripts/UI/`

**架构：场景专属 UISceneRoot + 全局覆盖层**

```
[MANAGERS] (DontDestroyOnLoad)
  └─ UIManager
      └─ GlobalCanvas (sortOrder ≥ 1000)
           ├─ ToastLayer      (1050) — 跨场景浮动提示
           └─ TransitionLayer (1100) — 跨场景转场遮罩

Scene
  └─ UISceneRoot (美术定制 Prefab, Awake 时自动注册)
       ├─ HUDLayer     (10)
       ├─ OverlayLayer (50)
       └─ ModalLayer   (90)
```

| 文件 | 职责 |
|------|------|
| `UIManager.cs` | **UI 单例管理器**。管理全局 Canvas（Toast + Transition）+ 场景根注册。层访问属性自动路由到全局或场景层。 |
| `UISceneRoot.cs` | **场景 UI 根节点**（新增）。挂在每个场景的 Canvas 上，Awake 时注册到 UIManager，OnDestroy 注销。美术每场景维护一个独立 Prefab。 |
| `TransitionSystem.cs` | 转场系统（全局 TransitionLayer），FadeBlack / FadeWhite / GlitchFade |
| `ToastSystem.cs` | 浮动提示（全局 ToastLayer），支持 FadeSlideUp / TypewriterFade / GlitchFlash |
| `ModalSystem.cs` | 模态弹窗（场景 ModalLayer），文本 / 确认 / 自定义 Prefab，三种入场动画 |
| `HUDSystem.cs` | HUD 数值条 + 状态灯（场景 HUDLayer），场景切换时自动清理旧绑定 |
| `DialoguePlayer.cs` | 对话播放器（场景 ModalLayer），打字机效果，场景切换时重建面板 |
| `ItemDisplaySystem.cs` | 道具展示（场景 OverlayLayer），收集数据跨场景持久，UI 面板随场景重建 |
| `TextEffectPlayer.cs` | 文字特效组件（打字机 / Decode / Glitch / Wave 等） |
| `EyeTracker.cs` | 眼球注视效果组件 |
| `HoverRevealButton.cs` | 透明按钮悬停显示组件 |
| `MainMenuController.cs` | 主菜单流程控制 |
---

### 5. Animations — 动画 & 场景过渡层

> 路径：`Assets/Scripts/Animations/`

| 文件 | 职责 |
|------|------|
| `CutsceneController.cs` | **过场动画控制器**。监听 Timeline (PlayableDirector) 结束事件 → 自动加载下一场景 |
| `AsyncSceneLoader.cs` | **异步场景加载器**。带进度条 UI 的场景加载（Slider + 百分比文本） |
| `SceneLoader.cs` | **简易场景加载器**。绑定按钮直接跳转场景 |
| `playercontroller.cs` | 2D 横版玩家控制（旧版，Legacy Input）。左右移动 + 翻转 + 步行动画 |
| `guardmoving.cs` | 守卫巡逻 AI — 左右往返移动 + 翻转 |
| `PlayerHide.cs` | 玩家躲藏 — 按 H 键切换图层顺序（花瓶后躲藏） |

---

### 6. 接水管 — 管道解谜小游戏

> 路径：`Assets/Scripts/接水管/`

| 文件 | 职责 |
|------|------|
| `PipeLogic.cs` | **单节管道逻辑**。管理开口方向（上/下/左/右），点击旋转 90°，支持起点/终点标记 |
| `LevelJudge.cs` | **关卡判定器**。BFS 水流模拟，从起点灌水，检查是否能到达终点 |

#### 接水管玩法

```
点击管道 → PipeLogic.OnMouseDown()
    │
    ├── 旋转 90°（开口方向轮换：up→right→down→left→up）
    └── 触发 LevelJudge.CheckPath()
            │
            ├── 收集所有管道到网格 Dictionary<Vector2Int, PipeLogic>
            ├── 从起点 BFS/DFS 灌水（检查相邻管道开口对齐）
            └── 水流到达终点 → 通关
```

---

### 7. 通用脚本

> 路径：`Assets/Scripts/`

| 文件 | 职责 |
|------|------|
| `CameraFollow.cs` | **2D 相机跟随**。平滑跟随目标 + 可选矩形边界限制 |

---

### 8. LLMModule — LLM 文本生成模块

> 路径：`Assets/LLMModule/`
>
> 独立模块，负责对接 OpenAI 兼容 API（DeepSeek 等），为庭审和证据收集提供 AI 生成文本。

#### 架构

```
LLMService (MonoBehaviour 入口)
    │
    └── ILLMTextGenerator (对外接口)
            │
            └── LLMTextGenerator (核心实现)
                    │
                    ├── PromptBuilder    → 构建 system/user prompt
                    ├── LLMApiClient     → HTTP 通信（并发限制 + 指数退避重试）
                    └── ResponseParser   → JSON 解析（容错兼容多种 key）
```

#### 文件清单

| 文件 | 层级 | 职责 |
|------|------|------|
| `LLMService.cs` | 入口 | MonoBehaviour 挂载点，初始化 Generator，对外暴露 `Generator` 属性 |
| `ILLMTextGenerator.cs` | 接口 | 三大功能接口：生成证据卡牌 / NPC 发言 / 证词评分 |
| `LLMTextGenerator.cs` | 实现 | 串联 Prompt → API → Parser，管理证据缓存 |
| `LLMConfig.cs` | 配置 | ScriptableObject 配置：API Key / 地址 / 模型 / 温度 / 超时 / 重试 / 并发 / 降级 |
| `LLMApiClient.cs` | 通信 | UnityWebRequest 封装，SemaphoreSlim 并发控制，指数退避重试 |
| `PromptBuilder.cs` | Prompt | 构建三种场景的 user prompt（JSON 格式），System Prompt 支持外部文件热重载 |
| `ResponseParser.cs` | 解析 | 解析三种响应：证据卡牌数组 / NPC 发言数组 / 0-10 评分 |
| `DTOs.cs` | 数据 | 请求/响应 DTO：`EvidenceRequest` / `NPCSpeechRequest` / `ArgumentEvalRequest` / `CardData` / `NPCSpeechResult` |

#### 数据配置子模块

> 路径：`Assets/LLMModule/Data/`

| 文件 | 职责 |
|------|------|
| `ChapterConfig.cs` | 章节策划配置结构：确认事实 / 证据卡牌定义 / 庭审议题 / NPC 配置 |
| `ChapterConfigLoader.cs` | 从 `StreamingAssets/Data/chapter_XX.json` 加载章节配置 |
| `chapter_01.json` | 第一章策划数据（JSON） |

#### 示例子模块

> 路径：`Assets/LLMModule/Example/`

| 文件 | 职责 |
|------|------|
| `LLMUsageExample.cs` | 代码调用示例 — 展示三种 API 的调用方式 |
| `TrialUIExample.cs` | 庭审 UI 完整示例 — 加载配置 → UI 绑定 → 调用 LLM → 输出到 Text → 更新 NPC 状态 |

#### LLM 调用三大场景

| 场景 | 接口 | 输入 | 输出 |
|------|------|------|------|
| 证据收集 | `GenerateEvidenceCards()` | 章节 + 事实 + 卡牌定义 | `CardData[]`（牌名 + 牌面文本） |
| 庭审发言 | `GenerateNPCSpeeches()` | 章节 + 事实 + 议题 + NPC 状态 | `NPCSpeechResult[]`（NPC 名 + 发言） |
| 证词评分 | `EvaluatePlayerArgument()` | 证词 + 使用的牌 + 事实 | `int`（0-10 分） |

---

## 依赖项

| 包 | 用途 |
|----|------|
| **UniTask** (Cysharp) | 异步编程 |
| **Newtonsoft.Json** | JSON 序列化/反序列化 |
| **TextMeshPro** | 文本渲染 |
| **Input System** | 新版输入系统 |
| **Timeline** | 过场动画 |
| **URP** | 通用渲染管线 |

---

## 新主菜单使用与调试速查

- 场景：打开 `Assets/Scenes/MainMenu.Unity`，在 Hierarchy 中保留 `Canvas`、`MainMenuManager`（挂 `MainMenuController`）、`EyeContainer`（挂 `EyeTracker`）和 `StartButton`（挂 `HoverRevealButton`）。
- 眼球调节：选中 `Eyeball`，在 `EyeTracker` 中修改 `maxOffsetX/Y`（像素）、`followSpeed`、`centerOffset`，运行时在 Scene 视图会显示活动椭圆辅助线。
- 悬停范围：选中 `StartButton`，在 `HoverRevealButton` 中调整 `Hitbox Padding`（例如 X:80, Y:40）以扩大鼠标检测区；或关闭 `expandHitbox` 并使用独立透明 HitArea 的方案（可另行实现）。
- EventSystem：场景中若存在 `EventSystem`，可删除场景自带的实例（Bootstrapper 会在 `[MANAGERS]` 下创建并保持唯一性）。
- 恢复旧资源：若误删或回退需要旧特效与美术，恢复路径：`Assets/_Recovery/OldMainMenu/`。

如果你需要，我可以：
- 自动把 `StartButton` 的 `Hitbox Padding` 调整为特定值（例如 X:80,Y:40）；
- 生成一份快速在 Editor 中执行的检查脚本，用于在运行时高亮 UI 问题（重复 Canvas/EventSystem/RectTransform scale 等）。

---

## 快速上手

1. **配置启动参数**：`Assets/Resources/Config/GameBootConfig.asset`
   - 设置场景名称（需与 Build Settings 一致）
   - 设置起始阶段（开发时可跳到任意阶段）
2. **配置 LLM**：`Assets → Create → LLM → Config`
   - 填写 API Key（或设环境变量 `LLM_API_KEY`）
   - 配置基础 URL 和模型名
3. **直接 Play** — 所有 Manager 自动创建，无需手动挂载

---

## 开发指南

- **新增场景**：`GameBootConfig` 加字段 → `SceneController.InjectConfig()` 加参数 → 添加 `LoadXxx()` 方法
- **新增阶段**：`GamePhase` 加枚举值 → `GameManager.EnterNewPhase()` 加 switch 分支
- **新增 Manager**：`GameBootstrapper.Bootstrap()` 的"创建核心 Manager"区域添加
- **修改 Prompt**：编辑 `StreamingAssets/LLMPrompts/system_prompt.txt`（无需重新编译）
- **新增章节数据**：在 `StreamingAssets/Data/` 添加 `chapter_XX.json`
