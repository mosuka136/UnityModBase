using System;
using System.Collections.Generic;
using System.Linq;
using UnityModBase.HProvider;
using UnityModBase.HUserSpace;

namespace UnityModBase.HGuiSpace
{
    /// <summary>
    /// 提供各 GUI 模块共用的用户选择器，并将具体用户内容和布局失效处理留给派生编辑器。
    /// 实例保存选择器展开状态，预期由单个 GUI 宿主在 Unity 主线程中复用和释放。
    /// </summary>
    /// <remarks>
    /// 本类只依据传入序列和用户标识绘制选项，不检查用户是否挂载了调用模块所需的子上下文。
    /// IMGUI 垂直布局成功开启后会在异常路径中闭合，避免污染同一帧后续控件的布局栈。
    /// </remarks>
    public abstract class UserEditorBase : IDisposable
    {
        /// <summary>
        /// 获取或设置用户选择列表是否展开。
        /// </summary>
        public bool IsExpanded { get; set; } = false;
        /// <summary>
        /// 获取派生编辑器共享的 Unity 服务。
        /// </summary>
        public IUnityProvider UnityService { get; }
        /// <summary>
        /// 获取用户选择器和派生编辑器共享的 IMGUI 提供器。
        /// </summary>
        public IUnityGuiProvider UnityGui { get; }

        /// <summary>
        /// 创建用户选择器基类。
        /// </summary>
        /// <param name="unityService">派生编辑器使用的 Unity 服务。</param>
        /// <param name="unityGui">用于绘制用户选择器的 IMGUI 提供器。</param>
        public UserEditorBase(IUnityProvider unityService, IUnityGuiProvider unityGui)
        {
            UnityService = unityService;
            UnityGui = unityGui;
        }

        /// <summary>
        /// 绘制用户选择区域，并在选择变化时通过引用参数返回新用户标识。
        /// 此方法只切换标识，不负责解析或替换 <paramref name="guiContext"/>。
        /// </summary>
        /// <param name="users">候选用户序列；不得为 null 或空，其中的 null 元素会被忽略。</param>
        /// <param name="selectedKey">当前用户标识；必须存在于过滤 null 后的候选序列中，选择变化时被更新。</param>
        /// <param name="guiContext">当前模块上下文，供派生类继续绘制使用。</param>
        /// <exception cref="ArgumentNullException"><paramref name="users"/> 为 null。</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="users"/> 不包含非 null 用户，或 <paramref name="selectedKey"/> 不在候选序列中。
        /// </exception>
        public virtual void Draw(IEnumerable<UserContext> users, ref string selectedKey, IUserContext guiContext)
        {
            if (users == null)
                throw new ArgumentNullException(nameof(users));

            var userArray = users.Where(user => user != null).ToArray();
            if (userArray.Length == 0)
                throw new ArgumentException("The user list does not contain any selectable users.", nameof(users));

            var currentIndex = -1;
            for (var i = 0; i < userArray.Length; i++)
            {
                if (userArray[i].UserId == selectedKey)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0)
                throw new ArgumentException($"The selectedKey '{selectedKey}' does not exist in the selectable user list.", nameof(selectedKey));

            UnityGui.BeginVertical(UnityGui.BoxStyle);
            try
            {
                UnityGui.Space(4);

                // 每帧解析 Translator，运行时切换语言后无需重建用户上下文或选择器缓存。
                if (UnityGui.Button(userArray[currentIndex].Name))
                    IsExpanded = !IsExpanded;

                if (IsExpanded)
                {
                    var newIndex = UnityGui.SelectionGrid(currentIndex, userArray.Select(user => user.Name.ToString()).ToArray(), 1);

                    if (currentIndex != newIndex)
                    {
                        selectedKey = userArray[newIndex].UserId;
                        IsExpanded = false;
                    }
                }

                UnityGui.Space(4);
            }
            finally
            {
                UnityGui.EndVertical();
            }
        }

        /// <summary>
        /// 通知派生编辑器当前用户上下文中的派生布局或显示状态需要重新计算。
        /// </summary>
        /// <param name="context">需要标记的模块上下文。</param>
        public abstract void SetStatusDirty(IUserContext context);

        /// <summary>
        /// 释放派生编辑器拥有的订阅或会话资源；基类本身不持有可释放资源。
        /// </summary>
        public virtual void Dispose()
        {
        }
    }
}
