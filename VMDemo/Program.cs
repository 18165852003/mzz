using System; // 引入基础类型相关命名空间。
using System.Collections.Generic; // 引入泛型集合相关命名空间。
using System.Linq; // 引入集合查询相关命名空间。
using System.Threading; // 引入线程和互斥锁相关命名空间。
using System.Threading.Tasks; // 引入异步任务相关命名空间。
using System.Windows.Forms; // 引入 WinForms 窗体和控件相关命名空间。

namespace VMDemo // 定义当前项目的命名空间。
{ // 命名空间开始。
    internal static class Program // 定义程序入口类，internal 表示仅当前程序集可访问，static 表示不需要实例化。
    { // 类开始。
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread] // 标记主线程使用单线程单元模型，WinForms 应用程序必须使用此模式才能正确处理 UI 和 COM 交互。
        static void Main() // 程序启动时由运行时自动调用的入口方法。
        { // 方法开始。
            bool createdNew; // 定义一个布尔变量，用于接收当前互斥锁是否为新创建的结果。
            using (var mutex = new Mutex(true, "MyApp_UniqueId_12345", out createdNew)) // 创建一个全局命名互斥锁，用于防止程序重复启动。第一个参数 true 表示当前线程立即获取锁的所有权；第二个参数是互斥锁的唯一名称；第三个参数输出当前锁是否为本次新创建。
            { // using 代码块开始，方法结束时自动释放互斥锁。
                if (!createdNew) // 如果互斥锁不是本次新创建的，说明已经有一个程序实例正在运行。
                { // 条件分支开始。
                    MessageBox.Show("程序已在运行中！", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information); // 弹出提示框告知用户程序已经在运行。
                    return; // 直接退出当前方法，不再启动新的窗体实例。
                } // 条件分支结束。

                Application.EnableVisualStyles(); // 启用 Windows 视觉样式，让控件外观使用操作系统当前主题风格。
                Application.SetCompatibleTextRenderingDefault(false); // 设置控件默认使用 GDI+ 文本渲染（false），而不是旧版 GDI 渲染，确保文字显示效果一致。
                Application.Run(new MainForm()); // 创建主窗体实例并启动消息循环，程序在此处阻塞运行，直到主窗体关闭后才继续执行后续代码。
            } // using 代码块结束，互斥锁在此自动释放。
        } // 方法结束。
    } // 类结束。
} // 命名空间结束。
