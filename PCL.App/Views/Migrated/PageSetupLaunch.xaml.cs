using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using PCL.App.Controls;
using PCL.App.Services;

using System.IO;
using Brushes = System.Windows.Media.Brushes;
using RadioButton = System.Windows.Controls.RadioButton;
using ComboBox = System.Windows.Controls.ComboBox;
using Color = System.Windows.Media.Color;
using Clipboard = System.Windows.Clipboard;
namespace PCL.App.Views.Migrated;

public partial class PageSetupLaunch : PCL.App.Controls.MyPageRight {

    private bool _initialized;
    private bool _suppressVisibleWarning;

    public PageSetupLaunch() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            if (_initialized) return;
            _initialized = true;
            PageSetupOtherSettings.EnsureRegistered();
            PageSetupOtherUi.PrepareBinding(this);
            SettingService.BindAll(this);
            WireInteractions();
            UpdateWindowSizeVisibility();
            UpdateSkinType();
            UpdateRamType();
            UpdateAdvanceResetVisibility();
            UpdateAdvanceRunWaitVisibility();
            RefreshRamDisplay();
            try {
                BtnSwitch.Visibility = AppServices.SelectedInstancePath is null ? Visibility.Collapsed : Visibility.Visible;
            } catch {
                BtnSwitch.Visibility = Visibility.Collapsed;
            }
        };
    }

    private void WireInteractions() {
        ComboArgumentWindowType.SelectionChanged += (_, _) => UpdateWindowSizeVisibility();
        ComboArgumentVisibie.SelectionChanged += Visible_Changed;
        RadioRamType0.Click += (_, _) => UpdateRamType();
        RadioRamType1.Click += (_, _) => UpdateRamType();
        SliderRamCustom.ValueChanged += (_, _) => RefreshRamDisplay();
        RadioSkinType3.Click += (_, _) => UpdateSkinType();
        RadioSkinType4.Click += (_, _) => UpdateSkinType();
        RadioSkinType0.Click += (_, _) => UpdateSkinType();
        RadioSkinType1.Click += (_, _) => UpdateSkinType();
        RadioSkinType2.Click += (_, _) => UpdateSkinType();
        TextAdvanceRun.TextChanged += (_, _) => UpdateAdvanceRunWaitVisibility();
        TextAdvanceJvm.TextChanged += (_, _) => UpdateAdvanceResetVisibility();
        BtnAdvanceJvmReset.Click += (_, _) => {
            AppServices.Settings.Reset("LaunchAdvanceJvm");
            TextAdvanceJvm.Text = PageSetupOtherSettings.DefaultJvm;
            UpdateAdvanceResetVisibility();
        };
        BtnSwitch.Click += (_, _) => NotificationService.Info("请在版本设置中调整该实例的启动选项。");
    }

    private void Visible_Changed(object sender, SelectionChangedEventArgs e) {
        if (_suppressVisibleWarning || ComboArgumentVisibie.SelectedIndex != 0) return;
        var result = System.Windows.MessageBox.Show(
            "若在游戏启动后立即关闭启动器，崩溃检测、更改游戏标题等功能将失效。\r\n" +
            "如果希望保留这些功能，可以选择让启动器在游戏启动后隐藏，游戏退出后自动关闭。",
            "提醒", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes) return;
        _suppressVisibleWarning = true;
        ComboArgumentVisibie.SelectedIndex = 1;
        _suppressVisibleWarning = false;
    }

    private void UpdateWindowSizeVisibility() {
        var visibility = ComboArgumentWindowType.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        TextArgumentWindowWidth.Visibility = visibility;
        TextArgumentWindowHeight.Visibility = visibility;
        LabArgumentWindowMiddle.Visibility = visibility;
    }

    private void UpdateSkinType() {
        var type = PageSetupOtherSettings.GetInt("LaunchSkinType");
        PanSkinID.Visibility = type == 3 ? Visibility.Visible : Visibility.Collapsed;
        PanSkinChange.Visibility = type == 4 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateRamType() {
        var custom = PageSetupOtherSettings.GetInt("LaunchRamType") == 1;
        SliderRamCustom.IsEnabled = custom;
        RefreshRamDisplay();
    }

    private void UpdateAdvanceResetVisibility() {
        BtnAdvanceJvmReset.Visibility = string.Equals(TextAdvanceJvm.Text, PageSetupOtherSettings.DefaultJvm, StringComparison.Ordinal)
            ? Visibility.Hidden
            : Visibility.Visible;
    }

    private void UpdateAdvanceRunWaitVisibility() {
        CheckAdvanceRunWait.Visibility = string.IsNullOrWhiteSpace(TextAdvanceRun.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void RefreshRamDisplay() {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        var availableMemory = GlobalMemoryStatusEx(ref status);
        var total = availableMemory ? status.TotalPhysical / 1024d / 1024d / 1024d : 0d;
        var available = availableMemory ? status.AvailablePhysical / 1024d / 1024d / 1024d : 0d;
        var game = PageSetupOtherSettings.GetInt("LaunchRamType") == 1
            ? GetCustomRam(PageSetupOtherSettings.GetInt("LaunchRamCustom", 15))
            : (available > 0 ? Math.Clamp(available * 0.5, 0.5, 8) : 2.5);
        var used = Math.Max(0, total - available);
        var actualGame = available > 0 ? Math.Min(game, available) : game;
        var empty = Math.Max(0, total - used - actualGame);
        LabRamGame.Text = $"{game:0.0} GB";
        LabRamUsed.Text = $"{used:0.0} GB";
        LabRamTotal.Text = total > 0 ? $" / {total:0.0} GB" : "";
        ColumnRamUsed.Width = new GridLength(Math.Max(used, 0.1), GridUnitType.Star);
        ColumnRamGame.Width = new GridLength(Math.Max(actualGame, 0.1), GridUnitType.Star);
        ColumnRamEmpty.Width = new GridLength(Math.Max(empty, 0.1), GridUnitType.Star);
    }

    private static double GetCustomRam(int value) {
        if (value <= 12) return value * 0.1 + 0.3;
        if (value <= 25) return (value - 12) * 0.5 + 1.5;
        if (value <= 33) return (value - 25) + 8;
        return (value - 33) * 2 + 16;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

}
