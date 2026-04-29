using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VMDemo.Contro
{
    public partial class UserControl1 : UserControl
    {


        public UserControl1()
        {
            InitializeComponent();
            AutoScaleMode = AutoScaleMode.None; // 禁用当前用户控件自动缩放，防止执行时布局被重新计算。
        }

        private void uiTableLayoutPanel2_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
