using System.IO;
using System.Windows;
using PCL.App.Controls;
using PCL.App.Services;
using PCL.Core.Minecraft;

namespace PCL.App.Views.Migrated;

public partial class PageInstanceExport : PCL.App.Controls.MyPageRight {

    private MinecraftInstanceLocation? _current;

    public PageInstanceExport() {
        InitializeComponent();
        BtnExport.Click += Export_Click;
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            _current = AppServices.MinecraftFolders.FindInstance(AppServices.SelectedInstanceName ?? "");
            if (_current is null) return;
            TextExportName.Text = _current.Version.Folder;
            TextExportVersion.Text = _current.Version.Folder;
        };
    }

    private void Export_Click(object sender, RoutedEventArgs e) {
        if (_current is null) {
            NotificationService.Warning("请先选择实例。");
            return;
        }
        var savePath = MeloongCore.Wpf.Dialogs.SaveFile(
            "选择整合包保存位置", TextExportName.Text.Trim() + ".zip", AppServices.Paths.Pcl,
            [("zip", "ZIP 压缩包")]);
        if (string.IsNullOrWhiteSpace(savePath)) return;
        try {
            var instancePath = Path.Combine(_current.Folder.Versions, _current.Version.Folder);
            var result = AdvancedMaintenanceService.ExportOfflinePack(instancePath, _current.Folder.Path, savePath);
            NotificationService.Info("整合包已导出：" + result);
        } catch (Exception ex) {
            NotificationService.Error("导出失败：" + ex.Message);
        }
    }

}
