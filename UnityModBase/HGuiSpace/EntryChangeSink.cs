using System;
using System.Collections.Generic;
using UnityModBase.HGuiSpace.Bindings;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 集中处理 GUI 编辑值的转换、暂存、延迟提交、可选重置及变更通知。
    /// 延迟以调用方传入的帧增量递减，实例没有并发保护，预期由所属 GUI 上下文在 Unity 主线程使用。
    /// </summary>
    public sealed class EntryChangeSink
    {
        // 每个条目只保留一个剩余延迟；同一项的新输入会覆盖倒计时并使用缓冲区中的最新序号值。
        private readonly Dictionary<IEntryBinding, float> _pendingEntries = new Dictionary<IEntryBinding, float>();

        // 记录暂存了待转换输入（来自 SetConvertedValue）的条目；只有这些条目在提交时刻按声明类型转换。
        private readonly HashSet<IEntryBinding> _convertPendingEntries = new HashSet<IEntryBinding>();

        /// <summary>
        /// 在有效新值实际写入条目后触发；无变化或无效输入不会触发。
        /// </summary>
        public event Action<IEntryBinding> OnEntryValueChanged;

        /// <summary>
        /// 在可重置条目恢复默认值后触发，即使默认值与原值相同也会触发。
        /// </summary>
        public event Action<IEntryBinding> OnEntryValueReset;

        /// <summary>
        /// 按指定延迟暂存待转换输入；转换推迟到提交时刻执行。
        /// 暂存阶段保留原始输入（如文本 "1."）用于界面回显，避免中间态文本被提前规范化；
        /// 仅当输入按目标类型转换失败时标记为无效，提交时会被丢弃。
        /// </summary>
        /// <param name="entry">目标条目绑定。</param>
        /// <param name="value">通常来自文本控件的待转换输入。</param>
        /// <param name="delay">提交延迟，单位由 <see cref="FlushValue"/> 的增量保持一致；小于等于 0 时立即提交。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public void SetConvertedValue(IEntryBinding entry, object value, float delay = 0.0f)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");

            var isValid = TypeConvert.TryTo(value, entry.ValueType, out _);
            _convertPendingEntries.Add(entry);
            SetValue(entry, value, isValid, delay);
        }

        /// <summary>
        /// 写入无键暂存值，并立即提交或启动延迟提交。
        /// </summary>
        /// <param name="entry">目标条目绑定。</param>
        /// <param name="value">要暂存的输入值。</param>
        /// <param name="isValid">为 false 时输入仅供回显，提交时会被丢弃。</param>
        /// <param name="delay">小于等于 0 时立即提交；正值会替换该条目已有的剩余延迟。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public void SetValue(IEntryBinding entry, object value, bool isValid = true, float delay = 0.0f)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");

            entry.EditBuffer.SetValue(value, isValid);

            if (delay <= 0.0f)
                Commit(entry);
            else
                _pendingEntries[entry] = delay;
        }

        /// <summary>
        /// 以来源键暂存值，并立即提交或启动延迟提交。
        /// 多来源同时写入时，最终提交由缓冲区记录的全局写入顺序决定，而不是键名决定。
        /// </summary>
        /// <param name="entry">目标条目绑定。</param>
        /// <param name="key">同一条目内区分输入来源的非空键。</param>
        /// <param name="value">要暂存的输入值。</param>
        /// <param name="isValid">为 false 时输入仅供回显，提交时会被丢弃。</param>
        /// <param name="delay">小于等于 0 时立即提交；正值会替换该条目已有的剩余延迟。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="key"/> 为 null 或空字符串。</exception>
        public void SetValue(IEntryBinding entry, string key, object value, bool isValid = true, float delay = 0.0f)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");

            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty.", nameof(key));

            entry.EditBuffer.SetValue(key, value, isValid);

            if (delay <= 0.0f)
                Commit(entry);
            else
                _pendingEntries[entry] = delay;
        }

        /// <summary>
        /// 取消目标条目的待提交值、恢复默认值并发送重置通知。
        /// </summary>
        /// <param name="entry">要恢复默认值的可重置条目绑定。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public void ResetValue(IResettableEntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");

            _pendingEntries.Remove(entry);
            _convertPendingEntries.Remove(entry);
            entry.EditBuffer.Clear();

            entry.ResetValue();
            OnEntryValueReset?.Invoke(entry);
        }

        /// <summary>
        /// 推进所有延迟提交的倒计时，并提交已到期项。
        /// </summary>
        /// <param name="deltaTime">从所有待提交项扣除的非缩放帧增量；调用方负责保证单位和取值合理。</param>
        public void FlushValue(float deltaTime)
        {
            var entries = new List<IEntryBinding>(_pendingEntries.Keys);
            foreach (var entry in entries)
            {
                var remainingDelay = _pendingEntries[entry] - deltaTime;
                if (remainingDelay <= 0.0f)
                {
                    _pendingEntries.Remove(entry);
                    Commit(entry);
                }
                else
                    _pendingEntries[entry] = remainingDelay;
            }
        }

        /// <summary>
        /// 尝试立即提交调用时快照中的全部待处理条目；每项提交前会先清除其延迟状态。
        /// 有效的最新输入会写回条目，无效输入会由编辑缓冲区丢弃；没有待处理项时按空操作处理。
        /// </summary>
        /// <remarks>
        /// 本方法用于用户上下文切换和销毁边界，防止不再推进的延迟项长期滞留。
        /// 提交前先从待处理集合移除当前项，因此提交期间发生的同项新输入会作为新的待处理项保留。
        /// 本方法不隔离条目写入或变更订阅者抛出的异常：发生异常的项已经移除，尚未遍历的项仍保持待处理。
        /// </remarks>
        public void CommitPending()
        {
            var entries = new List<IEntryBinding>(_pendingEntries.Keys);
            foreach (var entry in entries)
            {
                _pendingEntries.Remove(entry);
                Commit(entry);
            }
        }

        /// <summary>
        /// 尝试提交缓冲区中最新的有效值，并清空该条目的全部暂存输入。
        /// 暂存的待转换输入（如文本）在这一刻才转换为条目声明类型，转换失败则放弃写入；
        /// 仅在已提交值确实变化时发送变更通知。
        /// </summary>
        /// <param name="entry">要提交暂存值的条目绑定。</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> 为 null。</exception>
        public void Commit(IEntryBinding entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry), "Entry cannot be null.");

            // 只有 SetConvertedValue 暂存的待转换输入需要提交时刻转换；直写值的提交不读取条目声明类型。
            Func<object, object> transform = null;
            if (_convertPendingEntries.Remove(entry))
                transform = value => ConvertForCommit(value, entry.ValueType);

            var valueChanged = entry.EditBuffer.Commit(entry, transform);

            if (valueChanged)
                OnEntryValueChanged?.Invoke(entry);
        }

        /// <summary>
        /// 提交时刻的值转换：已可赋值给目标类型的值原样返回，文本等其余输入按目标类型转换，
        /// 转换失败返回 null 表示放弃本次写入。
        /// </summary>
        private static object ConvertForCommit(object value, Type targetType)
        {
            if (targetType.IsInstanceOfType(value))
                return value;

            return TypeConvert.TryTo(value, targetType, out var convertedValue) ? convertedValue : null;
        }
    }
}
