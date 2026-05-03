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
            ImageBaseData_V2 imageBase = null;
            for (int attempt = 0; attempt < MaxRetryCount; attempt++)
            {
                try
                {
                    imageBase = procedure.ModuResult.GetOutputImageV2("Img");
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"流程{pictureIndex} 第{attempt + 1}次读取图像异常：{ex.Message}");
                }

                if (imageBase != null && imageBase.ImageData != IntPtr.Zero && imageBase.Width > 0 && imageBase.Height > 0)
                {
                    return imageBase;
                }

                if (attempt < MaxRetryCount - 1)
                {
                    await Task.Delay(RetryDelayMs);
                }
            }

            Trace.WriteLine($"流程{pictureIndex} 重试{MaxRetryCount}次后仍无法获取有效图像，跳过本次显示。");
            return null;
        }

        /// <summary>
        /// 尝试读取流程结果中的 ROI 矩形框输出，读取失败返回 null，不抛异常。
        /// </summary>
        private List<RectBox> ReadProcedureRois(VmProcedure procedure)
        {
            try
            {
                return procedure.ModuResult.GetOutputBoxArray("ROI");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 尝试读取流程结果中指定名称的整数输出，返回第一个值。
        /// 读取失败或数据为空时返回 0。
        /// </summary>
        private int ReadFirstOutputInt(VmProcedure procedure, string outputName)
        {
            int value;
            if (TryReadFirstOutputInt(procedure, outputName, out value))
            {
                return value;
            }

            return 0;
        }

        /// <summary>
        /// 尝试读取流程结果中指定名称的整数输出。
        /// 读取成功返回 true，读取失败或数据为空时返回 false。
        /// </summary>
        private bool TryReadFirstOutputInt(VmProcedure procedure, string outputName, out int value)
        {
            value = 0;

            try
            {
                IntDataArray data = procedure.ModuResult.GetOutputInt(outputName);
                if (data.pIntVal != null && data.pIntVal.Length > 0)
                {
                    value = data.pIntVal[0];
                    return true;
                }
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// 构建图像上叠加显示的文字列表。
        /// 所有流程都显示匹配框个数，主流程额外显示匹配框总数和 OK/NG 状态。
        /// </summary>
        private List<Tuple<string, VM.PlatformSDKCS.PointF>> BuildDisplayTextItems(
            int pictureIndex, string processName, int countValue, int totalValue)
        {
            var items = new List<Tuple<string, VM.PlatformSDKCS.PointF>>();
            float y = 0;

            if (!string.IsNullOrWhiteSpace(processName))
            {
                items.Add(Tuple.Create($"流程：{processName}", new VM.PlatformSDKCS.PointF(10, y)));
                y += 130;
            }

            items.Add(Tuple.Create($"数量：{countValue}", new VM.PlatformSDKCS.PointF(10, y)));
            y += 130;

            if (pictureIndex == 0)
            {
                items.Add(Tuple.Create($"匹配框总数：{totalValue}", new VM.PlatformSDKCS.PointF(10, y)));
                y += 130;

                string status = totalValue > 0 ? "结果：OK" : "结果：NG";
                items.Add(Tuple.Create(status, new VM.PlatformSDKCS.PointF(10, y)));
            }

            return items;
        }

        /// <summary>
        /// 构建单次执行归并快照的图像叠加文字。
        /// 四路流程同级显示，不再按 pictureIndex 区分主流程和辅助流程。
        /// </summary>
        private List<Tuple<string, VM.PlatformSDKCS.PointF>> BuildSnapshotTextItems(ProcedureResultSnapshot snapshot)
        {
            var items = new List<Tuple<string, VM.PlatformSDKCS.PointF>>();
            float y = 0;

            if (!string.IsNullOrWhiteSpace(snapshot.ProcessName))
            {
                items.Add(Tuple.Create($"流程：{snapshot.ProcessName}", new VM.PlatformSDKCS.PointF(10, y)));
                y += 130;
            }

            items.Add(Tuple.Create($"数量：{snapshot.CountValue}", new VM.PlatformSDKCS.PointF(10, y)));
            y += 130;

            if(snapshot.PictureIndex == 0)
            {
                items.Add(Tuple.Create($"总数：{snapshot.TotalOutValue}", new VM.PlatformSDKCS.PointF(10, y)));
                y += 130;
            }
            

            //if (snapshot.HasOutValue)
            //{
            //    items.Add(Tuple.Create($"out：{snapshot.OutValue}", new VM.PlatformSDKCS.PointF(10, y)));
            //}

            return items;
        }

        /// <summary>
        /// 根据 pictureIndex 获取目标 ZoomPictureBox 并显示图像。
        /// 目标无效时释放传入的 bitmap，避免泄漏。
        /// </summary>
        private void UpdatePictureBoxImage(int pictureIndex, Bitmap bitmap)
        {
            ZoomPictureBox targetBox = pictureBoxes[pictureIndex] as ZoomPictureBox;
            if (targetBox == null || targetBox.IsDisposed)
            {
                bitmap.Dispose();
                return;
            }

            targetBox.BeginInvoke(new Action(() =>
            {
                targetBox.SetImage(bitmap);
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
                return;
            }

            if (pictureBoxes == null || snapshot.PictureIndex < 0 || snapshot.PictureIndex >= pictureBoxes.Count)
            {
                snapshot.DisposeBitmap();
                AppendLog($"流程 {GetSnapshotDisplayName(snapshot)} 无可用显示区域，已跳过图像显示。");
                return;
            }

            try
            {
                Bitmap bitmap = snapshot.Bitmap;
                snapshot.Bitmap = null;

                Color highlightColor = Color.Lime;
                bitmap = DrawRotatedRectangles(bitmap, snapshot.Rects, highlightColor, 3.0f);
                bitmap = DrawTextList(bitmap, BuildSnapshotTextItems(snapshot), highlightColor, 50f);
                UpdatePictureBoxImage(snapshot.PictureIndex, bitmap);
            }
            catch (Exception ex)
            {
                snapshot.DisposeBitmap();
                Trace.WriteLine($"显示流程快照异常（索引 {snapshot.PictureIndex}）：{ex}");
            }
        }

        /// <summary>
        /// 读取指定流程的执行结果（图像、矩形框、匹配个数），转换为 Bitmap 并显示到对应的图片框。
        /// 主流程（流程 1）和辅助流程（流程 2/3/4）共用此方法，通过 pictureIndex 区分输出内容。
        /// </summary>
        private void DisplayProcedureResult(VmProcedure procedure, int pictureIndex)
        {
            Task.Run(async () =>
            {
                ImageBaseData_V2 imageBase = await ReadProcedureImageWithRetry(procedure, pictureIndex);
                if (imageBase == null)
                {
                    return;
                }

                try
                {
                    List<RectBox> rects = ReadProcedureRois(procedure);
                    int countValue = ReadFirstOutputInt(procedure, "COUNT");
                    int totalValue = 0;
                    string processName = (pictureIndex >= 0 && pictureIndex < _procedureNames.Count)
                        ? _procedureNames[pictureIndex]
                        : string.Empty;

                    if (pictureIndex == 0)
                    {
                        totalValue = ReadFirstOutputInt(procedure, "out");
                        // TCP 完成消息统一由整轮归并逻辑发送，避免单个流程提前通知客户端。
                    }

                    var textItems = BuildDisplayTextItems(pictureIndex, processName, countValue, totalValue);

                    Bitmap bitmap = ConvertToBitmap(imageBase);
                    if (bitmap == null)
                    {
                        return;
                    }

                    // 主流程使用 Lime 高亮，辅助流程使用 DodgerBlue 高亮
                    Color highlightColor = pictureIndex == 0 ? Color.Lime : Color.Lime;
                    bitmap = DrawRotatedRectangles(bitmap, rects, highlightColor, 3.0f);
                    bitmap = DrawTextList(bitmap, textItems, highlightColor, 50f);
                    UpdatePictureBoxImage(pictureIndex, bitmap);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"流程执行回调异常（索引 {pictureIndex}）：{ex}");
                }
            });
        }
        #endregion
    }
}
