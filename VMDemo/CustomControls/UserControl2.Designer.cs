namespace VMDemo.Contro
{
    partial class UserControl2
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.uiTableLayoutPanel1 = new Sunny.UI.UITableLayoutPanel();
            this.zoomPictureBox1 = new VMDemo.Contro.ZoomPictureBox();
            this.zoomPictureBox2 = new VMDemo.Contro.ZoomPictureBox();
            this.uiTableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.zoomPictureBox1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.zoomPictureBox2)).BeginInit();
            this.SuspendLayout();
            // 
            // uiTableLayoutPanel1
            // 
            this.uiTableLayoutPanel1.ColumnCount = 2;
            this.uiTableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.uiTableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.uiTableLayoutPanel1.Controls.Add(this.zoomPictureBox1, 0, 0);
            this.uiTableLayoutPanel1.Controls.Add(this.zoomPictureBox2, 1, 0);
            this.uiTableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiTableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.uiTableLayoutPanel1.Name = "uiTableLayoutPanel1";
            this.uiTableLayoutPanel1.RowCount = 1;
            this.uiTableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.uiTableLayoutPanel1.Size = new System.Drawing.Size(1148, 898);
            this.uiTableLayoutPanel1.TabIndex = 0;
            this.uiTableLayoutPanel1.TagString = null;
            // 
            // zoomPictureBox1
            // 
            this.zoomPictureBox1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(47)))), ((int)(((byte)(52)))));
            this.zoomPictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.zoomPictureBox1.Location = new System.Drawing.Point(3, 3);
            this.zoomPictureBox1.Name = "zoomPictureBox1";
            this.zoomPictureBox1.Size = new System.Drawing.Size(568, 892);
            this.zoomPictureBox1.TabIndex = 0;
            this.zoomPictureBox1.TabStop = false;
            // 
            // zoomPictureBox2
            // 
            this.zoomPictureBox2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(47)))), ((int)(((byte)(52)))));
            this.zoomPictureBox2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.zoomPictureBox2.Location = new System.Drawing.Point(577, 3);
            this.zoomPictureBox2.Name = "zoomPictureBox2";
            this.zoomPictureBox2.Size = new System.Drawing.Size(568, 892);
            this.zoomPictureBox2.TabIndex = 1;
            this.zoomPictureBox2.TabStop = false;
            // 
            // UserControl2
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.uiTableLayoutPanel1);
            this.Name = "UserControl2";
            this.Size = new System.Drawing.Size(1148, 898);
            this.uiTableLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.zoomPictureBox1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.zoomPictureBox2)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private Sunny.UI.UITableLayoutPanel uiTableLayoutPanel1;
        private ZoomPictureBox zoomPictureBox1;
        private ZoomPictureBox zoomPictureBox2;
    }
}
