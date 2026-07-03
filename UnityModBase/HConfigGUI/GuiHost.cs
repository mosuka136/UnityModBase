using System;
using UnityEngine;
using UnityModBase.BSpace;
using UnityModBase.HClassAttribute;
using UnityModBase.HConfigGUI.Bindings;
using UnityModBase.HConfigGUI.Editor;
using UnityModBase.HConfigGUI.Resource;
using UnityModBase.HGuiSpace;
using UnityModBase.HotkeyManager;
using UnityModBase.HProvider;
using UnityModBase.HTranslatorSpace;

namespace UnityModBase.HConfigGUI
{
    /// <summary>
    /// 配置界面的 Unity 宿主组件。
    /// 该组件在游戏启动后由 <see cref="GameBootRegistery"/> 创建，负责接收热键并在 OnGUI 中绘制窗口。
    /// </summary>
    [RegisterOnGameBoot]
    public class GuiHost : MonoBehaviour
    {
        public readonly int WindowID = Guid.NewGuid().GetHashCode();

        public UserBinding User { get; private set; }
        public UserEditor UserEditor { get; private set; }
        public ToastEditor ToastEditor { get; private set; }
        public TooltipEditor TooltipEditor { get; private set; }
        public PopupEditor PopupEditor { get; private set; }

        public IUnityProvider UnityService { get; private set; }
        public IUnityGuiProvider UnityGui { get; private set; }
        public GuiStateStore GuiStateStore { get; private set; }
        public StyleResource StyleProvider { get; private set; }
        public LayoutResource LayoutProvider { get; private set; }

        public bool IsVisible { get; private set; } = false;
        // 首次打开后如果用户拖动过窗口，则不再用“点击窗口外”自动隐藏，避免拖动释放时误判为失焦。
        public bool HasDraggedWindowSinceOpen { get; private set; } = false;
        public Rect WindowRect { get; set; }
        public Hotkey ConfigUIHotkey { get; private set; }

        public void Awake()
        {
            try
            {
                ConfigUIHotkey = BConfigManager.ConfigUIHotkey.Value;
                BConfigManager.ConfigUIHotkey.OnValueChanged += (s, e) => ConfigUIHotkey = e;

                Translator.DefaultLanguage = BConfigManager.SetLanguage.Value;
                Translator.OnDefaultLanguageChanged += (s, e) => UserEditor.UpdateLayout();

                UnityGui = UnityGuiProvider.Instance;
                UnityService = UnityProvider.Instance;
                GuiStateStore = new GuiStateStore();
                StyleProvider = new StyleResource(UnityGui);
                LayoutProvider = new LayoutResource(UnityGui);

                User = new UserBinding();
                UserEditor = new UserEditor(UnityService, UnityGui, GuiStateStore, StyleProvider);
                ToastEditor = new ToastEditor(UnityService, UnityGui, StyleProvider);
                TooltipEditor = new TooltipEditor(UnityService, UnityGui, StyleProvider);
                PopupEditor = new PopupEditor(UnityGui, StyleProvider, GuiStateStore);
                GuiStateStore.SetBool(GuiStateStore.IsPopupOpenKey, false);

                GuiPipe.OnEntryValueChanged += e => ToastEditor.SetToast(TranslatorResource.Changed + e.Name);
                GuiPipe.OnEntryValueReset += e => ToastEditor.SetToast(TranslatorResource.ResetDone + e.Name);

                float width = UnityGui.ScreenWidth * 0.35f;
                float height = UnityGui.ScreenHeight * 0.7f;
                WindowRect = new Rect((UnityGui.ScreenWidth - width) / 2f, (UnityGui.ScreenHeight - height) / 2f, width, height);

                BLog.Debug($"Config GUI host created. WindowId={WindowID}");
            }
            catch (Exception ex)
            {
                BLog.Error("Failed to create Config GUI host.", ex);
                Destroy(this);
            }
        }

        public void Update()
        {
            if (ConfigUIHotkey?.WasPressedThisFrame() == true)
            {
                BLog.Debug("Config GUI toggle hotkey pressed.");
                ToggleVisibility();
            }

            UserEditor.Update(UnityService.UnscaledDeltaTime);
        }

        public void OnGUI()
        {
            if (!IsVisible)
                return;

            if (GuiStateStore.GetBool(GuiStateStore.IsPopupOpenKey))
            {
                PopupEditor.DrawPopup(GuiPipe.PopupTitle, GuiPipe.PopupWindowAction, GuiPipe.ClosePopupWindowAction);
                return;
            }

            var rect = GUI.Window(WindowID, WindowRect, DrawWindow, TranslatorResource.Title);

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

            BLog.Debug("Config GUI auto-hidden because focus was lost.");
            Hide();
            GUI.FocusControl(null);
        }

        /// <summary>
        /// 隐藏配置窗口
        /// </summary>
        public void Hide()
        {
            if (!IsVisible)
                return;

            IsVisible = false;
            HasDraggedWindowSinceOpen = false;

            BLog.Debug("Config GUI hidden.");
        }

        /// <summary>
        /// 切换配置窗口可见性。
        /// </summary>
        public void ToggleVisibility()
        {
            if (IsVisible)
                Hide();
            else
            {
                IsVisible = true;
                BLog.Debug("Config GUI shown.");
            }
        }
    }
}
