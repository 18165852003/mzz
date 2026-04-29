# AGENTS.md
## Assistant response conventions

- Always respond in Simplified Chinese.
- Explain plans, diffs, risks, and test steps in Chinese.
- Keep code, commands, file paths, and API names in English.
- When modifying code, summarize changes in Chinese.


本文件面向 AI 编程助手，描述项目结构、构建方式、运行时架构与开发约定。阅读者被假设对该项目一无所知。

---

## 项目概述

- **项目名称**：VMDemo
- **项目类型**：Windows 桌面 WinForms 应用程序（`.NET Framework 4.8`）
- **UI 框架**：SunnyUI `3.9.3`（通过 `packages.config` 管理）
- **核心用途**：VisionMaster 4.2 视觉方案的二次开发演示程序，支持加载 `.sol` 方案文件、单次执行、连续执行（硬件触发）、图像结果显示及 TCP 通讯。
- **运行依赖**：本程序**不是自包含应用**，必须在本地安装 **VisionMaster 4.2**，并依赖其 SDK 程序集（`VM.Core.dll`、`VM.PlatformSDKCS.dll` 及大量 `IMVS*` / `VMControls*` 模块）。

---

## 解决方案与工程结构

- **解决方案文件**：`VMDemo.sln`（Visual Studio 2022 格式，Format Version 12.00）
- **唯一编译项目**：`VMDemo/VMDemo.csproj`
- **目标框架**：`.NET Framework 4.8`
- **输出类型**：`WinExe`
- **平台目标**：解决方案配置为 `Any CPU`，但项目属性中 `PlatformTarget` 固定为 `x64`（Debug 和 Release 均如此），因为 VisionMaster SDK 为 64 位。
- **应用程序清单**：`app.manifest` 已配置。

---

## 构建与运行命令

> 推荐在已配置 `MSBuild.exe` 和 `nuget` 环境变量的命令行（如 Visual Studio Developer Command Prompt）中执行。本项目使用 `packages.config`，**不应使用 `dotnet build`**。

```bash
# 还原 NuGet 包
nuget restore VMDemo.sln

# Debug 构建
MSBuild.exe VMDemo.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU"

# Release 构建
MSBuild.exe VMDemo.sln /t:Build /p:Configuration=Release /p:Platform="Any CPU"

# 运行（Debug 构建后）
VMDemo\bin\Debug\VMDemo.exe
```

- 本仓库**没有测试项目**，验证方式以解决方案能否成功编译为主。

---

## 依赖与外部程序集

### NuGet 包

| 包名 | 版本 | 说明 |
|------|------|------|
| SunnyUI | 3.9.3 | WinForms UI 控件库 |
| SunnyUI.Common | 3.9.3 | SunnyUI 公共依赖 |

### VisionMaster SDK 引用（关键）

以下两个核心库通过**绝对路径**引用，路径指向 VisionMaster 4.2 安装目录：

- `C:\Program Files\VisionMaster4.2.0\Development\V4.x\ComControls\Assembly\VM.Core.dll`
- `C:\Program Files\VisionMaster4.2.0\Development\V4.x\ComControls\Assembly\VM.PlatformSDKCS.dll`

此外，`VMDemo.csproj` 还引用了大量以名称方式引用的 GAC/机器全局程序集，包括：

- `VMControls.*`（渲染与界面接口）
- `IMVS*`（VisionMaster 各视觉模块的 C# 封装，如 `IMVSLineFindModuCs`、`IMVSCnnDetectModuCs` 等）
- `ImageCollect*ModuCs`、`SaveImageCs`、`TriggerModuleCs` 等辅助模块
- `Apps.UIHelper`、`FrontendUI.WPF`、`PresentationCore`、`PresentationFramework` 等辅助渲染库

**重要**：这些引用均设置 `<Private>False</Private>`，表示编译时不复制到输出目录，运行时必须依赖 VisionMaster 安装环境或相关程序已注册到 GAC。

---

## 代码组织结构

```
VMDemo/
├── Program.cs                      # 程序入口，单实例互斥锁
├── MainForm.cs / .Designer.cs      # 主窗体（SunnyUI UIForm），工具栏与日志面板
├── MainForm.resx
├── TCPForm.cs / .Designer.cs       # TCP 通讯配置弹窗（SunnyUI UIForm）
├── TCPForm.resx
├── ControlHoverSelectionManager.cs # 工具栏图标悬停高亮与 ToolTip 管理器（单例）
├── SingletonManager.cs             # ⚠️ 未包含在 csproj 中，是旧草稿，不要直接使用
├── App.config                      # VisionMaster 服务启动配置（StartServerByExe / ServerPath）
├── app.manifest                    # 应用程序清单
├── packages.config                 # NuGet 包配置
├── Properties/                     # 程序集信息、资源、设置
├── Contro/                         # 自定义显示控件
│   ├── ZoomPictureBox.cs           # 自定义图像框：支持滚轮缩放、鼠标平移、双击复位
│   ├── UserControl1.cs             # 1 宫格布局（1 个流程）
│   ├── UserControl2.cs             # 2 宫格布局（2 个流程）
│   └── UserControl4.cs             # 4 宫格布局（3 个及以上流程）
└── VM/                             # SingletonManager 真身（分部类，按功能拆分为多个文件）
    ├── SingletonManager.cs           # 核心字段、单例入口、执行状态标志
    ├── SingletonManager.Loading.cs   # 方案加载、显示布局创建、回调绑定
    ├── SingletonManager.Run.cs       # 单次执行与连续执行的流程调度
    ├── SingletonManager.Result.cs    # 结果读取、图像转 Bitmap、文字叠加
    ├── SingletonManager.Drawing.cs   # VM 图像格式转换、旋转矩形绘制、文字绘制
    ├── SingletonManager.Log.cs       # 应用级日志（历史缓存 + 事件通知）
    ├── SingletonManager.Tcp.cs       # TCP 服务端：监听、连接管理、消息收发、配置持久化
    └── SingletonManager.Dispose.cs   # 资源释放
```

---

## 关键模块说明

### Program.cs
- 使用 `[STAThread]` 标记的 WinForms 入口。
- 通过全局命名 `Mutex`（`"MyApp_UniqueId_12345"`）实现**单实例运行**，重复启动会弹出提示框并退出。

### MainForm.cs
- 继承自 `Sunny.UI.UIForm`。
- 工具栏包含四个功能按钮（通过 `UISymbolLabel` 实现）：
  1. **TCP 通讯**：打开 `TCPForm` 弹窗。
  2. **加载方案**：打开文件对话框，筛选 `.sol` 文件，调用 `SingletonManager.Instance.Load(...)`。
  3. **单次执行**：调用 `SingletonManager.Instance.Run()`。
  4. **连续执行**：切换启动/停止 `SingletonManager.Instance.StartContinuousRun()` / `StopContinuousRun()`。
- 底部 `ListBox` 为日志显示区：
  - 自定义绘制（`DrawItem`），错误日志（含"失败"、"异常"、"错误"、"报警"关键字）显示为**红色**，正常日志为**绿色**。
  - 通过 `SingletonManager.LogReceived` 事件接收日志，自动追加时间戳并滚动到底部。
- 窗体加载时自动尝试启动 TCP 服务端（`TryAutoStartTcpServer`）。
- 窗体关闭时停止 TCP 服务端并释放方案资源。

### ControlHoverSelectionManager
- 单例类，负责为工具栏控件注册**悬停高亮**效果（背景色变深）和 **ToolTip** 提示。
- 通过反射操作 SunnyUI 控件的 `FillColor` / `FillColor2` 属性。
- **重要**：由于弹窗或模态操作可能导致 `MouseLeave` 事件不触发，任何按钮点击后必须调用 `ForceRestoreColor(control)` 强制恢复颜色。

### ZoomPictureBox（Contro/ZoomPictureBox.cs）
- 继承自 `PictureBox`，但**不依赖默认 `Image` 显示机制**，完全通过重写 `OnPaint` 自行绘制。
- 功能：
  - `SetImage(Bitmap)`：设置图像，旧图像会被自动释放（**所有权转移**）。
  - 鼠标左键拖动平移。
  - 滚轮缩放（以鼠标位置为中心）。
  - 双击或控件尺寸变化时自适应控件大小并居中。
- **约束**：外部将 `Bitmap` 传入 `SetImage(...)` 后，**不得再复用或手动释放该 Bitmap**。

### SingletonManager（VM/ 目录下的分部类）

这是本项目的**核心引擎**，所有与 VisionMaster SDK 的交互都集中于此。

#### 1. 方案加载（SingletonManager.Loading.cs）
- `Load(string path, UIPanel hostPanel)`：
  - 调用 `VmSolution.Load(path)` 加载 `.sol` 方案。
  - 通过 `GetAllProcedureList()` 统计有效流程数（`nProcessID != 0`）。
  - 根据流程数选择显示布局：1 个 → `UserControl1`，2 个 → `UserControl2`，≥3 个 → `UserControl4`。
  - 递归收集用户控件内所有 `PictureBox` 并按 `Name` 排序，映射到流程索引。
  - 为每个有效流程绑定 `OnWorkEndStatusCallBack`（统一回调为 `OnWorkEnd`）。

#### 2. 执行调度（SingletonManager.Run.cs）
- **单次执行（`Run()`）**：
  - 流程 2/3/4（索引非 0）先**并行执行**，通过 `TaskCompletionSource<bool>`（`_completionSignals`）等待全部完成。
  - 流程 1（索引 0）在所有并行流程完成后**串行执行**，并作为最终主结果。
  - 单次执行使用 `_isRunning` 标志防止重叠触发。
- **连续执行（硬件触发）**：
  - `StartContinuousRun()`：切换回调为 `OnContinuousWorkEnd`，启用 `ContinuousRunEnable = true`。
  - `StopContinuousRun()`：关闭连续执行，恢复单次回调。
  - 连续模式下同样保持"先并行后串行"的时序逻辑，主流程（流程 1）等待并行流程完成或超时（`ContinuousTimeoutMs = 5000ms`）。
  - 每轮触发后自动重置 `_continuousSignals`，准备下一轮。

#### 3. 结果处理与图像渲染（SingletonManager.Result.cs + Drawing.cs）
- `DisplayProcedureResult(...)` 在回调中异步读取流程结果并显示：
  - 图像输出名固定为 `"Img"`，通过 `GetOutputImageV2` 读取。
  - ROI 矩形框输出名固定为 `"ROI"`，通过 `GetOutputBoxArray` 读取。
  - 整数输出名固定为 `"COUNT"`；主流程（索引 0）额外读取 `"out"`。
- **重试机制**：首次执行时 SDK 结果缓冲区可能未就绪，`ReadProcedureImageWithRetry` 最多重试 3 次，每次间隔 100ms。
- 图像格式转换（`ConvertToBitmap`）目前支持：
  - `VM_PIXEL_MONO_08`（8 位灰度）
  - `VM_PIXEL_RGB24_C3`（24 位彩色，自动交换 R/B 通道）
- 绘制功能：
  - 在图像上绘制带角度的矩形框（`DrawRotatedRectangles`）。
  - 在图像上叠加文字信息（匹配框个数、匹配框总数等）。
  - 灰度图需先通过 `EnsureDrawable` 转换为 24 位 RGB 才能用 `Graphics` 绘制。

#### 4. TCP 通讯（SingletonManager.Tcp.cs）
- 内建简易 **TCP 服务端**（基于 `TcpListener` / `TcpClient`）。
- 配置文件路径：`程序目录/config/tcp_config.json`，包含 `ServerIp`、`ServerPort`、`AutoStart`。
- 默认配置：`127.0.0.1:7777`，`AutoStart = true`。
- 启动后自动保存配置；配置校验失败时回退到默认配置。
- 连接策略：只保留**一个客户端**，新客户端接入时自动断开旧客户端。
- 主流程（流程 1）执行完成后，会自动调用 `SendTcpMessage("1")` 向已连接客户端发送结果通知。
- TCP 日志与应用级日志分离，但 TCP 日志会同时汇入应用级日志。

#### 5. 日志（SingletonManager.Log.cs）
- 通过 `AppendLog(string)` 写日志。
- 内部维护 `_logHistory` 列表（上限 200 条，超出时移除最早记录）。
- 通过 `LogReceived` 事件通知 `MainForm` 更新 UI。

---

## 运行时架构要点

### 执行时序
```
单次执行 / 连续执行的每一轮：
├─ 并行阶段：流程 2、3、4 同时触发 Run()
│   └─ 各自 OnWorkEnd / OnContinuousWorkEnd 回调 → 显示图像 → 发完成信号
└─ 串行阶段：流程 1 等待并行信号全部到达（或超时）后触发 Run()
    └─ 回调 → 显示图像 → 若为单次执行则标记整体完成
```

### 显示布局与流程映射
- `UserControl1` / `UserControl2` / `UserControl4` 中放置的 `PictureBox`（实际为 `ZoomPictureBox`）按控件名称排序后，依次映射到流程索引 0、1、2、3。
- **改变 PictureBox 的 Name 或顺序会直接影响流程到显示面板的映射关系**。

### 线程与同步
- `SingletonManager` 内部大量使用 `Task.Run` 和 `async/await` 将 SDK 回调工作转移到后台线程，避免阻塞 UI。
- UI 更新统一通过 `BeginInvoke` 或 `Invoke` 调度到主线程。
- 连续执行模式下使用 `_continuousSignalLock` 保护 `_continuousSignals` 字典。
- `_isRunning` 和 `_isContinuousRunning` 标记为 `volatile`，确保多线程可见性。

---

## 配置说明（App.config）

```xml
<appSettings>
  <!-- 启动服务形式：0=默认系统服务，1=exe 方式启动 -->
  <add key="StartServerByExe" value="0" />
  <!-- 服务绝对路径（value 为空时默认拉起系统服务） -->
  <add key="ServerPath" value="" />
</appSettings>
```

- 该配置控制 VisionMaster 后台服务的启动方式，与程序自身的 TCP 服务端无关。

---

## 开发约定与代码风格

- **语言**：C#，代码注释以**中文**为主。
- **命名空间**：项目根命名空间为 `VMDemo`；自定义控件在 `VMDemo.Contro`。
- **单例模式**：`SingletonManager` 和 `ControlHoverSelectionManager` 均使用 `Lazy<T>` 实现线程安全单例。
- **分部类（partial）**：`SingletonManager` 按功能拆分为多个文件，禁止在同一文件中混入无关逻辑。
- **UI 缩放**：`MainForm` 和所有用户控件均显式设置 `AutoScaleMode = AutoScaleMode.None`，且 `MainForm` 设置 `ZoomScaleDisabled = true`、`ZoomScaleRect = Rectangle.Empty`，**严禁开启自动缩放**，以防止 SunnyUI 运行时布局漂移。
- **资源释放**：
  - `ZoomPictureBox.SetImage(...)` 接管 Bitmap 所有权，调用方不得再次释放。
  - `SingletonManager.Dispose()` 时会先停止 TCP 服务端和连续执行，再解绑回调、清空映射、释放方案实例。
- **异常处理**：对 SDK 结果读取、TCP 网络操作、流程停止等操作均包裹 try-catch，避免单点异常导致程序崩溃。

---

## 测试策略

- **本项目无单元测试项目**，验证方式以以下步骤为准：
  1. `nuget restore VMDemo.sln` 成功。
  2. `MSBuild.exe VMDemo.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU"` 成功（无编译错误）。
  3. 在已安装 VisionMaster 4.2 的环境中运行 `VMDemo.exe`。
  4. 手动验证：加载 `.sol` 方案 → 单次执行 → 图像是否正常显示 → ROI 和文字是否叠加正确 → 连续执行模式切换是否正常 → TCP 客户端能否收到主流程完成通知。

---

## 安全与注意事项

- **外部依赖路径硬编码**：`VMDemo.csproj` 中 `VM.Core.dll` 和 `VM.PlatformSDKCS.dll` 的 `HintPath` 是绝对路径 `C:\Program Files\VisionMaster4.2.0\...`。如果 VisionMaster 安装路径不同，需要手动修改 `.csproj` 或确保路径存在。
- **GAC 依赖**：大量 `IMVS*` 模块依赖机器全局程序集缓存，在新机器上编译/运行前需确保 VisionMaster 已正确安装并注册。
- **TCP 服务端无鉴权**：内建 TCP 服务端为明文通讯，无身份验证、无加密，仅用于局域网内部通知，**不建议直接暴露于公网**。
- **单实例互斥锁名称固定**：`"MyApp_UniqueId_12345"`，如需支持多开需修改此名称。
- **x64 强制**：虽然解决方案平台是 `Any CPU`，但 `PlatformTarget` 固定为 `x64`。不要在 32 位环境或强制 x86 模式下运行。

---

## 已知遗留问题与易错点

- `VMDemo/SingletonManager.cs`（根目录下）是一份**旧草稿**，**未被包含在 `VMDemo.csproj` 中**。所有业务逻辑都在 `VMDemo/VM/SingletonManager*.cs` 中，切勿混淆。
- 渲染逻辑硬编码依赖 VisionMaster 输出项名称：`"Img"`、`"ROI"`、`"COUNT"`、主流程的 `"out"`。如果方案中输出项名称不同，显示会异常。
- TCP 工具栏按钮在主界面已有完整实现（打开 `TCPForm`），旧 AGENTS.md 中提到的"空实现"已更新。

---

## 相关文件

- `CLAUDE.md`：包含相同项目指引的英文版本；如修改本文件，请同步更新 `CLAUDE.md` 以保持信息一致。
