using System; // 引入基础类型、异常和日期时间等常用类型。
using System.Collections.Generic; // 引入 List、Dictionary 等集合类型。
using System.Diagnostics; // 引入 Trace 调试输出能力。
using System.Drawing; // 引入 Bitmap 图像类型。
using System.Linq; // 引入 LINQ 排序、筛选和转换方法。
using System.Threading.Tasks; // 引入 Task 异步任务能力。
using VM.Core; // 引入 VisionMaster 核心 SDK 类型。
using VM.PlatformSDKCS; // 引入 VisionMaster 平台 SDK 类型。

namespace VMDemo // 声明项目主命名空间。
{ // 开始 VMDemo 命名空间。
    public sealed partial class SingletonManager // 声明 SingletonManager 分部类，本文件负责单次执行归并逻辑。
    { // 开始 SingletonManager 类体。
        /// <summary>
        /// 单个流程在某一轮单次执行中的结果快照。
        /// 回调线程中读取并复制结果，避免连续或后续执行覆盖 SDK 结果缓存。
        /// </summary>
        private sealed class ProcedureResultSnapshot // 定义单个流程结果快照对象。
        { // 开始流程结果快照类体。
            public int RoundId { get; set; } // 当前快照所属的单次执行轮次号。
            public VmProcedure Procedure { get; set; } // 产生该结果的流程对象。
            public int PictureIndex { get; set; } // 当前流程对应的显示区域索引。
            public string ProcessName { get; set; } // 当前流程名称，用于日志和图像文字叠加。
            public Bitmap Bitmap { get; set; } // 已从 SDK 图像缓存复制出来的位图，后续会转移给 ZoomPictureBox。
            public List<RectBox> Rects { get; set; } // 当前流程输出的 ROI 矩形框。
            public int CountValue { get; set; } // 当前流程输出的 COUNT 值。
            public bool HasCountValue { get; set; } // 当前流程是否存在 COUNT 整数输出。
            public int TotalOutValue { get; set; } // 四个流程 out 相加后的总值。
            public bool ShowTotalOutValue { get; set; } // 是否在该流程图像上显示总 out。
            public string ErrorMessage { get; set; } // 读取失败时的错误描述，成功时为空。

            public bool IsSuccess // 提供当前快照是否读取成功的只读判断。
            { // 开始 IsSuccess 属性体。
                get { return string.IsNullOrWhiteSpace(ErrorMessage) && Bitmap != null; } // 没有错误且有位图时认为成功。
            } // 结束 IsSuccess 属性体。

            /// <summary>
            /// 释放尚未交给显示控件接管的位图资源。
            /// </summary>
            public void DisposeBitmap() // 释放当前快照持有的 Bitmap。
            { // 开始释放位图方法。
                if (Bitmap != null) // 只有存在位图对象时才需要释放。
                { // 开始位图非空处理。
                    Bitmap.Dispose(); // 释放 Bitmap 占用的非托管图像资源。
                    Bitmap = null; // 清空引用，避免重复释放。
                } // 结束位图非空处理。
            } // 结束释放位图方法。
        } // 结束流程结果快照类体。

        /// <summary>
        /// 一次单次执行的归并状态。
        /// 所有有效流程都完成并写入 Snapshots 后，才认为本轮执行结束。
        /// </summary>
        private sealed class SingleRunRound // 定义一次单次执行的整轮归并状态。
        { // 开始单次执行轮次类体。
            public int RoundId { get; set; } // 程序侧生成的单次执行轮次号。
            public string ExternalRoundId { get; set; } // TCP 客户端下发的业务 RoundId，用于结果回传和追溯。
            public DateTime StartTime { get; set; } // 本轮开始时间，用于后续排查节拍和超时问题。
            public List<VmProcedure> ExpectedProcedures { get; set; } // 本轮期望收到结果的流程集合。
            public Dictionary<VmProcedure, ProcedureResultSnapshot> Snapshots { get; set; } // 已收到的流程结果快照。
            public TaskCompletionSource<SingleRunRound> Completion { get; set; } // 四路结果收齐时释放 Run() 的等待。
            public bool IsClosed { get; set; } // 本轮是否已经完成或超时关闭，防止迟到回调继续写入。

            public SingleRunRound() // 初始化单次执行轮次对象。
            { // 开始构造函数。
                ExpectedProcedures = new List<VmProcedure>(); // 初始化期望流程集合。
                Snapshots = new Dictionary<VmProcedure, ProcedureResultSnapshot>(); // 初始化结果快照字典。
                Completion = new TaskCompletionSource<SingleRunRound>(); // 初始化本轮完成信号。
            } // 结束构造函数。
        } // 结束单次执行轮次类体。

        #region 单次执行流程
        /// <summary>
        /// 单次执行当前方案中的所有有效流程。
        /// 四路流程同级并行触发，流程回调中立即读取结果快照，最后按同一轮次统一归并。
        /// </summary>
        public async void Run() // 从界面触发单次执行入口。
        { // 开始单次执行方法。
            if (_isRunning) // 判断当前是否已有单次执行正在运行。
            { // 开始重复触发保护。
                AppendLog("单次执行正在进行，请勿重复触发。", LogLevel.Warning); // 写入重复触发提示日志。
                return; // 直接退出，避免并发执行。
            } // 结束重复触发保护。

            if (!IsLoaded) // 判断是否已经加载 VisionMaster 方案。
            { // 开始未加载方案保护。
                AppendLog("单次执行失败：请先加载方案。", LogLevel.Error); // 写入未加载方案提示日志。
                return; // 直接退出，避免空方案执行。
            } // 结束未加载方案保护。

            List<KeyValuePair<VmProcedure, int>> procedures = GetOrderedProcedureEntries(); // 获取按显示索引排序的有效流程。
            if (procedures.Count == 0) // 判断当前方案是否没有有效流程。
            { // 开始无流程保护。
                AppendLog("单次执行失败：未找到有效流程。", LogLevel.Error); // 写入无有效流程提示日志。
                return; // 直接退出，避免后续空集合处理。
            } // 结束无流程保护。

            string externalRoundId; // 声明 TCP 客户端下发的业务 RoundId。
            string roundErrorMessage; // 声明消费 RoundId 失败时的错误信息。
            if (!TryConsumePendingRoundId(out externalRoundId, out roundErrorMessage)) // 尝试消费等待执行的 RoundId。
            { // 开始 RoundId 消费失败处理。
                AppendLog($"单次执行失败：{roundErrorMessage}", LogLevel.Error); // 写入 RoundId 消费失败日志。
                return; // 直接退出，避免无业务 RoundId 的执行。
            } // 结束 RoundId 消费失败处理。

            _isRunning = true; // 标记单次执行正在运行。
            SingleRunRound round = null; // 声明当前单次执行轮次对象。
            bool doneMessageSent = false; // 标记是否已经向 TCP 客户端发送 DONE 消息。

            try // 捕获单次执行主流程中的异常。
            { // 开始单次执行主流程。
                // 先登记本轮期望收到的流程集合，再启动流程，避免极快回调找不到归并轮次。
                round = CreateSingleRunRound(procedures, externalRoundId); // 创建并激活本轮归并状态。
                AppendLog($"单次执行开始：RoundId {round.ExternalRoundId}，内部轮次 {round.RoundId}，流程数 {procedures.Count}"); // 写入单次执行开始日志。

                // 所有流程同级启动，不再区分主流程和辅助流程，保证四路相机尽量同时采集。
                foreach (KeyValuePair<VmProcedure, int> item in procedures) // 遍历本轮所有有效流程。
                { // 开始逐流程启动。
                    try // 捕获单个流程启动异常。
                    { // 开始单流程启动。
                        item.Key.Run(); // 调用 VisionMaster SDK 启动当前流程。
                    } // 结束单流程启动。
                    catch (Exception ex) // 捕获当前流程启动异常。
                    { // 开始流程启动异常处理。
                        Trace.WriteLine($"流程 {item.Value + 1} 单次执行启动异常：{ex}"); // 输出流程启动异常到调试跟踪。
                        RegisterSingleRunFailure(round, item.Key, item.Value, $"流程启动异常：{ex.Message}"); // 登记失败快照，避免整轮一直等待。
                    } // 结束流程启动异常处理。
                } // 结束逐流程启动。

                // 等待四路回调读取完快照；如果 SDK 未回调或某一路异常卡住，则由超时保护收尾。
                Task timeoutTask = Task.Delay(RunAbsoluteTimeoutMs); // 创建整轮执行绝对超时任务。
                Task finishedTask = await Task.WhenAny(round.Completion.Task, timeoutTask); // 等待结果收齐或超时先发生。

                if (finishedTask == round.Completion.Task) // 判断是否正常等到所有流程结果。
                { // 开始正常完成处理。
                    SingleRunRound completedRound = await round.Completion.Task; // 取得已经收齐结果的轮次对象。
                    await Task.Run(() => FinalizeSingleRunRound(completedRound)); // 在线程池中统一显示、写日志并发送 TCP 通知。
                    doneMessageSent = true; // 标记 DONE 消息已在收尾逻辑中发送。
                } // 结束正常完成处理。
                else // 进入整轮超时处理。
                { // 开始超时处理。
                    // 超时后关闭本轮并释放已读到但不会显示的快照，避免资源泄漏和串轮。
                    List<string> missingNames = CloseSingleRunRound(round, true); // 关闭轮次并取得缺失流程名称。
                    string missingText = string.Join("、", missingNames); // 拼接缺失流程名称文本。
                    AppendLog($"单次执行超时：RoundId {round.ExternalRoundId}，内部轮次 {round.RoundId}，缺失流程：{missingText}", LogLevel.Error); // 写入单次执行超时日志。
                    SendTcpMessage(BuildRoundDoneMessage(round, 0, new List<string> { $"超时缺失流程：{missingText}" })); // 向 TCP 客户端发送超时 NG 消息。
                    doneMessageSent = true; // 标记 DONE 消息已发送。
                } // 结束超时处理。
            } // 结束单次执行主流程。
            catch (Exception ex) // 捕获单次执行整体异常。
            { // 开始整体异常处理。
                AppendLog($"单次执行异常：RoundId {externalRoundId}，{ex.Message}", LogLevel.Error); // 写入单次执行异常日志。
                Trace.WriteLine($"单次执行异常：{ex}"); // 输出完整异常到调试跟踪。
                if (!doneMessageSent) // 判断异常发生前是否尚未发送 DONE。
                { // 开始异常 DONE 发送保护。
                    SendTcpMessage($"DONE|{externalRoundId}|NG|{SanitizeTcpMessagePart($"单次执行异常：{ex.Message}")}"); // 向 TCP 客户端发送异常 NG 消息。
                    doneMessageSent = true; // 标记 DONE 消息已发送。
                } // 结束异常 DONE 发送保护。
            } // 结束整体异常处理。
            finally // 不论成功、失败还是异常都执行清理。
            { // 开始最终清理。
                // 不论成功、失败还是超时，都解除当前轮次并放开下一次单次执行入口。
                DeactivateSingleRunRound(round); // 解除当前活动轮次。
                ClearCurrentRoundId(externalRoundId); // 清空正在执行的业务 RoundId。
                _isRunning = false; // 放开下一次单次执行入口。
            } // 结束最终清理。
        } // 结束单次执行方法。

        /// <summary>
        /// 按显示索引返回当前加载方案中的有效流程，确保结果显示顺序稳定。
        /// </summary>
        private List<KeyValuePair<VmProcedure, int>> GetOrderedProcedureEntries() // 获取当前加载方案的有序流程集合。
        { // 开始获取有序流程方法。
            return _procedureIndexMap // 从流程到显示索引的映射开始查询。
                .Where(item => item.Key != null) // 过滤掉空流程对象。
                .OrderBy(item => item.Value) // 按显示区域索引排序。
                .ToList(); // 转换为列表供执行流程复用。
        } // 结束获取有序流程方法。

        /// <summary>
        /// 创建并激活一个新的单次执行轮次。
        /// </summary>
        private SingleRunRound CreateSingleRunRound(List<KeyValuePair<VmProcedure, int>> procedures, string externalRoundId) // 创建本轮执行归并对象。
        { // 开始创建单次执行轮次方法。
            SingleRunRound round = new SingleRunRound // 创建新的单次执行轮次。
            { // 开始轮次对象初始化。
                RoundId = ++_singleRunRoundSeed, // 递增并写入内部轮次号。
                ExternalRoundId = externalRoundId, // 保存 TCP 客户端下发的业务 RoundId。
                StartTime = DateTime.Now, // 记录本轮开始时间。
                ExpectedProcedures = procedures.Select(item => item.Key).ToList() // 保存本轮期望返回结果的流程集合。
            }; // 结束轮次对象初始化。

            lock (_singleRunLock) // 加锁保护活动轮次引用。
            { // 开始活动轮次写入保护。
                _activeSingleRunRound = round; // 将新轮次设置为当前活动轮次。
            } // 结束活动轮次写入保护。

            return round; // 返回新创建的轮次对象。
        } // 结束创建单次执行轮次方法。

        /// <summary>
        /// 统一的流程执行结束回调。
        /// 单次执行期间，回调只负责读取并缓存当前流程快照；四路到齐后再统一显示和通知。
        /// </summary>
        private void OnWorkEnd(object sender, EventArgs e) // VisionMaster 流程执行完成后的统一回调。
        { // 开始流程结束回调。
            VmProcedure procedure = sender as VmProcedure; // 将回调发送方转换为流程对象。
            if (procedure == null) // 判断回调发送方是否不是流程对象。
            { // 开始无效回调处理。
                return; // 无法识别流程时直接忽略。
            } // 结束无效回调处理。

            int pictureIndex; // 声明当前流程对应的显示索引。
            if (!_procedureIndexMap.TryGetValue(procedure, out pictureIndex)) // 尝试查找流程显示索引。
            { // 开始未映射流程处理。
                return; // 未映射流程不参与显示和归并。
            } // 结束未映射流程处理。

            SingleRunRound round = GetActiveSingleRunRound(procedure); // 获取该流程所属的当前活动轮次。
            if (round != null) // 判断该回调是否属于当前轮次。
            { // 开始本轮回调处理。
                // 回调线程只触发异步快照读取，避免长时间阻塞 SDK 回调。
                CaptureSingleRunSnapshotAsync(round, procedure, pictureIndex); // 后台读取并登记当前流程结果快照。
            } // 结束本轮回调处理。
        } // 结束流程结束回调。

        /// <summary>
        /// 获取当前流程所属的活动单次轮次。
        /// 不属于当前轮次的流程回调会被忽略，防止迟到回调污染新一轮结果。
        /// </summary>
        private SingleRunRound GetActiveSingleRunRound(VmProcedure procedure) // 根据流程对象获取当前活动轮次。
        { // 开始获取活动轮次方法。
            lock (_singleRunLock) // 加锁读取活动轮次状态。
            { // 开始活动轮次读取保护。
                if (_activeSingleRunRound == null || _activeSingleRunRound.IsClosed) // 判断当前是否没有可写入的活动轮次。
                { // 开始无活动轮次处理。
                    return null; // 返回空表示该回调不参与归并。
                } // 结束无活动轮次处理。

                if (!_activeSingleRunRound.ExpectedProcedures.Contains(procedure)) // 判断回调流程是否属于当前轮次。
                { // 开始非本轮流程处理。
                    return null; // 返回空表示忽略迟到或无关回调。
                } // 结束非本轮流程处理。

                return _activeSingleRunRound; // 返回当前活动轮次。
            } // 结束活动轮次读取保护。
        } // 结束获取活动轮次方法。

        /// <summary>
        /// 在后台读取当前流程结果快照，读取完成后写入轮次归并器。
        /// </summary>
        private void CaptureSingleRunSnapshotAsync(SingleRunRound round, VmProcedure procedure, int pictureIndex) // 异步读取单个流程的结果快照。
        { // 开始异步快照读取方法。
            Task.Run(async () => // 在线程池中执行异步读取，避免阻塞 SDK 回调线程。
            { // 开始后台任务体。
                ProcedureResultSnapshot snapshot = null; // 声明本流程结果快照。

                try // 捕获结果读取异常。
                { // 开始快照读取。
                    snapshot = await CaptureProcedureResultSnapshot(round.RoundId, procedure, pictureIndex); // 读取并复制当前流程结果。
                } // 结束快照读取。
                catch (Exception ex) // 捕获快照读取异常。
                { // 开始快照读取异常处理。
                    Trace.WriteLine($"流程 {pictureIndex + 1} 读取结果快照异常：{ex}"); // 输出读取异常到调试跟踪。
                    snapshot = CreateFailureSnapshot(round.RoundId, procedure, pictureIndex, $"读取结果异常：{ex.Message}"); // 创建失败快照让归并器继续推进。
                } // 结束快照读取异常处理。

                RegisterSingleRunSnapshot(round, snapshot); // 将读取到的快照登记到当前轮次。
            }); // 结束并启动后台任务。
        } // 结束异步快照读取方法。

        /// <summary>
        /// 立即读取并复制一个流程的图像和输出结果。
        /// </summary>
        private async Task<ProcedureResultSnapshot> CaptureProcedureResultSnapshot(int roundId, VmProcedure procedure, int pictureIndex) // 读取单个流程的图像和输出值。
        { // 开始流程结果读取方法。
            ProcedureResultSnapshot snapshot = new ProcedureResultSnapshot // 创建当前流程结果快照。
            { // 开始快照对象初始化。
                RoundId = roundId, // 写入内部轮次号。
                Procedure = procedure, // 保存产生结果的流程对象。
                PictureIndex = pictureIndex, // 保存当前流程显示索引。
                ProcessName = pictureIndex >= 0 && pictureIndex < _procedureNames.Count // 判断是否能从缓存列表取到流程名。
                    ? _procedureNames[pictureIndex] // 能取到时使用缓存流程名。
                    : string.Empty // 取不到时使用空名称。
            }; // 结束快照对象初始化。

            ImageBaseData_V2 imageBase = await ReadProcedureImageWithRetry(procedure, pictureIndex); // 带重试读取当前流程图像输出。
            if (imageBase == null) // 判断是否没有读到有效图像。
            { // 开始无图像处理。
                snapshot.ErrorMessage = "未获取到有效图像"; // 记录无图像错误。
                return snapshot; // 返回失败快照给归并器。
            } // 结束无图像处理。

            snapshot.Rects = ReadProcedureRois(procedure); // 读取当前流程 ROI 矩形框输出。
            snapshot.CountValue = ReadFirstOutputInt(procedure, "COUNT"); // 读取当前流程 COUNT 整数输出。
            snapshot.HasCountValue = snapshot.CountValue > 0;

            //snapshot.HasOutValue = TryReadFirstOutputInt(procedure, "out", out outValue); // 读取 out 输出是否存在。
            //snapshot.OutValue = outValue; // 保存 out 输出值。
            snapshot.Bitmap = ConvertToBitmap(imageBase); // 将 SDK 图像数据复制转换为 Bitmap。

            if (snapshot.Bitmap == null) // 判断图像格式转换是否失败。
            { // 开始图像转换失败处理。
                snapshot.ErrorMessage = "图像格式转换失败"; // 记录图像转换失败错误。
            } // 结束图像转换失败处理。

            return snapshot; // 返回当前流程结果快照。
        } // 结束流程结果读取方法。

        /// <summary>
        /// 创建读取失败的流程快照，让归并器也能感知该流程已经结束。
        /// </summary>
        private ProcedureResultSnapshot CreateFailureSnapshot(int roundId, VmProcedure procedure, int pictureIndex, string errorMessage) // 创建失败快照。
        { // 开始创建失败快照方法。
            return new ProcedureResultSnapshot // 返回一个只携带错误信息的快照。
            { // 开始失败快照初始化。
                RoundId = roundId, // 写入内部轮次号。
                Procedure = procedure, // 保存失败对应的流程对象。
                PictureIndex = pictureIndex, // 保存失败对应的显示索引。
                ProcessName = pictureIndex >= 0 && pictureIndex < _procedureNames.Count // 判断是否能从缓存列表取到流程名。
                    ? _procedureNames[pictureIndex] // 能取到时使用缓存流程名。
                    : string.Empty, // 取不到时使用空名称。
                ErrorMessage = errorMessage // 写入失败原因。
            }; // 结束失败快照初始化。
        } // 结束创建失败快照方法。

        /// <summary>
        /// 流程启动阶段失败时，直接登记失败快照，避免本轮一直等待。
        /// </summary>
        private void RegisterSingleRunFailure(SingleRunRound round, VmProcedure procedure, int pictureIndex, string errorMessage) // 登记流程启动阶段失败。
        { // 开始登记启动失败方法。
            ProcedureResultSnapshot snapshot = CreateFailureSnapshot(round.RoundId, procedure, pictureIndex, errorMessage); // 创建启动失败快照。
            RegisterSingleRunSnapshot(round, snapshot); // 将失败快照登记到轮次归并器。
        } // 结束登记启动失败方法。

        /// <summary>
        /// 将流程快照写入当前轮次。
        /// 同一流程只接受第一份结果，重复或迟到结果会被丢弃并释放资源。
        /// </summary>
        private void RegisterSingleRunSnapshot(SingleRunRound round, ProcedureResultSnapshot snapshot) // 将单流程快照登记到整轮结果中。
        { // 开始登记单流程快照方法。
            if (round == null || snapshot == null) // 判断轮次或快照是否为空。
            { // 开始空参数处理。
                if (snapshot != null) // 判断是否存在需要释放的快照。
                { // 开始空轮次快照释放。
                    snapshot.DisposeBitmap(); // 释放无法归并的快照位图。
                } // 结束空轮次快照释放。
                return; // 直接退出登记流程。
            } // 结束空参数处理。

            bool completed = false; // 标记本次登记后整轮是否已经完成。

            lock (_singleRunLock) // 加锁保护轮次快照字典。
            { // 开始快照登记锁保护。
                if (round.IsClosed || !ReferenceEquals(_activeSingleRunRound, round)) // 判断轮次是否已关闭或不再是活动轮次。
                { // 开始迟到快照处理。
                    snapshot.DisposeBitmap(); // 释放迟到或串轮快照位图。
                    return; // 忽略该快照。
                } // 结束迟到快照处理。

                if (!round.ExpectedProcedures.Contains(snapshot.Procedure)) // 判断快照流程是否属于本轮期望流程。
                { // 开始非本轮快照处理。
                    snapshot.DisposeBitmap(); // 释放无关流程快照位图。
                    return; // 忽略该快照。
                } // 结束非本轮快照处理。

                if (round.Snapshots.ContainsKey(snapshot.Procedure)) // 判断同一流程本轮是否已经登记过快照。
                { // 开始重复快照处理。
                    // 同一流程本轮只接收一次，重复回调不参与归并。
                    snapshot.DisposeBitmap(); // 释放重复回调产生的快照位图。
                    return; // 忽略重复快照。
                } // 结束重复快照处理。

                round.Snapshots.Add(snapshot.Procedure, snapshot); // 写入当前流程的第一份快照。
                completed = round.Snapshots.Count >= round.ExpectedProcedures.Count; // 判断本轮所有期望流程是否都已返回。

                if (completed) // 判断本轮是否已经收齐结果。
                { // 开始轮次完成标记。
                    round.IsClosed = true; // 标记轮次关闭，阻止后续迟到回调写入。
                } // 结束轮次完成标记。
            } // 结束快照登记锁保护。

            if (completed) // 判断是否需要唤醒等待中的 Run()。
            { // 开始完成信号释放。
                // 所有期望流程都已返回结果，唤醒 Run() 做统一显示和 TCP 通知。
                round.Completion.TrySetResult(round); // 释放本轮完成信号。
            } // 结束完成信号释放。
        } // 结束登记单流程快照方法。

        /// <summary>
        /// 本轮所有流程结果到齐后，统一显示图像、写日志并发送一次 TCP 通知。
        /// </summary>
        private void FinalizeSingleRunRound(SingleRunRound round) // 对已收齐结果的单次执行轮次做统一收尾。
        { // 开始单次执行收尾方法。
            if (round == null) // 判断轮次对象是否为空。
            { // 开始空轮次处理。
                return; // 空轮次无需收尾。
            } // 结束空轮次处理。

            int successCount = 0; // 初始化成功流程数量。
            List<string> errors = new List<string>(); // 初始化错误信息列表。
            List<ProcedureResultSnapshot> snapshots; // 声明按显示顺序排列的快照列表。

            lock (_singleRunLock) // 加锁读取本轮快照集合。
            { // 开始快照集合读取保护。
                snapshots = round.Snapshots.Values // 从本轮快照字典取出所有快照。
                    .OrderBy(snapshot => snapshot.PictureIndex) // 按显示索引排序，保证显示顺序稳定。
                    .ToList(); // 转换为列表，避免后续遍历受字典变化影响。
            } // 结束快照集合读取保护。

            int tovaliue = snapshots.Where(r => r.HasCountValue).Sum(t => t.CountValue);


            foreach (ProcedureResultSnapshot snapshot in snapshots) // 遍历本轮所有流程快照。
            {
                if (snapshot.PictureIndex == 0)
                {
                    snapshot.TotalOutValue = tovaliue;
                    snapshot.ShowTotalOutValue = true;
                }
                
                // 开始逐流程收尾处理。
                if (snapshot.IsSuccess) // 判断当前流程结果是否成功。
                { // 开始成功流程处理。
                    DisplayProcedureSnapshot(snapshot); // 显示当前流程图像和叠加结果。
                    successCount++; // 成功流程数量加一。
                } // 结束成功流程处理。
                else // 进入失败流程处理。
                { // 开始失败流程处理。
                    string error = $"流程 {GetSnapshotDisplayName(snapshot)} 结果读取失败：{snapshot.ErrorMessage}"; // 构建当前流程失败描述。
                    errors.Add(error); // 加入整轮错误列表。
                    AppendLog($"RoundId {round.ExternalRoundId} 内部轮次 {round.RoundId} {error}", LogLevel.Error); // 写入当前流程失败日志。
                } // 结束失败流程处理。
            } // 结束逐流程收尾处理。

            AppendLog($"单次执行完成：RoundId {round.ExternalRoundId}，内部轮次 {round.RoundId}，成功 {successCount}/{round.ExpectedProcedures.Count}"); // 写入整轮完成日志。
            SendTcpMessage(BuildRoundDoneMessage(round, successCount, errors)); // 根据整轮结果向 TCP 客户端发送 DONE 消息。
        } // 结束单次执行收尾方法。

        /// <summary>
        /// 构建整轮执行完成后的 TCP 回传消息。
        /// 四路流程全部成功才返回 OK，否则返回 NG 并附带失败原因。
        /// </summary>
        private string BuildRoundDoneMessage(SingleRunRound round, int successCount, List<string> errors) // 构建整轮执行完成 TCP 消息。
        { // 开始构建 DONE 消息方法。
            if (round == null) // 判断轮次对象是否为空。
            { // 开始空轮次消息处理。
                return "DONE||NG|内部轮次为空"; // 返回内部异常格式的 NG 消息。
            } // 结束空轮次消息处理。

            bool isOk = successCount >= round.ExpectedProcedures.Count && (errors == null || errors.Count == 0); // 判断是否全部流程成功且无错误。
            if (isOk) // 判断整轮是否 OK。
            { // 开始 OK 消息处理。
                return $"DONE|{round.ExternalRoundId}|OK"; // 返回业务 RoundId 对应的 OK 消息。
            } // 结束 OK 消息处理。

            string reason = errors != null && errors.Count > 0 // 判断是否有明确错误列表。
                ? string.Join("；", errors) // 有错误时拼接所有失败原因。
                : "流程结果不完整"; // 无明确错误时使用兜底失败原因。

            return $"DONE|{round.ExternalRoundId}|NG|{SanitizeTcpMessagePart(reason)}"; // 返回业务 RoundId 对应的 NG 消息。
        } // 结束构建 DONE 消息方法。

        /// <summary>
        /// 清理 TCP 消息字段中的分隔符和换行，避免客户端解析 DONE 消息时串字段。
        /// </summary>
        private string SanitizeTcpMessagePart(string value) // 清理 TCP 消息字段文本。
        { // 开始 TCP 字段清理方法。
            if (string.IsNullOrWhiteSpace(value)) // 判断待清理文本是否为空。
            { // 开始空文本处理。
                return string.Empty; // 空文本统一返回空字符串。
            } // 结束空文本处理。

            return value // 从原始文本开始链式清理。
                .Replace("|", "/") // 将协议分隔符替换为普通斜杠。
                .Replace("\r", " ") // 将回车替换为空格。
                .Replace("\n", " ") // 将换行替换为空格。
                .Trim(); // 去掉首尾空白后返回。
        } // 结束 TCP 字段清理方法。

        /// <summary>
        /// 获取快照对应的显示名称，用于日志输出。
        /// </summary>
        private string GetSnapshotDisplayName(ProcedureResultSnapshot snapshot) // 获取快照对应的显示名称。
        { // 开始获取快照显示名称方法。
            if (snapshot == null) // 判断快照是否为空。
            { // 开始空快照处理。
                return "未知流程"; // 空快照使用未知流程名称。
            } // 结束空快照处理。

            if (!string.IsNullOrWhiteSpace(snapshot.ProcessName)) // 判断快照是否带有流程名称。
            { // 开始流程名称命中处理。
                return snapshot.ProcessName; // 返回快照中保存的流程名称。
            } // 结束流程名称命中处理。

            return $"流程 {snapshot.PictureIndex + 1}"; // 没有流程名称时使用显示索引生成名称。
        } // 结束获取快照显示名称方法。

        /// <summary>
        /// 关闭单次执行轮次，并返回尚未收到结果的流程名称。
        /// </summary>
        private List<string> CloseSingleRunRound(SingleRunRound round, bool disposeSnapshots) // 关闭轮次并统计缺失流程。
        { // 开始关闭单次执行轮次方法。
            List<string> missingNames = new List<string>(); // 创建缺失流程名称列表。

            if (round == null) // 判断轮次对象是否为空。
            { // 开始空轮次处理。
                return missingNames; // 空轮次直接返回空缺失列表。
            } // 结束空轮次处理。

            lock (_singleRunLock) // 加锁保护轮次归并状态。
            { // 开始轮次关闭保护。
                round.IsClosed = true; // 标记轮次已关闭，拒绝后续迟到回调。

                foreach (VmProcedure procedure in round.ExpectedProcedures) // 遍历本轮所有期望流程。
                { // 开始缺失流程检查。
                    if (!round.Snapshots.ContainsKey(procedure)) // 判断该流程是否没有结果快照。
                    { // 开始记录缺失流程。
                        missingNames.Add(GetProcedureDisplayName(procedure)); // 加入缺失流程显示名称。
                    } // 结束记录缺失流程。
                } // 结束缺失流程检查。

                if (disposeSnapshots) // 判断是否需要释放已读取但不再显示的快照。
                { // 开始快照释放处理。
                    foreach (ProcedureResultSnapshot snapshot in round.Snapshots.Values) // 遍历当前已收到的快照。
                    { // 开始单个快照释放。
                        snapshot.DisposeBitmap(); // 释放快照中的位图资源。
                    } // 结束单个快照释放。
                    round.Snapshots.Clear(); // 清空快照字典。
                } // 结束快照释放处理。
            } // 结束轮次关闭保护。

            if (missingNames.Count == 0) // 判断是否没有缺失流程。
            { // 开始无缺失流程文本处理。
                missingNames.Add("无"); // 用“无”表示未缺失。
            } // 结束无缺失流程文本处理。

            return missingNames; // 返回缺失流程名称列表。
        } // 结束关闭单次执行轮次方法。

        /// <summary>
        /// 获取流程显示名称，优先使用加载方案时保存的流程名。
        /// </summary>
        private string GetProcedureDisplayName(VmProcedure procedure) // 获取流程用于日志显示的名称。
        { // 开始获取流程显示名称方法。
            int pictureIndex; // 声明流程对应的显示索引。
            if (procedure != null && _procedureIndexMap.TryGetValue(procedure, out pictureIndex) && // 判断流程存在且能找到显示索引。
                pictureIndex >= 0 && pictureIndex < _procedureNames.Count) // 判断显示索引落在流程名列表范围内。
            { // 开始命中流程名处理。
                return _procedureNames[pictureIndex]; // 返回加载方案时记录的流程名称。
            } // 结束命中流程名处理。

            return procedure == null ? "未知流程" : procedure.Name; // 没有缓存名称时返回未知流程或 SDK 流程名。
        } // 结束获取流程显示名称方法。

        /// <summary>
        /// 解除当前活动单次轮次。
        /// </summary>
        private void DeactivateSingleRunRound(SingleRunRound round) // 解除指定轮次的活动状态。
        { // 开始解除活动轮次方法。
            if (round == null) // 判断传入轮次是否为空。
            { // 开始空轮次处理。
                return; // 空轮次无需解除。
            } // 结束空轮次处理。

            lock (_singleRunLock) // 加锁保护活动轮次引用。
            { // 开始活动轮次清理保护。
                if (ReferenceEquals(_activeSingleRunRound, round)) // 只清理当前仍然指向该轮次的引用。
                { // 开始匹配轮次清理。
                    _activeSingleRunRound = null; // 清空活动轮次。
                } // 结束匹配轮次清理。
            } // 结束活动轮次清理保护。
        } // 结束解除活动轮次方法。
        #endregion
    } // 结束 SingletonManager 类体。
} // 结束 VMDemo 命名空间。
