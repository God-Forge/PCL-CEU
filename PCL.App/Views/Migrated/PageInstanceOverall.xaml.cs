using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using PCL.App.Controls;
using MeloongCore;
using PCL.App.Services;
using PCL.Core.Java;
using PCL.Core.Login;
using PCL.Core.Minecraft;
using PCL.Core.Mods;

namespace PCL.App.Views.Migrated;

public partial class PageInstanceOverall : PCL.App.Controls.MyPageRight {

    private MinecraftInstanceLocation? _current;
    private bool _loading;

    public PageInstanceOverall() {
        InitializeComponent();
        ComboDisplayType.SelectionChanged += DisplayType_Changed;
        BtnDisplayRename.Click += Rename_Click;
        BtnDisplayDesc.Click += Description_Click;
        BtnDisplayStar.Click += Star_Click;
        BtnFolderVersion.Click += (_, _) => OpenFolder(InstancePath);
        BtnFolderSaves.Click += (_, _) => OpenFolder(InstanceFolders.Saves);
        BtnFolderMods.Click += (_, _) => OpenFolder(InstanceFolders.Mods);
        BtnFolderScreen.Click += (_, _) => OpenFolder(InstanceFolders.Screenshots);
        BtnManageScript.Click += ExportScript_Click;
        BtnManageCheck.Click += Check_Click;
        BtnManageDelete.Click += Delete_Click;
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            Refresh();
        };
    }

    private string InstancePath => _current is null
        ? ""
        : Path.Combine(_current.Folder.Versions, _current.Version.Folder);

    private InstanceFolderSet InstanceFolders => _current is null
        ? new InstanceFolderSet()
        : InstanceManagementService.GetFolders(InstancePath);

    private void Refresh() {
        _loading = true;
        try {
            var instances = AppServices.MinecraftFolders.ScanAll();
            _current = instances.FirstOrDefault(instance =>
                           string.Equals(instance.Version.Folder, AppServices.SelectedInstanceName, StringComparison.OrdinalIgnoreCase))
                       ?? instances.FirstOrDefault();
            if (_current is not null) {
                AppServices.SelectedInstanceName = _current.Version.Folder;
                AppServices.SelectedMinecraftPath = _current.Folder.Path;
            }

            PanDisplayItem.Children.Clear();
            if (_current is null) {
                PanDisplayItem.Children.Add(new TextBlock {
                    Text = "没有可用的 Minecraft 实例。",
                    Margin = new Thickness(10),
                    Foreground = System.Windows.Media.Brushes.Gray
                });
                return;
            }

            var setup = InstanceManagementService.ReadSetup(InstancePath);
            ComboDisplayType.SelectedIndex = Math.Clamp(setup.DisplayType, 0, ComboDisplayType.Items.Count - 1);
            BtnDisplayStar.Text = setup.IsStar ? "从收藏夹中移除" : "加入收藏夹";
            PanDisplayItem.Children.Add(BuildSummary(setup));
        } catch (Exception ex) {
            PanDisplayItem.Children.Clear();
            PanDisplayItem.Children.Add(new TextBlock {
                Text = "读取实例信息失败：" + ex.Message,
                Margin = new Thickness(10),
                Foreground = System.Windows.Media.Brushes.Firebrick,
                TextWrapping = TextWrapping.Wrap
            });
        } finally {
            _loading = false;
        }
    }

    private UIElement BuildSummary(InstanceSetup setup) {
        if (_current is null) return new StackPanel();
        var panel = new StackPanel { Margin = new Thickness(10, 7, 10, 7) };
        panel.Children.Add(new TextBlock {
            Text = _current.Version.Id,
            FontSize = 20,
            FontWeight = FontWeights.SemiBold
        });
        panel.Children.Add(new TextBlock {
            Text = $"{_current.Version.Type} · {_current.Folder.Name} · {_current.Version.LibrariesCount} libraries · Java {_current.Version.JavaVersionMajor}",
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = System.Windows.Media.Brushes.Gray
        });
        panel.Children.Add(new TextBlock {
            Text = string.IsNullOrWhiteSpace(setup.CustomInfo)
                ? "未设置自定义描述"
                : setup.CustomInfo,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock {
            Text = InstancePath,
            Margin = new Thickness(0, 10, 0, 0),
            FontSize = 11,
            Foreground = System.Windows.Media.Brushes.Gray,
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }

    private void DisplayType_Changed(object sender, SelectionChangedEventArgs e) {
        if (_loading || _current is null) return;
        try {
            InstanceManagementService.SetDisplayType(InstancePath, Math.Max(0, ComboDisplayType.SelectedIndex));
        } catch (Exception ex) {
            NotificationService.Error("保存版本分类失败：" + ex.Message);
        }
    }

    private void Rename_Click(object sender, RoutedEventArgs e) {
        Logger.Info("[Instance] 点击重命名版本");
        if (_current is null) return;
        var oldName = _current.Version.Folder;
        string? newName;
        try {
            newName = InputDialog.Show("重命名版本", "输入新的版本名，文件夹、JSON 与 Jar 会一并重命名。", oldName);
        } catch (Exception ex) {
            Logger.Error(ex, "[Instance] 打开重命名输入框失败");
            NotificationService.Error("打开输入框失败：" + ex.Message);
            return;
        }
        Logger.Info("[Instance] 重命名输入框返回：" + (newName ?? "<null>"));
        if (string.IsNullOrWhiteSpace(newName) || string.Equals(newName.Trim(), oldName, StringComparison.Ordinal)) return;
        try {
            var newPath = InstanceManagementService.Rename(InstancePath, newName);
            AppServices.SelectedInstanceName = Path.GetFileName(newPath);
            Refresh();
            NotificationService.Info("重命名成功。");
        } catch (Exception ex) {
            NotificationService.Error("重命名失败：" + ex.Message);
        }
    }

    private void Description_Click(object sender, RoutedEventArgs e) {
        if (_current is null) return;
        var current = InstanceManagementService.ReadSetup(InstancePath).CustomInfo;
        var value = InputDialog.Show("更改描述", "修改版本的描述文本，留空则使用 PCL 的默认描述。", current);
        if (value is null) return;
        try {
            InstanceManagementService.SetCustomInfo(InstancePath, value);
            Refresh();
        } catch (Exception ex) {
            NotificationService.Error("保存描述失败：" + ex.Message);
        }
    }

    private void Star_Click(object sender, RoutedEventArgs e) {
        if (_current is null) return;
        try {
            InstanceManagementService.ToggleStar(InstancePath);
            Refresh();
        } catch (Exception ex) {
            NotificationService.Error("修改收藏状态失败：" + ex.Message);
        }
    }

    private void Check_Click(object sender, RoutedEventArgs e) {
        if (_current is null) return;
        try {
            var result = InstanceCheckService.Check(InstancePath, _current.Folder.Path);
            var lines = new List<string>();
            lines.AddRange(result.Issues.Select(issue => $"[{issue.Severity}] {issue.Message}"));
            lines.AddRange(result.ModIssues.Select(issue => $"[{issue.Severity}] {issue.FileName}: {issue.Message}"));
            if (lines.Count == 0) lines.Add("未发现明显问题。");
            System.Windows.MessageBox.Show(string.Join(Environment.NewLine, lines), "实例体检", MessageBoxButton.OK,
                result.HasErrors ? MessageBoxImage.Warning : MessageBoxImage.Information);
        } catch (Exception ex) {
            NotificationService.Error("体检失败：" + ex.Message);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e) {
        if (_current is null) return;
        var answer = System.Windows.MessageBox.Show(
            $"确定删除版本 {_current.Version.Id} 吗？\r\n该操作会删除整个实例文件夹，且无法撤销。",
            "版本删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        try {
            InstanceManagementService.Delete(InstancePath);
            AppServices.SelectedInstanceName = null;
            Refresh();
            NotificationService.Info("已删除版本。");
        } catch (Exception ex) {
            NotificationService.Error("删除失败：" + ex.Message);
        }
    }

    private void ExportScript_Click(object sender, RoutedEventArgs e) {
        if (_current is null) return;
        var savePath = MeloongCore.Wpf.Dialogs.SaveFile(
            "选择脚本保存位置", $"启动 {_current.Version.Folder}.bat", InstancePath,
            [("bat", "批处理文件")]);
        if (string.IsNullOrWhiteSpace(savePath)) return;
        try {
            var java = JavaScanner.FindBest(JavaScanner.DefaultRoots(
                AppServices.Paths.Base, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
                _current.Version.JavaVersionMajor);
            if (java is null) {
                NotificationService.Warning("未找到可用的 Java。");
                return;
            }
            var login = AppServices.CurrentLogin ?? OfflineLoginService.Create(Environment.UserName);
            var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(_current.Version.JsonPath));
            var plan = LaunchPlanService.Build(json, _current.Folder.Path, InstancePath,
                _current.Version.Folder, login);
            var content = new StringBuilder();
            content.AppendLine("@echo off");
            content.AppendLine("chcp 65001 >nul");
            content.AppendLine($"cd /d \"{InstancePath}\"");
            content.AppendLine($"\"{java.JavaExe}\" {LaunchArgumentFormatter.JoinForDisplay(plan.Arguments)}");
            File.WriteAllText(savePath, content.ToString(), new UTF8Encoding(false));
            NotificationService.Info("启动脚本已导出：" + savePath);
        } catch (Exception ex) {
            NotificationService.Error("导出启动脚本失败：" + ex.Message);
        }
    }

    private static void OpenFolder(string path) {
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

}
