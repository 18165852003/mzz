using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Sunny.UI;
using VM.Core;
using VMDemo.Contro;



namespace VMDemo
{
    public partial class MainForm : UIForm
    {
        private ProcessInfoList processInfoList;

        private string selectedProcessName;

        private const int MaxUiLogCount = 1000; // 主界面最多保留的日志条数，避免长时间运行后 ListBox 无限增长。

        public MainForm()
        {
            InitializeComponent(); // 初始化窗体中的所有界面组件。
            AutoScaleMode = AutoScaleMode.None; // 禁用窗体自动缩放，防止执行时触发布局缩放。
            ZoomScaleDisabled = true; // 禁用 SunnyUI 的缩放矩形机制，防止窗体和子控件在运行后整体变小。
            ZoomScaleRect = Rectangle.Empty; // 清空 SunnyUI 设计期记录的缩放基准矩形。
            Log.ListBox.DrawMode = DrawMode.OwnerDrawFixed;
            Log.ListBox.DrawItem += Log_DrawItem;
            RegisterHoverEffect();
            Load += MainForm_Load;
        }

        #region 为图标控件注册悬停高亮效果，并在鼠标移入时显示提示文字。
        private void RegisterHoverEffect()
        {
            Color hoverColor = Color.FromArgb(60, 60, 60);      // 鼠标悬停时的背景色
            Color selectedColor = Color.FromArgb(120, 120, 120);  // 选中后的背景高亮色
            SingletonManager.Instance.RegisterHoverText(uiSymbolLabel1, "TCP通讯", hoverColor, selectedColor, 3000);
            SingletonManager.Instance.RegisterHoverText(uiSymbolLabel2, "加载方案", hoverColor, selectedColor, 3000);
            SingletonManager.Instance.RegisterHoverText(uiSymbolLabel3, "单次执行", hoverColor, selectedColor, 3000);
        }
        #endregion

        #region TCP通讯
        private void uiSymbolLabel1_Click(object sender, EventArgs e)
        {
            TCPForm tCPForm = new TCPForm();
            tCPForm.ShowDialog();
        }
        #endregion

        #region 加载方案
        private void uiSymbolLabel2_Click(object sender, System.EventArgs e)
        {
            try
            {
                // 加载方案只负责初始化 VisionMaster 流程和显示区域；TCP 服务端在窗体加载时已经可提前监听。
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.Filter = "方案路径|*.sol";
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        SingletonManager.Instance.Load(dialog.FileName, uiPanel2);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"方案加载失败：{ex.Message}");
            }
            finally
            {
                
            }
        }
        #endregion

        #region 单次执行
        private void uiSymbolLabel3_Click(object sender, EventArgs e)
        {
            try
            {
                // 单次执行必须先有已加载方案，否则 TCP 客户端下发的 RoundId 只能停留在等待执行状态。
                if (!SingletonManager.Instance.IsLoaded)
                {
                    MessageBox.Show("请先加载方案");
                    return;
                }

                // TCP 接收到 ROUND|RoundId 后会先缓存为 PendingRoundId，这里负责把“没有业务号”的点击挡住。
                if (!SingletonManager.Instance.HasPendingRoundId)
                {
                    MessageBox.Show("请先由 TCP 客户端发送 RoundId");
                    return;
                }

                SingletonManager.Instance.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"单次执行异常：{ex.Message}");
            }
            finally
            {
                
            }
        }
        #endregion

        #region 窗体加载时自动启动 TCP 服务端
        /// <summary>
        /// 主窗体加载完成后，后台尝试自动启动 TCP 服务端监听。
        /// 这一步早于加载 .sol 方案，目的是允许外部客户端先连上并下发 RoundId。
        /// 成功或失败都只写日志，不阻塞主界面。
        /// </summary>
        private void MainForm_Load(object sender, EventArgs e)
        {
            SingletonManager.Instance.LogReceived += OnLogReceived;
            foreach (LogEntry log in SingletonManager.Instance.GetLogHistory())
            {
                AppendLog(log);
            }
            SingletonManager.Instance.AppendLog("程序已启动");
            SingletonManager.Instance.TryAutoStartTcpServer();
        }
        #endregion

        #region 释放资源
        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            SingletonManager.Instance.LogReceived -= OnLogReceived;
            SingletonManager.Instance.StopTcpServer(); // 先停止 TCP 服务端，避免后台监听线程在程序退出时仍然运行。
            if (SingletonManager.Instance.IsLoaded)
                SingletonManager.Instance.Dispose();
        }
        #endregion

        #region 日志显示

        /// <summary>
        /// 日志接收回调方法，确保在UI线程上执行日志追加操作
        /// </summary>
        /// <param name="entry">日志实体，包含时间、级别和正文。</param>
        private void OnLogReceived(LogEntry entry)
        {
            // 判断当前调用线程是否为UI线程（非UI线程时InvokeRequired为true）
            if (Log.InvokeRequired)
                // 非UI线程：通过BeginInvoke异步将日志追加操作调度到UI线程执行，避免跨线程访问控件异常
                Log.BeginInvoke(new Action(() => AppendLog(entry)));
            else
                // UI线程：直接调用AppendLog追加日志
                AppendLog(entry);
        }

        /// <summary>
        /// 将日志消息追加到ListBox控件中，并自动滚动到底部
        /// </summary>
        /// <param name="entry">日志实体，包含时间、级别和正文。</param>
        private void AppendLog(LogEntry entry)
        {
            if (entry == null) return;

            // 直接保存日志实体，绘制时可读取 Level，避免用文本关键字猜测颜色。
            Log.Items.Add(entry);
            while (Log.Items.Count > MaxUiLogCount)
            {
                Log.Items.RemoveAt(0);
            }

            // 设置ListBox的TopIndex为最后一项，实现自动滚动到最新日志的效果
            if (Log.Items.Count > 0)
            {
                Log.TopIndex = Log.Items.Count - 1;
            }
        }

        /// <summary>
        /// ListBox自定义绘制事件，实现错误日志红色高亮、正常日志绿色显示
        /// </summary>
        private void Log_DrawItem(object sender, DrawItemEventArgs e)
        {
            // 索引小于0表示无效项，直接返回不做绘制处理
            if (e.Index < 0) return;

            // 判断当前绘制项是否处于选中状态（通过位与运算检查Selected标志位）
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            // 获取当前项的日志实体，用于按级别绘制文本颜色。
            LogEntry entry = Log.ListBox.Items[e.Index] as LogEntry;
            string text = entry == null ? Log.ListBox.Items[e.Index].ToString() : entry.ToString();
            // 背景色：选中时使用深灰色(55,58,62)，未选中时使用控件默认背景色
            Color bgColor = isSelected ? Color.FromArgb(55, 58, 62) : Log.BackColor;
            // 文本色：按日志级别固定显示，避免错误日志因文案不同被误判。
            Color textColor = GetLogTextColor(entry);

            // 创建背景色画刷，using确保绘制完成后自动释放资源
            using (Brush bgBrush = new SolidBrush(bgColor))
            // 创建文本色画刷，using确保绘制完成后自动释放资源
            using (Brush textBrush = new SolidBrush(textColor))
            {
                // 使用背景色填充当前项的整个绘制区域
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
                // 在绘制区域内使用指定颜色绘制日志文本
                e.Graphics.DrawString(text, e.Font, textBrush, e.Bounds);
            }
        }

        /// <summary>
        /// 根据日志级别返回主界面显示颜色。
        /// </summary>
        private Color GetLogTextColor(LogEntry entry)
        {
            if (entry == null)
            {
                return Color.Lime;
            }

            switch (entry.Level)
            {
                case LogLevel.Error:
                    return Color.Red;
                case LogLevel.Warning:
                    return Color.Orange;
                default:
                    return Color.Lime;
            }
        }

        #endregion
    }
}
