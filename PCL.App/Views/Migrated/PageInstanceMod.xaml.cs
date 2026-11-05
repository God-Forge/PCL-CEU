using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.App.Controls;
using PCL.App.Services;
using MeloongCore;
using PCL.Core.Minecraft;
using PCL.Core.Mods;
using PCL.Core.Resources;

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

public partial class PageInstanceMod : PCL.App.Controls.MyPageRight {

    private enum ModFilter {
        All,
        Enabled,
        Disabled
    }

    private readonly List<ModInfo> _mods = new();
    private readonly List<ModIssue> _issues = new();
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private ModFilter _filter = ModFilter.All;
    private string _search = "";
    private string? _modsFolder;
    private int _loadVersion;

    public PageInstanceMod() {
        InitializeComponent();
        BtnFilterCanUpdate.Visibility = Visibility.Collapsed;
        BtnSelectUpdate.Visibility = Visibility.Visible;
        BtnFilterAll.IsChecked = true;

        SearchBox.TextChanged += (_, _) => {
            _search = SearchBox.Text.Trim();
            RefreshList();
        };
        BtnFilterAll.Click += (_, _) => SetFilter(ModFilter.All);
        BtnFilterEnabled.Click += (_, _) => SetFilter(ModFilter.Enabled);
        BtnFilterDisabled.Click += (_, _) => SetFilter(ModFilter.Disabled);

        BtnManageOpen.Click += (_, _) => OpenModsFolder();
        BtnHintOpen.Click += (_, _) => OpenModsFolder();
        BtnManageInstall.Click += (_, _) => InstallFromFiles();
        BtnHintInstall.Click += (_, _) => InstallFromFiles();
        BtnManageDownload.Click += (_, _) => NotificationService.Info("请在下载页面安装 Mod；当前页面仅管理本地 Mod 文件。");
        BtnHintDownload.Click += (_, _) => NotificationService.Info("请在下载页面安装 Mod；当前页面仅管理本地 Mod 文件。");
        BtnManageSelectAll.Click += (_, _) => SelectAllFiltered();

        BtnSelectCancel.Click += (_, _) => {
            _selected.Clear();
            RefreshList();
        };
        BtnSelectEnable.Click += (_, _) => SetSelectedEnabled(true);
        BtnSelectDisable.Click += (_, _) => SetSelectedEnabled(false);
        BtnSelectDelete.Click += (_, _) => DeleteMods(SelectedMods());
        BtnSelectUpdate.Click += (_, _) => _ = CheckUpdatesAsync();

        Unloaded += (_, _) => _loadVersion++;
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            _ = ReloadModsAsync();
        };
    }

    private async Task ReloadModsAsync() {
        var version = ++_loadVersion;
        PanLoad.Visibility = Visibility.Visible;
        PanAllBack.Visibility = Visibility.Collapsed;
        PanEmpty.Visibility = Visibility.Collapsed;
        try {
            var instancePath = ResolveInstancePath();
            if (instancePath is null) {
                _modsFolder = null;
                _mods.Clear();
                _selected.Clear();
                if (version == _loadVersion) {
                    PanAllBack.Visibility = Visibility.Collapsed;
                    PanEmpty.Visibility = Visibility.Visible;
                    RefreshSelectionBar();
                }
                return;
            }

            _modsFolder = InstanceManagementService.GetFolders(instancePath).Mods;
            var folder = _modsFolder;
            var result = await Task.Run(() => ModScanner.Scan(folder));
            if (version != _loadVersion) return;

            _mods.Clear();
            _mods.AddRange(result.Mods);
            _issues.Clear();
            _issues.AddRange(result.Issues);
            foreach (var issue in _issues) {
                Logger.Info($"[Mod 问题] {issue.Severity} {issue.FileName}：{issue.Message}");
            }
            var names = new HashSet<string>(_mods.Select(mod => mod.FileName), StringComparer.OrdinalIgnoreCase);
            _selected.RemoveWhere(name => !names.Contains(name));
        } catch (Exception ex) {
            if (version != _loadVersion) return;
            _mods.Clear();
            _selected.Clear();
            NotificationService.Error("读取 Mod 列表失败：" + ex.Message);
        } finally {
            if (version == _loadVersion) {
                PanLoad.Visibility = Visibility.Collapsed;
                RefreshList();
            }
        }
    }

    private static string? ResolveInstancePath() {
        try {
            if (!string.IsNullOrWhiteSpace(AppServices.SelectedInstanceName)) return AppServices.SelectedInstancePath;
            var first = AppServices.MinecraftFolders.ScanAll().FirstOrDefault();
            if (first is null) return null;
            AppServices.SelectedInstanceName = first.Version.Folder;
            AppServices.SelectedMinecraftPath = first.Folder.Path;
            return System.IO.Path.Combine(first.Folder.Versions, first.Version.Folder);
        } catch {
            return null;
        }
    }

    private void SetFilter(ModFilter filter) {
        _filter = filter;
        BtnFilterAll.IsChecked = filter == ModFilter.All;
        BtnFilterEnabled.IsChecked = filter == ModFilter.Enabled;
        BtnFilterDisabled.IsChecked = filter == ModFilter.Disabled;
        RefreshList();
    }

    private IEnumerable<ModInfo> VisibleMods() {
        return _mods.Where(MatchesFilter).Where(MatchesSearch);
    }

    private bool MatchesFilter(ModInfo mod) {
        return _filter switch {
            ModFilter.Enabled => mod.Enabled,
            ModFilter.Disabled => !mod.Enabled,
            _ => true
        };
    }

    private bool MatchesSearch(ModInfo mod) {
        if (string.IsNullOrWhiteSpace(_search)) return true;
        return mod.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)
               || mod.FileName.Contains(_search, StringComparison.OrdinalIgnoreCase)
               || mod.ModId.Contains(_search, StringComparison.OrdinalIgnoreCase)
               || mod.Loader.Contains(_search, StringComparison.OrdinalIgnoreCase)
               || mod.Version.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshList() {
        UpdateFilterCounts();
        if (_mods.Count == 0) {
            PanAllBack.Visibility = Visibility.Collapsed;
            PanEmpty.Visibility = Visibility.Visible;
            PanList.Children.Clear();
            RefreshSelectionBar();
            return;
        }

        PanAllBack.Visibility = Visibility.Visible;
        PanEmpty.Visibility = Visibility.Collapsed;
        PanList.Visibility = Visibility.Visible;
        PanList.Children.Clear();
        var visible = VisibleMods().ToList();
        if (visible.Count == 0) {
            PanList.Children.Add(new TextBlock {
                Text = "没有匹配当前筛选条件的 Mod。",
                Margin = new Thickness(4, 8, 4, 8),
                Foreground = System.Windows.Media.Brushes.Gray
            });
        } else {
            if (_issues.Count > 0) PanList.Children.Add(BuildIssuePanel());
            foreach (var mod in visible) PanList.Children.Add(BuildModRow(mod));
        }
        RefreshSelectionBar();
    }

    private void UpdateFilterCounts() {
        var enabled = _mods.Count(mod => mod.Enabled);
        BtnFilterAll.Text = $"全部 ({_mods.Count})";
        BtnFilterEnabled.Text = $"已启用 ({enabled})";
        BtnFilterDisabled.Text = $"已禁用 ({_mods.Count - enabled})";
    }

    /// <summary>展示扫描发现的问题（重复文件、缺失依赖、加载器冲突）。</summary>
    private UIElement BuildIssuePanel() {
        var summary = ModIssueSummarizer.Summarize(_issues);
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(new TextBlock {
            Text = $"发现 {summary.Total} 个问题（错误 {summary.Errors}）",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = summary.HasErrors
                ? new SolidColorBrush(Color.FromRgb(200, 40, 40))
                : new SolidColorBrush(Color.FromRgb(180, 130, 20))
        });
        foreach (var issue in summary.Highlights) {
            panel.Children.Add(new TextBlock {
                Text = $"· {issue.FileName}：{issue.Message}",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(6, 2, 0, 0),
                Foreground = System.Windows.Media.Brushes.Gray
            });
        }
        if (summary.Remaining > 0) {
            panel.Children.Add(new TextBlock {
                Text = $"…其余 {summary.Remaining} 条见日志",
                FontSize = 11,
                Margin = new Thickness(6, 2, 0, 0),
                Foreground = System.Windows.Media.Brushes.Gray
            });
        }
        return new Border {
            Background = new SolidColorBrush(Color.FromRgb(252, 248, 238)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 0, 10),
            Child = panel
        };
    }

    private UIElement BuildModRow(ModInfo mod) {
        var grid = new Grid { Margin = new Thickness(0, 5, 0, 5), MinHeight = 32 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var check = new CheckBox {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            ToolTip = "选择此 Mod"
        };
        check.Checked += (_, _) => {
            _selected.Add(mod.FileName);
            RefreshSelectionBar();
        };
        check.Unchecked += (_, _) => {
            _selected.Remove(mod.FileName);
            RefreshSelectionBar();
        };
        check.IsChecked = _selected.Contains(mod.FileName);
        Grid.SetColumn(check, 0);
        grid.Children.Add(check);

        var display = string.IsNullOrWhiteSpace(mod.Name) ? mod.FileName : mod.Name;
        var info = new TextBlock {
            Text = $"{display}  ·  {mod.Version}  ·  {mod.Loader}  ·  {(mod.Enabled ? "已启用" : "已禁用")}\r\n{mod.FileName}",
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = mod.FilePath
        };
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        var openButton = CreateRowButton("打开位置");
        openButton.Click += (_, _) => OpenModLocation(mod);
        Grid.SetColumn(openButton, 2);
        grid.Children.Add(openButton);

        var toggleButton = CreateRowButton(mod.Enabled ? "禁用" : "启用");
        toggleButton.Click += (_, _) => ToggleMod(mod);
        Grid.SetColumn(toggleButton, 3);
        grid.Children.Add(toggleButton);

        var deleteButton = CreateRowButton("删除");
        deleteButton.Click += (_, _) => DeleteMods(new[] { mod });
        Grid.SetColumn(deleteButton, 4);
        grid.Children.Add(deleteButton);

        return new Border {
            BorderBrush = System.Windows.Media.Brushes.Gainsboro,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = grid
        };
    }

    private static MyButton CreateRowButton(string text) {
        return new MyButton {
            Text = text,
            Height = 26,
            MinWidth = 74,
            Padding = new Thickness(8, 0, 8, 0),
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private List<ModInfo> SelectedMods() {
        return _mods.Where(mod => _selected.Contains(mod.FileName)).ToList();
    }

    private void RefreshSelectionBar() {
        var selected = SelectedMods();
        var count = selected.Count;
        if (count == 0) {
            CardSelect.Visibility = Visibility.Collapsed;
            PanListBack.Margin = new Thickness(0, 0, 0, 14);
            return;
        }

        LabSelect.Text = $"已选择 {count} 个文件";
        CardSelect.Visibility = Visibility.Visible;
        PanListBack.Margin = new Thickness(0, 0, 0, 95);
        BtnSelectEnable.IsEnabled = selected.Any(mod => !mod.Enabled);
        BtnSelectDisable.IsEnabled = selected.Any(mod => mod.Enabled);
        BtnSelectDelete.IsEnabled = true;
    }

    private void SelectAllFiltered() {
        var visible = VisibleMods().ToList();
        if (visible.Count == 0) return;
        if (visible.All(mod => _selected.Contains(mod.FileName))) {
            _selected.Clear();
        } else {
            foreach (var mod in visible) _selected.Add(mod.FileName);
        }
        RefreshList();
    }

    private void SetSelectedEnabled(bool enabled) {
        var targets = SelectedMods().Where(mod => mod.Enabled != enabled).ToList();
        if (targets.Count == 0) return;
        var failed = 0;
        foreach (var mod in targets) {
            try {
                if (!ChangeModState(mod, enabled)) failed++;
            } catch {
                failed++;
            }
        }
        if (failed > 0) NotificationService.Warning("部分 Mod 状态切换失败，请关闭正在运行的游戏后重试。");
        _selected.Clear();
        _ = ReloadModsAsync();
    }

    /// <summary>用本地 Mod ID 到 Modrinth 比对最新版本。</summary>
    private async Task CheckUpdatesAsync() {
        if (_mods.Count == 0) {
            NotificationService.Info("当前没有可检查的 Mod。");
            return;
        }
        BtnSelectUpdate.IsEnabled = false;
        try {
            var instance = string.IsNullOrWhiteSpace(AppServices.SelectedInstanceName)
                ? null
                : AppServices.MinecraftFolders.FindInstance(AppServices.SelectedInstanceName);
            var minecraftVersion = instance?.Version.Id;
            var loader = _mods.Select(mod => mod.Loader).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value != "unknown");
            var results = await ModUpdateChecker.CheckAsync(_mods, new ModrinthResourceService(), minecraftVersion, loader);
            var updatable = results.Where(result => result.HasUpdate).ToList();
            var failed = results.Where(result => result.Error is not null).ToList();
            foreach (var item in updatable) {
                Logger.Info($"[Mod 更新] {item.ModId}：{item.LocalVersion} → {item.LatestVersion}");
            }
            var message = $"可更新 {updatable.Count} 个，无更新 {results.Count - updatable.Count - failed.Count} 个";
            if (failed.Count > 0) message += $"，检查失败 {failed.Count} 个（多为 Modrinth 未收录）";
            NotificationService.Info(message, "Mod 更新检查");
        } catch (Exception ex) {
            Logger.Error(ex, "检查 Mod 更新失败");
            NotificationService.Error("检查更新失败：" + ex.Message, "Mod 更新检查");
        } finally {
            BtnSelectUpdate.IsEnabled = true;
        }
    }

    private void ToggleMod(ModInfo mod) {
        try {
            if (!ChangeModState(mod, !mod.Enabled)) return;
            _selected.Remove(mod.FileName);
            _ = ReloadModsAsync();
        } catch (Exception ex) {
            NotificationService.Error("切换 Mod 状态失败：" + ex.Message);
        }
    }

    private static bool ChangeModState(ModInfo mod, bool enabled) {
        var source = mod.FilePath;
        if (!File.Exists(source)) return false;
        string target;
        if (enabled) {
            if (!source.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) return false;
            target = source.Substring(0, source.Length - ".disabled".Length);
        } else {
            if (source.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) return false;
            target = source + ".disabled";
        }
        if (File.Exists(target)) {
            NotificationService.Warning("目标文件已存在，无法切换状态：\r\n" + System.IO.Path.GetFileName(target));
            return false;
        }
        File.Move(source, target);
        return true;
    }

    private void DeleteMods(IReadOnlyList<ModInfo> mods) {
        if (mods.Count == 0) return;
        var message = mods.Count == 1
            ? $"确定要删除 {mods[0].FileName} 吗？此操作不可撤销。"
            : $"确定要删除选中的 {mods.Count} 个 Mod 文件吗？此操作不可撤销。";
        if (System.Windows.MessageBox.Show(message, "删除 Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            != MessageBoxResult.Yes) return;

        var failed = 0;
        foreach (var mod in mods) {
            try {
                if (File.Exists(mod.FilePath)) File.Delete(mod.FilePath);
                _selected.Remove(mod.FileName);
            } catch {
                failed++;
            }
        }
        if (failed > 0) NotificationService.Warning("部分 Mod 删除失败，请关闭正在运行的游戏后重试。");
        _ = ReloadModsAsync();
    }

    private void OpenModsFolder() {
        var folder = _modsFolder;
        if (string.IsNullOrWhiteSpace(folder)) {
            var instancePath = ResolveInstancePath();
            if (instancePath is null) {
                NotificationService.Warning("请先选择实例。");
                return;
            }
            folder = InstanceManagementService.GetFolders(instancePath).Mods;
        }
        try {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        } catch (Exception ex) {
            NotificationService.Error("打开 Mod 文件夹失败：" + ex.Message);
        }
    }

    private static void OpenModLocation(ModInfo mod) {
        try {
            if (!File.Exists(mod.FilePath)) {
                NotificationService.Warning("文件已不存在。");
                return;
            }
            Process.Start(new ProcessStartInfo {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{mod.FilePath}\"",
                UseShellExecute = true
            });
        } catch (Exception ex) {
            NotificationService.Error("打开文件位置失败：" + ex.Message);
        }
    }

    private void InstallFromFiles() {
        var instancePath = ResolveInstancePath();
        if (instancePath is null) {
            NotificationService.Warning("请先选择实例。");
            return;
        }

        var folder = InstanceManagementService.GetFolders(instancePath).Mods;
        try {
            Directory.CreateDirectory(folder);
        } catch (Exception ex) {
            NotificationService.Error("创建 Mod 文件夹失败：" + ex.Message);
            return;
        }

        MeloongCore.OneOf<IEnumerable<(string[] Extensions, string Display)>, string> filter =
            "Mod 文件|*.jar;*.litemod;*.disabled;*.old|所有文件|*.*";
        var files = MeloongCore.Wpf.Dialogs.SelectFile("选择要安装的 Mod", true, folder, filter);
        if (files.Count == 0) return;

        var overwrite = files.Any(file => {
            var target = System.IO.Path.Combine(folder, System.IO.Path.GetFileName(file));
            return File.Exists(target)
                   && !string.Equals(System.IO.Path.GetFullPath(file), System.IO.Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase);
        });
        if (overwrite && System.Windows.MessageBox.Show(
                "目标文件夹中已存在同名文件，继续安装会覆盖这些文件。是否继续？",
                "安装 Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        var installed = 0;
        var failed = 0;
        foreach (var file in files) {
            try {
                var extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (extension is not (".jar" or ".litemod" or ".disabled" or ".old")) {
                    failed++;
                    continue;
                }
                var target = System.IO.Path.Combine(folder, System.IO.Path.GetFileName(file));
                if (string.Equals(System.IO.Path.GetFullPath(file), System.IO.Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) {
                    installed++;
                    continue;
                }
                File.Copy(file, target, overwrite: true);
                installed++;
            } catch {
                failed++;
            }
        }

        if (installed > 0) {
            NotificationService.Info($"已安装 {installed} 个 Mod 文件。");
            _ = ReloadModsAsync();
        }
        if (failed > 0) NotificationService.Warning($"有 {failed} 个文件未能安装。");
    }

}
