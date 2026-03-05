**UI 管理器 & 本次构建说明**

- 用途：记录本次新增/修改的 UI 相关模块、用法示例、Inspector 暴露项、以及模块回滚（按文件级）步骤，便于代码审核与快速回退。

**新增/修改总览**
- **新增（跨场景持久 / 放在 [MANAGERS] 下）**：
  - **UI 统一管理器：** [Assets/Scripts/UI/UIManager.cs](Assets/Scripts/UI/UIManager.cs)
    - 说明：单例，DontDestroyOnLoad，管理 4 层 Canvas（HUD/Overlay/Modal/Transition）并暴露子系统引用。
  - **场景转场：** [Assets/Scripts/UI/TransitionSystem.cs](Assets/Scripts/UI/TransitionSystem.cs)
    - 三种转场：FadeBlack / FadeWhite / GlitchFade。提供 FadeIn/FadeOut/CrossFade 接口。
  - **模态弹窗：** [Assets/Scripts/UI/ModalSystem.cs](Assets/Scripts/UI/ModalSystem.cs)
    - 通用文本弹窗 / 确认弹窗 / 自定义 Prefab，三种入场动画可选。
  - **浮动提示（Toast）：** [Assets/Scripts/UI/ToastSystem.cs](Assets/Scripts/UI/ToastSystem.cs)
    - 非打断提示，支持世界坐标飘字、FadeSlideUp/TypewriterFade/GlitchFlash 等风格。
  - **HUD（数值条 & 状态灯）：** [Assets/Scripts/UI/HUDSystem.cs](Assets/Scripts/UI/HUDSystem.cs)
    - 支持注册 ValueBar 与 Indicator，提供 SetValue / SetIndicator 等 API。
  - **道具展示（非背包）：** [Assets/Scripts/UI/ItemDisplaySystem.cs](Assets/Scripts/UI/ItemDisplaySystem.cs)
    - 网格 + 详情，快捷键（默认 Tab）呼出，支持多种入场动画。
  - **文字特效组件：** [Assets/Scripts/UI/TextEffectPlayer.cs](Assets/Scripts/UI/TextEffectPlayer.cs)
    - 多种文字效果（打字机/Decode/Glitch/Wave/Chromatic 等），可复用在对话、Toast 等。
  - **对话播放器：** [Assets/Scripts/UI/DialoguePlayer.cs](Assets/Scripts/UI/DialoguePlayer.cs)
    - 模态对话序列，集成 TextEffectPlayer，支持推进/跳过。
  - **道具数据 & 类型：** [Assets/Scripts/Data/ItemData.cs](Assets/Scripts/Data/ItemData.cs), [Assets/Scripts/Data/ItemType.cs](Assets/Scripts/Data/ItemType.cs)
    - 跨场景持久道具数据结构（icon/description/type/isCollected 等）。

- **修改**：
  - **数据管理器（扩展道具支持）**： [Assets/Scripts/Core/DataManager.cs](Assets/Scripts/Core/DataManager.cs)
    - 新增 allItems、CollectItem、HasItem、GetCollectedItems、OnItemCollected 事件。
    - 在收集时会同步调用 UIManager.Instance.ItemDisplay.AddItem(item)。
  - **Bootstrapper（注册 UIManager）**： [Assets/Scripts/Core/GameBootstrapper.cs](Assets/Scripts/Core/GameBootstrapper.cs)
    - 在创建 DataManager 之后创建 UIManager，并在上面挂载子系统组件与调用 InitializeUIRoot()。

**设计要点 / 约定**
- 跨场景的 UI（HUD、Toast、Modal、Transition、ItemDisplay）由 `UIManager` 持久化管理；场景专属 UI 仍放各场景 Canvas 中。
- `UIManager` 在 `GameBootstrapper` 中自动创建，无需手动在场景中放置（如需自定义 Prefab，请创建 Resources/Prefabs/UIRoot.prefab）。
- Inspector 可调参数：大多数系统在类上有 `[SerializeField]` 暴露常用参数（动画时长、颜色、样式等），便于视觉迭代。
- 风格：整体倾向“神秘压抑、暗色调”，运行时默认创建的 UI 使用暗色基调与冷白/暗绿的点缀色。
- 动画依赖：本次实现使用 DOTween（DG.Tweening）。请在 Unity 中导入 DOTween 并执行 Tools/DOTween Utility Panel → Setup（或通过 Asset Store）。

**主要 API / 使用示例**
- 由系统自动初始化，代码中直接使用单例调用（示例）：

  - 显示 Toast：

    `UIManager.Instance.Toast.Show("获得：阿卡那牌【愚者】");`

  - 打开模态文本弹窗：

    `UIManager.Instance.Modal.ShowText("记忆碎片","这是一段记忆内容。", "关闭", ()=>{ /* 回调 */ });`

  - 场景转场（淡入 -> 切场景 -> 淡出）：

    `UIManager.Instance.Transition.CrossFade(0.6f, ()=>{ SceneController.Instance.LoadAbyss(); }, 0.6f);`

  - 显示对话序列：

    `UIManager.Instance.Dialogue.PlaySequence(entries, ()=>{ Debug.Log("对话结束"); });`

  - 注册并更新 HUD：

    在场景中创建 UI 元素（Image/TMP），然后在启动时调用：

    `UIManager.Instance.HUD.RegisterValueBar("chaos", chaosFillImage, chaosLabel, chaosValueText);`

    `UIManager.Instance.HUD.SetValue("chaos", 0.45f, "+5 混乱");`

  - 收集道具（Gameplay 逻辑触发）：

    `DataManager.Instance.CollectItem("arcana_fool");`  // 自动触发 UI 动画并产生 Toast

**Inspector 关键字段（快速导航）**
- UI 管理器： [Assets/Scripts/UI/UIManager.cs](Assets/Scripts/UI/UIManager.cs)
  - hudLayer / overlayLayer / modalLayer / transitionLayer
  - modalBackground / modalBgAlpha / modalBgFadeDuration
  - 子系统引用：Transition / Modal / Toast / HUD / Dialogue / ItemDisplay

- TransitionSystem： [Assets/Scripts/UI/TransitionSystem.cs](Assets/Scripts/UI/TransitionSystem.cs)
  - fadeImage / defaultDuration / transitionType / glitchIntensity

- ModalSystem： [Assets/Scripts/UI/ModalSystem.cs](Assets/Scripts/UI/ModalSystem.cs)
  - animationType / textModalPrefab / confirmModalPrefab / scaleFrom

- ToastSystem： [Assets/Scripts/UI/ToastSystem.cs](Assets/Scripts/UI/ToastSystem.cs)
  - defaultStyle / defaultDuration / toastPrefab / fontSize

- HUDSystem： [Assets/Scripts/UI/HUDSystem.cs](Assets/Scripts/UI/HUDSystem.cs)
  - barStyle / barTweenDuration / barColorGradient / indicatorOn / indicatorOff

- ItemDisplaySystem： [Assets/Scripts/UI/ItemDisplaySystem.cs](Assets/Scripts/UI/ItemDisplaySystem.cs)
  - panelPrefab / cardSlotPrefab / toggleKey / toastOnCollect

- TextEffectPlayer： [Assets/Scripts/UI/TextEffectPlayer.cs](Assets/Scripts/UI/TextEffectPlayer.cs)
  - effectType / typewriterCharDelay / decodeChars / waveAmplitude

**回滚（按模块/文件级）**
- 如果需要回退某个子系统（例如撤销 ModalSystem）：可以使用 git 恢复或删除对应文件并提交。常用命令示例：

  - 恢复单个已修改文件到仓库 HEAD 的状态（撤销本地未提交改动）：

    `git restore --staged <path>`
    `git restore <path>`

    例如：

    `git restore Assets/Scripts/UI/ModalSystem.cs`

  - 从上一个提交恢复（指定 commit）：

    `git restore --source=HEAD~1 --worktree --staged Assets/Scripts/UI/ModalSystem.cs`

  - 删除新增文件并提交（若文件是新添加且尚未追踪）：

    `git rm Assets/Scripts/UI/ModalSystem.cs`
    `git commit -m "Revert ModalSystem"`

  - 一次性回退整个 UI 模块（示例，执行前请确认 commit 历史）：

    `git checkout <commit_before_ui_changes> -- Assets/Scripts/UI`  // 将 UI 文件夹恢复为指定提交的状态
    `git commit -m "Revert UI module to <commit_before_ui_changes>"

- 建议在回滚前先评审依赖关系：例如 `DataManager` 已改动依赖 `UIManager`（CollectItem 调用），若回滚 UIManager 需同步移除这处调用或同时回滚 `DataManager`。

**代码审查 & 同步清单（建议 Pull/Review 时重点关注）**
- 核心新增/修改文件：
  - [Assets/Scripts/UI/UIManager.cs](Assets/Scripts/UI/UIManager.cs)
  - [Assets/Scripts/UI/TransitionSystem.cs](Assets/Scripts/UI/TransitionSystem.cs)
  - [Assets/Scripts/UI/ModalSystem.cs](Assets/Scripts/UI/ModalSystem.cs)
  - [Assets/Scripts/UI/ToastSystem.cs](Assets/Scripts/UI/ToastSystem.cs)
  - [Assets/Scripts/UI/HUDSystem.cs](Assets/Scripts/UI/HUDSystem.cs)
  - [Assets/Scripts/UI/ItemDisplaySystem.cs](Assets/Scripts/UI/ItemDisplaySystem.cs)
  - [Assets/Scripts/UI/TextEffectPlayer.cs](Assets/Scripts/UI/TextEffectPlayer.cs)
  - [Assets/Scripts/UI/DialoguePlayer.cs](Assets/Scripts/UI/DialoguePlayer.cs)
  - [Assets/Scripts/Data/ItemData.cs](Assets/Scripts/Data/ItemData.cs)
  - [Assets/Scripts/Core/DataManager.cs](Assets/Scripts/Core/DataManager.cs) (修改)
  - [Assets/Scripts/Core/GameBootstrapper.cs](Assets/Scripts/Core/GameBootstrapper.cs) (修改)

- 代码审查要点：
  - 确保 DOTween 在项目中正确导入并完成 Setup（否则编译会提示找不到 DG.Tweening）。
  - `GameBootstrapper` 的 CreateManager 顺序：UIManager 在 DataManager 之后创建，确保 `UIManager` 的 Initialize 不依赖尚未创建的服务。
  - `DataManager.CollectItem` 在触发 UI 更新前**不会**假设 UI 存在（已有空值保护，但建议在无 UI 时也能正常工作）。

**运行与验证步骤（快速上手）**
1. 在 Unity 编辑器导入 DOTween（Asset Store）并在菜单 Tools/DOTween Utility Panel 执行 Setup。
2. 打开项目，确保无编译错误（Unity 会自动编译）。
3. Play：Bootstrapper 会自动创建 [MANAGERS]，并实例化 UIManager（若存在 Resources/Prefabs/UIRoot.prefab 则使用该 Prefab，否则用运行时回退 UI）。
4. 在游戏运行时测试以下点：
   - 触发 DataManager.CollectItem(...)，观察是否弹出 Toast 并进入 ItemDisplay（若已打开）。
   - 在庭审场景触发 UIManager.Instance.Dialogue.PlaySingle(...)，观察模态背景与对话打字机效果。
   - 场景切换时调用 UIManager.Instance.Transition.CrossFade(...)，检查淡入淡出效果。

**后续建议**
- 将 UIRoot Prefab（自定义美术）放在 Resources/Prefabs/UIRoot.prefab，以便美术/UI 组在不改代码的情况下替换视觉元素。
- 把 `ItemData` 的配置（卡牌图、描述）做成 ScriptableObject 列表，便于策划编辑与版本管理。
- 为关键 UI 操作添加日志（可选 verbosity 标志），便于多人调试。

---

