using System.Collections.Generic;
// System.Collections.Generic 提供 List<T>、KeyValuePair 等集合类型。
using System.Drawing; // 引入绘图命名空间，保留与显示控件相关的颜色/尺寸类型引用。
using System.Linq; // 引入 LINQ 命名空间，用于筛选和排序流程、控件集合。
using System.Windows.Forms; // 引入 WinForms 命名空间，提供 Control、PictureBox 等控件类型。
using Sunny.UI; // 引入 SunnyUI 命名空间，提供 UIPanel 承载控件。
using VM.Core; // 引入 VisionMaster 核心命名空间，提供 VmSolution、VmProcedure 等类型。
using VM.PlatformSDKCS; // 引入 VisionMaster 平台 SDK 命名空间，提供流程信息结构。
using VMDemo.Contro; // 引入项目自定义控件命名空间，提供 UserControl1/2/4。

namespace VMDemo // 定义当前项目命名空间。
{ // 命名空间开始。
    public sealed partial class SingletonManager // 声明 SingletonManager 分部类，本文件负责方案加载、显示布局创建和回调绑定。
    { // 类开始。
        #region 加载方案
        /// <summary>
        /// 加载指定方案，并根据流程数量创建对应的图像显示界面。
        /// 回调在此处绑定一次，后续执行时不再重复绑定。
        /// </summary>
        /// <param name="path">方案文件路径。</param>
        /// <param name="uI2">显示流程图像的承载面板。</param>
        public void Load(string path, UIPanel uI2)
        {
            if (string.IsNullOrWhiteSpace(path)) // 判断方案路径是否有效。
            {
                return; // 如果路径无效则直接返回。
            }

            DetachCallbacks(); // 加载新方案前先移除旧流程回调，避免重复触发。
            _procedureIndexMap.Clear(); // 清空旧流程和显示索引的映射关系。
            _procedureNames.Clear(); // 清空旧流程名称列表。

            VmSolution.Load(path); // 加载指定路径下的方案文件。
            count = CountValidProcesses(); // 获取当前方案中有效流程的数量。
            CreateDisplayLayout(count, uI2); // 根据流程数量创建对应数量的显示控件。
            BindCallbacks(); // 为每个有效流程绑定统一的执行结束回调。
            AppendLog($"方案加载成功，共 {count} 个有效流程");
        }

        /// <summary>
        /// 移除当前已绑定的所有流程结束回调。
        /// </summary>
        private void DetachCallbacks()
        {
            foreach (KeyValuePair<VmProcedure, int> item in _procedureIndexMap) // 遍历当前已缓存的流程回调绑定关系。
            {
                if (item.Key != null) // 判断当前流程对象是否有效。
                {
                    item.Key.OnWorkEndStatusCallBack -= OnWorkEnd; // 解除当前流程对象上的结束回调订阅。
                }
            }
        }

        /// <summary>
        /// 获取当前方案中有效流程的数量。
        /// </summary>
        private int CountValidProcesses()
        {
            ProcessInfoList infoList = VmSolution.Instance.GetAllProcedureList(); // 获取当前方案中的全部流程信息。
            return infoList.astProcessInfo.Where(r => r.nProcessID != 0).Count(); // 仅统计流程 ID 不为零的有效流程数量。
        }

        /// <summary>
        /// 根据流程数量创建对应的显示控件，并挂载到目标面板中。
        /// </summary>
        /// <param name="procedureCount">当前有效流程数量。</param>
        /// <param name="hostPanel">承载流程界面的显示面板。</param>
        private void CreateDisplayLayout(int procedureCount, UIPanel hostPanel)
        {
            if (procedureCount == 1) // 判断当前是否为 1 个流程。
            {
                userControl = new UserControl1(); // 创建 1 宫格流程显示控件。
            }
            else if (procedureCount == 2) // 判断当前是否为 2 个流程。
            {
                userControl = new UserControl2(); // 创建 2 宫格流程显示控件。
            }
            else // 其余情况默认按 4 宫格显示。
            {
                userControl = new UserControl4(); // 创建 4 宫格流程显示控件。
            }

            userControl.Dock = DockStyle.Fill; // 让流程显示控件填满目标面板。
            hostPanel.Controls.Clear(); // 清空目标面板中原有的控件内容。
            hostPanel.Controls.Add(userControl); // 将新的流程显示控件添加到目标面板中。

            pictureBoxes = GetAllPictureBoxes(userControl); // 挂载完成后，从用户控件中收集所有 PictureBox。
            foreach (PictureBox pb in pictureBoxes) // 统一设置所有图片框的显示模式。
            {
                pb.SizeMode = PictureBoxSizeMode.Zoom; // 将图片框显示模式设为按比例缩放。
            }

            // 为每个显示区域预留默认悬停提示文本，后续如果恢复 RegisterHoverText，可在这里统一注册。
            for (int i = 0; i < pictureBoxes.Count; i++) // 遍历每一个图片显示区域。
            {
                string defaultTip = i == 0 ? "流程 1：主结果画面" : $"流程 {i + 1}：辅助画面"; // 根据显示索引构建默认提示文字。
                
            }
        }

        /// <summary>
        /// 递归收集指定控件下的所有 PictureBox，按名称排序后返回。
        /// </summary>
        private List<PictureBox> GetAllPictureBoxes(Control parent)
        {
            List<PictureBox> result = new List<PictureBox>(); // 创建临时图片框列表。

            foreach (Control ctrl in parent.Controls) // 遍历当前父控件中的所有子控件。
            {
                if (ctrl is PictureBox pictureBox) // 判断当前子控件是否为图片框。
                {
                    result.Add(pictureBox); // 如果是图片框则加入结果列表。
                }

                if (ctrl.HasChildren) // 判断当前子控件下是否还包含子控件。
                {
                    result.AddRange(GetAllPictureBoxes(ctrl)); // 继续递归收集子控件中的图片框。
                }
            }

            return result.OrderBy(p => p.Name).ToList(); // 按名称排序后返回，确保显示顺序稳定。
        }

        /// <summary>
        /// 为当前方案中的每个有效流程绑定统一的执行结束回调，并记录流程对象与图片框索引的映射关系。
        /// 只在 Load 时调用一次。
        /// </summary>
        private void BindCallbacks()
        {
            ProcessInfoList infoList = VmSolution.Instance.GetAllProcedureList(); // 获取当前方案中的全部流程信息。
            int pictureIndex = 0; // 定义当前图片框索引，从 0 开始递增。

            for (int i = 0; i < infoList.nNum; i++) // 遍历当前方案中的所有流程信息。
            {
                if (infoList.astProcessInfo[i].nProcessID == 0) // 跳过无效流程。
                {
                    continue;
                }

                VmProcedure procedure = VmSolution.Instance[infoList.astProcessInfo[i].strProcessName] as VmProcedure; // 根据流程名称获取流程对象。
                if (procedure == null) // 判断流程对象是否获取成功。
                {
                    continue; // 如果未获取到流程对象则跳过。
                }

                string processName = infoList.astProcessInfo[i].strProcessName; // 读取当前有效流程名称。
                procedure.OnWorkEndStatusCallBack += OnWorkEnd; // 绑定统一的流程执行结束回调。
                _procedureIndexMap[procedure] = pictureIndex; // 保存当前流程对象和对应图片框索引的映射关系。
                _procedureNames.Add(processName); // 按顺序保存流程名称。

                if (pictureIndex < pictureBoxes.Count) // 判断当前流程是否有对应的图片显示区域。
                {
                    string tip = pictureIndex == 0 // 根据主流程/辅助流程构建更具体的悬浮提示文本。
                        ? $"流程：{processName}（主结果画面）\n输出：Img、ROI、COUNT、out"
                        : $"流程：{processName}\n输出：Img、ROI、COUNT";
                    
                }

                pictureIndex++; // 图片框索引递增。
            }
        }
        #endregion
    } // 类结束。
} // 命名空间结束。
