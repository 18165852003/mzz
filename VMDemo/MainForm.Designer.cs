namespace VMDemo
{
    partial class MainForm
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

        #region Windows 窗体设计器生成的代码

        /// <summary>
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.uiPanel1 = new Sunny.UI.UIPanel();
            this.uiSymbolLabel3 = new Sunny.UI.UISymbolLabel();
            this.uiSymbolLabel2 = new Sunny.UI.UISymbolLabel();
            this.uiSymbolLabel1 = new Sunny.UI.UISymbolLabel();
            this.uiPanel2 = new Sunny.UI.UIPanel();
            this.uiPanel3 = new Sunny.UI.UIPanel();
            this.Log = new Sunny.UI.UIListBox();
            this.tableLayoutPanel1.SuspendLayout();
            this.uiPanel1.SuspendLayout();
            this.uiPanel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(38)))), ((int)(((byte)(42)))));
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.uiPanel1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.uiPanel2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.uiPanel3, 0, 2);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 35);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 3F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 85F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1770, 1154);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // uiPanel1
            // 
            this.uiPanel1.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(47)))), ((int)(((byte)(52)))));
            this.uiPanel1.Controls.Add(this.uiSymbolLabel3);
            this.uiPanel1.Controls.Add(this.uiSymbolLabel2);
            this.uiPanel1.Controls.Add(this.uiSymbolLabel1);
            this.uiPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPanel1.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(47)))), ((int)(((byte)(52)))));
            this.uiPanel1.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(44)))), ((int)(((byte)(47)))), ((int)(((byte)(52)))));
            this.uiPanel1.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiPanel1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(234)))), ((int)(((byte)(238)))));
            this.uiPanel1.Location = new System.Drawing.Point(0, 0);
            this.uiPanel1.Margin = new System.Windows.Forms.Padding(0);
            this.uiPanel1.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiPanel1.Name = "uiPanel1";
            this.uiPanel1.RadiusSides = Sunny.UI.UICornerRadiusSides.None;
            this.uiPanel1.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(81)))), ((int)(((byte)(88)))));
            this.uiPanel1.RectSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.None;
            this.uiPanel1.ShowText = false;
            this.uiPanel1.Size = new System.Drawing.Size(1770, 34);
            this.uiPanel1.Style = Sunny.UI.UIStyle.Custom;
            this.uiPanel1.StyleCustomMode = true;
            this.uiPanel1.TabIndex = 0;
            this.uiPanel1.Text = null;
            this.uiPanel1.TextAlignment = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // uiSymbolLabel3
            // 
            this.uiSymbolLabel3.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabel3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(202)))), ((int)(((byte)(202)))), ((int)(((byte)(203)))));
            this.uiSymbolLabel3.Location = new System.Drawing.Point(81, 0);
            this.uiSymbolLabel3.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabel3.Name = "uiSymbolLabel3";
            this.uiSymbolLabel3.Size = new System.Drawing.Size(33, 34);
            this.uiSymbolLabel3.Symbol = 61469;
            this.uiSymbolLabel3.SymbolColor = System.Drawing.Color.FromArgb(((int)(((byte)(202)))), ((int)(((byte)(202)))), ((int)(((byte)(203)))));
            this.uiSymbolLabel3.SymbolSize = 30;
            this.uiSymbolLabel3.TabIndex = 2;
            this.uiSymbolLabel3.Click += new System.EventHandler(this.uiSymbolLabel3_Click);
            // 
            // uiSymbolLabel2
            // 
            this.uiSymbolLabel2.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabel2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(202)))), ((int)(((byte)(202)))), ((int)(((byte)(203)))));
            this.uiSymbolLabel2.Location = new System.Drawing.Point(42, 0);
            this.uiSymbolLabel2.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabel2.Name = "uiSymbolLabel2";
            this.uiSymbolLabel2.Size = new System.Drawing.Size(33, 34);
            this.uiSymbolLabel2.Symbol = 61717;
            this.uiSymbolLabel2.SymbolColor = System.Drawing.Color.FromArgb(((int)(((byte)(202)))), ((int)(((byte)(202)))), ((int)(((byte)(203)))));
            this.uiSymbolLabel2.SymbolSize = 30;
            this.uiSymbolLabel2.TabIndex = 1;
            this.uiSymbolLabel2.Click += new System.EventHandler(this.uiSymbolLabel2_Click);
            // 
            // uiSymbolLabel1
            // 
            this.uiSymbolLabel1.Font = new System.Drawing.Font("微软雅黑", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiSymbolLabel1.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(202)))), ((int)(((byte)(202)))), ((int)(((byte)(203)))));
            this.uiSymbolLabel1.Location = new System.Drawing.Point(3, 0);
            this.uiSymbolLabel1.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiSymbolLabel1.Name = "uiSymbolLabel1";
            this.uiSymbolLabel1.Size = new System.Drawing.Size(33, 34);
            this.uiSymbolLabel1.Symbol = 61459;
            this.uiSymbolLabel1.SymbolColor = System.Drawing.Color.FromArgb(((int)(((byte)(202)))), ((int)(((byte)(202)))), ((int)(((byte)(203)))));
            this.uiSymbolLabel1.SymbolSize = 30;
            this.uiSymbolLabel1.TabIndex = 0;
            this.uiSymbolLabel1.Click += new System.EventHandler(this.uiSymbolLabel1_Click);
            // 
            // uiPanel2
            // 
            this.uiPanel2.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(55)))), ((int)(((byte)(60)))));
            this.uiPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPanel2.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(55)))), ((int)(((byte)(60)))));
            this.uiPanel2.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(52)))), ((int)(((byte)(55)))), ((int)(((byte)(60)))));
            this.uiPanel2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiPanel2.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(234)))), ((int)(((byte)(238)))));
            this.uiPanel2.Location = new System.Drawing.Point(0, 34);
            this.uiPanel2.Margin = new System.Windows.Forms.Padding(0);
            this.uiPanel2.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiPanel2.Name = "uiPanel2";
            this.uiPanel2.RadiusSides = Sunny.UI.UICornerRadiusSides.None;
            this.uiPanel2.RectSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.None;
            this.uiPanel2.ShowText = false;
            this.uiPanel2.Size = new System.Drawing.Size(1770, 980);
            this.uiPanel2.Style = Sunny.UI.UIStyle.Custom;
            this.uiPanel2.StyleCustomMode = true;
            this.uiPanel2.TabIndex = 1;
            this.uiPanel2.Text = null;
            this.uiPanel2.TextAlignment = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // uiPanel3
            // 
            this.uiPanel3.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(42)))), ((int)(((byte)(46)))));
            this.uiPanel3.Controls.Add(this.Log);
            this.uiPanel3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.uiPanel3.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(42)))), ((int)(((byte)(46)))));
            this.uiPanel3.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(42)))), ((int)(((byte)(46)))));
            this.uiPanel3.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.uiPanel3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(234)))), ((int)(((byte)(238)))));
            this.uiPanel3.Location = new System.Drawing.Point(0, 1014);
            this.uiPanel3.Margin = new System.Windows.Forms.Padding(0);
            this.uiPanel3.MinimumSize = new System.Drawing.Size(1, 1);
            this.uiPanel3.Name = "uiPanel3";
            this.uiPanel3.RadiusSides = Sunny.UI.UICornerRadiusSides.None;
            this.uiPanel3.RectSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.None;
            this.uiPanel3.ShowText = false;
            this.uiPanel3.Size = new System.Drawing.Size(1770, 140);
            this.uiPanel3.Style = Sunny.UI.UIStyle.Custom;
            this.uiPanel3.StyleCustomMode = true;
            this.uiPanel3.TabIndex = 2;
            this.uiPanel3.Text = null;
            this.uiPanel3.TextAlignment = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // Log
            // 
            this.Log.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(42)))), ((int)(((byte)(46)))));
            this.Log.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Log.FillColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(42)))), ((int)(((byte)(46)))));
            this.Log.FillColor2 = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(42)))), ((int)(((byte)(46)))));
            this.Log.FillDisableColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(42)))), ((int)(((byte)(46)))));
            this.Log.Font = new System.Drawing.Font("宋体", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.Log.ForeColor = System.Drawing.Color.Lime;
            this.Log.HoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(58)))), ((int)(((byte)(62)))));
            this.Log.ItemHeight = 24;
            this.Log.ItemSelectBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(55)))), ((int)(((byte)(58)))), ((int)(((byte)(62)))));
            this.Log.ItemSelectForeColor = System.Drawing.Color.Lime;
            this.Log.Location = new System.Drawing.Point(0, 0);
            this.Log.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Log.MinimumSize = new System.Drawing.Size(1, 1);
            this.Log.Name = "Log";
            this.Log.Padding = new System.Windows.Forms.Padding(2);
            this.Log.RectColor = System.Drawing.Color.Transparent;
            this.Log.ScrollBarBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(42)))), ((int)(((byte)(46)))));
            this.Log.ScrollBarColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this.Log.ScrollBarStyleInherited = false;
            this.Log.ShowText = false;
            this.Log.Size = new System.Drawing.Size(1770, 140);
            this.Log.StyleCustomMode = true;
            this.Log.TabIndex = 0;
            this.Log.Text = null;
            // 
            // MainForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(36)))), ((int)(((byte)(38)))), ((int)(((byte)(42)))));
            this.ClientSize = new System.Drawing.Size(1770, 1189);
            this.ControlBoxCloseFillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(201)))), ((int)(((byte)(92)))), ((int)(((byte)(54)))));
            this.ControlBoxFillHoverColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(138)))), ((int)(((byte)(36)))));
            this.ControlBoxForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(170)))), ((int)(((byte)(174)))), ((int)(((byte)(181)))));
            this.Controls.Add(this.tableLayoutPanel1);
            this.Font = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(234)))), ((int)(((byte)(238)))));
            this.Name = "MainForm";
            this.RectColor = System.Drawing.Color.FromArgb(((int)(((byte)(78)))), ((int)(((byte)(81)))), ((int)(((byte)(88)))));
            this.ShowIcon = false;
            this.ShowShadow = false;
            this.Style = Sunny.UI.UIStyle.Custom;
            this.StyleCustomMode = true;
            this.Text = "VM二次开发";
            this.TitleColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(31)))), ((int)(((byte)(35)))));
            this.TitleFont = new System.Drawing.Font("Microsoft YaHei UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.TitleForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(232)))), ((int)(((byte)(234)))), ((int)(((byte)(238)))));
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.ZoomScaleRect = new System.Drawing.Rectangle(22, 22, 1770, 1189);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.uiPanel1.ResumeLayout(false);
            this.uiPanel3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private Sunny.UI.UIPanel uiPanel1;
        private Sunny.UI.UIPanel uiPanel2;
        private Sunny.UI.UIPanel uiPanel3;
        private Sunny.UI.UISymbolLabel uiSymbolLabel1;
        private Sunny.UI.UISymbolLabel uiSymbolLabel2;
        private Sunny.UI.UISymbolLabel uiSymbolLabel3;
        private Sunny.UI.UIListBox Log;
    }
}

