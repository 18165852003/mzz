using System;                                    // 引入系统基础命名空间，提供基础类型
using System.Collections.Generic;                // 引入泛型集合命名空间，提供List<T>集合类型
using System.Diagnostics;                        // 引入诊断命名空间，提供Trace.WriteLine调试输出

namespace VMDemo                                 // 定义项目命名空间
{
    public sealed partial class SingletonManager // 声明单例管理器的partial分部类，本文件负责应用级日志功能
    {
        private readonly List<string> _logHistory = new List<string>(); // 只读日志历史列表，用于缓存历史日志记录
        private const int MaxLogHistoryCount = 200; // 日志历史最大保留条数常量，超过则移除最早的记录

        /// <summary>
        /// 应用级日志事件，MainForm 订阅后将日志显示到 Log ListBox。
        /// </summary>
        public event Action<string> LogReceived; // 定义应用级日志事件，外部订阅此事件接收日志消息

        /// <summary>
        /// 记录一条应用级日志，同步写 Trace、缓存历史、触发事件。
        /// </summary>
        public void AppendLog(string message)    // 公开方法：追加一条应用级日志
        {
            Trace.WriteLine($"[LOG] {message}"); // 输出到调试输出窗口，便于开发时查看日志
            lock (_logHistory)                   // 加锁保护日志历史列表，确保线程安全
            {
                _logHistory.Add(message);        // 将日志消息添加到历史列表末尾
                if (_logHistory.Count > MaxLogHistoryCount) // 检查历史记录是否超过最大保留条数
                    _logHistory.RemoveAt(0);     // 超过上限时移除最早的一条日志，防止内存无限增长
            }
            LogReceived?.Invoke(message);        // 触发日志事件，通知所有订阅者（如MainForm）显示日志
        }

        /// <summary>
        /// 获取历史日志快照，用于 MainForm 打开后补显示。
        /// </summary>
        public List<string> GetLogHistory()      // 公开方法：获取日志历史快照
        {
            lock (_logHistory)                   // 加锁确保线程安全
            {
                return new List<string>(_logHistory); // 返回历史列表的副本，避免外部修改原列表
            }
        }
    }
}
