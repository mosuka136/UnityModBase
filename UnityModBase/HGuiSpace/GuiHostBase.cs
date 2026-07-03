using System;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HGuiSpace
{
    public abstract class GuiHostBase<T> : MonoBehaviour where T : class
    {
        public readonly int WindowID = Guid.NewGuid().GetHashCode();

        public UserBindingBase<T> User { get; protected set; }
        public UserEditorBase UserEditor { get; protected set; }
        public ToastEditor ToastEditor { get; protected set; }
        public TooltipEditor TooltipEditor { get; protected set; }

        public IUnityProvider UnityService { get; protected set; }
        public IUnityGuiProvider UnityGui { get; protected set; }
        public IStyleResource StyleProvider { get; protected set; }

        public bool IsVisible { get; protected set; } = false;
        // 首次打开后如果用户拖动过窗口，则不再用“点击窗口外”自动隐藏，避免拖动释放时误判为失焦。
        public bool HasDraggedWindowSinceOpen { get; protected set; } = false;

        public Translator Title { get; protected set; }
        public Rect WindowRect { get; protected set; }
        public Hotkey UIHotkey { get; protected set; }

        public virtual void Awake()
        {
            try
            {
                UnityGui = UnityGuiProvider.Instance;
                UnityService = UnityProvider.Instance;

                ToastEditor = new ToastEditor(UnityService, UnityGui, StyleProvider);
                TooltipEditor = new TooltipEditor(UnityService, UnityGui, StyleProvider);

                BLog.Debug($"[{WindowID}] GUI host created.");
            }
            catch (Exception ex)
            {
                BLog.Error($"[{WindowID}] Failed to create GUI host.", ex);
                Destroy(this);
            }
        }

        public virtual void Update()
        {
            if (UIHotkey?.WasPressedThisFrame() == true)
            {
                BLog.Debug($"[{WindowID}] Config GUI toggle hotkey pressed.");
                ToggleVisibility();
            }
        }

        public virtual void OnGUI()
        {
            if (!IsVisible)
                return;

            var rect = GUI.Window(WindowID, WindowRect, DrawWindow, Title);

            if (!HasDraggedWindowSinceOpen && WindowRect.position != rect.position)
                HasDraggedWindowSinceOpen = true;
            WindowRect = rect;

            TryAutoHideOnFocusLost();
        }

        public virtual void DrawWindow(int id)
        {
            UnityGui.BeginArea(new Rect(10f, 30f, WindowRect.width - 20f, WindowRect.height - 40f));
            UserEditor.Draw(User);
            UnityGui.EndArea();

            ToastEditor.DrawToast(WindowRect);
            TooltipEditor.DrawTooltip(WindowRect);
            GUI.DragWindow();
        }

        public virtual void TryAutoHideOnFocusLost()
        {
            if (HasDraggedWindowSinceOpen)
                return;

            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.MouseDown)
                return;

            if (WindowRect.Contains(currentEvent.mousePosition))
                return;

            BLog.Debug($"[{WindowID}] GUI auto-hidden because focus was lost.");
            Hide();
            GUI.FocusControl(null);
        }

        /// <summary>
        /// 隐藏配置窗口
        /// </summary>
        public virtual void Hide()
        {
            if (!IsVisible)
                return;

            IsVisible = false;
            HasDraggedWindowSinceOpen = false;

            BLog.Debug($"[{WindowID}] GUI hidden.");
        }

        /// <summary>
        /// 切换配置窗口可见性。
        /// </summary>
        public virtual void ToggleVisibility()
        {
            if (IsVisible)
                Hide();
            else
            {
                IsVisible = true;
                BLog.Debug($"[{WindowID}] GUI shown.");
            }
        }
    }
}
