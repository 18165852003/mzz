using VM.Core;

namespace VMDemo // 定义当前项目命名空间。
{ // 命名空间开始。
    public sealed partial class SingletonManager // 声明 SingletonManager 分部类，本文件负责资源释放。
    { // 类开始。
        #region 释放资源
        /// <summary>
        /// 释放当前方案及相关缓存资源。
        /// </summary>
        public void Dispose()
        {
            StopTcpServer(); // 释放资源前先停止 TCP 服务端，避免后台监听线程在程序退出时仍然运行。
            DetachCallbacks(); // 释放前先移除所有流程结束回调。
            _procedureIndexMap.Clear(); // 清空流程和显示索引的映射关系。
            pictureBoxes = null; // 清空图片框列表引用。
            userControl = null; // 清空当前流程界面控件引用。

            if (VmSolution.Instance == null) // 判断当前方案实例是否存在。
            {
                return; // 如果方案实例为空则直接结束方法。
            }

            VmSolution.Instance.Dispose(); // 释放当前已加载的方案实例资源。
        }
        #endregion
    } // 类结束。
} // 命名空间结束。
