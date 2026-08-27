using System;

namespace UnityModBase.HControlSpace
{
    /// <summary>
    /// 描述实时控制项的值刷新策略。预定义策略不携带条件，条件策略通过工厂方法创建。
    /// </summary>
    public sealed class ControlUpdatePolicy
    {
        /// <summary>每帧更新。</summary>
        public static ControlUpdatePolicy EveryFrame { get; } = new ControlUpdatePolicy(ControlUpdateKind.EveryFrame, null);

        /// <summary>每秒更新。</summary>
        public static ControlUpdatePolicy EverySecond { get; } = new ControlUpdatePolicy(ControlUpdateKind.EverySecond, null);

        /// <summary>界面显示时每帧更新。</summary>
        public static ControlUpdatePolicy WhenVisibleEveryFrame { get; } = new ControlUpdatePolicy(ControlUpdateKind.WhenVisibleEveryFrame, null);

        /// <summary>界面显示时每秒更新。</summary>
        public static ControlUpdatePolicy WhenVisibleEverySecond { get; } = new ControlUpdatePolicy(ControlUpdateKind.WhenVisibleEverySecond, null);

        /// <summary>不自动更新。</summary>
        public static ControlUpdatePolicy Never { get; } = new ControlUpdatePolicy(ControlUpdateKind.Never, null);

        /// <summary>获取策略种类。</summary>
        public ControlUpdateKind Kind { get; }

        /// <summary>获取条件委托；非条件策略为 null。</summary>
        public Func<bool> Condition { get; }

        private ControlUpdatePolicy(ControlUpdateKind kind, Func<bool> condition)
        {
            Kind = kind;
            Condition = condition;
        }

        /// <summary>
        /// 创建条件更新策略。每帧求值条件，只要返回 true 就读取一次监听对象。
        /// </summary>
        /// <param name="condition">不可为 null 的条件委托。</param>
        public static ControlUpdatePolicy When(Func<bool> condition)
        {
            return new ControlUpdatePolicy(
                ControlUpdateKind.When,
                condition ?? throw new ArgumentNullException(nameof(condition)));
        }

        /// <summary>
        /// 创建界面显示期间生效的条件更新策略。
        /// </summary>
        /// <param name="condition">不可为 null 的条件委托。</param>
        public static ControlUpdatePolicy WhenVisible(Func<bool> condition)
        {
            return new ControlUpdatePolicy(
                ControlUpdateKind.WhenVisible,
                condition ?? throw new ArgumentNullException(nameof(condition)));
        }
    }
}
