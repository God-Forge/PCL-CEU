using MeloongCore;
using System.IO;
using System.Windows;
using PCL.App.Controls;
using PCL.App.Services;
using PCL.Core.Minecraft;
using PCL.Core.Modpack;

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
        var rules = CollectRules();
        if (rules.IsEmpty) {
            NotificationService.Warning("没有勾选任何要导出的内容。", "导出整合包");
            return;
        }
        var savePath = MeloongCore.Wpf.Dialogs.SaveFile(
            "选择整合包保存位置", TextExportName.Text.Trim() + ".zip", AppServices.Paths.Pcl,
            [("zip", "ZIP 压缩包")]);
        if (string.IsNullOrWhiteSpace(savePath)) return;
        try {
            var instancePath = Path.Combine(_current.Folder.Versions, _current.Version.Folder);
            Logger.Info($"[导出] 规则：{rules.Describe()}");
            var result = AdvancedMaintenanceService.ExportOfflinePack(
                instancePath, _current.Folder.Path, savePath, new ModpackExportOptions(), rules);
            NotificationService.Info("整合包已导出：" + result, "导出整合包");
        } catch (Exception ex) {
            Logger.Error(ex, "导出整合包失败");
            NotificationService.Error("导出失败：" + ex.Message, "导出整合包");
        }
    }

    /// <summary>
    /// 汇总界面里所有已勾选项的 Rules。这些规则来自 XAML 里的 ExportOption，
    /// 由 <see cref="ExportRuleSet"/> 解析为包含/排除模式。
    /// </summary>
    private ExportRuleSet CollectRules() {
        var sets = new List<ExportRuleSet>();
        foreach (var check in PageSetupOtherUi.Descendants(PanOptions).OfType<MyCheckBox>()) {
            if (check.IsChecked != true) continue;
            if (check.Tag is not ExportOption option) continue;
            if (string.IsNullOrWhiteSpace(option.Rules)) continue;
            sets.Add(ExportRuleSet.Parse(option.Rules));
        }
        return ExportRuleSet.Merge(sets);
    }

}
