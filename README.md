# Galaxy Agent · 42agent

> Unity 2D 自主智能体生存探索模拟器。你不操控角色，而是俯瞰一群 **Agent** 在程序生成的外星球上自主探索、采集、战斗、调查——由 LLM + Utility AI + FSM 三层 AI 驱动。
>
> **版本：v1.0.0「文明黎明」** · Unity 6000.3.11f1 · 命名空间 `GalaxyAgent`

---

## 环境要求

| 项 | 要求 |
|---|---|
| **Unity** | `6000.3.11f1`（**必须**该版本，URP 17.3.0 / Cinemachine 3.1.7 等包已锁定） |
| **操作系统** | **Windows**（当前 SQLite 用 Windows 原生库 `sqlite3.dll`，macOS/Linux 需替换对应平台库，见下文「平台说明」） |
| **本地 LLM（可选）** | [Ollama](https://ollama.com/)，推荐模型 `qwen3:8b`。**不装也能玩**——LLM 不可用时高层决策自动降级为规则 AI |

---

## 快速开始（安装）

### 1. 安装 Unity

通过 [Unity Hub](https://unity.com/download) 安装 **6000.3.11f1**（Installs → Install Editor → 选 6000.3.11f1）。

### 2. 克隆仓库

```bash
git clone <仓库地址> 42agent
cd 42agent
```

### 3. 用 Unity Hub 打开项目

- Unity Hub → **Open → Add project from disk** → 选择 `42agent` 目录
- 等待编译完成。若因网络无法拉取 `unity-mcp`，可从 `Packages/manifest.json` 删除该行（它是编辑器自动化辅助工具，**非游戏运行依赖**）

### 4. 运行

- 打开场景 `Assets/Scenes/MainMenu.unity`（项目入口）
- 点击编辑器顶部 **▶ Play**

游戏流程：`MainMenu → MapGeneration（配置星球并生成）→ GameScene（主循环）`。

---

## 配置

### LLM（Ollama）— 可选但推荐

让 Agent 由大模型"思考"高层战略，体验完整。

```bash
# 1. 安装 Ollama 后，拉取模型
ollama pull qwen3:8b

# 2. Ollama 默认服务在 http://localhost:11434，无需额外启动命令（装完常驻）
```

游戏内绑定：
- GameScene 底栏 → **LLM 对话** / **配置** 按钮，确认 `URL` 与 `Model`（默认 `http://localhost:11434` + `qwen3:8b`）
- 该配置**随存档保存/恢复**

> **不配置 LLM 也能玩**：`LLMManager.IsAvailable=false` 时，高层决策自动跳过，纯走紧急规则 + 中层 Utility AI。

### 游戏数值配置

运行时可调数值（Agent / 世界时间 / 战斗采集发现 / LLM 四组）：

| 入口 | 路径 |
|---|---|
| 编辑器 | 顶部菜单 `Tools → 游戏配置`（GameConfigEditorWindow） |
| 游戏内 | GameScene 底栏 → **配置** 按钮 |

修改后写入 `game_config.json`，运行时实时读取。

### 科技树配表（策划向）

顶部菜单 `Tools → 科技树`（TechTreeEditorWindow）：
- **导入 / 导出 CSV**（一行一节点，Excel 可编辑）
- **烘焙 JSON**（运行时真相）
- ScriptableObject 资产可视化同步

---

## 数据与存档位置

运行时数据统一存放在 `Application.persistentDataPath`：

```
Windows:  %LOCALAPPDATA%/../LocalLow/DefaultCompany/42agent/
```

| 文件 | 说明 |
|---|---|
| `galaxy_agent_saves.db` | SQLite 存档（Agent 状态、基地仓库、游戏时间、LLM 配置、已解锁科技） |
| `game_config.json` | 游戏数值配置（首次运行自动生成默认值） |
| `tech_tree.json` | 科技树配置（首次运行自动生成内置 7 项） |

> **清空存档 / 重置配置**：退出游戏后删除上述目录内对应文件即可。

---

## 平台说明

当前**仅支持 Windows**，原因：`Assets/Plugins/SQLite/sqlite3.dll` 是 Windows 原生库（P/Invoke 调用）。

迁移到其他平台：在 `Assets/Plugins/SQLite/` 下放入对应平台原生库（macOS: `libsqlite3.dylib` / Linux: `libsqlite3.so`），并为各自的 `.meta` 设置正确平台。其余代码无平台耦合。

---

## 项目结构

```
Assets/
├─ Scenes/              # MainMenu / MapGeneration / GameScene（入口为 MainMenu）
├─ Scripts/             # 全部 C# 代码，命名空间 GalaxyAgent
│  ├─ AI/               # AgentBrain / AgentController（三层决策）
│  ├─ Map/              # MapGenerator / ChunkManager / CameraController
│  ├─ UI/               # GameHUD / RuntimeUIBuilder / 各面板（运行时自构建）
│  ├─ Tech/             # 科技树：数据模型 / Manager / Store / CSV 转换 / Asset
│  ├─ Config/           # GameConfig 数值系统
│  ├─ Database/         # SaveLoadManager / SQLite 封装
│  ├─ LLM/              # LLMManager / Client / Providers(Ollama) / PromptBuilder
│  └─ Core/             # Singleton / EventBus / 事件定义 / 常量
├─ Plugins/SQLite/      # sqlite3.dll（Windows 原生库）
└─ Editor/              # 编辑器扩展窗口（#if UNITY_EDITOR）
```

---

## 技术栈

| 项 | 说明 |
|---|---|
| 引擎 | Unity 6000.3.11f1，2D 正交（URP） |
| 语言 | C#，命名空间 `GalaxyAgent` |
| UI | 纯 UGUI，**运行时自构建**（无 prefab、无编辑器拖拽，统一 `RuntimeUIBuilder`） |
| 数据库 | SQLite，自带 `sqlite3.dll`，P/Invoke 封装（Schema v5） |
| LLM | 本地 Ollama，HTTP 调用 |
| 架构 | 自实现 `EventBus` + `Singleton<T>` 泛型基类 |
| 注释 | 全中文，每个文件含文件头说明 |
| 美术 | 暂无美术资源，全部用**纯色块**标识 |

---

 

## Git 提交说明

- 仓库已含标准 Unity `.gitignore`：忽略 `Library/` `Temp/` `Obj/` `Build/` `Logs/` 及 IDE 临时文件。
- `Assets/` 与 `Packages/` 全部纳入版本管理。

---

*v1.0.0「星际探索」——科技已点亮，文明行将启程。*
