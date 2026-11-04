using System.IO;
using System.Windows;
using System.Windows.Controls;
using PCL.App.Controls;
using PCL.App.Services;
using PCL.Core.Net;
using PCL.Core.Update;

namespace PCL.App.Views.Migrated;

public partial class PageSetupSystem : PCL.App.Controls.MyPageRight {

    private bool _initialized;

    public PageSetupSystem() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            if (_initialized) return;
            _initialized = true;
            PageSetupOtherSettings.EnsureRegistered();
            PageSetupOtherUi.PrepareBinding(this);
            SettingService.BindAll(this);
            WireInteractions();
        };
    }

    private void WireInteractions() {
        ComboDownloadSource.SelectionChanged += (_, _) =>
            NotificationService.Info("下载源设置已保存，将在下一次下载时生效。");
        ComboSystemActivity.SelectionChanged += Activity_Changed;
        ComboSystemUpdate.SelectionChanged += UpdateMode_Changed;
        SliderDownloadThread.ValueChanged += Thread_Changed;
        CheckDebugMode.Checked += (_, _) => NotificationService.Info("调试信息将在刷新或重新启动启动器后显示。");
        BtnSystemUpdate.Click += (_, _) => _ = CheckUpdateAsync();
        BtnSystemIdentify.Click += (_, _) => PageSetupOtherUi.CopyText(PageSetupOtherSettings.Identify);
        BtnSystemUnlock.Click += (_, _) => NotificationService.Info("土豆码功能尚未迁移到 C# 版本。");
        BtnSystemSettingExp.Click += (_, _) => ExportSettings();
        BtnSystemSettingImp.Click += (_, _) => ImportSettings();
    }

    private static string SettingsPath => Path.Combine(AppContext.BaseDirectory, "PCL", "Setup.ini");

    private static void ExportSettings() {
        var path = MeloongCore.Wpf.Dialogs.SaveFile("导出设置", "PCL-Setup.ini",
            AppServices.Paths.Pcl, [("ini", "INI 设置文件")]);
        if (string.IsNullOrWhiteSpace(path)) return;
        try {
            if (!File.Exists(SettingsPath)) {
                NotificationService.Warning("设置文件尚不存在。");
                return;
            }
            File.Copy(SettingsPath, path, true);
            NotificationService.Info("设置已导出：" + path);
        } catch (Exception ex) {
            NotificationService.Error("导出设置失败：" + ex.Message);
        }
    }

    private static void ImportSettings() {
        var files = MeloongCore.Wpf.Dialogs.SelectFile("导入设置", false, AppServices.Paths.Pcl,
            "INI 设置文件|*.ini");
        if (files.Count == 0) return;
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.Copy(files[0], SettingsPath, true);
            NotificationService.Info("设置已导入，请重新启动启动器以完全生效。");
        } catch (Exception ex) {
            NotificationService.Error("导入设置失败：" + ex.Message);
        }
    }
    private void Activity_Changed(object sender, SelectionChangedEventArgs e) {
        if (ComboSystemActivity.SelectedIndex != 2) return;
        var result = System.Windows.MessageBox.Show(
            "选择该选项后，即使将来出现严重问题，你也无法获取相关通知。\r\n" +
            "一般选择“仅在有重要通知时显示公告”即可。",
            "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes) return;
        if (e.RemovedItems.Count > 0) ComboSystemActivity.SelectedItem = e.RemovedItems[0];
    }

    private void UpdateMode_Changed(object sender, SelectionChangedEventArgs e) {
        if (ComboSystemUpdate.SelectedIndex != 3) return;
        var result = System.Windows.MessageBox.Show(
            "选择该选项后，即使启动器将来出现严重问题，你也无法获取更新并获得修复。\r\n" +
            "一般选择“仅在有重大漏洞更新时显示提示”即可。",
            "警告", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes) return;
        if (e.RemovedItems.Count > 0) ComboSystemUpdate.SelectedItem = e.RemovedItems[0];
    }

    private void Thread_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) {
        if (e.NewValue < 100 || PageSetupOtherSettings.GetBool("HintDownloadThread")) return;
        AppServices.Settings.Set("HintDownloadThread", true);
        NotificationService.Warning("设置过多的下载线程可能会导致下载时出现非常严重的卡顿。\r\n一般设置 64 线程即可满足大多数下载需求。");
    }

    private async Task CheckUpdateAsync() {
        BtnSystemUpdate.IsEnabled = false;
        try {
            using var fetcher = new HttpStringFetcher();
            var info = await UpdateCheckerService.CheckAsync(fetcher, PageSetupOtherSettings.UpdateApiUrl, PageSetupOtherSettings.CurrentVersion);
            if (info.HasUpdate) {
                NotificationService.Info($"发现新版本：{info.LatestVersion}\r\n{info.Url}");
                if (!string.IsNullOrWhiteSpace(info.Url)) PageSetupOtherUi.OpenUrl(info.Url);
            } else {
                NotificationService.Info("当前已是最新版本。");
            }
        } catch (Exception ex) {
            NotificationService.Warning("检查更新失败：" + ex.Message);
        } finally {
            BtnSystemUpdate.IsEnabled = true;
        }
    }

}
