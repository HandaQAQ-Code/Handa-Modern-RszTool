# Handa-Modern-RszTool 🎮✨

> **生化危机 4 重制版 (RE4R) 全流程改版专家与 RE ENGINE 现代化资源编辑套件**  
> Modern RE ENGINE Resource Editor powered by WPF-UI & Fluent Design (Mica, WinUI 3).

[![.NET 8](https://img.shields.io/badge/.NET-8.0--windows-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![.NET Framework](https://img.shields.io/badge/.NET-Framework%204.7.2-brightgreen.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net472)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/HandaQAQ-Code/Handa-Modern-RszTool)](https://github.com/HandaQAQ-Code/Handa-Modern-RszTool/releases)

---

## 🌟 核心特色 (Key Features)

### 1. 现代化 WinUI 3 & Fluent 视觉体验
- **Mica / Acrylic 云母云母原生背景**：适配 Windows 11/10 现代视觉规范，丝滑沉浸。
- **全动态多主题系统**：
  - 🎨 **WinUI** (经典浅紫深灰)
  - 🌙 **Dark** (专业暗黑开发者风格)
  - ☀️ **Light** (清爽明亮界面)
  - 🌸 **Makaro** (温润马卡龙配色)
- **主题强调色隔离**：彻底阻断系统异常黄色强调色渗透，保证全控件配色统一高级。
- **现代化对话框与弹窗**：全面替换原生 Win32 弹窗，采用圆角阴影、状态徽章与 Fluent `ui:Button` 的现代弹窗体系。
- **人体工学层级树**：统一规范每层 30px 高度，配备展开平滑动画，多选批量支持，层级不再一高一低。
- **全局字体缩放**：工具栏一键滑块实时调整全局字号，兼顾高分屏与紧凑视效。

---

### 2. 生化危机 4 重制版 (RE4R) 专精改版模块

#### 🏷️ 子物体 ID / ContextID / StageID 批量分配与递增
- 一键扫描选中物体及其后代结构中的 `ContextID`、`StageID` 与 `_DropItemID`。
- 自动递增与步长分配：支持指定 Group、起始 Index 与步长，自动防止 ID 冲突并支持溢出回滚。
- 组件原生物体直接多选批量应用，支持同名子对象智能聚合折叠。

#### 🚩 UVAR v3 Flag 管理与 GUID 触发器绑定
- 原生内置 `*.uvar.3` 文件解析与交互视图，无须反复切换标签页。
- 批量生成递增数字 Flag（如 `Location47_000` 自动延续前缀与编号）。
- 实时追踪全局 Flag 的 `Used` / `Unused` 使用状态。
- GUID 字段一键关联 UVAR 库选择器与随机生成器。

#### 🎭 敌人复制与 MontageID 动画姿态随机化
- 复制/多重复制敌人时自动激活 MontageID 随机化面板。
- 自动提取特定敌人的所有专属 MontageID 动画姿态枚举。
- 支持混合自定义附加 ID 池（十进制/十六进制）。
- 具备预设记忆与管理体系（支持保存、覆盖、恢复默认预设）。

#### 🎁 掉落物比例配比与随机化 (`_DropItemID`)
- 严格针对 `_DropItemID` 字段进行安全配比。
- 支持按任意比例（如 `7:3`、`70:30` 或多物品混合比例）在指定敌人群体中离散分配掉落。
- 采用最大余数配额取整算法与随机种子打散，支持实时应用前预览。

#### 📐 Transform 坐标批量编辑与佣兵生成点导入
- 批量调整/偏移物体的坐标（X/Y/Z/W）、旋转四元数、缩放比。
- 支持从游戏生成的佣兵模式生成点 JSON 直接导入并自动生成或填充场景 Transform。

---

### 3. 多语言支持与枚举智能汉化
- 深度集成 RE4R 枚举库（2000+ 原生类型）。
- 全新改进的本地化匹配引擎：精准读取 `re4_enum.zh-CN.json`，在 ItemID、WeaponID、StatusEffectID 下拉框中直接显示中文译名（如 `it_sm70_500_00_0 (手枪弹药)`）。
- 自动双路径解析与语言回退，即使外部调用也能无损加载汉化。

---

### 4. 极致稳定性与向下兼容
- **健壮配置加载**：向下兼容所有旧版本 `RszTool.App.SaveData.json`（支持整数/字符串混合枚举与旧字段），配置损坏自动备份 `.corrupted.bak` 恢复，绝不崩溃。
- **双框架支持**：原生编译输出现代 `.NET 8 (net8.0-windows)` 与向下兼容的 `.NET Framework 4.7.2`。

---

## 🚀 快速上手 (Quick Start)

### 下载运行
1. 从 [Releases](https://github.com/HandaQAQ-Code/Handa-Modern-RszTool/releases) 页面下载最新发布的二进制压缩包。
2. 解压后，双击运行根目录下的 `RszTool.App.exe` 或 `Start-ID-Flag-Editor.cmd`。
3. 可将任何 `.scn` / `.pfb` / `.user` / `.uvar` 文件直接拖拽入主窗口打开。

### 从源码编译 (Build from Source)
要求安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 及 Visual Studio 2022 (包含 .NET 桌面开发组件)。

```bash
# 克隆仓库
git clone git@github.com:HandaQAQ-Code/Handa-Modern-RszTool.git
cd Handa-Modern-RszTool

# 还原依赖并编译 Release
dotnet build RszTool.sln -c Release

# 发布独立应用程序
dotnet publish RszTool.App/RszTool.App.csproj -c Release -f net8.0-windows -o ./publish/Modern-RszTool
```

---

## 📁 目录结构 (Repository Structure)

```text
├── RszTool/                  # RE ENGINE RSZ 核心解析与文件读写库 (.scn, .pfb, .user, .uvar)
├── RszTool.App/              # 现代 WPF-UI 桌面客户端应用程序
│   ├── ViewModels/           # MVVM 视图模型与业务逻辑
│   ├── Views/                # XAML 界面、主题字典与自定义控件
│   ├── Themes/               # WinUI / Dark / Light / Makaro 调色板定义
│   └── Data/Enums/           # 游戏原生枚举与中文对照库 (re4_enum.json / re4_enum.zh-CN.json)
├── RszTool.Test/             # 单元验证与自动化批处理测试工程
├── Docs/                     # 详细改版操作指南与配置示例
└── publish/                  # 生成发布产物目录 (已忽略版本控制)
```

---

## 🤝 鸣谢与致敬 (Credits & Acknowledgments)

- 核心架构基于 [RszTool](https://github.com/czastack/RszTool) (by @czastack) 深度演进重构。
- 灵感来自 alphazolam 开发的 [RE_RSZ](https://github.com/alphazolam/RE_RSZ) 与 [EMV-Engine](https://github.com/alphazolam/EMV-Engine)。
- UI 交互基于现代化 [WPF-UI](https://github.com/lepoco/wpfui) 库。

---

## 📜 开源许可 (License)

本项目基于 [MIT 许可证](LICENSE) 开源。
