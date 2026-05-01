# 四相机测量 RoundId 执行逻辑说明

## 1. 当前代码整体执行流程

当前程序的执行链路如下：

```text
加载方案
  ↓
TCP 客户端发送 ROUND|R001
  ↓
程序保存 PendingRoundId，并回复 ACK|R001|READY
  ↓
点击“单次执行”
  ↓
程序消费 PendingRoundId，变成 CurrentRoundId
  ↓
四个流程并行 Run()
  ↓
四个流程回调 OnWorkEnd
  ↓
分别读取图像、ROI、COUNT，形成快照
  ↓
四路快照收齐
  ↓
统一显示图像
  ↓
统一发送 DONE|R001|OK/NG
  ↓
清空 CurrentRoundId
```

## 2. 方案加载阶段

入口文件：

```text
VMDemo/Core/SingletonManager.Loading.cs
```

入口方法：

```csharp
public void Load(string path, UIPanel uI2)
```

执行内容：

1. 清空旧流程映射。
2. 加载 `.sol` 方案。
3. 创建 1/2/4 宫格显示布局。
4. 绑定每个有效流程的执行结束回调。

关键代码逻辑：

```csharp
procedure.OnWorkEndStatusCallBack += OnWorkEnd;
_procedureIndexMap[procedure] = pictureIndex;
_procedureNames.Add(processName);
```

含义：

- `OnWorkEnd` 是每个流程执行完成后的统一回调入口。
- `_procedureIndexMap` 保存流程对象和画面索引的关系。
- `_procedureNames` 保存流程名称，用于日志和图像文字叠加。

## 3. TCP 接收 RoundId 阶段

入口文件：

```text
VMDemo/Core/SingletonManager.Tcp.cs
```

客户端发送：

```text
ROUND|R001
```

接收入口：

```csharp
ReceiveClientLoop(...)
```

收到 TCP 数据后调用：

```csharp
HandleTcpClientMessage(message);
```

协议解析方法：

```csharp
TryParseRoundCommand(command, out roundId);
```

当前只支持：

```text
ROUND|RoundId
```

如果解析成功，会调用：

```csharp
TryAcceptRoundId(roundId, out errorMessage);
```

成功时保存为 `_pendingRoundId`，并回复：

```text
ACK|R001|READY
```

如果当前已有待执行或正在执行的 `RoundId`，回复：

```text
BUSY|R001
```

格式错误时回复：

```text
ERR|BAD_COMMAND
```

## 4. RoundId 状态管理

入口文件：

```text
VMDemo/Core/SingletonManager.Round.cs
```

核心字段：

```csharp
private readonly object _roundLock = new object();
private string _pendingRoundId = null;
private string _currentRoundId = null;
```

含义：

- `_pendingRoundId`：TCP 客户端已经下发，但还没有点击“单次执行”的 RoundId。
- `_currentRoundId`：已经被“单次执行”消费，当前正在执行中的 RoundId。

关键方法：

```csharp
public bool HasPendingRoundId { get; }
private bool TryAcceptRoundId(string roundId, out string errorMessage);
private bool TryConsumePendingRoundId(out string roundId, out string errorMessage);
private void ClearCurrentRoundId(string roundId);
```

状态变化：

```text
收到 ROUND|R001
  ↓
_pendingRoundId = R001

点击单次执行
  ↓
_currentRoundId = R001
_pendingRoundId = null

执行结束
  ↓
_currentRoundId = null
```

## 5. 单次执行入口

按钮入口文件：

```text
VMDemo/MainForm.cs
```

按钮方法：

```csharp
uiSymbolLabel3_Click(...)
```

点击“单次执行”时，先检查：

```csharp
if (!SingletonManager.Instance.IsLoaded)
```

再检查：

```csharp
if (!SingletonManager.Instance.HasPendingRoundId)
```

如果没有 TCP 客户端发来的 `RoundId`，提示：

```text
请先由 TCP 客户端发送 RoundId
```

通过检查后调用：

```csharp
SingletonManager.Instance.Run();
```

## 6. 四流程执行与归并

入口文件：

```text
VMDemo/Core/SingletonManager.Run.cs
```

入口方法：

```csharp
public async void Run()
```

执行顺序：

1. 检查 `_isRunning`，防止重复执行。
2. 检查是否已加载方案。
3. 调用 `TryConsumePendingRoundId(...)` 消费待执行 RoundId。
4. 获取当前所有有效流程。
5. 创建当前轮次对象 `SingleRunRound`。
6. 四个流程依次调用 `Run()`，但归属于同一轮。
7. 等待所有流程回调。
8. 成功则统一显示并回传 TCP。
9. 超时或异常则回传 `DONE|RoundId|NG|原因`。
10. 最后清空 `_currentRoundId`。

创建轮次：

```csharp
round = CreateSingleRunRound(procedures, externalRoundId);
```

流程启动：

```csharp
item.Key.Run();
```

这里的 `externalRoundId` 就是 TCP 客户端发来的 `RoundId`。

## 7. 流程回调与结果快照

每个流程执行完成后，SDK 会触发：

```csharp
OnWorkEnd(object sender, EventArgs e)
```

位置：

```text
VMDemo/Core/SingletonManager.Run.cs
```

`OnWorkEnd` 会判断当前流程是否属于活动轮次，然后异步读取快照：

```csharp
CaptureSingleRunSnapshotAsync(round, procedure, pictureIndex);
```

真正读取流程结果的位置是：

```csharp
CaptureProcedureResultSnapshot(int roundId, VmProcedure procedure, int pictureIndex)
```

当前读取内容：

```csharp
ImageBaseData_V2 imageBase = await ReadProcedureImageWithRetry(procedure, pictureIndex);
snapshot.Rects = ReadProcedureRois(procedure);
snapshot.CountValue = ReadFirstOutputInt(procedure, "COUNT");
snapshot.Bitmap = ConvertToBitmap(imageBase);
```

含义：

- `Img`：读取图像。
- `ROI`：读取矩形框。
- `COUNT`：读取整数输出。
- `Bitmap`：把 VisionMaster 图像转换成 WinForms 可显示的位图。

## 8. 四路结果完成后做什么

四路快照全部写入后，会进入：

```csharp
FinalizeSingleRunRound(SingleRunRound round)
```

位置：

```text
VMDemo/Core/SingletonManager.Run.cs
```

执行内容：

1. 按画面索引排序四路结果。
2. 成功的流程调用 `DisplayProcedureSnapshot(snapshot)` 显示图像。
3. 失败的流程写日志。
4. 统计成功数量。
5. 调用 `BuildRoundDoneMessage(...)` 构建 TCP 完成消息。
6. 调用 `SendTcpMessage(...)` 回传 TCP 客户端。

成功回传：

```text
DONE|R001|OK
```

失败回传：

```text
DONE|R001|NG|流程 Camera2 结果读取失败
```

## 9. 想增加读取流程输出，应该改哪里

核心位置：

```text
VMDemo/Core/SingletonManager.Run.cs
```

方法：

```csharp
CaptureProcedureResultSnapshot(...)
```

这里是每个流程执行完成后集中读取输出的地方。

当前代码已经读取：

```csharp
snapshot.Rects = ReadProcedureRois(procedure);
snapshot.CountValue = ReadFirstOutputInt(procedure, "COUNT");
```

如果要读取新的整数输出，比如 `M1`，建议分三步。

### 第一步：在快照类里加字段

位置：

```text
VMDemo/Core/SingletonManager.Run.cs
```

类：

```csharp
private sealed class ProcedureResultSnapshot
```

新增：

```csharp
public int M1Value { get; set; } // 当前流程输出的 M1 测量值。
```

### 第二步：在 CaptureProcedureResultSnapshot 中读取

位置：

```csharp
CaptureProcedureResultSnapshot(...)
```

新增：

```csharp
snapshot.M1Value = ReadFirstOutputInt(procedure, "M1");
```

### 第三步：如果要显示到图像上，改 BuildSnapshotTextItems

位置：

```text
VMDemo/Core/SingletonManager.Result.cs
```

方法：

```csharp
BuildSnapshotTextItems(ProcedureResultSnapshot snapshot)
```

新增：

```csharp
items.Add(Tuple.Create($"M1：{snapshot.M1Value}", new VM.PlatformSDKCS.PointF(10, y)));
y += 130;
```

## 10. 当前已有的输出读取工具

文件：

```text
VMDemo/Core/SingletonManager.Result.cs
```

已有方法：

```csharp
ReadProcedureImageWithRetry(...)  // 读取 Img
ReadProcedureRois(...)            // 读取 ROI
ReadFirstOutputInt(...)           // 读取 COUNT / out 这类 int 输出
TryReadFirstOutputInt(...)        // 实际调用 procedure.ModuResult.GetOutputInt(...)
```

如果 VisionMaster 输出是整数，直接复用：

```csharp
ReadFirstOutputInt(procedure, "输出名")
```

如果 VisionMaster 输出是浮点数，不要用 `GetOutputInt`，需要新增对应的读取方法，例如：

```csharp
TryReadFirstOutputDouble(...)
ReadFirstOutputDouble(...)
```

新增位置建议放在：

```text
VMDemo/Core/SingletonManager.Result.cs
```

并和 `TryReadFirstOutputInt(...)` 放在一起。

## 11. 后续增加测量输出的推荐结构

如果四相机后续要统一读取多个测量值，建议不要每个字段都单独散落在代码里，可以在 `ProcedureResultSnapshot` 中新增：

```csharp
public Dictionary<string, double> MeasureValues { get; set; }
```

然后读取：

```csharp
snapshot.MeasureValues = ReadMeasureValues(procedure);
```

第一版可以先固定读取：

```text
M1
M2
M3
Result
ErrorCode
```

等 `.sol` 方案输出项稳定后，再改成实际名称，例如：

```text
Width
Height
Distance
Diameter
Gap
```

## 12. 最重要的修改点总结

代码执行主线：

```text
TCP 收 ROUND → 保存 PendingRoundId → 点击执行 → 消费 RoundId → 四流程执行 → 四路回调 → 快照归并 → DONE 回传
```

增加流程输出的主入口：

```text
VMDemo/Core/SingletonManager.Run.cs
CaptureProcedureResultSnapshot(...)
```

增加读取工具的位置：

```text
VMDemo/Core/SingletonManager.Result.cs
TryReadFirstOutputInt(...) 附近
```

增加图像叠加显示的位置：

```text
VMDemo/Core/SingletonManager.Result.cs
BuildSnapshotTextItems(...)
```
