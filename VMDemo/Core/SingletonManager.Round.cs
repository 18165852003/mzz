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
            Accepted,
            Busy,
            Used,
            Invalid
        }

        /// <summary>
        /// RoundId 历史文件结构，用于持久化已经开始执行过的 RoundId。
        /// </summary>
        private class RoundHistory
        {
            public List<string> UsedRoundIds { get; set; }
            public string LastSavedTime { get; set; }
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
                lock (_roundLock)
                {
                    return !string.IsNullOrWhiteSpace(_pendingRoundId);
                }
            }
        }

        /// <summary>
        /// 获取当前等待执行的 RoundId 快照，仅用于界面提示和日志显示。
        /// </summary>
        public string GetPendingRoundId()
        {
            lock (_roundLock)
            {
                return _pendingRoundId;
            }
        }

        /// <summary>
        /// 接收 TCP 客户端下发的 RoundId。
        /// 仅在 RoundId 未使用、没有待执行任务、没有正在执行任务时接收新值。
        /// </summary>
        private RoundAcceptResult TryAcceptRoundId(string roundId, out string errorMessage)
        {
            errorMessage = null;
            string normalizedRoundId = NormalizeRoundId(roundId);

            if (string.IsNullOrWhiteSpace(normalizedRoundId))
            {
                errorMessage = "RoundId为空";
                return RoundAcceptResult.Invalid;
            }

            EnsureRoundHistoryLoaded();

            lock (_roundLock)
            {
                if (_usedRoundIds.Contains(normalizedRoundId))
                {
                    errorMessage = $"RoundId已使用过：{normalizedRoundId}";
                    return RoundAcceptResult.Used;
                }

                if (!string.IsNullOrWhiteSpace(_pendingRoundId))
                {
                    errorMessage = $"已有待执行RoundId：{_pendingRoundId}";
                    return RoundAcceptResult.Busy;
                }

                if (!string.IsNullOrWhiteSpace(_currentRoundId))
                {
                    errorMessage = $"当前RoundId正在执行：{_currentRoundId}";
                    return RoundAcceptResult.Busy;
                }

                _pendingRoundId = normalizedRoundId;
                return RoundAcceptResult.Accepted;
            }
        }

        /// <summary>
        /// 点击单次执行时消费等待中的 RoundId。
        /// 消费成功后立刻写入已使用历史，保证即使后续 NG、超时或程序崩溃，该 RoundId 也不会再次使用。
        /// </summary>
        private bool TryConsumePendingRoundId(out string roundId, out string errorMessage)
        {
            roundId = null;
            errorMessage = null;
            List<string> usedRoundIdsSnapshot = null;

            EnsureRoundHistoryLoaded();

            lock (_roundLock)
            {
                if (string.IsNullOrWhiteSpace(_pendingRoundId))
                {
                    errorMessage = "请先由 TCP 客户端发送 RoundId";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(_currentRoundId))
                {
                    errorMessage = $"当前RoundId正在执行：{_currentRoundId}";
                    return false;
                }

                if (_usedRoundIds.Contains(_pendingRoundId))
                {
                    errorMessage = $"RoundId已使用过：{_pendingRoundId}";
                    _pendingRoundId = null;
                    return false;
                }

                roundId = _pendingRoundId;
                _currentRoundId = _pendingRoundId;
                _pendingRoundId = null;
                _usedRoundIds.Add(_currentRoundId);
                usedRoundIdsSnapshot = _usedRoundIds.OrderBy(id => id).ToList();
            }

            SaveRoundHistorySnapshot(usedRoundIdsSnapshot);
            return true;
        }

        /// <summary>
        /// 当前轮次结束后清空正在执行的 RoundId。
        /// 只清空 CurrentRoundId，不会从已使用历史中删除该 RoundId。
        /// </summary>
        private void ClearCurrentRoundId(string roundId)
        {
            string normalizedRoundId = NormalizeRoundId(roundId);

            lock (_roundLock)
            {
                if (string.Equals(_currentRoundId, normalizedRoundId, StringComparison.Ordinal))
                {
                    _currentRoundId = null;
                }
            }
        }

        /// <summary>
        /// 获取 RoundId 历史文件路径。
        /// 文件位于程序目录 config/round_history.json。
        /// </summary>
        private string GetRoundHistoryPath()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config");
            return Path.Combine(dir, "round_history.json");
        }

        /// <summary>
        /// 首次使用 RoundId 状态时加载本地历史文件，恢复程序重启前已经使用过的 RoundId。
        /// </summary>
        private void EnsureRoundHistoryLoaded()
        {
            lock (_roundLock)
            {
                if (_roundHistoryLoaded)
                {
                    return;
                }
            }

            List<string> loadedRoundIds = new List<string>();
            string path = GetRoundHistoryPath();

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    RoundHistory history = serializer.Deserialize<RoundHistory>(json);

                    if (history != null && history.UsedRoundIds != null)
                    {
                        foreach (string item in history.UsedRoundIds)
                        {
                            string normalizedRoundId = NormalizeRoundId(item);
                            if (!string.IsNullOrWhiteSpace(normalizedRoundId))
                            {
                                loadedRoundIds.Add(normalizedRoundId);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($"读取RoundId历史失败：{ex.Message}");
                }
            }

            lock (_roundLock)
            {
                foreach (string item in loadedRoundIds)
                {
                    _usedRoundIds.Add(item);
                }

                _roundHistoryLoaded = true;
            }
        }

        /// <summary>
        /// 保存已使用 RoundId 快照到本地文件。
        /// 写文件不长期占用 _roundLock，避免阻塞 TCP 接收线程。
        /// </summary>
        private void SaveRoundHistorySnapshot(List<string> usedRoundIdsSnapshot)
        {
            if (usedRoundIdsSnapshot == null)
            {
                return;
            }

            try
            {
                string path = GetRoundHistoryPath();
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                RoundHistory history = new RoundHistory
                {
                    UsedRoundIds = usedRoundIdsSnapshot,
                    LastSavedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(history);
                File.WriteAllText(path, json, Encoding.UTF8);
                AppendLog($"RoundId历史已保存：{usedRoundIdsSnapshot.Count}条");
            }
            catch (Exception ex)
            {
                AppendLog($"保存RoundId历史失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 统一清理 RoundId 字符串，避免协议中的换行或空格影响比较。
        /// </summary>
        private string NormalizeRoundId(string roundId)
        {
            return string.IsNullOrWhiteSpace(roundId) ? null : roundId.Trim();
        }
    }
}
