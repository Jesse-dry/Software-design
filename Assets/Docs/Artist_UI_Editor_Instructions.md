# 美术 UI 操作手册 — Unity Editor 逐场景操作指南

> **适用対象：** 完全不懂代码的美术同学  
> **Unity 版本：** 2022.3 LTS 及以上  
> **最后更新：** 2026-03-06

---

## 目录

1. [读前必知——UI 是怎么工作的](#1-读前必知ui-是怎么工作的)
2. [一次性准备工作（全局，只做一次）](#2-一次性准备工作全局只做一次)
3. [中文字体配置（可选但强烈建议）](#3-中文字体配置可选但强烈建议)
4. [Memory 场景 UI 操作](#4-memory-场景-ui-操作)
5. [Court（庭审）场景 UI 操作](#5-court庭审场景-ui-操作)
6. [Abyss / 机房 / 走廊 v0.2（探索类场景）UI 操作](#6-abyss--机房--走廊-v02探索类场景-ui-操作)
7. [接水管 / 水管房间（小游戏场景）UI 操作](#7-接水管--水管房间小游戏场景-ui-操作)
8. [CutsceneScene（过场动画）UI 操作](#8-cutscenescene过场动画-ui-操作)
9. [全局 Toast 浮动提示——外观调整](#9-全局-toast-浮动提示外观调整)
10. [全局转场效果——颜色 & 时长调整](#10-全局转场效果颜色--时长调整)
11. [弹窗外观调整（ModalSystem）](#11-弹窗外观调整modalsystem)
12. [HUD 数值条 & 状态灯——外观调整](#12-hud-数值条--状态灯外观调整)
13. [道具展示面板外观调整（ItemDisplaySystem）](#13-道具展示面板外观调整itemdisplaysystem)
14. [对话框外观调整（DialoguePlayer）](#14-对话框外观调整dialogueplayer)
15. [常见问题 FAQ](#15-常见问题-faq)
16. [绝对禁止事项（新手保命清单）](#16-绝对禁止事项新手保命清单)

---

## 1. 读前必知——UI 是怎么工作的

### 1.1 整体架构（用图说话）

```
游戏运行时（Runtime）：

┌─────────────────────────────────────────────┐
│  [MANAGERS] 对象（跨场景永不销毁）            │
│  └─ UIManager                               │
│       └─ GlobalCanvas（只有两个全局层）       │
│            ├─ ToastLayer   ← 浮动提示        │
│            └─ TransitionLayer ← 场景转场黑幕 │
└─────────────────────────────────────────────┘

┌─────────────────────────────────────────────┐
│  当前场景（随场景打开 / 关闭）                 │
│  └─ UIRoot_XXX（你放在场景里的 Prefab）       │
│       ├─ HUDLayer     ← 血条、碎片计数等     │
│       ├─ OverlayLayer ← 道具面板、暂停菜单   │
│       └─ ModalLayer   ← 弹窗、对话框         │
└─────────────────────────────────────────────┘
```

**关键概念（一句话版本）：**

| 概念 | 一句话解释 |
|------|-----------|
| **[MANAGERS]** | 游戏启动时程序自动创建，永远不消失，你不需要管它 |
| **UIRoot_XXX Prefab** | 你每个场景要放的"UI 根容器"，每个场景有自己专属的一个 |
| **HUDLayer** | 一直显示在屏幕角落的信息，如碎片数量、计时器 |
| **OverlayLayer** | 按键打开的面板，如道具列表、暂停菜单 |
| **ModalLayer** | 弹出来要你点击才能关掉的窗口，如对话框、确认框 |
| **ToastLayer** | 屏幕上短暂飘过的小提示文字，如"拾取了记忆碎片" |
| **TransitionLayer** | 场景切换时的黑幕/白幕效果 |

### 1.2 你的工作边界

✅ **你负责的：**
- 在每个场景里放好对应的 UIRoot Prefab
- 在 Prefab 里调整颜色、大小、位置、字体
- 在 UIManager 的 Inspector 里调整动画参数和颜色

❌ **不用管的：**
- [MANAGERS] 对象——程序自动创建
- UIManager 脚本挂载——程序员已经设置好
- 代码里的任何逻辑——完全不用碰

---

## 2. 一次性准备工作（全局，只做一次）

> 这一节只需要做一次，以后新场景直接跳到对应章节。

### 步骤 1：生成所有场景的 UIRoot Prefab

1. 在 Unity 顶部菜单栏点击 **`Tools`**
2. 鼠标悬停到 **`UI`**，展开子菜单
3. 点击 **`Generate All UISceneRoot Prefabs`**

   ![菜单位置示意：Tools → UI → Generate All UISceneRoot Prefabs]

4. 等待 1~3 秒，Console 窗口（底部）会出现绿色日志：

   ```
   [UISceneRootGenerator] 已生成: Assets/Prefabs/UI/UIRoot_Memory.prefab
   [UISceneRootGenerator] 已生成: Assets/Prefabs/UI/UIRoot_Court.prefab
   ... （共 8 个）
   [UISceneRootGenerator] 完成！新建 8 个 Prefab，路径: Assets/Prefabs/UI/
   ```

5. 在 **Project 窗口**（左下或底部面板）导航到 `Assets/Prefabs/UI/`，确认以下 Prefab 已生成：

   | 文件名 | 对应场景 |
   |--------|---------|
   | `UIRoot_Memory.prefab` | Memory（记忆碎片场景） |
   | `UIRoot_Court.prefab` | Court（庭审场景） |
   | `UIRoot_Abyss.prefab` | Abyss（深渊场景） |
   | `UIRoot_Corridor.prefab` | 走廊v0.2 |
   | `UIRoot_ServerRoom.prefab` | 机房 |
   | `UIRoot_PipePuzzle.prefab` | 接水管小游戏 |
   | `UIRoot_PipeRoom.prefab` | 水管房间小游戏 |
   | `UIRoot_Cutscene.prefab` | CutsceneScene（过场动画） |

   > **提示：** 如果已经生成过，再次运行会跳过（不会覆盖），Console 会显示"已存在，跳过"。如果想强制重新生成 Memory，单独用 `Tools → UI → Generate UISceneRoot — Memory Only`。

### 步骤 2：确认 [MANAGERS] 已存在（验证程序员工作）

1. 打开任意一个场景（如 Memory.Unity）
2. 按 **Play** 运行
3. 在 **Hierarchy 窗口**中查找名为 `[MANAGERS]` 的对象
4. 如果能找到，说明程序员的 Bootstrapper 工作正常，你什么都不用做
5. 按 **Stop** 停止运行

   > **如果找不到 [MANAGERS]：** 告知程序员，他需要检查 `GameBootstrapper.cs`。

---

## 3. 中文字体配置（可选但强烈建议）

> 跳过此节不影响游戏运行，但运行时字体会用系统字体临时替代（发布前必须配置）。

### 3.1 查看当前字体状态

1. 菜单 **`Tools → UI → Validate Chinese Font Setup`**
2. 查看 Console：
   - ✓ 绿色日志 = 字体已正确配置，可跳过本节
   - ✗ 橙色警告 = 需要配置

### 3.2 配置中文字体（完整步骤）

**第一步：获取中文字体文件**

推荐使用免费商用字体：
- **Noto Sans SC**（Google Fonts 免费下载）
- **思源黑体 / Source Han Sans**（Adobe 开源免费）
- 下载后得到 `.ttf` 或 `.otf` 文件

**第二步：导入到 Unity**

1. 在 Project 窗口导航到 `Assets/Fonts/`（没有就新建文件夹）
2. 把下载的 `.ttf` 文件直接**拖进**该文件夹
3. Unity 会自动导入（稍等几秒）

**第三步：创建 TMP 字体资源**

1. 菜单 **`Window → TextMeshPro → Font Asset Creator`**
2. 在弹出窗口中：
   - **Source Font File**：点击右边的圆圈，选择你刚导入的 `.ttf`
   - **Atlas Resolution**：改为 `4096 × 4096`
   - **Atlas Population Mode**：选 `Dynamic`
   - **Character Set**：选 `Unicode Range (Hex)`
   - **Unicode Range**：粘贴以下内容（复制粘贴，不要手打）：
     ```
     20-7E,2000-206F,3000-303F,4E00-9FFF,FF00-FFEF
     ```
3. 点击 **`Generate Font Atlas`** 按钮（等待约 10~30 秒）
4. 生成完成后点击 **`Save`** 按钮
5. 在弹出的保存窗口中，导航到 `Assets/Resources/Fonts/`
6. 文件名改为 `ChineseTMP`（**必须**是这个名字）
7. 点击 Save

**第四步：验证**

1. 再次运行 `Tools → UI → Validate Chinese Font Setup`
2. 看到 `✓ TMP 中文字体已配置` 即成功

---

## 4. Memory 场景 UI 操作

### 4.1 将 UIRoot 放入场景

1. 在 Unity 顶部菜单打开场景：**`File → Open Scene`** → 选择 `Assets/Scenes/Memory.Unity`
2. 在 **Project 窗口**中找到 `Assets/Prefabs/UI/UIRoot_Memory.prefab`
3. **直接拖拽**这个 Prefab 到 **Hierarchy 窗口**（场景层级列表）的空白区域
4. Hierarchy 中出现 `UIRoot_Memory` 对象即成功

   > **注意：** 每个场景只放 **一个** UIRoot，不要重复放！

5. 按 **Ctrl+S** 保存场景

### 4.2 Memory 场景 UI 包含哪些元素

运行后你会看到（打开 Hierarchy → 展开 UIRoot_Memory）：

```
UIRoot_Memory
├─ HUDLayer
│    ├─ FragmentCounter（右上角碎片计数面板）
│    │    ├─ Icon（紫色方块图标占位）
│    │    └─ CountText（显示"碎片 0/4"文字）
│    └─ InteractionPrompt（底部"按 E 交互"提示，默认隐藏）
├─ OverlayLayer（目前为空，留给其他面板）
└─ ModalLayer
     └─ ModalBackground（半透明黑色遮罩，弹窗时自动出现）
```

### 4.3 调整碎片计数面板外观

**修改位置和大小：**

1. 在 Hierarchy 中找到 `UIRoot_Memory → HUDLayer → FragmentCounter`
2. 点击选中它
3. 在 **Inspector 窗口**（右侧面板）找到 **`Rect Transform`** 组件：
   - **Anchors（锚点）**：当前是右上角（Anchor Preset 显示为右上角图标）— 建议保持
   - **Pos X / Pos Y**：调整面板相对右上角的偏移距离（目前 X=-30, Y=-30，即距右边30像素，距上边30像素）
   - **Width / Height**：调整面板尺寸（目前 200×60）

**修改背景颜色：**

1. 仍然选中 `FragmentCounter`
2. Inspector 中找到 **`Image`** 组件
3. 点击 **Color** 旁边的色块，在色盘窗口选择你想要的颜色
4. 透明度：拖动色盘窗口中的 **A（Alpha）** 滑块控制透明度

**修改图标（替换紫色方块）：**

1. 选中 `FragmentCounter → Icon`
2. Inspector 中的 `Image` 组件
3. **Source Image** 字段：点击旁边的圆圈，选择你准备好的碎片图标 Sprite
4. 如果没有图标 Sprite，保持现有紫色占位即可（待美术资源完成后替换）

**修改计数文字样式：**

1. 选中 `FragmentCounter → CountText`
2. Inspector 中会看到 **`Text (Script)`** 组件（注意：这是旧版 Text，后期可换 TMP）
3. 可修改：
   - **Font Size**：字号
   - **Color**：文字颜色
   - **Alignment**：居中/左对齐等

   > **进阶（可选）：** 把 Text 组件换成 TextMeshPro (TMP)，支持更好的中文显示。操作：
   > 1. 记下 Text 组件的文字内容和大小
   > 2. 右键 `CountText`，点 **Add Component**，搜索 `TextMeshPro - Text (UI)`
   > 3. 复制原来 Text 的设置到 TMP 组件
   > 4. 右键原来的 `Text (Script)` 组件头部，选 **Remove Component**

**修改"按 E 交互"提示条：**

1. 选中 `HUDLayer → InteractionPrompt`
2. 这个对象**默认是隐藏的**（SetActive = false），游戏代码会在玩家靠近可交互物体时自动显示它
3. 如果想预览它的外观，在 Inspector 中勾选对象最左侧的勾选框（临时显示，保存时记得取消勾选！）
4. 修改 `PromptText` 的文字、颜色、字号同上文方法

### 4.4 保存修改（很重要！）

修改后必须「保存为 Prefab」，否则下次打开场景改动会丢失：

1. 在 Hierarchy 中选中 `UIRoot_Memory`
2. 在 Inspector 最顶部，有一行 **Prefab 工具栏**，点击 **`Overrides`**（覆盖）下拉菜单
3. 点击 **`Apply All`**（应用全部修改到 Prefab）
4. 弹出确认窗口，点 **Apply**

   **或者** 使用快捷方式：拖动 UIRoot_Memory 重新覆盖到 Project 的 Prefab 文件上。

---

## 5. Court（庭审）场景 UI 操作

### 5.1 将 UIRoot 放入场景

1. 打开场景 `Assets/Scenes/Court.Unity`
2. 将 `Assets/Prefabs/UI/UIRoot_Court.prefab` 拖入 Hierarchy

### 5.2 Court 场景 UI 结构

```
UIRoot_Court
├─ HUDLayer
│    └─ CourtStatusBar（顶部居中状态栏，显示"庭审进行中"）
│         └─ StateText（状态文字）
├─ OverlayLayer
│    └─ EvidencePanel（左侧证据列表面板，默认隐藏）
│         └─ Title（"证据列表"标题文字）
└─ ModalLayer
     └─ ModalBackground（弹窗遮罩）
```

### 5.3 调整庭审状态栏

**状态栏位置：**

1. 选中 `HUDLayer → CourtStatusBar`
2. Rect Transform 中：
   - 当前锚定在屏幕顶部居中（Anchor = 上中）
   - **Pos Y** 为 `-10`（即距顶部 10 像素），可调整让它更高或更低
   - **Width / Height**：可调整宽度（默认 600×50）

**状态文字样式：**

1. 选中 `CourtStatusBar → StateText`
2. 修改 `Text (Script)` 的：
   - **Text**：当前显示"庭审进行中"（这是初始占位文字，运行时由代码替换）
   - **Font Size**：建议 20~28
   - **Color**：当前为金黄色，可根据美术风格调整

**状态栏背景颜色：**

1. 选中 `CourtStatusBar`
2. `Image` 组件的 Color：当前为半透明深紫色 `(0.05, 0.02, 0.05, 0.70)`

### 5.4 调整证据列表面板

> **注意：** `EvidencePanel` 默认是隐藏的，代码控制何时打开。

1. 临时显示预览：选中 `EvidencePanel` → Inspector 顶部勾选框打勾
2. 调整面板大小：Rect Transform 设置（当前为左下区域，宽约 30% 屏幕宽）
3. 修改完后记得**取消勾选**（恢复隐藏状态）

**添加证据槽位（可选，美术占位）：**

想在面板里添加卡牌/图片占位，可以手动在 `EvidencePanel` 下添加子对象：
1. 右键 `EvidencePanel` → **`UI → Image`**
2. 调整这个 Image 的大小、位置，设置 Source Image 为证据图片
3. 可以复制（Ctrl+D）多个，代码运行时会自动管理真正的证据槽

---

## 6. Abyss / 机房 / 走廊 v0.2（探索类场景）UI 操作

> 这三个场景使用同一类型的 UIRoot（Exploration），操作完全相同。

### 6.1 各场景对应 Prefab

| 场景文件 | 放入的 Prefab |
|---------|--------------|
| Abyss.Unity | `UIRoot_Abyss.prefab` |
| 机房.unity | `UIRoot_ServerRoom.prefab` |
| 走廊v0.2.unity | `UIRoot_Corridor.prefab` |

### 6.2 探索场景 UI 结构

```
UIRoot_XXX（探索类）
├─ HUDLayer
│    └─ InteractionPrompt（底部居中"提示文本"条，默认隐藏）
│         └─ PromptText（具体文字）
├─ OverlayLayer（空，留给道具面板等叠加层）
└─ ModalLayer
     └─ ModalBackground（弹窗遮罩）
```

### 6.3 操作步骤

1. 打开对应场景
2. 将对应 Prefab 拖入 Hierarchy
3. 展开查看 HUDLayer 下的 `InteractionPrompt`
4. 这里的样式调整与 Memory 场景的同名组件完全相同（参考 [4.3 节](#43-调整碎片计数面板外观)）

### 6.4 为探索场景添加血量/状态条（如果策划需要）

如果需要血量条等 HUD 元素：
1. 在 `HUDLayer` 下右键 → **`UI → Slider`**（进度条）或 **`UI → Image`**（自定义填充图）
2. 调整好位置和外观
3. 告知程序员这个对象的名字，他会编写代码让它动起来

---

## 7. 接水管 / 水管房间（小游戏场景）UI 操作

### 7.1 各场景对应 Prefab

| 场景文件 | 放入的 Prefab |
|---------|--------------|
| 接水管.unity | `UIRoot_PipePuzzle.prefab` |
| 水管房间.unity | `UIRoot_PipeRoom.prefab` |

### 7.2 小游戏场景 UI 结构

```
UIRoot_XXX（小游戏）
├─ HUDLayer
│    └─ TimerPanel（顶部居中计时器面板）
│         └─ TimerText（显示"00:00"）
├─ OverlayLayer
│    └─ ResultPanel（居中结果面板，默认隐藏）
│         └─ ResultText（显示"通关！"）
└─ ModalLayer
     └─ ModalBackground（弹窗遮罩）
```

### 7.3 调整计时器

1. 打开对应场景，拖入对应 Prefab
2. 选中 `HUDLayer → TimerPanel`
3. **背景颜色**：`Image` 组件 → Color 调整
4. **位置**：Rect Transform 的 Pos Y 调整距顶部距离（默认 -15）
5. **大小**：Width=160, Height=50，可按需调整

**计时器文字：**
1. 选中 `TimerPanel → TimerText`
2. **Font Size**：建议 24~32，计时器要够大够清晰
3. **Color**：当前白色，可改为金色、橙色等醒目颜色

### 7.4 调整通关/失败结果面板

1. 临时显示：选中 `OverlayLayer → ResultPanel` → Inspector 勾选框打勾
2. **面板大小和位置**：Rect Transform 调整
   - 当前：屏幕中间偏中下区域（anchorMin=(0.2, 0.3), anchorMax=(0.8, 0.7)）
   - 可以改为全屏居中：anchorMin=(0.3, 0.2), anchorMax=(0.7, 0.8)
3. **背景颜色**：`Image` 组件 → Color
4. **结果文字**：选中 `ResultText` 修改字号、颜色
5. **想添加分数/时间显示**：在 `ResultPanel` 下右键 → `UI → Text` 添加更多文字元素
6. 修改完毕后**取消勾选**恢复隐藏

---

## 8. CutsceneScene（过场动画）UI 操作

### 8.1 放置 UIRoot

1. 打开 `Assets/Scenes/CutsceneScene.unity`
2. 将 `UIRoot_Cutscene.prefab` 拖入 Hierarchy

### 8.2 过场场景 UI 结构

Cutscene 类型极简，只有基础三层，没有任何预置 HUD 元素：

```
UIRoot_Cutscene
├─ HUDLayer（空）
├─ OverlayLayer（空）
└─ ModalLayer
     └─ ModalBackground（弹窗遮罩）
```

### 8.3 为过场动画添加字幕（可选）

如需屏幕底部显示字幕：
1. 右键 `HUDLayer` → **`UI → Text - TextMeshPro`**
2. 命名为 `SubtitleText`
3. Rect Transform 设置：
   - Anchor：Bottom Center（底部居中）
   - Pos Y：80（距底部 80 像素）
   - Width：1000, Height：60
4. TMP 组件设置：
   - Alignment：Center/Bottom 对齐
   - Font Size：24
   - Color：白色或浅黄色
5. 告知程序员字幕对象名为 `SubtitleText`

---

## 9. 全局 Toast 浮动提示——外观调整

> Toast 是跨场景的，在所有场景都生效。调整在 **[MANAGERS]→UIManager** 的 Inspector 里。

### 9.1 找到 Toast 配置

Toast 的配置挂在 UIManager 的 `ToastSystem` 组件上。**但有一个问题：** [MANAGERS] 只在运行时存在，编辑模式下找不到它。

**解决方法：使用运行时调整流程**

1. 按 **Play** 运行
2. 在 Hierarchy 中找到 `[MANAGERS]` → `UIManager`
3. 在 Inspector 中找到 `Toast System (Script)` 组件（需展开）
4. 调整你想要的参数（**运行中的修改不会保存**）
5. 记下你喜欢的数值
6. 按 **Stop** 停止
7. 在 Project 窗口找到 UIManager 所在的 Prefab 或直接在 GameBootstrapper 处查询——询问程序员如何将这些配置持久化

### 9.2 Toast 可调整参数一览

| 参数名 | 中文说明 | 默认值 | 建议范围 |
|--------|---------|--------|---------|
| Default Style | 默认样式 | FadeSlideUp（渐入上浮） | FadeSlideUp / TypewriterFade / GlitchFlash |
| Default Duration | 显示时长（秒） | 2 | 1.5 ~ 4 |
| Max Toasts | 最多同时显示几条 | 5 | 3 ~ 6 |
| Toast Spacing | 多条 Toast 间距 | 50 | 40 ~ 70 |
| Font Size | 字号 | 22 | 18 ~ 28 |
| Text Color | 普通文字颜色 | 浅灰白 | 根据风格 |
| Warning Color | 警告文字颜色 | 红色 | 保持醒目 |
| Positive Color | 正面反馈颜色 | 绿色 | 根据风格 |
| Fade In Duration | 渐入时长 | 0.3 | 0.2 ~ 0.5 |
| Fade Out Duration | 渐出时长 | 0.5 | 0.3 ~ 0.8 |
| Slide Up Distance | 上浮距离 | 60 | 40 ~ 100 |
| Start Offset | 出现位置（距底部） | (0, 150) | (0, 80) ~ (0, 200) |

---

## 10. 全局转场效果——颜色 & 时长调整

> 转场（黑幕淡入淡出）是全局效果，在 TransitionSystem 组件上配置。

### 10.1 可调整参数

同样在运行时的 `[MANAGERS] → UIManager → Transition System (Script)` 中：

| 参数名 | 中文说明 | 默认值 | 说明 |
|--------|---------|--------|------|
| Default Duration | 默认转场时长（秒） | 1 | 越大越慢 |
| Transition Type | 转场效果类型 | FadeBlack | 见下表 |
| Glitch Intensity | Glitch 扭曲强度 | 5 | 仅 GlitchFade 生效 |
| Glitch Frequency | Glitch 频率 | 0.05 | 越小越快 |
| Glitch Duration | Glitch 持续时间 | 0.5 | 秒 |
| Fade In Ease | 淡入缓动曲线 | InQuad | 可改 InCubic 等 |
| Fade Out Ease | 淡出缓动曲线 | OutQuad | 可改 OutCubic 等 |

**三种转场类型对比：**

| 类型 | 效果描述 | 适用场景 |
|------|---------|---------|
| **FadeBlack** | 纯黑色淡入淡出 | 通用、睡眠感、庄重 |
| **FadeWhite** | 白色闪光淡入淡出 | 记忆闪回、时间跳跃 |
| **GlitchFade** | 故障扭曲 + 淡入淡出 | 数字世界、意识崩溃 |

---

## 11. 弹窗外观调整（ModalSystem）

> 弹窗包括：文本弹窗（确认按钮）、确认弹窗（是/否）。

### 11.1 方法一：修改 ModalSystem 参数（快速调整动画）

在运行时 `[MANAGERS] → UIManager → Modal System (Script)`：

| 参数名 | 中文说明 | 默认值 |
|--------|---------|--------|
| Animation Type | 弹窗出现动画 | FadeScale（缩放淡入） |
| Anim Duration | 动画时长 | 0.35 秒 |
| Scale From | 初始缩放比（FadeScale） | 0.8 |
| Slide Distance | 滑入距离（SlideUp） | 300 像素 |
| Open Ease | 开启缓动 | OutBack（有回弹感） |
| Close Ease | 关闭缓动 | InQuad |

**三种弹窗动画：**

| 类型 | 效果 | 建议场景 |
|------|------|---------|
| **FadeScale** | 从略小缩放淡入 | 通用，比较自然 |
| **SlideUp** | 从下方滑入 | 手机风格，轻快 |
| **GlitchIn** | 故障闪烁出现 | 科技感、异常状态 |

### 11.2 方法二：使用自定义弹窗 Prefab（完全自定义外观）

1. 在 Project 窗口中，右键 **`Assets/Prefabs/UI/`** → **`Create → Prefab`**，命名为 `TextModal`
2. 双击打开 Prefab 编辑模式
3. 为根对象添加 `Canvas Group` 组件（用于淡入淡出动画）
4. 在根对象下创建以下子结构：

   ```
   TextModal（Image - 弹窗背景）
   ├─ TitleText（TextMeshPro - 标题文字）
   ├─ BodyText（TextMeshPro - 正文文字）
   └─ CloseButton（Button + Image - 关闭按钮）
        └─ ButtonLabel（TextMeshPro - 按钮文字"确认"）
   ```

   > **关键命名：** 子对象名字必须是 `TitleText`、`BodyText`、`CloseButton`，代码通过这些名字自动找到并填充内容。

5. 调整背景颜色、圆角（通过 Sprite）、字体等外观
6. 完成后保存 Prefab
7. 告知程序员 Prefab 路径，他会将其绑定到 `Modal System` 的 `Text Modal Prefab` 字段

---

## 12. HUD 数值条 & 状态灯——外观调整

> HUD 数值条（如混乱值进度条）和状态灯（如 NPC 是否被说服的圆点指示器）。

### 12.1 HUD 组件参数

在运行时 `[MANAGERS] → UIManager → HUD System (Script)`：

| 参数名 | 中文说明 | 默认值 |
|--------|---------|--------|
| Bar Style | 数值条变化效果 | SmoothLerp（平滑过渡） |
| Bar Tween Duration | 变化动画时长 | 0.5 秒 |
| Bar Color Gradient | 数值条颜色渐变（低→高） | 可设置颜色梯度 |
| Delta Text Color | 飘字颜色（如"+3混乱"） | 金黄色 |
| Indicator Off | 状态灯关闭颜色 | 暗灰色 |
| Indicator On | 状态灯开启颜色 | 亮绿色 |
| Indicator Anim Duration | 状态灯切换动画时长 | 0.4 秒 |

**三种数值条效果：**

| 类型 | 效果 | 适用 |
|------|------|------|
| **SmoothLerp** | 平滑线性过渡 | 通用，适合理性值/HP |
| **Pulse** | 变化时有脉冲闪烁 | 强调变化，适合重要数值 |
| **Glitch** | 不稳定抖动 | 混乱值、精神状态不稳定 |

### 12.2 Bar Color Gradient（颜色渐变）详细操作

1. 在 Inspector 中找到 **Bar Color Gradient** 字段，旁边有一个颜色条
2. 双击颜色条，打开 **Gradient Editor（渐变编辑器）**
3. 颜色条下方的小方块是"颜色节点"：
   - 左边节点 = 数值为 0（低）时的颜色
   - 右边节点 = 数值为 1（高）时的颜色
4. 点击颜色节点，在下方 Color 框修改颜色
5. 点击颜色条空白处可添加新节点
6. 建议风格：低值偏绿/蓝（平静），高值偏红/紫（危险/混乱）

---

## 13. 道具展示面板外观调整（ItemDisplaySystem）

> 按 Tab 键呼出的卡牌收集面板（展示收集的记忆碎片、证据卡等）。

### 13.1 组件参数

在运行时 `[MANAGERS] → UIManager → Item Display System (Script)`：

| 参数名 | 中文说明 | 默认值 |
|--------|---------|--------|
| Panel Animation | 面板出现动画 | SlideFromLeft（从左滑入） |
| Anim Duration | 动画时长 | 0.4 秒 |
| Toggle Key | 呼出快捷键 | Tab |
| Toast On Collect | 收集道具时是否弹 Toast | 是 |
| Collect Anim Duration | 收集时的入列动画时长 | 0.6 秒 |

**三种面板动画：**

| 类型 | 效果 |
|------|------|
| **SlideFromLeft** | 从屏幕左侧滑入 |
| **FadeIn** | 整体淡入 |
| **Expand** | 从中心向外展开 |

### 13.2 使用自定义道具面板 Prefab

如需完全自定义外观：

完整 Prefab 需包含以下**命名严格**的子对象：

```
ItemPanel（根，需有 CanvasGroup + RectTransform）
├─ ItemGrid（需有 GridLayoutGroup 组件）
├─ DetailPanel（详情区域容器）
│    ├─ DetailIcon（Image - 道具图标）
│    ├─ DetailTitle（TMP_Text - 道具名称）
│    └─ DetailDesc（TMP_Text - 道具描述）
└─ CloseButton（Button - 关闭按钮）
```

> 子对象名字必须严格匹配上述名称，代码通过名字查找。

---

## 14. 对话框外观调整（DialoguePlayer）

> 打字机效果的剧情对话框（庭审 NPC 发言、记忆碎片阅读等）。

### 14.1 组件参数

在运行时 `[MANAGERS] → UIManager → Dialogue Player (Script)`：

| 参数名 | 中文说明 | 默认值 |
|--------|---------|--------|
| Text Effect | 文字效果类型 | Typewriter（打字机） |
| Char Delay | 打字机每字符间隔（秒） | 0.04 | 
| Advance Key | 推进/继续键 | Space |
| Skip Key | 跳过整条对话键 | Return（Enter）|
| Speaker Color | 说话人名字颜色 | 青绿色 |
| Dialogue Color | 对话正文颜色 | 浅灰白 |
| Panel Anim Duration | 面板出现动画时长 | 0.3 秒 |

**文字效果类型：**

| 类型 | 描述 |
|------|------|
| **Typewriter** | 逐字打出（最常见） |
| **Decode** | 先显示乱码再"解码"成正确文字 |
| **Glitch** | 文字随机闪烁出现 |
| **Wave** | 文字波浪形上下起伏 |
| **Chromatic** | RGB 三色偏移效果 |

### 14.2 自定义对话面板外观

对话面板 Prefab 需包含：

```
DialoguePanel（根，需有 CanvasGroup）
├─ SpeakerName（TMP_Text - 说话人名字） ← 名字必须是SpeakerName
├─ DialogueText（TMP_Text - 对话正文） ← 名字必须是DialogueText
├─ Portrait（Image - 角色立绘，可选） ← 名字必须是Portrait
└─ AdvanceButton（Button - 继续按钮，可选） ← 名字必须是AdvanceButton
```

**调整建议：**
- 对话框背景建议使用 9-Slice Sprite（九宫格缩放），防止拉伸
- 字号建议：说话人名 20~22，正文 18~20
- 对话框建议放在屏幕下方约 1/4 处，高度约 150~200 像素

---

## 15. 常见问题 FAQ

### Q1：放了 UIRoot Prefab，运行后对话/Toast 不显示？

**排查步骤：**
1. 确认 [MANAGERS] 在 Hierarchy 中存在（运行时查看）
2. 确认 UIRoot Prefab 中 UISceneRoot 组件的三个层引用不为空：
   - 选中 `UIRoot_XXX` → Inspector → `UI Scene Root (Script)` 组件
   - 查看 `Hud Layer` / `Overlay Layer` / `Modal Layer` 三个字段，应各自指向对应的层对象
   - 如果有字段显示 `None (Rect Transform)`，需要手动拖入（展开 Hierarchy，把对应的层节点拖到字段上）

### Q2：UISceneRoot 组件的层引用是空的怎么办？

这说明 Prefab 的内部引用丢失了，按下面步骤修复：

1. 选中 `UIRoot_XXX`（Hierarchy 中）
2. Inspector 中找到 `UI Scene Root (Script)` 组件
3. 展开 Hierarchy 找到里面的 `HUDLayer`、`OverlayLayer`、`ModalLayer` 三个子对象
4. 把 `HUDLayer` 的对象**拖入** `Hud Layer` 字段，以此类推
5. 完成后应用 Overrides 保存到 Prefab

### Q3：场景里有多个 UIRoot，怎么处理？

**一个场景只能有一个 UIRoot！** 

删除多余的：
1. Hierarchy 中多余的 UIRoot 对象 → 右键 → **Delete**
2. 保存场景

### Q4：修改了 Prefab 里的颜色，但关闭场景后改动丢失？

两种原因：
- **忘记应用 Overrides：** 选中 UIRoot → Inspector → Overrides → Apply All
- **修改了 Prefab Mode 里的东西但没保存：** 在 Prefab Mode 里修改后，点击右上角 **`Save`** 按钮

### Q5：Toast 文字出现乱码（方块）？

字体未配置，参考[第 3 节](#3-中文字体配置可选但强烈建议)配置中文 TMP 字体。

### Q6：场景运行一片漆黑/完全看不到 UI？

可能是 TransitionLayer 的黑幕没有关闭。排查：
1. 运行游戏
2. Hierarchy → [MANAGERS] → UIManager → GlobalCanvas → TransitionLayer → FadeImage
3. 检查这个 Image 的 Alpha 是否为 1（全黑）
4. 如果是，告知程序员场景初始化时未调用 `Transition.FadeOut()`

### Q7：想预览弹窗的外观但又不想运行游戏？

1. 在场景中临时添加一个 Panel（**不要保存**）
2. 根据[第 11 节](#11-弹窗外观调整modalsystem)的结构手动搭建
3. 调整外观满意后，把结构和参数记录到设计稿
4. 删除临时 Panel
5. 按照结构制作正式 Prefab

### Q8：想在运行模式下看调整效果，但改完 Stop 后参数恢复了？

Unity 在运行模式下的修改**不会自动保存**。解决方法：
1. 运行时调整好参数
2. 在修改的组件上**右键组件标题**→ **Copy Component**
3. Stop
4. 找到对应的 Prefab 或 GameObject，在相同组件上**右键**→ **Paste Component Values**

---

## 16. 绝对禁止事项（新手保命清单）

> 以下操作会破坏 UI 系统，导致无法运行，**请务必避免**：

| ❌ 禁止操作 | ✅ 正确做法 |
|-----------|-----------|
| 删除 `HUDLayer`、`OverlayLayer`、`ModalLayer` 三个层节点 | 只在层内增删子对象，不要删层本身 |
| 手动修改 `UISceneRoot` 组件的三个层引用使其指向错误对象 | 如果引用丢失，参考 FAQ Q2 重新正确绑定 |
| 在一个场景中放多个不同的 UIRoot Prefab | 每个场景只放一个 UIRoot |
| 把 HUDLayer 的 Canvas sortingOrder 改成 1000 以上 | 场景层保持 10/50/90，全局层才用 1000+ |
| 在 `[MANAGERS]` 对象或 `UIManager` 上删除组件 | 运行时的 MANAGERS 由程序管理，不要动 |
| 将 UIRoot Prefab 的 Canvas `Render Mode` 改成 World Space | 保持 Screen Space - Overlay |
| 删除或重命名 `ModalBackground` 子对象 | 如需修改外观，改颜色/大小，不要删除或改名 |
| 不点 Apply Overrides 就关闭场景 | 每次修改完都要 Apply Overrides |

---

## 附录：快速操作速查表

| 我想做的事 | 操作路径 |
|-----------|---------|
| 生成所有场景 UIRoot Prefab | `Tools → UI → Generate All UISceneRoot Prefabs` |
| 配置中文字体 | `Tools → UI → Setup Chinese TMP Font (Help)` |
| 检查字体是否正确 | `Tools → UI → Validate Chinese Font Setup` |
| 修改 Toast 样式 | 运行时 [MANAGERS]→UIManager→Toast System 组件 Inspector |
| 修改弹窗动画 | 运行时 [MANAGERS]→UIManager→Modal System 组件 Inspector |
| 修改转场效果 | 运行时 [MANAGERS]→UIManager→Transition System 组件 Inspector |
| 修改场景 HUD 颜色/位置 | 直接在 Hierarchy 中找到对应 UIRoot 的子节点修改 |
| 调整道具面板呼出键 | 运行时 [MANAGERS]→UIManager→Item Display System Inspector |
| 修改对话框打字速度 | 运行时 [MANAGERS]→UIManager→Dialogue Player 的 Char Delay |
| 预览交互提示条外观 | 找到场景 UIRoot→HUDLayer→InteractionPrompt，临时勾选显示 |
| 保存 Prefab 修改 | 选中 UIRoot → Inspector → Overrides → Apply All |

---

*文档维护：如有疑问联系程序员，UI 逻辑相关问题看 `Assets/Docs/UIManager_README.md`。*
