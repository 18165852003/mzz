using System;
using System.Windows.Forms;
using Sunny.UI;

namespace VMDemo
{
    public partial class TCPForm : UIForm
    {
        public TCPForm()
        {
            InitializeComponent();
            Load += TCPForm_Load;
            FormClosed += TCPForm_FormClosed;
            uiButton1.Click += UiButton1_Click;
            uiButton2.Click += UiButton2_Click;
            uiButton3.Click += UiButton3_Click;
            uiButton4.Click += UiButton4_Click;
        }

        /// <summary>
        /// 窗体加载时从 SingletonManager 读取配置填入输入框，并订阅日志事件。
        /// </summary>
        private void TCPForm_Load(object sender, EventArgs e)
        {
            uiTextBox1.Text = SingletonManager.Instance.GetTcpConfigIp();
            uiTextBox2.Text = SingletonManager.Instance.GetTcpConfigPort().ToString();
            SingletonManager.Instance.TcpLogReceived += OnTcpLogReceived;
            foreach (string message in SingletonManager.Instance.GetTcpLogHistory())
            {
                AppendLog(message);
            }
            UpdateButtonState();
        }

        /// <summary>
        /// 窗体关闭时取消订阅日志事件，防止后台线程访问已释放的控件。
        /// </summary>
        private void TCPForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            SingletonManager.Instance.TcpLogReceived -= OnTcpLogReceived;
        }

        /// <summary>
        /// 接收 TCP 日志并显示到 TxtLog，使用 BeginInvoke 确保在 UI 线程执行。
        /// </summary>
        private void OnTcpLogReceived(string message)
        {
            if (TxtLog.InvokeRequired)
            {
                TxtLog.BeginInvoke(new Action(() => AppendLog(message)));
            }
            else
            {
                AppendLog(message);
            }
        }

        /// <summary>
        /// 将日志消息追加到 TxtLog 并自动滚动到底部。
        /// </summary>
        private void AppendLog(string message)
        {
            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        }

        /// <summary>
        /// 启动服务按钮：启动本程序的 TCP 服务端监听。
        /// </summary>
        private void UiButton1_Click(object sender, EventArgs e)
        {
            string ip = uiTextBox1.Text?.Trim();
            string portText = uiTextBox2.Text?.Trim();

            if (string.IsNullOrWhiteSpace(ip))
            {
                MessageBox.Show("请输入监听 IP 地址。");
                return;
            }

            int port;
            if (!int.TryParse(portText, out port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("请输入有效的端口号（1-65535）。");
                return;
            }

            try
            {
                SingletonManager.Instance.StartTcpServer(ip, port);
                UpdateButtonState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 停止服务按钮：停止本程序的 TCP 服务端监听。
        /// </summary>
        private void UiButton2_Click(object sender, EventArgs e)
        {
            SingletonManager.Instance.StopTcpServer();
            UpdateButtonState();
        }

        /// <summary>
        /// 确认配置按钮：保存监听 IP 和端口到本地配置文件。
        /// </summary>
        private void UiButton3_Click(object sender, EventArgs e)
        {
            string ip = uiTextBox1.Text?.Trim();
            string portText = uiTextBox2.Text?.Trim();

            if (string.IsNullOrWhiteSpace(ip))
            {
                MessageBox.Show("请输入监听 IP 地址。");
                return;
            }

            int port;
            if (!int.TryParse(portText, out port) || port <= 0 || port > 65535)
            {
                MessageBox.Show("请输入有效的端口号（1-65535）。");
                return;
            }

            SingletonManager.Instance.SaveTcpConfig(ip, port);
        }

        /// <summary>
        /// 发送消息按钮：服务端发送文本给当前已连接的客户端。
        /// </summary>
        private void UiButton4_Click(object sender, EventArgs e)
        {
            string message = textBox1.Text;
            if (string.IsNullOrEmpty(message))
            {
                MessageBox.Show("请输入要发送的内容。");
                return;
            }

            SingletonManager.Instance.SendTcpMessage(message);
        }

        /// <summary>
        /// 根据 TCP 服务端和客户端状态更新按钮的启用/禁用状态。
        /// </summary>
        private void UpdateButtonState()
        {
            bool serverRunning = SingletonManager.Instance.IsTcpServerRunning;
            uiButton1.Enabled = !serverRunning;
            uiButton2.Enabled = serverRunning;
            uiButton4.Enabled = serverRunning;
        }
    }
}
