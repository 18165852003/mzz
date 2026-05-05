using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace VMDemo
{
    public sealed partial class SingletonManager
    {
        /// <summary>
        /// RoundId 接收结果，用于 TCP 层区分“接收成功、正在忙、已使用、格式无效”。
        /// </summary>
        private enum RoundAcceptResult
        {
            Accepted, // RoundId 已接收并进入待执行状态。
            Busy, // 当前已有待执行或正在执行的 RoundId。
            Used, // RoundId 已经使用过，不能重复接收。
            Invalid // RoundId 为空或格式无效。
        }

        /// <summary>
        /// RoundId 历史文件结构，用于持久化已经开始执行过的 RoundId。
        /// </summary>
        private class RoundHistory
        {
            public List<string> UsedRoundIds { get; set; } // 已经消费执行过的 RoundId 列表。
            public string LastSavedTime { get; set; } // 最近一次保存历史文件的时间。
        }

        /// <summary>
        /// 保护 PLC/TCP 下发的 RoundId 状态，避免 TCP 接收线程和执行线程并发修改。
        /// </summary>
        private readonly object _roundLock = new object();

        /// <summary>
        /// 已由 TCP 客户端下发、但尚未被单次执行消费的 RoundId。
        /// </summary>
        private string _pendingRoundId = null;

        /// <summary>
        /// 当前正在执行中的 RoundId，用于执行完成后回传 DONE 消息。
        /// </summary>
        private string _currentRoundId = null;

        /// <summary>
        /// 内存中的已使用 RoundId 集合，防止程序运行期间重复使用同一个 RoundId。
        /// </summary>
        private readonly HashSet<string> _usedRoundIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 标记 round_history.json 是否已经加载过，避免每次 TCP 消息都重复读文件。
        /// </summary>
        private bool _roundHistoryLoaded = false;

        /// <summary>
        /// 获取当前是否存在等待执行的 RoundId。
        /// PendingRoundId 表示 TCP 客户端已经下发，操作员可以点击单次执行。
        /// </summary>
        public bool HasPendingRoundId
        {
            get
            {
                lock (_roundLock) // 加锁读取待执行状态，避免 TCP 接收线程和 UI 线程并发访问。
                {
                    return !string.IsNullOrWhiteSpace(_pendingRoundId); // 有待执行 RoundId 时返回 true。
                }
            }
        }

        /// <summary>
        /// 获取当前等待执行的 RoundId 快照，仅用于界面提示和日志显示。
        /// </summary>
        public string GetPendingRoundId()
        {
            lock (_roundLock) // 加锁读取当前待执行 RoundId。
            {
                return _pendingRoundId; // 返回当前缓存的待执行 RoundId。
            }
        }

        /// <summary>
        /// 接收 TCP 客户端下发的 RoundId。
        /// 仅在 RoundId 未使用、没有待执行任务、没有正在执行任务时接收新值。
        /// </summary>
        private RoundAcceptResult TryAcceptRoundId(string roundId, out string errorMessage)
        {
            errorMessage = null; // 初始化错误信息。
            string normalizedRoundId = NormalizeRoundId(roundId); // 统一清理协议字段中的空白字符。

            if (string.IsNullOrWhiteSpace(normalizedRoundId)) // 判断 RoundId 是否为空。
            {
                errorMessage = "RoundId为空"; // 写入格式错误原因。
                return RoundAcceptResult.Invalid; // 返回无效状态。
            }

            EnsureRoundHistoryLoaded(); // 首次接收前加载本地已使用历史。

            lock (_roundLock) // 加锁修改 RoundId 状态机字段。
            {
                if (_usedRoundIds.Contains(normalizedRoundId)) // 已使用集合中存在该 RoundId 时拒绝接收。
                {
                    errorMessage = $"RoundId已使用过：{normalizedRoundId}"; // 写入重复使用原因。
                    return RoundAcceptResult.Used; // 返回已使用状态。
                }

                if (!string.IsNullOrWhiteSpace(_pendingRoundId)) // 已经有等待执行的 RoundId 时拒绝新的 RoundId。
                {
                    errorMessage = $"已有待执行RoundId：{_pendingRoundId}"; // 写入忙碌原因。
                    return RoundAcceptResult.Busy; // 返回忙碌状态。
                }

                if (!string.IsNullOrWhiteSpace(_currentRoundId)) // 当前已有正在执行的 RoundId 时拒绝新的 RoundId。
                {
                    errorMessage = $"当前RoundId正在执行：{_currentRoundId}"; // 写入正在执行原因。
                    return RoundAcceptResult.Busy; // 返回忙碌状态。
                }

                _pendingRoundId = normalizedRoundId; // 将新 RoundId 放入等待执行状态。
                return RoundAcceptResult.Accepted; // 返回接收成功状态。
            }
        }

        /// <summary>
        /// 点击单次执行时消费等待中的 RoundId。
        /// 消费成功后立刻写入已使用历史，保证即使后续 NG、超时或程序崩溃，该 RoundId 也不会再次使用。
        /// </summary>
        private bool TryConsumePendingRoundId(out string roundId, out string errorMessage)
        {
            roundId = null; // 初始化输出 RoundId。
            errorMessage = null; // 初始化错误信息。
            List<string> usedRoundIdsSnapshot = null; // 保存要落盘的已使用 RoundId 快照。

            EnsureRoundHistoryLoaded(); // 消费前确保本地历史已经加载。

            lock (_roundLock) // 加锁完成 pending -> current 的状态切换。
            {
                if (string.IsNullOrWhiteSpace(_pendingRoundId)) // 没有待执行 RoundId 时不能开始执行。
                {
                    errorMessage = "请先由 TCP 客户端发送 RoundId"; // 写入用户可理解的失败原因。
                    return false; // 返回消费失败。
                }

                if (!string.IsNullOrWhiteSpace(_currentRoundId)) // 已经有 RoundId 正在执行时不能重复开始。
                {
                    errorMessage = $"当前RoundId正在执行：{_currentRoundId}"; // 写入执行中原因。
                    return false; // 返回消费失败。
                }

                if (_usedRoundIds.Contains(_pendingRoundId)) // 防御性检查：待执行值如果已在历史中，则不能再执行。
                {
                    errorMessage = $"RoundId已使用过：{_pendingRoundId}"; // 写入重复使用原因。
                    _pendingRoundId = null; // 清掉异常的待执行状态。
                    return false; // 返回消费失败。
                }

                roundId = _pendingRoundId; // 输出本次要执行的业务 RoundId。
                _currentRoundId = _pendingRoundId; // 将待执行 RoundId 切换为正在执行状态。
                _pendingRoundId = null; // 清空待执行槽位，避免再次消费。
                _usedRoundIds.Add(_currentRoundId); // 立即标记为已使用，保证失败或超时后也不能复用。
                usedRoundIdsSnapshot = _usedRoundIds.OrderBy(id => id).ToList(); // 复制并排序历史快照，供锁外写文件。
            }

            SaveRoundHistorySnapshot(usedRoundIdsSnapshot); // 将已使用历史落盘。
            return true; // 返回消费成功。
        }

        /// <summary>
        /// 当前轮次结束后清空正在执行的 RoundId。
        /// 只清空 CurrentRoundId，不会从已使用历史中删除该 RoundId。
        /// </summary>
        private void ClearCurrentRoundId(string roundId)
        {
            string normalizedRoundId = NormalizeRoundId(roundId); // 统一清理传入的 RoundId。

            lock (_roundLock) // 加锁清理正在执行状态。
            {
                if (string.Equals(_currentRoundId, normalizedRoundId, StringComparison.Ordinal)) // 只清理当前轮次对应的 RoundId。
                {
                    _currentRoundId = null; // 清空正在执行状态。
                }
            }
        }

        /// <summary>
        /// 获取 RoundId 历史文件路径。
        /// 文件位于程序目录 config/round_history.json。
        /// </summary>
        private string GetRoundHistoryPath()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config"); // 拼接程序输出目录下的 config 目录。
            return Path.Combine(dir, "round_history.json"); // 返回 RoundId 历史文件完整路径。
        }

        /// <summary>
        /// 首次使用 RoundId 状态时加载本地历史文件，恢复程序重启前已经使用过的 RoundId。
        /// </summary>
        private void EnsureRoundHistoryLoaded()
        {
            lock (_roundLock) // 加锁检查历史是否已经加载。
            {
                if (_roundHistoryLoaded) // 已加载过则不重复读取文件。
                {
                    return; // 直接返回，避免重复 IO。
                }
            }

            List<string> loadedRoundIds = new List<string>(); // 保存从文件读取到的有效 RoundId。
            string path = GetRoundHistoryPath(); // 获取历史文件路径。

            if (File.Exists(path)) // 文件存在时才尝试读取。
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8); // 使用 UTF-8 读取历史 JSON。
                    JavaScriptSerializer serializer = new JavaScriptSerializer(); // 创建 JSON 反序列化器。
                    RoundHistory history = serializer.Deserialize<RoundHistory>(json); // 将 JSON 转为历史对象。

                    if (history != null && history.UsedRoundIds != null) // 判断历史对象和列表是否有效。
                    {
                        foreach (string item in history.UsedRoundIds) // 遍历文件中的每个 RoundId。
                        {
                            string normalizedRoundId = NormalizeRoundId(item); // 清理历史值中的空白字符。
                            if (!string.IsNullOrWhiteSpace(normalizedRoundId)) // 只保留有效 RoundId。
                            {
                                loadedRoundIds.Add(normalizedRoundId); // 加入待合并历史列表。
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"读取RoundId历史失败：{ex.Message}", LogLevel.Error); // 读取失败时只记日志，不阻塞程序启动。
                }
            }

            lock (_roundLock) // 加锁把文件历史合并到内存集合。
            {
                foreach (string item in loadedRoundIds) // 遍历本次读取到的历史 RoundId。
                {
                    _usedRoundIds.Add(item); // 写入已使用集合，HashSet 会自动去重。
                }

                _roundHistoryLoaded = true; // 标记历史已经加载完成。
            }
        }

        /// <summary>
        /// 保存已使用 RoundId 快照到本地文件。
        /// 写文件不长期占用 _roundLock，避免阻塞 TCP 接收线程。
        /// </summary>
        private void SaveRoundHistorySnapshot(List<string> usedRoundIdsSnapshot)
        {
            if (usedRoundIdsSnapshot == null) // 没有快照时无需写文件。
            {
                return; // 直接返回。
            }

            try
            {
                string path = GetRoundHistoryPath(); // 获取历史文件完整路径。
                string dir = Path.GetDirectoryName(path); // 获取历史文件所在目录。
                if (!Directory.Exists(dir)) // 目录不存在时先创建。
                {
                    Directory.CreateDirectory(dir); // 创建 config 目录。
                }

                RoundHistory history = new RoundHistory // 构建要写入本地文件的历史对象。
                {
                    UsedRoundIds = usedRoundIdsSnapshot, // 写入已使用 RoundId 快照。
                    LastSavedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") // 记录当前保存时间。
                };

                JavaScriptSerializer serializer = new JavaScriptSerializer(); // 创建 JSON 序列化器。
                string json = serializer.Serialize(history); // 将历史对象序列化为 JSON。
                File.WriteAllText(path, json, Encoding.UTF8); // 使用 UTF-8 写入历史文件。
                AppendLog($"RoundId历史已保存：{usedRoundIdsSnapshot.Count}条"); // 记录保存结果。
            }
            catch (Exception ex)
            {
                AppendLog($"保存RoundId历史失败：{ex.Message}", LogLevel.Error); // 写文件失败时记录日志，避免异常冒泡影响执行线程。
            }
        }

        /// <summary>
        /// 统一清理 RoundId 字符串，避免协议中的换行或空格影响比较。
        /// </summary>
        private string NormalizeRoundId(string roundId)
        {
            return string.IsNullOrWhiteSpace(roundId) ? null : roundId.Trim(); // 空白值统一返回 null，非空值去掉首尾空白。
        }
    }
}
