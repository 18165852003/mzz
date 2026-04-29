using System; // 引入基础类型和 Lazy<T> 所在的命名空间。

namespace VMDemo // 定义当前项目的命名空间。
{ // 命名空间开始。
    /// <summary>
    /// 定义一个最简的单例模式密封类。
    /// </summary>
    internal sealed class SingletonManager // 定义一个密封类，禁止其他类继承。
    { // 类开始。
        private static readonly Lazy<SingletonManager> _instance = // 使用延迟加载方式保存当前类的唯一实例。
            new Lazy<SingletonManager>(() => new SingletonManager()); // 指定单例对象的创建逻辑。

        public static SingletonManager Instance => _instance.Value; // 对外提供访问唯一实例的静态入口。

        private SingletonManager() // 私有构造函数，防止外部通过 new 直接创建对象。
        { // 构造函数开始。
        } // 构造函数结束。
    } // 类结束。
} // 命名空间结束。
