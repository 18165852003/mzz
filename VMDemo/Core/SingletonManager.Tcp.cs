using System;                                    // 引入系统基础命名空间，提供基础类型和异常处理
using System.Collections.Generic;                // 引入泛型集合命名空间，提供List<T>等集合类型
using System.Diagnostics;                        // 引入诊断命名空间，提供Trace.WriteLine调试输出
using System.IO;                                 // 引入IO命名空间，提供文件读写和路径操作
using System.Net;                                // 引入网络命名空间，提供IPAddress等网络类型
using System.Net.Sockets;                        // 引入Socket命名空间，提供TcpListener/TcpClient等TCP通信类
using System.Text;                               // 引入文本命名空间，提供Encoding编码转换
using System.Threading;                          // 引入线程命名空间，提供CancellationTokenSource取消令牌
using System.Threading.Tasks;                    // 引入任务命名空间，提供Task.Run异步任务
using System.Web.Script.Serialization;           // 引入JavaScript序列化命名空间，提供JSON序列化/反序列化

namespace VMDemo                                 // 定义项目命名空间
{
    public sealed partial class SingletonManager // 声明单例管理器的partial分部类，本文件负责TCP通信功能
    {
        #region TCP 通讯

        /// <summary>
        /// TCP 配置类，用于保存和读取本地 TCP 服务端参数。
        /// </summary>
        private class TcpConfig                  // 定义私有内部类，用于封装TCP配置参数
        {
            /// <summary>本程序 TCP 服务端监听 IP。</summary>
            public string ServerIp { get; set; } // TCP服务端监听的IP地址属性

            /// <summary>本程序 TCP 服务端监听端口。</summary>
            public int ServerPort { get; set; }  // TCP服务端监听的端口号属性

            /// <summary>程序下次启动后是否自动启动 TCP 服务端。</summary>
            public bool AutoStart { get; set; }  // 是否自动启动TCP服务端的开关属性

            /// <summary>最后保存配置的时间字符串，便于排查配置是否生效。</summary>
            public string LastSavedTime { get; set; } // 记录最后保存配置的时间戳属性
        }

        // TCP 服务端运行状态字段。
        private TcpListener _tcpListener;                   // TCP服务端监听器实例，用于监听客户端连接请求
        private TcpClient _tcpClient;                       // 当前已连接的TCP客户端实例
        private NetworkStream _tcpStream;                   // 当前客户端的网络数据流，用于读写数据
        private CancellationTokenSource _tcpServerCts;      // 取消令牌源，用于控制后台任务的取消
        private readonly object _tcpLock = new object();    // 线程锁对象，用于保护共享资源的线程安全
        private readonly List<string> _tcpLogHistory = new List<string>(); // TCP日志历史记录列表，最多保留200条
        private TcpConfig _tcpConfig;                       // TCP配置缓存实例，避免重复读取配置文件

        /// <summary>
        /// 获取 TCP 服务端是否已经启动监听。
        /// </summary>
        public bool IsTcpServerRunning            // 公开属性：判断TCP服务端是否正在运行
        {
            get
            {
                lock (_tcpLock)                   // 加锁确保线程安全
                {
                    return _tcpListener != null;  // 监听器不为null表示服务端已启动
                }
            }
        }

        /// <summary>
        /// 获取是否已有客户端连接。
        /// </summary>
        public bool IsTcpClientConnected          // 公开属性：判断是否有客户端连接
        {
            get
            {
                lock (_tcpLock)                   // 加锁确保线程安全
                {
                    return _tcpClient != null && _tcpClient.Connected; // 客户端不为null且处于已连接状态
                }
            }
        }

        /// <summary>
        /// TCP 日志事件，SingletonManager 只负责抛出日志，TCPForm 负责显示。
        /// </summary>
        public event Action<string> TcpLogReceived; // 定义TCP日志事件，外部订阅此事件接收TCP日志

        /// <summary>
        /// 记录 TCP 日志，并同步缓存到历史列表。
        /// </summary>
        private void AppendTcpLog(string message) // 私有方法：追加TCP日志
        {
            Trace.WriteLine($"[TCP] {message}");  // 输出到调试输出窗口，便于开发调试

            lock (_tcpLock)                       // 加锁保护日志历史列表
            {
                _tcpLogHistory.Add(message);      // 将日志消息添加到历史列表末尾
                if (_tcpLogHistory.Count > 200)   // 如果历史记录超过200条
                {
                    _tcpLogHistory.RemoveAt(0);   // 移除最早的一条日志，防止内存无限增长
                }
            }

            TcpLogReceived?.Invoke(message);      // 触发TCP日志事件，通知外部订阅者（如TCPForm）
            AppendLog($"[TCP] {message}");        // 同时追加到应用级主日志，确保日志不丢失
        }

        /// <summary>
        /// 获取已经产生的 TCP 日志快照，用于 TCPForm 打开后补显示启动阶段日志。
        /// </summary>
        public List<string> GetTcpLogHistory()    // 公开方法：获取TCP日志历史快照
        {
            lock (_tcpLock)                       // 加锁确保线程安全
            {
                return new List<string>(_tcpLogHistory); // 返回历史列表的副本，避免外部修改原列表
            }
        }

        /// <summary>
        /// 获取配置文件的完整路径。
        /// </summary>
        private string GetTcpConfigPath()         // 私有方法：获取TCP配置文件路径
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config"); // 拼接config子目录路径
            return Path.Combine(dir, "tcp_config.json"); // 返回完整的配置文件路径：程序目录/config/tcp_config.json
        }

        /// <summary>
        /// 读取本地 tcp_config.json 配置文件。
        /// 如果文件不存在或损坏，返回默认配置，不影响程序启动。
        /// </summary>
        private TcpConfig LoadTcpConfig()         // 私有方法：从文件加载TCP配置
        {
            string path = GetTcpConfigPath();     // 获取配置文件完整路径
            if (!File.Exists(path))               // 检查配置文件是否存在
            {
                // 文件不存在时返回默认配置：本地回环地址、7777端口、自动启动
                return new TcpConfig { ServerIp = "127.0.0.1", ServerPort = 7777, AutoStart = true };
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8); // 以UTF8编码读取配置文件全部内容
                JavaScriptSerializer serializer = new JavaScriptSerializer(); // 创建JSON序列化器实例
                TcpConfig config = serializer.Deserialize<TcpConfig>(json);   // 将JSON字符串反序列化为TcpConfig对象

                // 校验配置有效性：配置不为null、IP不为空白、端口在1-65535范围内
                if (config == null || string.IsNullOrWhiteSpace(config.ServerIp) || config.ServerPort <= 0 || config.ServerPort > 65535)
                {
                    AppendTcpLog("配置文件内容无效，使用默认配置。"); // 记录配置无效的日志
                    // 返回默认配置
                    return new TcpConfig { ServerIp = "127.0.0.1", ServerPort = 7777, AutoStart = true };
                }

                return config;                    // 返回有效的配置对象
            }
            catch (Exception ex)                  // 捕获读取或反序列化过程中的异常
            {
                AppendTcpLog($"读取配置文件失败：{ex.Message}，使用默认配置。"); // 记录异常日志
                // 返回默认配置，确保程序能正常启动
                return new TcpConfig { ServerIp = "127.0.0.1", ServerPort = 7777, AutoStart = true };
            }
        }

        /// <summary>
        /// 保存监听 IP、监听端口到本地 json 文件，自动设置 AutoStart=true。
        /// </summary>
        public void SaveTcpConfig(string serverIp, int serverPort) // 公开方法：保存TCP配置到文件
        {
            if (string.IsNullOrWhiteSpace(serverIp)) // 校验IP地址是否为空或空白
            {
                AppendTcpLog("保存失败：IP 地址不能为空。"); // 记录校验失败日志
                return;                               // 校验不通过，直接返回
            }

            if (serverPort <= 0 || serverPort > 65535) // 校验端口号是否在有效范围内
            {
                AppendTcpLog("保存失败：端口号必须在 1-65535 范围内。"); // 记录校验失败日志
                return;                               // 校验不通过，直接返回
            }

            TcpConfig config = new TcpConfig      // 创建新的配置对象
            {
                ServerIp = serverIp,              // 设置监听IP地址
                ServerPort = serverPort,          // 设置监听端口号
                AutoStart = true,                 // 保存后自动设置为自动启动模式
                LastSavedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") // 记录当前保存时间
            };

            try
            {
                string path = GetTcpConfigPath(); // 获取配置文件完整路径
                string dir = Path.GetDirectoryName(path); // 获取配置文件所在目录路径
                if (!Directory.Exists(dir))       // 检查目录是否存在
                {
                    Directory.CreateDirectory(dir); // 目录不存在则创建
                }

                JavaScriptSerializer serializer = new JavaScriptSerializer(); // 创建JSON序列化器实例
                string json = serializer.Serialize(config); // 将配置对象序列化为JSON字符串
                File.WriteAllText(path, json, Encoding.UTF8); // 以UTF8编码将JSON写入配置文件
                _tcpConfig = config;              // 更新内存中的配置缓存
                AppendTcpLog($"配置已保存：{serverIp}:{serverPort}"); // 记录保存成功日志
            }
            catch (Exception ex)                  // 捕获写入文件过程中的异常
            {
                AppendTcpLog($"保存配置文件失败：{ex.Message}"); // 记录保存失败的异常日志
            }
        }

        /// <summary>
        /// 获取当前缓存的 TCP 配置，如果未缓存则从文件加载。
        /// </summary>
        private TcpConfig GetTcpConfig()          // 私有方法：获取TCP配置（带缓存）
        {
            if (_tcpConfig == null)               // 检查缓存是否为空
            {
                _tcpConfig = LoadTcpConfig();     // 缓存为空时从文件加载配置
            }

            return _tcpConfig;                    // 返回缓存的配置对象
        }

        /// <summary>
        /// 获取当前缓存的 TCP 配置中的监听 IP。
        /// </summary>
        public string GetTcpConfigIp()            // 公开方法：获取配置中的监听IP
        {
            return GetTcpConfig().ServerIp;       // 返回配置中的ServerIp属性值
        }

        /// <summary>
        /// 获取当前缓存的 TCP 配置中的监听端口。
        /// </summary>
        public int GetTcpConfigPort()             // 公开方法：获取配置中的监听端口
        {
            return GetTcpConfig().ServerPort;     // 返回配置中的ServerPort属性值
        }

        /// <summary>
        /// 程序启动后自动读取配置，如果 AutoStart=true 则后台启动 TCP 服务端监听。
        /// 启动失败只记录日志，不弹窗阻塞主界面。
        /// </summary>
        public void TryAutoStartTcpServer()       // 公开方法：尝试自动启动TCP服务端
        {
            try
            {
                _tcpConfig = LoadTcpConfig();     // 从文件加载TCP配置
                if (!_tcpConfig.AutoStart)        // 检查是否启用自动启动
                {
                    AppendTcpLog("自动启动已关闭，跳过 TCP 服务端启动。"); // 自动启动关闭，记录日志
                    return;                       // 不自动启动，直接返回
                }

                string ip = _tcpConfig.ServerIp;  // 获取配置中的监听IP
                int port = _tcpConfig.ServerPort; // 获取配置中的监听端口
                // 校验IP和端口是否有效
                if (string.IsNullOrWhiteSpace(ip) || port <= 0 || port > 65535)
                {
                    AppendTcpLog("自动启动失败：配置中的 IP 或端口无效。"); // 配置无效，记录日志
                    return;                       // 配置无效，直接返回
                }

                Task.Run(() =>                    // 在后台线程中启动TCP服务端，避免阻塞主线程
                {
                    try
                    {
                        StartTcpServer(ip, port); // 调用启动TCP服务端方法
                    }
                    catch (Exception ex)          // 捕获启动过程中的异常
                    {
                        AppendTcpLog($"自动启动 TCP 服务端失败：{ex.Message}"); // 记录启动失败日志
                    }
                });
            }
            catch (Exception ex)                  // 捕获加载配置过程中的异常
            {
                AppendTcpLog($"自动启动异常：{ex.Message}"); // 记录异常日志
            }
        }

        /// <summary>
        /// 校验 IP 和端口是否有效，并输出解析后的 IPAddress。
        /// </summary>
        private bool ValidateTcpEndpoint(string serverIp, int serverPort, out IPAddress ipAddress) // 私有方法：校验TCP端点参数
        {
            ipAddress = null;                     // 初始化输出参数为null

            if (string.IsNullOrWhiteSpace(serverIp)) // 校验IP地址是否为空或空白
            {
                AppendTcpLog("启动失败：监听 IP 地址不能为空。"); // 记录校验失败日志
                return false;                     // 返回校验失败
            }

            if (serverPort <= 0 || serverPort > 65535) // 校验端口号是否在有效范围内
            {
                AppendTcpLog("启动失败：端口号必须在 1-65535 范围内。"); // 记录校验失败日志
                return false;                     // 返回校验失败
            }

            try
            {
                ipAddress = IPAddress.Parse(serverIp); // 将IP字符串解析为IPAddress对象
            }
            catch                                 // 解析失败时捕获异常
            {
                AppendTcpLog($"启动失败：IP 地址格式无效：{serverIp}"); // 记录IP格式错误日志
                return false;                     // 返回校验失败
            }

            return true;                          // 所有校验通过，返回成功
        }

        /// <summary>
        /// 关闭当前已连接的客户端及其网络流，并清空字段。
        /// 调用方需要自行持有 _tcpLock。
        /// </summary>
        private void CloseCurrentTcpClient()      // 私有方法：关闭当前TCP客户端连接
        {
            try { _tcpStream?.Close(); } catch { } // 安全关闭网络流，忽略可能的异常
            try { _tcpClient?.Close(); } catch { } // 安全关闭TCP客户端，忽略可能的异常

            _tcpStream = null;                    // 清空网络流引用
            _tcpClient = null;                    // 清空客户端引用
        }

        /// <summary>
        /// 清空 TCP 服务端监听器和取消令牌字段。
        /// 调用方需要自行持有 _tcpLock。
        /// </summary>
        private void ClearTcpServerState()        // 私有方法：清空TCP服务端状态
        {
            _tcpListener = null;                  // 清空监听器引用
            _tcpServerCts = null;                 // 清空取消令牌源引用
        }

        /// <summary>
        /// 启动 TCP 服务端监听，并在后台等待客户端连接。
        /// 启动成功后自动保存配置。
        /// </summary>
        public void StartTcpServer(string serverIp, int serverPort) // 公开方法：启动TCP服务端
        {
            lock (_tcpLock)                       // 加锁检查服务端状态
            {
                if (_tcpListener != null)         // 检查监听器是否已存在
                {
                    AppendTcpLog("TCP 服务端已在运行，请勿重复启动。"); // 服务端已运行，记录日志
                    return;                       // 已在运行，直接返回
                }
            }

            IPAddress ipAddress;                  // 声明IPAddress变量用于接收解析结果
            if (!ValidateTcpEndpoint(serverIp, serverPort, out ipAddress)) // 校验IP和端口参数
            {
                return;                           // 校验失败，直接返回
            }

            try
            {
                TcpListener listener = new TcpListener(ipAddress, serverPort); // 创建TCP监听器实例
                listener.Start();                 // 开始监听指定IP和端口

                CancellationTokenSource cts = new CancellationTokenSource(); // 创建取消令牌源

                lock (_tcpLock)                   // 加锁更新服务端状态
                {
                    _tcpListener = listener;      // 保存监听器实例
                    _tcpServerCts = cts;          // 保存取消令牌源
                }

                AcceptClientLoop(listener, cts.Token); // 启动后台客户端连接等待循环
                SaveTcpConfig(serverIp, serverPort);   // 启动成功后自动保存配置到文件
                AppendTcpLog($"TCP 服务端已启动，监听 {serverIp}:{serverPort}"); // 记录启动成功日志
            }
            catch (Exception ex)                  // 捕获启动过程中的异常
            {
                AppendTcpLog($"TCP 服务端启动失败：{ex.Message}"); // 记录启动失败日志
                throw;                            // 重新抛出异常，让调用方知道启动失败
            }
        }

        /// <summary>
        /// 停止 TCP 服务端，断开当前客户端，停止监听和接收循环。
        /// 可以重复调用，多次调用不抛异常。
        /// </summary>
        public void StopTcpServer()               // 公开方法：停止TCP服务端
        {
            lock (_tcpLock)                       // 加锁确保线程安全
            {
                try { _tcpServerCts?.Cancel(); } catch { } // 安全取消后台任务，忽略异常
                CloseCurrentTcpClient();          // 关闭当前已连接的客户端
                try { _tcpListener?.Stop(); } catch { } // 安全停止监听器，忽略异常
                ClearTcpServerState();            // 清空服务端状态字段
            }

            AppendTcpLog("TCP 服务端已停止。");   // 记录服务端停止日志
        }

        /// <summary>
        /// 后台循环等待客户端连接。
        /// 当前策略是只保留一个客户端，新客户端连接时断开旧客户端。
        /// </summary>
        private void AcceptClientLoop(TcpListener listener, CancellationToken ct) // 私有方法：客户端连接等待循环
        {
            Task.Run(async () =>                  // 在后台线程中异步运行连接等待循环
            {
                while (!ct.IsCancellationRequested) // 循环直到收到取消请求
                {
                    try
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync(); // 异步等待客户端连接

                        lock (_tcpLock)           // 加锁更新客户端状态
                        {
                            CloseCurrentTcpClient(); // 关闭旧的客户端连接（只保留一个客户端）
                            _tcpClient = client; // 保存新连接的客户端实例
                            _tcpStream = client.GetStream(); // 获取新客户端的网络数据流
                        }

                        AppendTcpLog($"客户端已连接：{client.Client.RemoteEndPoint}"); // 记录客户端连接成功日志
                        ReceiveClientLoop(client, client.GetStream(), ct); // 启动该客户端的数据接收循环
                    }
                    catch (Exception ex)          // 捕获等待连接过程中的异常
                    {
                        if (!ct.IsCancellationRequested) // 仅在非取消状态下记录异常
                        {
                            AppendTcpLog($"等待客户端连接异常：{ex.Message}"); // 记录连接异常日志
                        }
                    }
                }
            }, ct);                               // 将取消令牌传递给Task.Run
        }

        /// <summary>
        /// 后台循环接收当前客户端发来的消息。
        /// </summary>
        private void ReceiveClientLoop(TcpClient client, NetworkStream stream, CancellationToken ct) // 私有方法：数据接收循环
        {
            Task.Run(() =>                        // 在后台线程中运行数据接收循环
            {
                byte[] buffer = new byte[4096];   // 创建4096字节的接收缓冲区

                try
                {
                    // 循环条件：未收到取消请求且客户端仍处于连接状态
                    while (!ct.IsCancellationRequested && client.Connected)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length); // 同步读取客户端数据到缓冲区
                        if (bytesRead == 0)       // 读取到0字节表示客户端已断开连接
                        {
                            AppendTcpLog("客户端已断开连接。"); // 记录客户端断开日志
                            break;                // 跳出接收循环
                        }

                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead); // 将接收到的字节数据转换为UTF8字符串
                        AppendTcpLog($"收到客户端数据：{message}"); // 记录接收到的数据日志
                        HandleTcpClientMessage(message); // 解析测试阶段 RoundId 下发协议，并根据状态回复客户端
                    }
                }
                catch (Exception ex)              // 捕获接收数据过程中的异常
                {
                    if (!ct.IsCancellationRequested) // 仅在非取消状态下记录异常
                    {
                        AppendTcpLog($"接收客户端数据异常：{ex.Message}"); // 记录接收异常日志
                    }
                }
                finally                           // 无论正常结束还是异常都会执行
                {
                    lock (_tcpLock)               // 加锁检查客户端状态
                    {
                        if (_tcpClient == client) // 仅当当前客户端仍是同一实例时才关闭（防止误关新客户端）
                        {
                            CloseCurrentTcpClient(); // 关闭客户端连接并清空引用
                        }
                    }
                }
            }, ct);                               // 将取消令牌传递给Task.Run
        }

        /// <summary>
        /// 处理 TCP 客户端发来的测试阶段 RoundId 下发协议。
        /// 当前支持格式：ROUND|R001，可一次接收多行命令。
        /// </summary>
        private void HandleTcpClientMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                SendTcpMessage("ERR|BAD_COMMAND");
                return;
            }

            string[] commands = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string command in commands)
            {
                string roundId;
                if (!TryParseRoundCommand(command, out roundId))
                {
                    AppendTcpLog($"未知客户端命令：{command.Trim()}");
                    SendTcpMessage("ERR|BAD_COMMAND");
                    continue;
                }

                string errorMessage;
                RoundAcceptResult acceptResult = TryAcceptRoundId(roundId, out errorMessage);
                if (acceptResult == RoundAcceptResult.Accepted)
                {
                    AppendTcpLog($"已接收RoundId：{roundId}，等待单次执行。");
                    SendTcpMessage($"ACK|{roundId}|READY");
                }
                else if (acceptResult == RoundAcceptResult.Used)
                {
                    AppendTcpLog($"RoundId接收失败：{errorMessage}");
                    SendTcpMessage($"ERR|ROUND_USED|{roundId}");
                }
                else if (acceptResult == RoundAcceptResult.Busy)
                {
                    AppendTcpLog($"RoundId接收失败：{errorMessage}");
                    SendTcpMessage($"BUSY|{roundId}");
                }
                else
                {
                    AppendTcpLog($"RoundId接收失败：{errorMessage}");
                    SendTcpMessage($"ERR|BAD_ROUND|{roundId}");
                }
            }
        }

        /// <summary>
        /// 解析客户端下发的 RoundId 命令。
        /// 第一版仅支持 ROUND|RoundId，保持协议简单，便于本地图片流程测试。
        /// </summary>
        private bool TryParseRoundCommand(string message, out string roundId)
        {
            roundId = null;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string[] parts = message.Trim().Split('|');
            if (parts.Length != 2 || !string.Equals(parts[0], "ROUND", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            roundId = parts[1].Trim();
            return !string.IsNullOrWhiteSpace(roundId);
        }

        /// <summary>
        /// 服务端主动发送文本给当前已连接的客户端。
        /// </summary>
        public void SendTcpMessage(string message) // 公开方法：向已连接客户端发送消息
        {
            if (string.IsNullOrEmpty(message))    // 校验消息内容是否为空
            {
                AppendTcpLog("发送失败：消息内容为空。"); // 消息为空，记录日志
                return;                           // 直接返回不发送
            }

            lock (_tcpLock)                       // 加锁确保线程安全
            {
                if (_tcpListener == null)         // 检查TCP服务端是否已启动
                {
                    AppendTcpLog("发送失败：TCP 服务端未启动。"); // 服务端未启动，记录日志
                    return;                       // 直接返回
                }

                // 检查客户端是否已连接且网络流可用
                if (_tcpClient == null || !_tcpClient.Connected || _tcpStream == null)
                {
                    AppendTcpLog("发送失败：TCP 服务端未连接客户端。"); // 无客户端连接，记录日志
                    return;                       // 直接返回
                }

                try
                {
                    byte[] data = Encoding.UTF8.GetBytes(message); // 将消息字符串转换为UTF8字节数组
                    _tcpStream.Write(data, 0, data.Length); // 将字节数据写入网络流发送给客户端
                    _tcpStream.Flush();           // 刷新网络流，确保数据立即发送
                    AppendTcpLog($"发送给客户端：{message}"); // 记录发送成功日志
                }
                catch (Exception ex)              // 捕获发送过程中的异常
                {
                    AppendTcpLog($"发送异常：{ex.Message}"); // 记录发送异常日志
                }
            }
        }

        #endregion
    }
}
