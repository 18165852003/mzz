using System; // 引入基础类型和异常处理相关命名空间。
using System.Drawing; // 引入 Bitmap、Graphics、Color 等绘图相关命名空间。
using System.Drawing.Drawing2D; // 引入插值模式等高级绘图相关命名空间。
using System.Windows.Forms; // 引入 PictureBox、鼠标事件等 WinForms 相关命名空间。

namespace VMDemo.Contro // 定义当前自定义控件所在的命名空间。
{ // 命名空间开始。
    /// <summary>
    /// 支持鼠标拖动平移、滚轮缩放和双击复位的自定义图片框控件。
    /// 继承自 PictureBox，通过重写 OnPaint 自行绘制图像，不依赖 PictureBox 默认的 Image 显示机制。
    /// </summary>
    public class ZoomPictureBox : PictureBox // 继承 PictureBox，复用其基础控件行为。
    { // 类开始。
        private Bitmap _sourceImage; // 保存当前要显示的原始图像，由 SetImage 方法设置。
        private float _zoom = 1.0f; // 当前缩放倍数，1.0 表示原始大小。
        private PointF _offset; // 当前图像左上角相对于控件左上角的偏移量，用于控制平移。
        private bool _isDragging; // 标记当前是否正在进行鼠标拖动操作。
        private Point _dragStartPoint; // 记录鼠标拖动开始时的鼠标位置。
        private PointF _dragStartOffset; // 记录鼠标拖动开始时的图像偏移量，用于计算拖动差值。

        private const float MinZoom = 0.05f; // 允许的最小缩放倍数，防止图片缩太小看不见。
        private const float MaxZoom = 50.0f; // 允许的最大缩放倍数，防止图片放太大导致性能问题。
        private const float ZoomInFactor = 1.1f; // 每次滚轮向上滚动时的放大比例。
        private const float ZoomOutFactor = 0.9f; // 每次滚轮向下滚动时的缩小比例。

        /// <summary>
        /// 构造函数，初始化控件基础设置。
        /// </summary>
        public ZoomPictureBox() // 公开构造函数。
        { // 构造函数开始。
            DoubleBuffered = true; // 开启双缓冲，防止绘制时画面闪烁。
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true); // 进一步优化绘制性能，确保所有绘制都走 OnPaint。
        } // 构造函数结束。

        /// <summary>
        /// 设置当前要显示的图像，并自动将图像缩放到控件大小并居中显示。
        /// 调用此方法后会自动释放上一张图像的资源。
        /// </summary>
        /// <param name="image">要显示的 Bitmap 图像对象。</param>
        public void SetImage(Bitmap image) // 对外提供设置图像的统一入口。
        { // 方法开始。
            Bitmap oldImage = _sourceImage; // 先保存旧图像引用，稍后释放。
            _sourceImage = image; // 将新图像赋值给内部字段。
            FitToControl(); // 让新图像自适应控件大小并居中显示。
            oldImage?.Dispose(); // 释放上一张图像占用的资源，避免内存泄漏。
        } // 方法结束。

        /// <summary>
        /// 获取当前正在显示的原始图像。
        /// </summary>
        public Bitmap SourceImage => _sourceImage; // 对外提供当前图像的只读访问。

        #region 自定义绘制
        /// <summary>
        /// 重写控件绘制方法。
        /// 不使用 PictureBox 默认的 Image 显示机制，而是根据当前缩放倍数和偏移量手动绘制图像。
        /// </summary>
        /// <param name="e">绘制事件参数，包含当前的 Graphics 绘图对象。</param>
        protected override void OnPaint(PaintEventArgs e) // 重写 OnPaint 以实现自定义图像绘制。
        { // 方法开始。
            e.Graphics.Clear(BackColor); // 先用控件背景色清空整个绘制区域，避免残留上一帧的图像。

            if (_sourceImage == null) // 判断当前是否存在要显示的图像。
            { // 条件分支开始。
                return; // 如果没有图像则不绘制任何内容。
            } // 条件分支结束。

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear; // 设置高质量双线性插值，让缩放后的图像边缘更平滑。
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality; // 设置高质量像素偏移模式，提升绘制精度。

            float drawWidth = _sourceImage.Width * _zoom; // 计算当前缩放后的图像显示宽度。
            float drawHeight = _sourceImage.Height * _zoom; // 计算当前缩放后的图像显示高度。

            e.Graphics.DrawImage(_sourceImage, _offset.X, _offset.Y, drawWidth, drawHeight); // 在指定偏移位置绘制缩放后的图像。
        } // 方法结束。
        #endregion

        #region 鼠标拖动平移
        /// <summary>
        /// 鼠标按下时开始拖动。
        /// 记录当前鼠标位置和图像偏移量作为拖动起点。
        /// </summary>
        protected override void OnMouseDown(MouseEventArgs e) // 重写鼠标按下事件。
        { // 方法开始。
            if (e.Button == MouseButtons.Left && _sourceImage != null) // 仅在左键按下且有图像时才启动拖动。
            { // 条件分支开始。
                _isDragging = true; // 标记当前进入拖动状态。
                _dragStartPoint = e.Location; // 记录鼠标按下时的位置。
                _dragStartOffset = _offset; // 记录拖动开始时的图像偏移量。
                Cursor = Cursors.Hand; // 将鼠标光标切换为手型，提示用户当前正在拖动。
            } // 条件分支结束。

            base.OnMouseDown(e); // 调用基类方法，保持默认行为链完整。
        } // 方法结束。

        /// <summary>
        /// 鼠标移动时更新图像偏移量，实现拖动平移。
        /// 计算当前鼠标位置与拖动起点的差值，加到拖动开始时的偏移量上。
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e) // 重写鼠标移动事件。
        { // 方法开始。
            if (_isDragging) // 判断当前是否处于拖动状态。
            { // 条件分支开始。
                float deltaX = e.X - _dragStartPoint.X; // 计算鼠标在 X 方向上移动的像素距离。
                float deltaY = e.Y - _dragStartPoint.Y; // 计算鼠标在 Y 方向上移动的像素距离。
                _offset = new PointF(_dragStartOffset.X + deltaX, _dragStartOffset.Y + deltaY); // 将拖动距离叠加到拖动起点偏移量上，得到新的图像偏移位置。
                Invalidate(); // 触发控件重绘，让图像显示到新位置。
            } // 条件分支结束。

            base.OnMouseMove(e); // 调用基类方法，保持默认行为链完整。
        } // 方法结束。

        /// <summary>
        /// 鼠标松开时结束拖动。
        /// </summary>
        protected override void OnMouseUp(MouseEventArgs e) // 重写鼠标松开事件。
        { // 方法开始。
            if (_isDragging) // 判断当前是否正在拖动。
            { // 条件分支开始。
                _isDragging = false; // 标记拖动状态结束。
                Cursor = Cursors.Default; // 将鼠标光标恢复为默认箭头。
            } // 条件分支结束。

            base.OnMouseUp(e); // 调用基类方法，保持默认行为链完整。
        } // 方法结束。
        #endregion

        #region 滚轮缩放
        /// <summary>
        /// 滚轮滚动时缩放图像。
        /// 以鼠标当前位置为缩放中心，保证鼠标指向的图像点在缩放前后不变。
        /// </summary>
        protected override void OnMouseWheel(MouseEventArgs e) // 重写鼠标滚轮事件。
        { // 方法开始。
            if (_sourceImage == null) // 判断当前是否存在要显示的图像。
            { // 条件分支开始。
                base.OnMouseWheel(e); // 如果没有图像则只调用基类方法。
                return; // 直接结束方法。
            } // 条件分支结束。

            float oldZoom = _zoom; // 保存缩放前的倍数，后续计算偏移调整时使用。
            float zoomDelta = e.Delta > 0 ? ZoomInFactor : ZoomOutFactor; // 根据滚轮方向决定放大还是缩小。
            _zoom *= zoomDelta; // 将当前缩放倍数乘以缩放增量。
            _zoom = Math.Max(MinZoom, Math.Min(_zoom, MaxZoom)); // 将缩放倍数限制在允许的最小值和最大值之间。

            float zoomRatio = _zoom / oldZoom; // 计算缩放前后的倍数比值。
            _offset = new PointF( // 以鼠标位置为中心调整图像偏移量，确保鼠标指向的图像点在缩放前后保持不变。
                e.X - (e.X - _offset.X) * zoomRatio, // 调整 X 方向偏移量。
                e.Y - (e.Y - _offset.Y) * zoomRatio); // 调整 Y 方向偏移量。

            Invalidate(); // 触发控件重绘，显示缩放后的图像。
            base.OnMouseWheel(e); // 调用基类方法，保持默认行为链完整。
        } // 方法结束。
        #endregion

        #region 双击复位
        /// <summary>
        /// 双击控件时将图像恢复为自适应控件大小并居中显示。
        /// </summary>
        protected override void OnDoubleClick(EventArgs e) // 重写双击事件。
        { // 方法开始。
            FitToControl(); // 让图像自适应控件大小并居中显示。
            base.OnDoubleClick(e); // 调用基类方法，保持默认行为链完整。
        } // 方法结束。

        /// <summary>
        /// 控件尺寸变化时自动重新适配图像显示。
        /// </summary>
        protected override void OnSizeChanged(EventArgs e) // 重写控件尺寸变化事件。
        { // 方法开始。
            base.OnSizeChanged(e); // 先调用基类方法，完成默认的尺寸变化处理。
            FitToControl(); // 控件大小改变后重新计算缩放和偏移，让图像始终自适应居中。
        } // 方法结束。
        #endregion

        #region 内部方法
        /// <summary>
        /// 让当前图像自适应控件大小并居中显示。
        /// 计算宽高方向各自需要的缩放比例，取较小值确保图像完整显示在控件内，然后居中。
        /// </summary>
        private void FitToControl() // 图像自适应控件大小并居中。
        { // 方法开始。
            if (_sourceImage == null || Width <= 0 || Height <= 0) // 判断图像和控件尺寸是否有效。
            { // 条件分支开始。
                _zoom = 1.0f; // 如果图像无效则重置缩放倍数。
                _offset = PointF.Empty; // 重置偏移量。
                Invalidate(); // 触发重绘以清空显示。
                return; // 直接结束方法。
            } // 条件分支结束。

            float zoomX = (float)Width / _sourceImage.Width; // 计算宽度方向上的缩放比例。
            float zoomY = (float)Height / _sourceImage.Height; // 计算高度方向上的缩放比例。
            _zoom = Math.Min(zoomX, zoomY); // 取两个方向中较小的缩放比例，确保图像完整显示不超出控件。

            float drawWidth = _sourceImage.Width * _zoom; // 计算自适应后的图像显示宽度。
            float drawHeight = _sourceImage.Height * _zoom; // 计算自适应后的图像显示高度。
            _offset = new PointF((Width - drawWidth) / 2.0f, (Height - drawHeight) / 2.0f); // 将图像居中放置在控件内。

            Invalidate(); // 触发控件重绘，显示自适应后的图像。
        } // 方法结束。

        /// <summary>
        /// 释放当前控件持有的图像资源。
        /// </summary>
        /// <param name="disposing">如果为 true 则释放托管资源。</param>
        protected override void Dispose(bool disposing) // 重写控件释放方法。
        { // 方法开始。
            if (disposing) // 判断是否需要释放托管资源。
            { // 条件分支开始。
                _sourceImage?.Dispose(); // 释放当前持有的原始图像资源。
                _sourceImage = null; // 清空图像引用。
            } // 条件分支结束。

            base.Dispose(disposing); // 调用基类释放方法，完成控件的默认清理。
        } // 方法结束。
        #endregion
    } // 类结束。
} // 命名空间结束。
