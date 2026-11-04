using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PCL.App.Controls;
using PCL.App.Services;

using Brushes = System.Windows.Media.Brushes;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using ProgressBar = System.Windows.Controls.ProgressBar;
using ListBox = System.Windows.Controls.ListBox;
using CheckBox = System.Windows.Controls.CheckBox;
using RadioButton = System.Windows.Controls.RadioButton;
using ComboBox = System.Windows.Controls.ComboBox;
using Color = System.Windows.Media.Color;
using Cursors = System.Windows.Input.Cursors;
using Size = System.Windows.Size;
using Clipboard = System.Windows.Clipboard;
namespace PCL.App.Views.Migrated;

public enum LinkMode {
    Create,
    Join
}

public partial class PageLinkMain : PCL.App.Controls.MyPageRight {

    public bool IsLinkModuleAvailable => false;

    public LinkMode? SelectedMode { get; private set; }

    public event Action<LinkMode>? ModeSelected;

    public PageLinkMain() {
        InitializeComponent();
        PanSelectCreate.Background = Brushes.Transparent;
        PanSelectJoin.Background = Brushes.Transparent;
        PanSelectCreate.Cursor = Cursors.Hand;
        PanSelectJoin.Cursor = Cursors.Hand;

        PanSelectCreate.MouseLeftButtonUp += (_, e) => {
            SelectMode(LinkMode.Create);
            ShowUnavailable("创建房间");
            e.Handled = true;
        };
        PanSelectJoin.MouseLeftButtonUp += (_, e) => {
            SelectMode(LinkMode.Join);
            ShowUnavailable("加入房间");
            e.Handled = true;
        };

        BtnLoadCancel.Click += (_, _) => ShowWaiting();
        BtnFinishExit.Click += (_, _) => ShowWaiting();
        BtnFinishCopy.Click += (_, _) => NotificationService.Info("当前没有可复制的邀请码。");
        BtnFinishIp.MouseLeftButtonUp += (_, _) => NotificationService.Info("当前没有可复制的服务器地址。");
        BtnFinishPort.MouseLeftButtonUp += (_, _) => NotificationService.Info("当前没有可复制的端口。");
        BtnFinishPing.MouseLeftButtonUp += (_, _) => NotificationService.Info("当前没有可用的延迟信息。");
        BtnFinishPlayer.MouseLeftButtonUp += (_, _) => NotificationService.Info("当前没有可用的房间人数信息。");

        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            ShowWaiting();
        };
    }

    public void SelectMode(LinkMode mode) {
        SelectedMode = mode;
        PanSelectCreate.Opacity = mode == LinkMode.Create ? 1 : 0.6;
        PanSelectJoin.Opacity = mode == LinkMode.Join ? 1 : 0.6;
        ModeSelected?.Invoke(mode);
    }

    private void ShowWaiting() {
        PanLoad.Visibility = Visibility.Collapsed;
        PanContent.Visibility = Visibility.Visible;
        PanSelect.Visibility = Visibility.Visible;
        PanFinish.Visibility = Visibility.Collapsed;
        PanAlways.Visibility = Visibility.Visible;
        if (SelectedMode is null) SelectMode(LinkMode.Create);
    }

    private static void ShowUnavailable(string action) {
        System.Windows.MessageBox.Show(
            $"联机模块尚未迁移到当前 C#/.NET 8 版本，无法{action}。\r\n\r\n" +
            "EasyTier 核心、联机模块 CLI 及相关外部组件需要单独提供；" +
            "在完成迁移前不会伪造联机成功，也不会创建或加入任何房间。",
            "联机功能不可用", MessageBoxButton.OK, MessageBoxImage.Information);
    }

}
