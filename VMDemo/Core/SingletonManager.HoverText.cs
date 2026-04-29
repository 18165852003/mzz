using System;                                    // 引入系统基础命名空间，提供基础类型
using System.Collections.Generic;                // 引入泛型集合命名空间，提供 Dictionary、HashSet 等集合类型
using System.Drawing;                            // 引入绘图命名空间，提供 Color、Point 等类型
using System.Windows.Forms;                      // 引入 WinForms 命名空间，提供 Control、ToolTip、MouseEventArgs 等类型

namespace VMDemo                                 // 定义项目命名空间，与现有其他 SingletonManager 分部类文件保持一致
{
    /// <summary>
    /// SingletonManager 分部类：集中管理官方 ToolTip 悬浮提示、悬停背景高亮和单击背景色变化。
    /// </summary>
    public sealed partial class SingletonManager   // 声明单例管理器的 partial 分部类，本文件负责悬停提示与视觉反馈
    {
        // ---------- 官方 ToolTip 实例 ----------

        /// <summary>
        /// 这里使用 WinForms 官方悬浮提示组件，避免额外维护提示文字控件。
        /// </summary>
        private readonly ToolTip _hoverToolTip = new ToolTip
        {
            AutoPopDelay = 5000,                   // 悬浮提示持续显示的最长时间（毫秒）
            InitialDelay = 300,                    // 鼠标悬停到显示提示的初始延迟（毫秒）
            ReshowDelay = 100,                     // 鼠标在控件间快速移动时重新显示的延迟（毫秒）
            ShowAlways = true                      // 即使控件被禁用或父窗体非活动时也允许显示提示
        };

        // ---------- 状态字段 ----------

        /// <summary>
        /// 记录每个控件注册时的原始背景色。
        /// </summary>
        private readonly Dictionary<Control, Color> _hoverOriginalBackColors = new Dictionary<Control, Color>();

        /// <summary>
        /// 记录每个控件鼠标悬停时的背景色。
        /// </summary>
        private readonly Dictionary<Control, Color> _hoverBackColors = new Dictionary<Control, Color>();

        /// <summary>
        /// 记录每个控件左键单击时使用的临时背景色。
        /// </summary>
        private readonly Dictionary<Control, Color> _hoverClickBackColors = new Dictionary<Control, Color>();

        /// <summary>
        /// 记录每个控件对应的官方悬浮窗文字。
        /// </summary>
        private readonly Dictionary<Control, string> _hoverToolTipTexts = new Dictionary<Control, string>();

        /// <summary>
        /// 记录每个控件官方悬浮窗的显示时长（毫秒），鼠标移入后显示指定时间后自动消失。
        /// </summary>
        private readonly Dictionary<Control, int> _hoverToolTipDurations = new Dictionary<Control, int>();

        /// <summary>
        /// 记录已经注册过悬停事件的控件集合，防止同一个控件重复绑定事件。
        /// </summary>
        private readonly HashSet<Control> _hoverRegisteredControls = new HashSet<Control>();

        // ---------- UI 线程安全辅助 ----------

        /// <summary>
        /// WinForms 控件只能在创建它的 UI 线程中更新，后台线程或回调中修改控件前需要切回 UI 线程。
        /// </summary>
        /// <param name="control">用于判断 InvokeRequired 的目标控件。</param>
        /// <param name="action">需要在 UI 线程执行的操作。</param>
        private void ExecuteOnUiThread(Control control, Action action)
        {
            if (control.InvokeRequired)              // 如果当前线程不是控件的创建线程
            {
                control.BeginInvoke(new Action(action)); // 通过 BeginInvoke 异步切换到 UI 线程执行
            }
            else
            {
                action();                            // 当前已在 UI 线程，直接执行
            }
        }

        // ---------- 注册与事件处理 ----------

        /// <summary>
        /// 为目标控件注册鼠标悬停文字提示、悬停背景高亮与左键单击背景色变化功能。
        /// </summary>
        /// <param name="targetControl">需要监听鼠标移入、移出、左键单击的目标控件。</param>
        /// <param name="text">鼠标移入时通过官方 ToolTip 悬浮窗显示的文字。</param>
        /// <param name="hoverBackColor">鼠标悬停时的背景色。</param>
        /// <param name="clickBackColor">鼠标左键单击后临时切换的背景色。</param>
        /// <param name="toolTipDurationMs">官方 ToolTip 悬浮窗显示时长（毫秒），默认 3000 毫秒。</param>
        public void RegisterHoverText(
            Control targetControl,
            string text,
            Color hoverBackColor,
            Color clickBackColor,
            int toolTipDurationMs = 3000)
        {
            if (targetControl == null || text == null)
                return;                              // 参数为空时不执行注册，避免空引用异常

            if (!_hoverRegisteredControls.Contains(targetControl))
            {
                // 首次注册：绑定鼠标事件并记录原始背景色
                _hoverRegisteredControls.Add(targetControl);
                _hoverOriginalBackColors[targetControl] = targetControl.BackColor;

                // 鼠标移入事件：在控件下方显示官方悬浮提示窗，并切换为悬停背景色
                targetControl.MouseEnter += (sender, e) =>
                {
                    ExecuteOnUiThread(targetControl, () =>
                    {
                        if (_hoverToolTipTexts.TryGetValue(targetControl, out string tipText) &&
                            _hoverToolTipDurations.TryGetValue(targetControl, out int duration))
                        {
                            _hoverToolTip.Show(tipText, targetControl, new Point(0, targetControl.Height + 4), duration);
                        }
                        if (_hoverBackColors.TryGetValue(targetControl, out Color backColor))
                        {
                            targetControl.BackColor = backColor;
                        }
                    });
                };

                // 鼠标左键按下事件：临时改变控件背景色
                targetControl.MouseDown += (sender, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        ExecuteOnUiThread(targetControl, () =>
                        {
                            if (_hoverClickBackColors.TryGetValue(targetControl, out Color backColor))
                            {
                                targetControl.BackColor = backColor;
                            }
                        });
                    }
                };

                // 鼠标松开事件：恢复控件背景色
                targetControl.MouseUp += (sender, e) =>
                {
                    ExecuteOnUiThread(targetControl, () =>
                    {
                        if (targetControl.ClientRectangle.Contains(targetControl.PointToClient(Cursor.Position)))
                        {
                            // 鼠标仍在控件内，恢复为悬停背景色
                            if (_hoverBackColors.TryGetValue(targetControl, out Color hoverColor))
                            {
                                targetControl.BackColor = hoverColor;
                            }
                        }
                        else
                        {
                            // 鼠标已移出控件，恢复为原始背景色
                            if (_hoverOriginalBackColors.TryGetValue(targetControl, out Color originalColor))
                            {
                                targetControl.BackColor = originalColor;
                            }
                        }
                    });
                };

                // 鼠标移出事件：隐藏官方悬浮提示窗，并恢复控件原始背景色
                targetControl.MouseLeave += (sender, e) =>
                {
                    ExecuteOnUiThread(targetControl, () =>
                    {
                        _hoverToolTip.Hide(targetControl);
                        if (_hoverOriginalBackColors.TryGetValue(targetControl, out Color originalBackColor))
                        {
                            targetControl.BackColor = originalBackColor;
                        }
                    });
                };
            }
            else
            {
                // 控件已注册过，仅更新配置字典，不重复绑定事件
            }

            // 无论是否首次注册，都更新提示文字、悬停背景色、单击背景色和显示时长配置
            _hoverToolTipTexts[targetControl] = text;
            _hoverBackColors[targetControl] = hoverBackColor;
            _hoverClickBackColors[targetControl] = clickBackColor;
            _hoverToolTipDurations[targetControl] = toolTipDurationMs;
            // 注意：不调用 _hoverToolTip.SetToolTip，避免 ToolTip 自动重复弹出。
            // 提示文字的显示完全由 MouseEnter 手动控制，显示指定时长后自动消失。
        }
    }
}
