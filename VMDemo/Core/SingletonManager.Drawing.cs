using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using VM.PlatformSDKCS;

namespace VMDemo
{
    public sealed partial class SingletonManager
    {
        #region 图像转换
        /// <summary>
        /// 将 VM 图像数据转换为 WinForms 可显示的 Bitmap 对象。
        /// 自动判断灰度图和彩色图。
        /// </summary>
        private Bitmap ConvertToBitmap(ImageBaseData_V2 imageData)
        {
            if (imageData == null || imageData.ImageData == IntPtr.Zero || imageData.Width <= 0 || imageData.Height <= 0) // 判断图像数据是否有效。
            {
                return null;
            }

            switch (imageData.Pixelformat) // 根据图像像素格式选择对应的 Bitmap 构建方式。
            {
                case VMPixelFormat.VM_PIXEL_MONO_08: // 8 位灰度图像。
                    return CreateMonoBitmap(imageData);
                case VMPixelFormat.VM_PIXEL_RGB24_C3: // 24 位彩色图像。
                    return CreateRgbBitmap(imageData);
                default:
                    Trace.WriteLine($"暂不支持的像素格式：{imageData.Pixelformat}"); // 输出不支持格式信息到调试日志。
                    return null;
            }
        }

        /// <summary>
        /// 将灰度图像数据转换为 8 位灰度 Bitmap。
        /// </summary>
        private Bitmap CreateMonoBitmap(ImageBaseData_V2 imageData)
        {
            Bitmap bitmap = new Bitmap(imageData.Width, imageData.Height, PixelFormat.Format8bppIndexed); // 创建 8 位灰度格式的 Bitmap。
            ColorPalette palette = bitmap.Palette; // 获取当前 Bitmap 的调色板。

            for (int i = 0; i < 256; i++) // 初始化 256 级灰度调色板。
            {
                palette.Entries[i] = Color.FromArgb(i, i, i); // 将每一级灰度映射到对应颜色。
            }

            bitmap.Palette = palette; // 将初始化后的调色板写回 Bitmap。
            CopyImageDataToBitmap(imageData, bitmap, 1, false); // 将原始灰度图像数据拷贝到 Bitmap。
            return bitmap;
        }

        /// <summary>
        /// 将 RGB24 图像数据转换为 24 位彩色 Bitmap。
        /// </summary>
        private Bitmap CreateRgbBitmap(ImageBaseData_V2 imageData)
        {
            Bitmap bitmap = new Bitmap(imageData.Width, imageData.Height, PixelFormat.Format24bppRgb); // 创建 24 位彩色格式的 Bitmap。
            CopyImageDataToBitmap(imageData, bitmap, 3, true); // 将原始彩色图像数据拷贝到 Bitmap，并交换 R/B 通道。
            return bitmap;
        }

        /// <summary>
        /// 将 VM 原始图像缓冲区拷贝到 Bitmap 像素内存中。
        /// </summary>
        private void CopyImageDataToBitmap(ImageBaseData_V2 imageData, Bitmap bitmap, int bytesPerPixel, bool swapRgb)
        {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height); // 定义与 Bitmap 尺寸一致的操作区域。
            BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, bitmap.PixelFormat); // 锁定位图像素区域。

            try
            {
                int totalBytes = checked((int)imageData.DataLen); // 获取原始图像总字节数。
                int rowBytes = imageData.Width * bytesPerPixel; // 计算每一行真正的像素数据字节数。
                int srcStride = totalBytes / imageData.Height; // 计算原始图像每一行的步长。
                int dstStride = Math.Abs(bitmapData.Stride); // 获取目标 Bitmap 每一行的步长。

                if (srcStride < rowBytes) // 判断原始图像步长是否小于理论行字节数。
                {
                    srcStride = rowBytes; // 如果过小则按理论行字节数进行修正。
                }

                byte[] srcBuffer = new byte[totalBytes]; // 创建源图像字节数组。
                byte[] dstBuffer = new byte[dstStride * bitmap.Height]; // 创建目标 Bitmap 字节数组。
                Marshal.Copy(imageData.ImageData, srcBuffer, 0, totalBytes); // 将非托管图像缓冲区拷贝到托管源数组中。

                for (int y = 0; y < bitmap.Height; y++) // 按行遍历整张图像。
                {
                    int srcOffset = y * srcStride; // 计算当前行在源数组中的起始偏移。
                    int dstOffset = y * dstStride; // 计算当前行在目标数组中的起始偏移。
                    Buffer.BlockCopy(srcBuffer, srcOffset, dstBuffer, dstOffset, rowBytes); // 将当前行像素数据拷贝到目标数组中。

                    if (swapRgb) // 判断当前是否需要交换 RGB 通道顺序。
                    {
                        for (int x = 0; x < rowBytes; x += 3) // 按像素遍历当前行彩色数据。
                        {
                            byte temp = dstBuffer[dstOffset + x]; // 暂存 R 通道。
                            dstBuffer[dstOffset + x] = dstBuffer[dstOffset + x + 2]; // 将 B 通道写入 R 位置。
                            dstBuffer[dstOffset + x + 2] = temp; // 将暂存的 R 写入 B 位置。
                        }
                    }
                }

                Marshal.Copy(dstBuffer, 0, bitmapData.Scan0, dstBuffer.Length); // 将目标数组中的像素数据整体写回 Bitmap 内存。
            }
            finally
            {
                bitmap.UnlockBits(bitmapData); // 释放 Bitmap 像素区域锁定。
            }
        }

        /// <summary>
        /// 确保 Bitmap 可以被 Graphics.FromImage 使用。
        /// 如果当前是索引像素格式（如 8 位灰度图），则转换为 24 位彩色格式后返回。
        /// </summary>
        /// <param name="bitmap">待检查的 Bitmap 对象。</param>
        /// <returns>返回可用于 GDI+ 绘制的 Bitmap 对象。</returns>
        private Bitmap EnsureDrawable(Bitmap bitmap)
        {
            if ((bitmap.PixelFormat & PixelFormat.Indexed) == 0) // 判断当前 Bitmap 是否为非索引格式。
            {
                return bitmap; // 如果不是索引格式，说明可以直接用 Graphics 绘制，直接返回。
            }

            Bitmap newBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb); // 创建一张同尺寸的 24 位彩色 Bitmap。
            using (Graphics g = Graphics.FromImage(newBitmap)) // 基于新 Bitmap 创建绘图对象。
            {
                g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height); // 将原始灰度图绘制到新的彩色 Bitmap 上。
            }

            bitmap.Dispose(); // 释放原始灰度 Bitmap 占用的资源。
            return newBitmap; // 返回转换后的彩色 Bitmap。
        }

        /// <summary>
        /// 在 Bitmap 图像上绘制带角度的矩形框。
        /// 这里直接使用流程结果中的图像坐标进行绘制，因此 PictureBox 缩放显示时矩形框也会同步缩放。
        /// </summary>
        /// <param name="bitmap">待绘制的目标图像。</param>
        /// <param name="rects">要绘制的旋转矩形框集合。</param>
        /// <param name="color">矩形框颜色。</param>
        /// <param name="thickness">矩形框线宽。</param>
        /// <returns>返回绘制完成后的 Bitmap 对象。</returns>
        private Bitmap DrawRotatedRectangles(Bitmap bitmap, List<RectBox> rects, Color color, float thickness)
        {
            if (bitmap == null) // 判断目标图像是否有效。
            {
                return null; // 如果目标图像为空则直接返回空值。
            }

            if (rects == null || rects.Count == 0) // 判断当前是否存在可绘制的矩形框结果。
            {
                return bitmap; // 如果没有矩形框结果，则直接返回原图像。
            }

            bitmap = EnsureDrawable(bitmap); // Graphics.FromImage 不支持索引像素格式（如灰度图），需要先转为 24 位彩色图才能绘制。

            using (Graphics graphics = Graphics.FromImage(bitmap)) // 基于当前 Bitmap 创建绘图对象。
            using (Pen pen = new Pen(color, thickness)) // 创建用于绘制矩形框的画笔。
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias; // 开启抗锯齿，提升旋转矩形边缘的显示质量。

                foreach (RectBox rect in rects) // 遍历当前所有旋转矩形框结果。
                {
                    if (rect.BoxWidth <= 0 || rect.BoxHeight <= 0) // 判断当前矩形框尺寸是否有效。
                    {
                        continue; // 如果矩形框尺寸无效，则跳过当前结果。
                    }
                    //VM.PlatformSDKCS.PointF pointF = rect.CenterPoint;
                    System.Drawing.PointF[] points = GetRotatedRectanglePoints(rect); // 计算当前旋转矩形框的四个顶点坐标。
                    graphics.DrawPolygon(pen, points); // 按顶点顺序将当前旋转矩形框绘制到图像上。
                }

            }

            return bitmap; // 返回绘制完成后的 Bitmap 图像。
        }

        /// <summary>
        /// 根据矩形框中心点、宽高和旋转角度，计算旋转矩形的四个顶点坐标。
        /// </summary>
        /// <param name="rect">当前要计算的旋转矩形框结果。</param>
        /// <returns>返回四个顶点坐标数组，顺序为左上、右上、右下、左下。</returns>
        private System.Drawing.PointF[] GetRotatedRectanglePoints(RectBox rect)
        {
            float halfWidth = rect.BoxWidth / 2.0f; // 计算矩形框一半宽度。
            float halfHeight = rect.BoxHeight / 2.0f; // 计算矩形框一半高度。
            double radians = rect.Angle * Math.PI / 180.0; // 将角度转换为弧度，便于后续做旋转矩阵计算。
            double cos = Math.Cos(radians); // 计算当前旋转角度对应的余弦值。
            double sin = Math.Sin(radians); // 计算当前旋转角度对应的正弦值。

            System.Drawing.PointF[] localPoints = new System.Drawing.PointF[] // 先构建一个未旋转、以中心点为原点的局部矩形。
            {
                new System.Drawing.PointF(-halfWidth, -halfHeight), // 左上角局部坐标。
                new System.Drawing.PointF(halfWidth, -halfHeight), // 右上角局部坐标。
                new System.Drawing.PointF(halfWidth, halfHeight), // 右下角局部坐标。
                new System.Drawing.PointF(-halfWidth, halfHeight) // 左下角局部坐标。
            };

            System.Drawing.PointF[] result = new System.Drawing.PointF[4]; // 创建用于保存旋转后顶点坐标的结果数组。
            for (int index = 0; index < localPoints.Length; index++) // 逐个计算每个顶点旋转并平移后的最终坐标。
            {
                float x = localPoints[index].X; // 获取当前顶点的局部 X 坐标。
                float y = localPoints[index].Y; // 获取当前顶点的局部 Y 坐标。

                result[index] = new System.Drawing.PointF( // 根据二维旋转矩阵公式，计算顶点在图像坐标系下的最终位置。
                    (float)(x * cos - y * sin + rect.CenterPoint.X), // 旋转并平移后的 X 坐标。
                    (float)(x * sin + y * cos + rect.CenterPoint.Y)); // 旋转并平移后的 Y 坐标。
            }

            return result; // 返回旋转矩形的四个顶点坐标。
        }

        /// <summary>
        /// 在 Bitmap 图像上的指定坐标位置绘制一段文字。
        /// </summary>
        /// <param name="bitmap">待绘制的目标图像。</param>
        /// <param name="text">要绘制的文字内容。</param>
        /// <param name="position">文字左上角在图像上的坐标位置。</param>
        /// <param name="color">文字颜色。</param>
        /// <param name="fontSize">文字大小，默认 20。</param>
        /// <returns>返回绘制完成后的 Bitmap 对象。</returns>
        private Bitmap DrawText(Bitmap bitmap, string text, VM.PlatformSDKCS.PointF position, Color color, float fontSize = 20f)
        {
            if (bitmap == null || string.IsNullOrWhiteSpace(text)) // 判断图像或文字是否有效。
            {
                return bitmap; // 无法绘制时直接返回原图，避免空引用异常。
            }

            bitmap = EnsureDrawable(bitmap); // 确保图像格式支持 GDI+ 绘制。

            using (Graphics graphics = Graphics.FromImage(bitmap)) // 基于目标图像创建绘图对象。
            using (Font font = new Font("微软雅黑", fontSize, FontStyle.Bold)) // 创建用于绘制叠加文字的字体。
            using (SolidBrush textBrush = new SolidBrush(color)) // 创建文字画刷。
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(128, 0, 0, 0))) // 创建半透明黑色背景画刷，提升文字可读性。
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias; // 开启图形抗锯齿。
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias; // 开启文字抗锯齿。

                SizeF textSize = graphics.MeasureString(text, font); // 计算文字占用区域，便于绘制背景框。
                float x = position.X; // 获取文字左上角 X 坐标。
                float y = position.Y; // 获取文字左上角 Y 坐标。

                graphics.FillRectangle(bgBrush, x - 2, y - 2, textSize.Width + 4, textSize.Height + 4); // 绘制文字底部半透明背景。
                graphics.DrawString(text, font, textBrush, x, y); // 绘制文字内容。
            }

            return bitmap; // 返回绘制后的图像。
        }

        /// <summary>
        /// 在 Bitmap 图像上的多个指定坐标位置分别绘制对应文字。
        /// </summary>
        /// <param name="bitmap">待绘制的目标图像。</param>
        /// <param name="textItems">文字列表，每一项包含文字内容和对应的图像坐标位置。</param>
        /// <param name="color">文字颜色。</param>
        /// <param name="fontSize">文字大小，默认 20。</param>
        /// <returns>返回绘制完成后的 Bitmap 对象。</returns>
        private Bitmap DrawTextList(Bitmap bitmap, List<Tuple<string, VM.PlatformSDKCS.PointF>> textItems, Color color, float fontSize = 20f)
        {
            if (bitmap == null || textItems == null || textItems.Count == 0) // 判断图像和文字列表是否有效。
            {
                return bitmap; // 没有可绘制内容时直接返回原图。
            }

            bitmap = EnsureDrawable(bitmap); // 确保图像格式支持 GDI+ 绘制。

            using (Graphics graphics = Graphics.FromImage(bitmap)) // 基于目标图像创建绘图对象。
            using (Font font = new Font("微软雅黑", fontSize, FontStyle.Bold)) // 创建统一的叠加文字字体。
            using (SolidBrush textBrush = new SolidBrush(color)) // 创建文字画刷。
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(128, 0, 0, 0))) // 创建半透明背景画刷。
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias; // 开启图形抗锯齿。
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias; // 开启文字抗锯齿。

                foreach (Tuple<string, VM.PlatformSDKCS.PointF> item in textItems) // 遍历每一条待绘制的文字。
                {
                    if (string.IsNullOrWhiteSpace(item.Item1)) // 跳过空文字，避免绘制无意义内容。
                    {
                        continue; // 继续处理下一条文字。
                    }

                    SizeF textSize = graphics.MeasureString(item.Item1, font); // 测量当前文字区域大小。
                    float x = item.Item2.X; // 获取当前文字 X 坐标。
                    float y = item.Item2.Y; // 获取当前文字 Y 坐标。

                    graphics.FillRectangle(bgBrush, x - 2, y - 2, textSize.Width + 4, textSize.Height + 4); // 先绘制文字背景框。
                    graphics.DrawString(item.Item1, font, textBrush, x, y); // 再绘制文字内容。
                }
            }

            return bitmap; // 返回绘制后的图像。
        }
        #endregion
    }
}
