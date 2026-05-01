using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using VM.Core;
using VM.PlatformSDKCS;

namespace VMDemo
{
    public sealed partial class SingletonManager
    {
        /// <summary>
        /// 单个流程在某一轮单次执行中的结果快照。
        /// 回调线程中读取并复制结果，避免连续或后续执行覆盖 SDK 结果缓存。
        /// </summary>
        private sealed class ProcedureResultSnapshot
        {
            public int RoundId { get; set; } // 当前快照所属的单次执行轮次号。
            public VmProcedure Procedure { get; set; } // 产生该结果的流程对象。
            public int PictureIndex { get; set; } // 当前流程对应的显示区域索引。
            public string ProcessName { get; set; } // 当前流程名称，用于日志和图像文字叠加。
            public Bitmap Bitmap { get; set; } // 已从 SDK 图像缓存复制出来的位图，后续会转移给 ZoomPictureBox。
            public List<RectBox> Rects { get; set; } // 当前流程输出的 ROI 矩形框。
            public int CountValue { get; set; } // 当前流程输出的 COUNT 值。
            //public bool HasOutValue { get; set; } // 当前流程是否存在 out 整数输出。
            //public int OutValue { get; set; } // 当前流程输出的 out 值。
            public string ErrorMessage { get; set; } // 读取失败时的错误描述，成功时为空。

            public bool IsSuccess
            {
                get { return string.IsNullOrWhiteSpace(ErrorMessage) && Bitmap != null; }
            }

            /// <summary>
            /// 释放尚未交给显示控件接管的位图资源。
            /// </summary>
            public void DisposeBitmap()
            {
                if (Bitmap != null)
                {
                    Bitmap.Dispose();
                    Bitmap = null;
                }
            }
        }

        /// <summary>
        /// 一次单次执行的归并状态。
        /// 所有有效流程都完成并写入 Snapshots 后，才认为本轮执行结束。
        /// </summary>
        private sealed class SingleRunRound
        {
            public int RoundId { get; set; } // 程序侧生成的单次执行轮次号。
            public string ExternalRoundId { get; set; } // TCP 客户端下发的业务 RoundId，用于结果回传和追溯。
            public DateTime StartTime { get; set; } // 本轮开始时间，用于后续排查节拍和超时问题。
            public List<VmProcedure> ExpectedProcedures { get; set; } // 本轮期望收到结果的流程集合。
            public Dictionary<VmProcedure, ProcedureResultSnapshot> Snapshots { get; set; } // 已收到的流程结果快照。
            public TaskCompletionSource<SingleRunRound> Completion { get; set; } // 四路结果收齐时释放 Run() 的等待。
            public bool IsClosed { get; set; } // 本轮是否已经完成或超时关闭，防止迟到回调继续写入。

            public SingleRunRound()
            {
                ExpectedProcedures = new List<VmProcedure>();
                Snapshots = new Dictionary<VmProcedure, ProcedureResultSnapshot>();
                Completion = new TaskCompletionSource<SingleRunRound>();
            }
        }

        #region 单次执行流程
        /// <summary>
        /// 单次执行当前方案中的所有有效流程。
        /// 四路流程同级并行触发，流程回调中立即读取结果快照，最后按同一轮次统一归并。
        /// </summary>
        public async void Run()
        {
            if (_isRunning)
            {
                AppendLog("单次执行正在进行，请勿重复触发。");
                return;
            }

            if (!IsLoaded)
            {
                AppendLog("单次执行失败：请先加载方案。");
                return;
            }

            List<KeyValuePair<VmProcedure, int>> procedures = GetOrderedProcedureEntries();
            if (procedures.Count == 0)
            {
                AppendLog("单次执行失败：未找到有效流程。");
                return;
            }

            string externalRoundId;
            string roundErrorMessage;
            if (!TryConsumePendingRoundId(out externalRoundId, out roundErrorMessage))
            {
                AppendLog($"单次执行失败：{roundErrorMessage}");
                return;
            }

            _isRunning = true;
            SingleRunRound round = null;
            bool doneMessageSent = false;

            try
            {
                // 先登记本轮期望收到的流程集合，再启动流程，避免极快回调找不到归并轮次。
                round = CreateSingleRunRound(procedures, externalRoundId);
                AppendLog($"单次执行开始：RoundId {round.ExternalRoundId}，内部轮次 {round.RoundId}，流程数 {procedures.Count}");

                // 所有流程同级启动，不再区分主流程和辅助流程，保证四路相机尽量同时采集。
                foreach (KeyValuePair<VmProcedure, int> item in procedures)
                {
                    try
                    {
                        item.Key.Run();
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"流程 {item.Value + 1} 单次执行启动异常：{ex}");
                        RegisterSingleRunFailure(round, item.Key, item.Value, $"流程启动异常：{ex.Message}");
                    }
                }

                // 等待四路回调读取完快照；如果 SDK 未回调或某一路异常卡住，则由超时保护收尾。
                Task timeoutTask = Task.Delay(RunAbsoluteTimeoutMs);
                Task finishedTask = await Task.WhenAny(round.Completion.Task, timeoutTask);

                if (finishedTask == round.Completion.Task)
                {
                    SingleRunRound completedRound = await round.Completion.Task;
                    await Task.Run(() => FinalizeSingleRunRound(completedRound));
                    doneMessageSent = true;
                }
                else
                {
                    // 超时后关闭本轮并释放已读到但不会显示的快照，避免资源泄漏和串轮。
                    List<string> missingNames = CloseSingleRunRound(round, true);
                    string missingText = string.Join("、", missingNames);
                    AppendLog($"单次执行超时：RoundId {round.ExternalRoundId}，内部轮次 {round.RoundId}，缺失流程：{missingText}");
                    SendTcpMessage(BuildRoundDoneMessage(round, 0, new List<string> { $"超时缺失流程：{missingText}" }));
                    doneMessageSent = true;
                }
            }
            catch (Exception ex)
            {
                AppendLog($"单次执行异常：RoundId {externalRoundId}，{ex.Message}");
                Trace.WriteLine($"单次执行异常：{ex}");
                if (!doneMessageSent)
                {
                    SendTcpMessage($"DONE|{externalRoundId}|NG|{SanitizeTcpMessagePart($"单次执行异常：{ex.Message}")}");
                    doneMessageSent = true;
                }
            }
            finally
            {
                // 不论成功、失败还是超时，都解除当前轮次并放开下一次单次执行入口。
                DeactivateSingleRunRound(round);
                ClearCurrentRoundId(externalRoundId);
                _isRunning = false;
            }
        }

        /// <summary>
        /// 按显示索引返回当前加载方案中的有效流程，确保结果显示顺序稳定。
        /// </summary>
        private List<KeyValuePair<VmProcedure, int>> GetOrderedProcedureEntries()
        {
            return _procedureIndexMap
                .Where(item => item.Key != null)
                .OrderBy(item => item.Value)
                .ToList();
        }

        /// <summary>
        /// 创建并激活一个新的单次执行轮次。
        /// </summary>
        private SingleRunRound CreateSingleRunRound(List<KeyValuePair<VmProcedure, int>> procedures, string externalRoundId)
        {
            SingleRunRound round = new SingleRunRound
            {
                RoundId = ++_singleRunRoundSeed,
                ExternalRoundId = externalRoundId,
                StartTime = DateTime.Now,
                ExpectedProcedures = procedures.Select(item => item.Key).ToList()
            };

            lock (_singleRunLock)
            {
                _activeSingleRunRound = round;
            }

            return round;
        }

        /// <summary>
        /// 获取当前流程所属的活动单次轮次。
        /// 不属于当前轮次的流程回调会被忽略，防止迟到回调污染新一轮结果。
        /// </summary>
        private SingleRunRound GetActiveSingleRunRound(VmProcedure procedure)
        {
            lock (_singleRunLock)
            {
                if (_activeSingleRunRound == null || _activeSingleRunRound.IsClosed)
                {
                    return null;
                }

                if (!_activeSingleRunRound.ExpectedProcedures.Contains(procedure))
                {
                    return null;
                }

                return _activeSingleRunRound;
            }
        }

        /// <summary>
        /// 解除当前活动单次轮次。
        /// </summary>
        private void DeactivateSingleRunRound(SingleRunRound round)
        {
            if (round == null)
            {
                return;
            }

            lock (_singleRunLock)
            {
                if (ReferenceEquals(_activeSingleRunRound, round))
                {
                    _activeSingleRunRound = null;
                }
            }
        }

        /// <summary>
        /// 关闭单次执行轮次，并返回尚未收到结果的流程名称。
        /// </summary>
        private List<string> CloseSingleRunRound(SingleRunRound round, bool disposeSnapshots)
        {
            List<string> missingNames = new List<string>();

            if (round == null)
            {
                return missingNames;
            }

            lock (_singleRunLock)
            {
                round.IsClosed = true;

                foreach (VmProcedure procedure in round.ExpectedProcedures)
                {
                    if (!round.Snapshots.ContainsKey(procedure))
                    {
                        missingNames.Add(GetProcedureDisplayName(procedure));
                    }
                }

                if (disposeSnapshots)
                {
                    foreach (ProcedureResultSnapshot snapshot in round.Snapshots.Values)
                    {
                        snapshot.DisposeBitmap();
                    }
                    round.Snapshots.Clear();
                }
            }

            if (missingNames.Count == 0)
            {
                missingNames.Add("无");
            }

            return missingNames;
        }

        /// <summary>
        /// 获取流程显示名称，优先使用加载方案时保存的流程名。
        /// </summary>
        private string GetProcedureDisplayName(VmProcedure procedure)
        {
            int pictureIndex;
            if (procedure != null && _procedureIndexMap.TryGetValue(procedure, out pictureIndex) &&
                pictureIndex >= 0 && pictureIndex < _procedureNames.Count)
            {
                return _procedureNames[pictureIndex];
            }

            return procedure == null ? "未知流程" : procedure.Name;
        }

        /// <summary>
        /// 统一的流程执行结束回调。
        /// 单次执行期间，回调只负责读取并缓存当前流程快照；四路到齐后再统一显示和通知。
        /// </summary>
        private void OnWorkEnd(object sender, EventArgs e)
        {
            VmProcedure procedure = sender as VmProcedure;
            if (procedure == null)
            {
                return;
            }

            int pictureIndex;
            if (!_procedureIndexMap.TryGetValue(procedure, out pictureIndex))
            {
                return;
            }

            SingleRunRound round = GetActiveSingleRunRound(procedure);
            if (round != null)
            {
                // 回调线程只触发异步快照读取，避免长时间阻塞 SDK 回调。
                CaptureSingleRunSnapshotAsync(round, procedure, pictureIndex);
            }
        }

        /// <summary>
        /// 在后台读取当前流程结果快照，读取完成后写入轮次归并器。
        /// </summary>
        private void CaptureSingleRunSnapshotAsync(SingleRunRound round, VmProcedure procedure, int pictureIndex)
        {
            Task.Run(async () =>
            {
                ProcedureResultSnapshot snapshot = null;

                try
                {
                    snapshot = await CaptureProcedureResultSnapshot(round.RoundId, procedure, pictureIndex);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"流程 {pictureIndex + 1} 读取结果快照异常：{ex}");
                    snapshot = CreateFailureSnapshot(round.RoundId, procedure, pictureIndex, $"读取结果异常：{ex.Message}");
                }

                RegisterSingleRunSnapshot(round, snapshot);
            });
        }

        /// <summary>
        /// 立即读取并复制一个流程的图像和输出结果。
        /// </summary>
        private async Task<ProcedureResultSnapshot> CaptureProcedureResultSnapshot(int roundId, VmProcedure procedure, int pictureIndex)
        {
            ProcedureResultSnapshot snapshot = new ProcedureResultSnapshot
            {
                RoundId = roundId,
                Procedure = procedure,
                PictureIndex = pictureIndex,
                ProcessName = pictureIndex >= 0 && pictureIndex < _procedureNames.Count
                    ? _procedureNames[pictureIndex]
                    : string.Empty
            };

            ImageBaseData_V2 imageBase = await ReadProcedureImageWithRetry(procedure, pictureIndex);
            if (imageBase == null)
            {
                snapshot.ErrorMessage = "未获取到有效图像";
                return snapshot;
            }

            snapshot.Rects = ReadProcedureRois(procedure);
            snapshot.CountValue = ReadFirstOutputInt(procedure, "COUNT");

            int outValue;
            //snapshot.HasOutValue = TryReadFirstOutputInt(procedure, "out", out outValue);
            //snapshot.OutValue = outValue;
            snapshot.Bitmap = ConvertToBitmap(imageBase);

            if (snapshot.Bitmap == null)
            {
                snapshot.ErrorMessage = "图像格式转换失败";
            }

            return snapshot;
        }

        /// <summary>
        /// 创建读取失败的流程快照，让归并器也能感知该流程已经结束。
        /// </summary>
        private ProcedureResultSnapshot CreateFailureSnapshot(int roundId, VmProcedure procedure, int pictureIndex, string errorMessage)
        {
            return new ProcedureResultSnapshot
            {
                RoundId = roundId,
                Procedure = procedure,
                PictureIndex = pictureIndex,
                ProcessName = pictureIndex >= 0 && pictureIndex < _procedureNames.Count
                    ? _procedureNames[pictureIndex]
                    : string.Empty,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 流程启动阶段失败时，直接登记失败快照，避免本轮一直等待。
        /// </summary>
        private void RegisterSingleRunFailure(SingleRunRound round, VmProcedure procedure, int pictureIndex, string errorMessage)
        {
            ProcedureResultSnapshot snapshot = CreateFailureSnapshot(round.RoundId, procedure, pictureIndex, errorMessage);
            RegisterSingleRunSnapshot(round, snapshot);
        }

        /// <summary>
        /// 将流程快照写入当前轮次。
        /// 同一流程只接受第一份结果，重复或迟到结果会被丢弃并释放资源。
        /// </summary>
        private void RegisterSingleRunSnapshot(SingleRunRound round, ProcedureResultSnapshot snapshot)
        {
            if (round == null || snapshot == null)
            {
                if (snapshot != null)
                {
                    snapshot.DisposeBitmap();
                }
                return;
            }

            bool completed = false;

            lock (_singleRunLock)
            {
                if (round.IsClosed || !ReferenceEquals(_activeSingleRunRound, round))
                {
                    snapshot.DisposeBitmap();
                    return;
                }

                if (!round.ExpectedProcedures.Contains(snapshot.Procedure))
                {
                    snapshot.DisposeBitmap();
                    return;
                }

                if (round.Snapshots.ContainsKey(snapshot.Procedure))
                {
                    // 同一流程本轮只接收一次，重复回调不参与归并。
                    snapshot.DisposeBitmap();
                    return;
                }

                round.Snapshots.Add(snapshot.Procedure, snapshot);
                completed = round.Snapshots.Count >= round.ExpectedProcedures.Count;

                if (completed)
                {
                    round.IsClosed = true;
                }
            }

            if (completed)
            {
                // 所有期望流程都已返回结果，唤醒 Run() 做统一显示和 TCP 通知。
                round.Completion.TrySetResult(round);
            }
        }

        /// <summary>
        /// 本轮所有流程结果到齐后，统一显示图像、写日志并发送一次 TCP 通知。
        /// </summary>
        private void FinalizeSingleRunRound(SingleRunRound round)
        {
            if (round == null)
            {
                return;
            }

            int successCount = 0;
            List<string> errors = new List<string>();
            List<ProcedureResultSnapshot> snapshots;

            lock (_singleRunLock)
            {
                snapshots = round.Snapshots.Values
                    .OrderBy(snapshot => snapshot.PictureIndex)
                    .ToList();
            }

            foreach (ProcedureResultSnapshot snapshot in snapshots)
            {
                if (snapshot.IsSuccess)
                {
                    DisplayProcedureSnapshot(snapshot);
                    successCount++;
                }
                else
                {
                    string error = $"流程 {GetSnapshotDisplayName(snapshot)} 结果读取失败：{snapshot.ErrorMessage}";
                    errors.Add(error);
                    AppendLog($"RoundId {round.ExternalRoundId} 内部轮次 {round.RoundId} {error}");
                }
            }

            AppendLog($"单次执行完成：RoundId {round.ExternalRoundId}，内部轮次 {round.RoundId}，成功 {successCount}/{round.ExpectedProcedures.Count}");
            SendTcpMessage(BuildRoundDoneMessage(round, successCount, errors));
        }

        /// <summary>
        /// 构建整轮执行完成后的 TCP 回传消息。
        /// 四路流程全部成功才返回 OK，否则返回 NG 并附带失败原因。
        /// </summary>
        private string BuildRoundDoneMessage(SingleRunRound round, int successCount, List<string> errors)
        {
            if (round == null)
            {
                return "DONE||NG|内部轮次为空";
            }

            bool isOk = successCount >= round.ExpectedProcedures.Count && (errors == null || errors.Count == 0);
            if (isOk)
            {
                return $"DONE|{round.ExternalRoundId}|OK";
            }

            string reason = errors != null && errors.Count > 0
                ? string.Join("；", errors)
                : "流程结果不完整";

            return $"DONE|{round.ExternalRoundId}|NG|{SanitizeTcpMessagePart(reason)}";
        }

        /// <summary>
        /// 清理 TCP 消息字段中的分隔符和换行，避免客户端解析 DONE 消息时串字段。
        /// </summary>
        private string SanitizeTcpMessagePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("|", "/")
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }

        /// <summary>
        /// 获取快照对应的显示名称，用于日志输出。
        /// </summary>
        private string GetSnapshotDisplayName(ProcedureResultSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return "未知流程";
            }

            if (!string.IsNullOrWhiteSpace(snapshot.ProcessName))
            {
                return snapshot.ProcessName;
            }

            return $"流程 {snapshot.PictureIndex + 1}";
        }
        #endregion
    }
}
