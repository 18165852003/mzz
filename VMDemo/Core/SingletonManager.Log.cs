using System;                                    // 引入系统基础命名空间，提供基础类型
using System.Collections.Generic;                // 引入泛型集合命名空间，提供List<T>集合类型
using System.Diagnostics;                        // 引入诊断命名空间，提供Trace.WriteLine调试输出 b

namespace VMDemo                                 // 定义项目命名空间
{
    public enum LogLevel                         // 定义应用级日志级别，用于界面按级别着色
    {
        Info,                                    // 普通信息日志
        Warning,                                 // 预警日志，当前预留给后续扩展
        Error                                    // 错误日志
    }

    public sealed class LogEntry                 // 定义日志实体，避免界面通过文本关键字猜测日志级别
    {
        public DateTime Time { get; set; }       // 日志产生时间
        public LogLevel Level { get; set; }      // 日志级别
        public string Message { get; set; }      // 日志正文

        public override string ToString()        // ListBox 默认显示时使用统一格式
        {
            return $"[{Time:yyyy-MM-dd HH:mm:ss:fff}] {Message}";
        }
    }

    public sealed partial class SingletonManager // 声明单例管理器的partial分部类，本文件负责应用级日志功能
    {
        private readonly List<LogEntry> _logHistory = new List<LogEntry>(); // 只读日志历史列表，用于缓存历史日志记录
        private const int MaxLogHistoryCount = 200; // 日志历史最大保留条数常量，超过则移除最早的记录

        /// <summary>
        /// 应用级日志事件，MainForm 订阅后将日志显示到 Log ListBox。
        /// </summary>
        public event Action<LogEntry> LogReceived; // 定义应用级日志事件，外部订阅此事件接收日志消息

        /// <summary>
        /// 记录一条应用级日志，同步写 Trace、缓存历史、触发事件。
        /// </summary>
        public void AppendLog(string message, LogLevel level = LogLevel.Info) // 公开方法：追加一条应用级日志
        {
            LogEntry entry = new LogEntry        // 创建日志实体，把时间、级别和正文固定下来
            {
                Time = DateTime.Now,
                Level = level,
                Message = message
            };

            Trace.WriteLine($"[LOG][{level}] {message}"); // 输出到调试输出窗口，便于开发时查看日志
            lock (_logHistory)                   // 加锁保护日志历史列表，确保线程安全
            {
                _logHistory.Add(entry);          // 将日志消息添加到历史列表末尾
                if (_logHistory.Count > MaxLogHistoryCount) // 检查历史记录是否超过最大保留条数
                    _logHistory.RemoveAt(0);     // 超过上限时移除最早的一条日志，防止内存无限增长
            }
            LogReceived?.Invoke(entry);          // 触发日志事件，通知所有订阅者（如MainForm）显示日志
        }

        /// <summary>
        /// 获取历史日志快照，用于 MainForm 打开后补显示。
        /// </summary>
        public List<LogEntry> GetLogHistory()    // 公开方法：获取日志历史快照
        {
            lock (_logHistory)                   // 加锁确保线程安全
            {
                return new List<LogEntry>(_logHistory); // 返回历史列表的副本，避免外部修改原列表
            }
        }
    }
}
