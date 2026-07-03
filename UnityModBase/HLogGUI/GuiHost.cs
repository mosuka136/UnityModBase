using System;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;
using UnityModBase.HGuiSpace;
using UnityModBase.HLogGUI.Resource;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HLogGUI
{
    [RegisterOnGameBoot]
    public class GuiHost : MonoBehaviour
    {
        public readonly int WindowID = Guid.NewGuid().GetHashCode();

        public ToastEditor ToastEditor { get; private set; }
        public TooltipEditor TooltipEditor { get; private set; }
        public IUnityProvider UnityService { get; private set; }
        public IUnityGuiProvider UnityGui { get; private set; }
        public StyleResource StyleProvider { get; private set; }
        public UserEditor UserEditor { get; private set; }
        public UserBinding User { get; private set; }

        public bool IsVisible { get; private set; } = false;
        // 首次打开后如果用户拖动过窗口，则不再用“点击窗口外”自动隐藏，避免拖动释放时误判为失焦。
        public bool HasDraggedWindowSinceOpen { get; private set; } = false;
        public Rect WindowRect { get; set; }
        public Hotkey LogUIHotkey { get; private set; }

        public void Awake()
        {
            try
            {
                LogUIHotkey = BConfigManager.LogUIHotkey.Value;
                BConfigManager.LogUIHotkey.OnValueChanged += (s, e) => LogUIHotkey = e;
                Translator.OnDefaultLanguageChanged += (s, e) => UserEditor.ListEditor.IsColumnWidthDirty = true;

                UnityGui = UnityGuiProvider.Instance;
                UnityService = UnityProvider.Instance;
                StyleProvider = new StyleResource(UnityGui);
                ToastEditor = new ToastEditor(UnityService, UnityGui, StyleProvider);
                TooltipEditor = new TooltipEditor(UnityService, UnityGui, StyleProvider);
                User = new UserBinding();
                UserEditor = new UserEditor(UnityService, UnityGui, StyleProvider, ToastEditor);

                float width = UnityGui.ScreenWidth * 0.8f;
                float height = UnityGui.ScreenHeight * 0.5f;
                WindowRect = new Rect((UnityGui.ScreenWidth - width) / 2f, (UnityGui.ScreenHeight - height) / 2f, width, height);

                BLog.Debug($"Log GUI host created. WindowId={WindowID}");
            }
            catch (Exception ex)
            {
                BLog.Error("Failed to create Log GUI host.", ex);
                Destroy(this);
            }
        }

        public void OnDestroy()
        {
        }

        public void Update()
        {
            if (LogUIHotkey?.WasPressedThisFrame() == true)
            {
                BLog.Debug("Log GUI toggle hotkey pressed.");
                ToggleVisibility();
            }
        }

        public void OnGUI()
        {
            if (!IsVisible)
                return;

            var rect = WindowRect;
            rect.width = UnityService.Clamp(UserEditor.ListEditor.TotalColumnWidth, UnityGui.ScreenWidth * 0.5f, UnityGui.ScreenWidth * 0.9f);

            rect = GUI.Window(WindowID, rect, DrawWindow, TranslatorResource.Title);

            if (!HasDraggedWindowSinceOpen && WindowRect.position != rect.position)
                HasDraggedWindowSinceOpen = true;
            WindowRect = rect;

            TryAutoHideOnFocusLost();
        }

        public void DrawWindow(int id)
        {
            UnityGui.BeginArea(new Rect(10f, 30f, WindowRect.width - 20f, WindowRect.height - 40f));
            UserEditor.Draw(User);
            UnityGui.EndArea();

            ToastEditor.DrawToast(WindowRect);
            TooltipEditor.DrawTooltip(WindowRect);
            GUI.DragWindow();
        }

        public void TryAutoHideOnFocusLost()
        {
            if (HasDraggedWindowSinceOpen)
                return;

            var currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.MouseDown)
                return;

            if (WindowRect.Contains(currentEvent.mousePosition))
                return;

            BLog.Debug("Log GUI auto-hidden because focus was lost.");
            Hide();
            GUI.FocusControl(null);
        }

        /// <summary>
        /// 隐藏日志窗口
        /// </summary>
        public void Hide()
        {
            if (!IsVisible)
                return;

            IsVisible = false;
            HasDraggedWindowSinceOpen = false;

            BLog.Debug("Log GUI hidden.");
        }

        /// <summary>
        /// 切换日志窗口可见性。
        /// </summary>
        public void ToggleVisibility()
        {
            if (IsVisible)
                Hide();
            else
            {
                IsVisible = true;
                BLog.Debug("Log GUI shown.");
            }
        }
    }
}
