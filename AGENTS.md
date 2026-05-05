# AGENTS.md

## 助手回复约定

- 始终使用简体中文回复。
- 计划、改动说明、风险说明、验证步骤都用中文解释。
- 代码、命令、文件路径、类名、方法名、API 名称保持英文原样。
- 修改代码后，用中文总结改动点。
- 修改文件时始终使用 `apply_patch`，不要用普通重定向或脚本直接覆盖文件。

---

## 关联 Skill

本项目已整理为 Codex skill：

- Skill 显示名称：`VMDemo VisionMaster`
- Skill 名称：`$vmdemo-visionmaster`
- Skill 文件：`C:\Users\33826\.codex\skills\vmdemo-visionmaster\SKILL.md`

后续需要更新本项目说明、解释执行链路、修改 `RoundId` / TCP / `SingletonManager` 相关代码时，可以在请求中显式写：

```text
使用 $vmdemo-visionmaster 更新 AGENTS.md
```

或：

```text
使用 $vmdemo-visionmaster 检查当前项目结构并修改 RoundId 逻辑
```

---

## 项目概述

- **项目名称**：`VMDemo`
- **项目类型**：Windows 桌面 WinForms 应用程序，目标框架为 `.NET Framework 4.8`
- **UI 框架**：`SunnyUI 3.9.3`，通过 `packages.config` 管理。
- **核心用途**：基于 Hikrobot VisionMaster 4.2 SDK 的二次开发演示程序，用于加载 `.sol` 方案、执行多流程视觉检测、显示图像/ROI/文字叠加结果，并通过 TCP 与外部客户端按 `RoundId` 协议联动。
- **运行依赖**：本程序不是自包含应用，编译和运行都依赖本机已安装 VisionMaster 4.2 及其 SDK/GAC 程序集。

---

## 解决方案与工程结构

- **解决方案文件**：`VMDemo.sln`
- **唯一编译项目**：`VMDemo/VMDemo.csproj`
- **目标框架**：`.NET Framework 4.8`
- **输出类型**：`WinExe`
- **平台目标**：解决方案平台是 `Any CPU`，但项目 `PlatformTarget` 固定为 `x64`，不要改成 `x86`。
- **当前有效业务代码目录**：`VMDemo/Core/SingletonManager*.cs`
- **旧草稿文件**：`VMDemo/SingletonManager.cs` 未包含在 `VMDemo.csproj` 中，除非项目文件发生变化，否则不要把它当作有效实现入口。

---

## 构建与验证命令

本项目使用 `packages.config`，不要优先使用 `dotnet build`。在这台机器上，已验证的构建路径是：

```bash
nuget restore VMDemo.sln
dotnet msbuild VMDemo.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU"
```

如果要使用传统 Visual Studio MSBuild，也可以在正确环境中执行：

```bash
MSBuild.exe VMDemo.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU"
MSBuild.exe VMDemo.sln /t:Build /p:Configuration=Release /p:Platform="Any CPU"
```

本仓库没有单元测试项目，主要验证方式是成功编译，并在安装了 VisionMaster 4.2 的机器上手动运行 `VMDemo/bin/Debug/VMDemo.exe`。

---

## 依赖与外部程序集

### NuGet 包

| 包名 | 版本 | 说明 |
| --- | --- | --- |
| `SunnyUI` | `3.9.3` | WinForms UI 控件库 |
| `SunnyUI.Common` | `3.9.3` | SunnyUI 公共依赖 |

### VisionMaster SDK 引用

关键 SDK 通过绝对路径引用：

- `C:\Program Files\VisionMaster4.2.0\Development\V4.x\ComControls\Assembly\VM.Core.dll`
- `C:\Program Files\VisionMaster4.2.0\Development\V4.x\ComControls\Assembly\VM.PlatformSDKCS.dll`

项目还引用大量 `IMVS*`、`VMControls.*`、`ImageCollect*ModuCs`、`SaveImageCs`、`TriggerModuleCs` 等 VisionMaster 全局程序集。多数引用设置了 `<Private>False</Private>`，运行时依赖本机 VisionMaster 安装环境或 GAC 注册状态。

---

## 当前代码组织

```text
VMDemo/
├── Program.cs
├── MainForm.cs / MainForm.Designer.cs / MainForm.resx
├── TCPForm.cs / TCPForm.Designer.cs / TCPForm.resx
├── SingletonManager.cs                 # 旧草稿，未编译进项目
├── App.config
├── app.manifest
├── packages.config
├── Properties/
├── CustomControls/
│   ├── ZoomPictureBox.cs
│   ├── UserControl1.cs / .Designer.cs / .resx
│   ├── UserControl2.cs / .Designer.cs / .resx
│   └── UserControl4.cs / .Designer.cs / .resx
└── Core/
    ├── SingletonManager.cs
    ├── SingletonManager.Loading.cs
    ├── SingletonManager.Round.cs
    ├── SingletonManager.Run.cs
    ├── SingletonManager.Result.cs
    ├── SingletonManager.Drawing.cs
    ├── SingletonManager.Log.cs
    ├── SingletonManager.Tcp.cs
    ├── SingletonManager.Dispose.cs
    └── SingletonManager.HoverText.cs
```

注意：目录名是 `CustomControls`，但自定义控件命名空间仍是 `VMDemo.Contro`，引用时保持现状，不要随手重命名。

---

## 主界面行为

### `MainForm.cs`

- 继承 `Sunny.UI.UIForm`。
- 构造函数中关闭 WinForms 和 SunnyUI 自动缩放：
  - `AutoScaleMode = AutoScaleMode.None`
  - `ZoomScaleDisabled = true`
  - `ZoomScaleRect = Rectangle.Empty`
- 当前工具栏实际注册 3 个按钮：
  1. `uiSymbolLabel1`：打开 `TCPForm`
  2. `uiSymbolLabel2`：加载 `.sol` 方案并调用 `SingletonManager.Instance.Load(...)`
  3. `uiSymbolLabel3`：执行单次检测，调用 `SingletonManager.Instance.Run()`
- 单次执行前会做两个 UI 门控：
  - 未加载方案时提示“请先加载方案”
  - 没有待执行 `RoundId` 时提示“请先由 TCP 客户端发送 RoundId”
- `MainForm_Load` 会订阅 `SingletonManager.LogReceived`，回放历史日志，并调用 `TryAutoStartTcpServer()`。
- `MainForm_FormClosed` 会取消日志订阅、停止 TCP 服务端，并释放已加载方案资源。
- 底部日志 `ListBox` 使用自绘：包含“失败 / 异常 / 错误 / 报警”的日志显示红色，其余日志显示绿色。

### 悬浮提示与高亮

当前悬浮提示逻辑在 `VMDemo/Core/SingletonManager.HoverText.cs` 中，通过 `RegisterHoverText(...)` 为工具栏控件注册：

- 原生 WinForms `ToolTip`
- 鼠标悬停背景色
- 单击后的选中背景色

后续调整工具栏交互时，优先沿用这个分部类，不要重新引入一套平行的 hover 管理器。

---

## `SingletonManager` 分部类职责

### `SingletonManager.cs`

- 提供线程安全单例 `SingletonManager.Instance`。
- 保存流程到显示区域的映射 `_procedureIndexMap`。
- 保存当前显示控件、图片框列表、流程名称列表。
- 保存单次执行状态：
  - `_isRunning`
  - `_singleRunLock`
  - `_singleRunRoundSeed`
  - `_activeSingleRunRound`
- `IsLoaded` 通过 `VmSolution.Instance` 和有效流程数量判断方案是否已加载。

### `SingletonManager.Loading.cs`

- `Load(string path, UIPanel hostPanel)` 加载 `.sol` 方案。
- 通过 `VmSolution.Instance.GetAllProcedureList()` 获取有效流程。
- 根据流程数选择 `UserControl1`、`UserControl2` 或 `UserControl4`。
- 递归收集用户控件中的 `PictureBox`，按 `Name` 排序后映射到流程索引。
- 为每个有效流程绑定 `OnWorkEndStatusCallBack += OnWorkEnd`。

### `SingletonManager.Run.cs`

当前执行模型是 **RoundId 门控的一次性单次执行**：

- 点击单次执行后，先调用 `TryConsumePendingRoundId(...)` 消费 TCP 客户端下发的 `RoundId`。
- 所有有效流程同级并行触发 `Run()`，不再按“流程 2/3/4 并行、流程 1 串行”的旧模式执行。
- 每个流程完成后进入 `OnWorkEnd(...)` 回调。
- 回调中异步调用 `CaptureProcedureResultSnapshot(...)`，立即复制图像和输出值，避免 SDK 后续执行覆盖结果缓存。
- 所有流程快照收齐后，`FinalizeSingleRunRound(...)` 统一显示图像、写日志、发送 TCP `DONE` 消息。
- `RunAbsoluteTimeoutMs = 1000`，如果某些流程没有回调，会超时返回 `DONE|RoundId|NG|原因`。
- 不论成功、失败、异常还是超时，最后都会调用 `ClearCurrentRoundId(...)` 清空当前执行中的 `RoundId`，但不会从已使用历史中删除该 `RoundId`。

### `SingletonManager.Round.cs`

负责 `RoundId` 状态机和单次使用规则：

- `_pendingRoundId`：TCP 客户端已下发、等待操作员点击单次执行。
- `_currentRoundId`：已经被单次执行消费，当前正在执行。
- `_usedRoundIds`：内存中的已使用集合。
- `round_history.json`：程序重启后仍要防重复的持久化历史。
- `TryAcceptRoundId(...)`：TCP 收到 `ROUND|...` 时校验并接收。
- `TryConsumePendingRoundId(...)`：点击单次执行时消费，并立即写入已使用历史。
- `ClearCurrentRoundId(...)`：一轮结束后只清空当前执行状态，不回滚已使用状态。

关键语义：`RoundId` 在“开始执行消费”时就永久视为已使用，不是等 OK 后才保存。即使后续 NG、超时或程序崩溃，也不能再次使用同一个 `RoundId`。

### `SingletonManager.Tcp.cs`

负责内建 TCP 服务端：

- 默认配置：`127.0.0.1:7777`，`AutoStart = true`
- 配置文件：程序输出目录下 `config/tcp_config.json`
- 只保留一个客户端，新客户端接入时断开旧客户端。
- 支持客户端一次发送多行命令。
- 当前协议：
  - 客户端下发：`ROUND|RoundId`
  - 接收成功：`ACK|RoundId|READY`
  - 忙碌或已有待执行：`BUSY|RoundId`
  - 已使用：`ERR|ROUND_USED|RoundId`
  - 格式错误：`ERR|BAD_ROUND|RoundId` 或 `ERR|BAD_COMMAND`
  - 执行完成：`DONE|RoundId|OK`
  - 执行失败：`DONE|RoundId|NG|原因`
- `SendTcpMessage(...)` 不会自动追加换行，如客户端协议需要换行，需要双方显式约定。

### `SingletonManager.Result.cs`

负责读取输出、绘制叠加文字、显示图像：

- 图像输出名固定为 `"Img"`，通过 `GetOutputImageV2("Img")` 读取。
- ROI 输出名固定为 `"ROI"`，通过 `GetOutputBoxArray("ROI")` 读取。
- 数量输出名固定为 `"COUNT"`，通过 `ReadFirstOutputInt(...)` 读取。
- `CaptureProcedureResultSnapshot(...)` 是新增流程输出读取的第一落点。
- `BuildSnapshotTextItems(...)` 是把新输出显示到图像叠加文字中的落点。
- `UpdatePictureBoxImage(...)` 最终调用 `ZoomPictureBox.SetImage(...)`，位图所有权会转移给控件。

### `SingletonManager.Drawing.cs`

- 负责 VisionMaster 图像数据转 `Bitmap`。
- 支持 `VM_PIXEL_MONO_08` 和 `VM_PIXEL_RGB24_C3`。
- 负责绘制旋转矩形 ROI 和图像文字。

### `SingletonManager.Log.cs`

- `AppendLog(string)` 写入应用日志。
- 内部保留最多 200 条历史日志。
- 通过 `LogReceived` 通知主界面追加显示。

### `SingletonManager.Dispose.cs`

- 停止 TCP 服务端。
- 解绑流程回调。
- 清空流程映射、显示控件和方案实例。
- 释放时不要恢复或重启连续执行逻辑，当前代码已没有连续执行功能。

---

## `ZoomPictureBox` 约束

文件：`VMDemo/CustomControls/ZoomPictureBox.cs`

- 继承 `PictureBox`，但不依赖默认 `Image` 显示机制，而是重写 `OnPaint` 自绘。
- 支持鼠标左键拖动平移、滚轮缩放、双击复位、尺寸变化自适应。
- `SetImage(Bitmap image)` 会接管传入 `Bitmap` 的所有权，并释放上一张图。
- 调用方把 `Bitmap` 传给 `SetImage(...)` 后，不要再次复用或手动释放该对象。

---

## 配置文件

### `App.config`

```xml
<appSettings>
  <add key="StartServerByExe" value="0" />
  <add key="ServerPath" value="" />
</appSettings>
```

该配置控制 VisionMaster 后台服务启动方式，与本程序内建 TCP 服务端无关。

### TCP 配置

- 路径：`程序输出目录/config/tcp_config.json`
- 字段：`ServerIp`、`ServerPort`、`AutoStart`、`LastSavedTime`
- 保存 TCP 配置时会自动设置 `AutoStart = true`。

### RoundId 历史

- 路径：`程序输出目录/config/round_history.json`
- 字段：`UsedRoundIds`、`LastSavedTime`
- 首次使用 `RoundId` 状态时懒加载。
- `TryConsumePendingRoundId(...)` 消费成功后立即保存。

---

## 开发约定

- C# 注释以中文为主，协议、状态机、持久化、线程同步相关逻辑尤其要保留维护导向的中文注释。
- 保持 `SingletonManager.XXX.cs` 分部类组织方式，不要把无关逻辑混到单个大文件里。
- 修改 `SingletonManager` 前先确认 `VMDemo.csproj` 当前包含的是 `VMDemo/Core/SingletonManager*.cs`。
- UI 缩放设置不要随意开启，避免 SunnyUI 运行时布局漂移。
- TCP 服务端无鉴权、无加密，只适合内网或本地调试，不要建议直接暴露到公网。
- 如果搜索“连续执行”相关遗留内容，要排除 `bin/**`、`obj/**` 等构建输出。
- 当前源码里部分旧文件可能存在中文编码显示异常；如果要修正文案或注释，优先使用 UTF-8 保存，并用构建验证确认没有破坏语法。

---

## 常见修改入口

- **改 TCP 协议解析或回复**：`VMDemo/Core/SingletonManager.Tcp.cs`
- **改 RoundId 接收、消费、持久化规则**：`VMDemo/Core/SingletonManager.Round.cs`
- **改单次执行调度、超时、归并、DONE 消息**：`VMDemo/Core/SingletonManager.Run.cs`
- **增加读取流程输出**：先看 `CaptureProcedureResultSnapshot(...)`，再看 `ReadFirstOutputInt(...)` / `TryReadFirstOutputInt(...)`
- **增加图像叠加文字**：`BuildSnapshotTextItems(...)`
- **改图像绘制或格式转换**：`VMDemo/Core/SingletonManager.Drawing.cs`
- **改主界面按钮和日志显示**：`VMDemo/MainForm.cs` 和 `VMDemo/MainForm.Designer.cs`
- **改悬浮提示和高亮**：`VMDemo/Core/SingletonManager.HoverText.cs`
- **改图片框交互**：`VMDemo/CustomControls/ZoomPictureBox.cs`

---

## 测试策略

优先按以下顺序验证：

1. `nuget restore VMDemo.sln`
2. `dotnet msbuild VMDemo.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU"`
3. 在安装了 VisionMaster 4.2 的环境中运行 `VMDemo/bin/Debug/VMDemo.exe`
4. 手动验证：
   - 启动后 TCP 服务端自动监听
   - TCP 客户端发送 `ROUND|R001`
   - 主界面点击“单次执行”
   - 四路流程图像正常显示
   - ROI 和文字叠加正确
   - 客户端收到 `DONE|R001|OK` 或 `DONE|R001|NG|原因`
   - 重复发送已使用的 `RoundId` 返回 `ERR|ROUND_USED|RoundId`

---

## 已知遗留点

- `VMDemo/SingletonManager.cs` 是旧草稿，不在当前项目编译项里。
- 当前有效实现目录是 `VMDemo/Core`，不是旧文档里提到的 `VMDemo/VM`。
- 当前项目没有连续执行入口，旧的 `StartContinuousRun()`、`StopContinuousRun()`、`uiSymbolLabel4` 相关说明不再适用。
- 输出项名称 `"Img"`、`"ROI"`、`"COUNT"` 是硬编码约定，VisionMaster 方案内输出名不一致会导致显示或统计异常。
