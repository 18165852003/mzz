using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using VM.Core;
using VM.PlatformSDKCS;
using VMDemo.Contro;

namespace VMDemo
{
    public sealed partial class SingletonManager
    {
        #region 读取指定流程的执行结果（图像、矩形框、匹配个数），转换为 Bitmap 并显示到对应的图片框
        /// <summary>
        /// 读取指定流程的执行结果（图像、矩形框、匹配个数），转换为 Bitmap 并显示到对应的图片框。
        /// 主流程（流程 1）和辅助流程（流程 2/3/4）共用此方法，通过 pictureIndex 区分输出内容。
        /// </summary>
        /// <param name="procedure">当前执行完成的流程对象。</param>
        /// <param name="pictureIndex">当前流程对应的图片框索引。</param>
        private const int MaxRetryCount = 3; // 结果读取最大重试次数，首次执行时 SDK 可能尚未就绪。
        private const int RetryDelayMs = 100; // 每次重试前的等待间隔，给 SDK 缓冲区填充留出时间。

        /// <summary>
        /// 带重试的图像读取，解决首次执行时 SDK 结果缓冲区尚未就绪的问题。
        /// 返回有效 ImageBaseData_V2 或 null。
        /// </summary>
        private async Task<ImageBaseData_V2> ReadProcedureImageWithRetry(VmProcedure procedure, int pictureIndex)
        {
            ImageBaseData_V2 imageBase = null; // 保存当前尝试读取到的图像结果。
            for (int attempt = 0; attempt < MaxRetryCount; attempt++) // 按最大重试次数循环读取图像。
            {
                try
                {
                    imageBase = procedure.ModuResult.GetOutputImageV2("Img"); // 从流程输出中读取固定名称 Img 的图像。
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"流程{pictureIndex} 第{attempt + 1}次读取图像异常：{ex.Message}"); // 记录本次读取失败原因，继续允许后续重试。
                }

                if (imageBase != null && imageBase.ImageData != IntPtr.Zero && imageBase.Width > 0 && imageBase.Height > 0) // 判断是否已经拿到可显示图像。
                {
                    return imageBase; // 读取成功，立即返回有效图像。
                }

                if (attempt < MaxRetryCount - 1) // 如果还没到最后一次尝试，则等待后继续重试。
                {
                    await Task.Delay(RetryDelayMs); // 等待 SDK 结果缓冲区就绪。
                }
            }

            Trace.WriteLine($"流程{pictureIndex} 重试{MaxRetryCount}次后仍无法获取有效图像，跳过本次显示。"); // 所有重试失败后写调试日志。
            return null; // 返回空值，让调用方跳过显示。
        }

        /// <summary>
        /// 尝试读取流程结果中的 ROI 矩形框输出，读取失败返回 null，不抛异常。
        /// </summary>
        private List<RectBox> ReadProcedureRois(VmProcedure procedure)
        {
            try
            {
                return procedure.ModuResult.GetOutputBoxArray("ROI"); // 从流程输出中读取固定名称 ROI 的矩形框数组。
            }
            catch
            {
                return null; // ROI 不存在或读取失败时返回 null，显示层按无 ROI 处理。
            }
        }

        /// <summary>
        /// 尝试读取流程结果中指定名称的整数输出，返回第一个值。
        /// 读取失败或数据为空时返回 0。
        /// </summary>
        private int ReadFirstOutputInt(VmProcedure procedure, string outputName)
        {
            int value; // 接收 TryReadFirstOutputInt 读取到的整数值。
            if (TryReadFirstOutputInt(procedure, outputName, out value)) // 尝试读取指定整数输出。
            {
                return value; // 读取成功则返回第一个整数值。
            }

            return 0; // 读取失败时默认返回 0，避免调用方再做异常处理。
        }

        /// <summary>
        /// 尝试读取流程结果中指定名称的整数输出。
        /// 读取成功返回 true，读取失败或数据为空时返回 false。
        /// </summary>
        private bool TryReadFirstOutputInt(VmProcedure procedure, string outputName, out int value)
        {
            value = 0; // 先给输出参数默认值。

            try
            {
                IntDataArray data = procedure.ModuResult.GetOutputInt(outputName); // 从流程输出中读取指定名称的整数数组。
                if (data.pIntVal != null && data.pIntVal.Length > 0) // 判断数组是否包含可用数据。
                {
                    value = data.pIntVal[0]; // 取第一个整数作为当前输出值。
                    return true; // 标记读取成功。
                }
            }
            catch
            {
                // 读取失败时吞掉异常，调用方通过 false 判断即可。
            }

            return false; // 没有读到有效整数时返回失败。
        }

        /// <summary>
        /// 构建单次执行归并快照的图像叠加文字。
        /// 四路流程同级显示，不再按 pictureIndex 区分主流程和辅助流程。
        /// </summary>
        private List<Tuple<string, VM.PlatformSDKCS.PointF>> BuildSnapshotTextItems(ProcedureResultSnapshot snapshot)
        {
            var items = new List<Tuple<string, VM.PlatformSDKCS.PointF>>(); // 创建快照路径的叠加文字列表。
            float y = 0; // 记录当前文字绘制的纵向位置。

            if (!string.IsNullOrWhiteSpace(snapshot.ProcessName)) // 如果快照包含流程名，则显示流程名。
            {
                items.Add(Tuple.Create($"流程：{snapshot.ProcessName}", new VM.PlatformSDKCS.PointF(10, y))); // 添加流程名文字。
                y += 130; // 下移下一行文字位置。
            }

            items.Add(Tuple.Create($"数量：{snapshot.CountValue}", new VM.PlatformSDKCS.PointF(10, y))); // 添加当前流程 COUNT 输出。
            y += 130; // 下移下一行文字位置。

            if(snapshot.PictureIndex == 0) // 只在第一个显示区域显示四流程 out 汇总值。
            {
                items.Add(Tuple.Create($"总数：{snapshot.TotalOutValue}", new VM.PlatformSDKCS.PointF(10, y))); // 添加四流程 out 汇总文字。
                y += 130; // 预留下一行文字位置。
            }

            return items; // 返回快照叠加文字列表。
        }

        /// <summary>
        /// 根据 pictureIndex 获取目标 ZoomPictureBox 并显示图像。
        /// 目标无效时释放传入的 bitmap，避免泄漏。
        /// </summary>
        private void UpdatePictureBoxImage(int pictureIndex, Bitmap bitmap)
        {
            ZoomPictureBox targetBox = pictureBoxes[pictureIndex] as ZoomPictureBox; // 根据索引获取目标图片框，并转换为支持缩放的自定义控件。
            if (targetBox == null || targetBox.IsDisposed) // 判断目标控件是否可用。
            {
                bitmap.Dispose(); // 控件不可用时释放传入图像，避免 Bitmap 泄漏。
                return; // 结束显示流程。
            }

            targetBox.BeginInvoke(new Action(() => // 切回 UI 线程更新图片框内容。
            {
                targetBox.SetImage(bitmap); // 将 Bitmap 交给 ZoomPictureBox，后续由控件接管释放。
            }));
        }

        /// <summary>
        /// 显示单次执行归并后的流程快照。
        /// bitmap 所有权在成功调用 UpdatePictureBoxImage 后转移给 ZoomPictureBox。
        /// </summary>
        private void DisplayProcedureSnapshot(ProcedureResultSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Bitmap == null)
            {
                return; // 快照或图像为空时直接结束显示。
            }

            if (pictureBoxes == null || snapshot.PictureIndex < 0 || snapshot.PictureIndex >= pictureBoxes.Count)
            {
                snapshot.DisposeBitmap(); // 没有显示区域时释放快照图像。
                AppendLog($"流程 {GetSnapshotDisplayName(snapshot)} 无可用显示区域，已跳过图像显示。", LogLevel.Warning); // 记录跳过显示原因。
                return; // 结束显示流程。
            }

            try
            {
                Bitmap bitmap = snapshot.Bitmap; // 取出快照中的图像对象。
                snapshot.Bitmap = null; // 清空快照引用，避免 catch/finally 中重复释放。

                Color highlightColor = Color.Lime; // 设置当前快照显示的 ROI 和文字高亮颜色。
                bitmap = DrawRotatedRectangles(bitmap, snapshot.Rects, highlightColor, 3.0f); // 将 ROI 绘制到图像上。
                bitmap = DrawTextList(bitmap, BuildSnapshotTextItems(snapshot), highlightColor, 50f); // 将流程名、数量、汇总值绘制到图像上。
                UpdatePictureBoxImage(snapshot.PictureIndex, bitmap); // 将绘制后的图像显示到对应图片框。
            }
            catch (Exception ex)
            {
                snapshot.DisposeBitmap(); // 异常时释放仍由快照持有的图像资源。
                Trace.WriteLine($"显示流程快照异常（索引 {snapshot.PictureIndex}）：{ex}"); // 记录显示异常，便于排查 UI 或图像问题。
            }
        }

        #endregion
    }
}
