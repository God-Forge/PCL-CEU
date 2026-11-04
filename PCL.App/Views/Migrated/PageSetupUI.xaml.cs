using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.App.Controls;
using PCL.App.Services;

using System.IO;
using Brushes = System.Windows.Media.Brushes;
using RadioButton = System.Windows.Controls.RadioButton;
using ComboBox = System.Windows.Controls.ComboBox;
using Color = System.Windows.Media.Color;
using Clipboard = System.Windows.Clipboard;
namespace PCL.App.Views.Migrated;

public partial class PageSetupUI : PCL.App.Controls.MyPageRight {

    private static readonly (byte R, byte G, byte B)[] AccentColors = [
        (0, 120, 212),
        (54, 190, 170),
        (80, 180, 90),
        (220, 180, 40),
        (150, 105, 70),
        (70, 70, 80),
        (210, 90, 150),
        (125, 90, 210),
        (190, 150, 40),
        (230, 120, 40),
        (220, 60, 60),
        (40, 140, 220),
        (230, 180, 60),
        (230, 120, 200),
        (80, 80, 90)
    ];

    private bool _initialized;

    public static bool HiddenForceShow { get; set; }

    public PageSetupUI() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            if (_initialized) return;
            _initialized = true;
            PageSetupOtherSettings.EnsureRegistered();
            PageSetupOtherUi.PrepareBinding(this);
            SettingService.BindAll(this);
            WireInteractions();
            try {
                RefreshUi();
            } catch {
            }
        };
    }

    private void WireInteractions() {
        foreach (var radio in PageSetupOtherUi.Descendants(PanLauncherTheme).OfType<RadioButton>()) {
            radio.Checked += (_, _) => ApplyTheme();
        }
        RadioLogoType0.Click += (_, _) => ApplyLogoType();
        RadioLogoType1.Click += (_, _) => ApplyLogoType();
        RadioLogoType2.Click += (_, _) => ApplyLogoType();
        RadioLogoType3.Click += (_, _) => ApplyLogoType();
        RadioCustomType0.Click += (_, _) => ApplyCustomType();
        RadioCustomType1.Click += (_, _) => ApplyCustomType();
        RadioCustomType2.Click += (_, _) => ApplyCustomType();
        RadioCustomType3.Click += (_, _) => ApplyCustomType();
        CheckMusicStart.Checked += (_, _) => {
            if (CheckMusicStart.IsChecked == true) CheckMusicStop.IsChecked = false;
        };
        CheckMusicStop.Checked += (_, _) => {
            if (CheckMusicStop.IsChecked == true) CheckMusicStart.IsChecked = false;
        };
        BtnBackgroundOpen.Click += (_, _) => PageSetupOtherUi.OpenFolder(EnsureFolder("Pictures"));
        BtnBackgroundRefresh.Click += (_, _) => RefreshBackground(true);
        BtnBackgroundClear.Click += (_, _) => ClearFolder("Pictures", "背景图片");
        BtnMusicOpen.Click += (_, _) => PageSetupOtherUi.OpenFolder(EnsureFolder("Musics"));
        BtnMusicRefresh.Click += (_, _) => RefreshMusic(true);
        BtnMusicClear.Click += (_, _) => ClearFolder("Musics", "背景音乐");
        BtnLogoChange.Click += (_, _) => ChangeLogo();
        BtnLogoDelete.Click += (_, _) => DeleteLogo();
        BtnCustomRefresh.Click += (_, _) => NotificationService.Info("自定义主页会在下次打开启动页时重新加载。");
        BtnCustomFile.Click += (_, _) => CreateCustomHomeTutorial();
        BtnCustomTutorial.Click += (_, _) => System.Windows.MessageBox.Show(
            "1. 点击“生成教学文件”，在 PCL 文件夹下生成 Custom.xaml。\r\n" +
            "2. 使用记事本等工具修改并保存该文件。\r\n" +
            "3. 刷新主页以查看效果。",
            "主页自定义教程", MessageBoxButton.OK, MessageBoxImage.Information);
        BtnCustomFolder.Click += (_, _) => PageSetupOtherUi.OpenFolder(AppServices.Paths.Pcl);
    }

    private void CreateCustomHomeTutorial() {
        var path = Path.Combine(AppServices.Paths.Pcl, "Custom.xaml");
        try {
            if (!File.Exists(path)) {
                File.WriteAllText(path, """
<StackPanel xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
    <TextBlock Text="自定义主页" FontSize="28" FontWeight="Bold" Margin="20" />
    <TextBlock Text="编辑此文件后，在启动页刷新即可看到效果。" Margin="20,0,20,20" />
</StackPanel>
""", new UTF8Encoding(false));
            }
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        } catch (Exception ex) {
            NotificationService.Error("生成自定义主页文件失败：" + ex.Message);
        }
    }
    private void RefreshUi() {
        ApplyTheme();
        ApplyLogoType();
        ApplyCustomType();
        RefreshBackground(false);
        RefreshMusic(false);
        CardSwitch.Visibility = HiddenForceShow || !PageSetupOtherSettings.GetBool("UiHiddenFunctionHidden")
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyTheme() {
        var index = Math.Clamp(PageSetupOtherSettings.GetInt("UiLauncherTheme"), 0, AccentColors.Length - 1);
        var color = AccentColors[index];
        if (System.Windows.Application.Current is not { } application) return;
        var accent = Color.FromRgb(color.R, color.G, color.B);
        application.Resources["PclAccentBrush"] = new SolidColorBrush(accent);
        application.Resources["PclAccentDarkBrush"] = new SolidColorBrush(Color.FromRgb(
            (byte)(accent.R * 0.8), (byte)(accent.G * 0.8), (byte)(accent.B * 0.8)));
    }

    private void ApplyLogoType() {
        var type = PageSetupOtherSettings.GetInt("UiLogoType", 1);
        CheckLogoLeft.Visibility = type == 0 ? Visibility.Visible : Visibility.Collapsed;
        PanLogoText.Visibility = type == 2 ? Visibility.Visible : Visibility.Collapsed;
        PanLogoChange.Visibility = type == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyCustomType() {
        var type = PageSetupOtherSettings.GetInt("UiCustomType");
        PanCustomPreset.Visibility = type == 3 ? Visibility.Visible : Visibility.Collapsed;
        PanCustomLocal.Visibility = type == 1 ? Visibility.Visible : Visibility.Collapsed;
        PanCustomNet.Visibility = type == 2 ? Visibility.Visible : Visibility.Collapsed;
        HintCustom.Visibility = type is 1 or 2 ? Visibility.Visible : Visibility.Collapsed;
        HintCustomWarn.Visibility = type is 1 or 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshBackground(bool notify) {
        var path = EnsureFolder("Pictures");
        var count = CountFiles(path, new[] { "ini", "db" });
        var show = count > 0;
        PanBackgroundOpacity.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        PanBackgroundBlur.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        PanBackgroundSuit.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        BtnBackgroundClear.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        CardBackground.Title = show ? $"背景图片（{count} 张）" : "背景图片";
        if (notify) NotificationService.Info(show ? $"已检测到 {count} 张背景图片。" : "未检测到可用背景图片。");
    }

    private void RefreshMusic(bool notify) {
        var path = EnsureFolder("Musics");
        var count = CountFiles(path, new[] { "ini", "jpg", "txt", "cfg", "lrc", "db", "png" });
        var show = count > 0;
        PanMusicVolume.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        PanMusicDetail.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        BtnMusicClear.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        CardMusic.Title = show ? $"背景音乐（{count} 首）" : "背景音乐";
        if (notify) NotificationService.Info(show ? $"已检测到 {count} 首背景音乐。" : "未检测到可用背景音乐。");
    }

    private void ChangeLogo() {
        try {
            var file = MeloongCore.Wpf.Dialogs.SelectFile("选择图片", false, AppServices.Paths.Pcl).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(file)) return;
            var target = System.IO.Path.Combine(AppServices.Paths.Pcl, "Logo.png");
            System.IO.File.Copy(file, target, true);
            RadioLogoType3.IsChecked = true;
            ApplyLogoType();
            NotificationService.Info("标题栏图片已更新。");
        } catch (Exception ex) {
            NotificationService.Warning("设置标题栏图片失败：" + ex.Message);
        }
    }

    private void DeleteLogo() {
        try {
            var target = System.IO.Path.Combine(AppServices.Paths.Pcl, "Logo.png");
            if (System.IO.File.Exists(target)) System.IO.File.Delete(target);
            RadioLogoType1.IsChecked = true;
            ApplyLogoType();
            NotificationService.Info("标题栏图片已清空。");
        } catch (Exception ex) {
            NotificationService.Warning("清空标题栏图片失败：" + ex.Message);
        }
    }

    private void ClearFolder(string folder, string name) {
        var path = EnsureFolder(folder);
        var result = System.Windows.MessageBox.Show(
            $"即将删除{name}文件夹中的所有文件。\r\n此操作不可撤销，是否继续？",
            "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        try {
            foreach (var file in System.IO.Directory.GetFiles(path)) System.IO.File.Delete(file);
            foreach (var directory in System.IO.Directory.GetDirectories(path)) System.IO.Directory.Delete(directory, true);
            if (folder == "Pictures") RefreshBackground(false);
            else RefreshMusic(false);
            NotificationService.Info($"{name}已清空。");
        } catch (Exception ex) {
            NotificationService.Warning($"清空{name}失败：" + ex.Message);
        }
    }

    private static string EnsureFolder(string name) {
        var path = System.IO.Path.Combine(AppServices.Paths.Pcl, name);
        try {
            System.IO.Directory.CreateDirectory(path);
        } catch {
        }
        return path;
    }

    private static int CountFiles(string path, IReadOnlyCollection<string> ignoredExtensions) {
        try {
            return System.IO.Directory.EnumerateFiles(path, "*", System.IO.SearchOption.AllDirectories)
                .Count(file => !ignoredExtensions.Contains(System.IO.Path.GetExtension(file).TrimStart('.').ToLowerInvariant()));
        } catch {
            return 0;
        }
    }

}
