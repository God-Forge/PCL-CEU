using PCL.App.Controls;
using PCL.App.Services;
using PCL.Core.Download;
using PCL.Core.Java;
using PCL.Core.Minecraft;
using PCL.Core.Net;
using PCLCS;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PCL.App.Views.Migrated;

public partial class PageDownloadInstall : PCL.App.Controls.MyPageRight {

    private sealed record VersionEntry(string Id, string Type);

    private sealed record LoaderChoice(string Loader, string Version, string? Display = null);

    private sealed record LoaderLoadResult(string Loader, IReadOnlyList<LoaderChoice> Choices, string? Error);

    private readonly List<(MyListItem Item, string Id)> _versionItems = new();
    private readonly List<(MyListItem Item, LoaderChoice Choice)> _loaderItems = new();
    private readonly Dictionary<string, (MyCard Card, string Title)> _loaderCards = new();

    private System.Windows.Controls.ProgressBar _progressBar = null!;
    private System.Windows.Controls.TextBox _logBox = null!;
    private int _versionLoadGeneration;
    private int _loaderLoadGeneration;
    private string? _minecraftVersion;
    private LoaderChoice? _selectedLoader;
    private bool _isInstalling;
    private bool _isNameEdited;
    private bool _isUpdatingName;
    private bool _isInitialized;
    private int _loggedCompletedFiles = -1;

    public PageDownloadInstall() {
        InitializeComponent();
        _loaderCards["Forge"] = (CardForge, CardForge.Title ?? "Forge");
        _loaderCards["NeoForge"] = (CardNeoForge, CardNeoForge.Title ?? "NeoForge");
        _loaderCards["Fabric"] = (CardFabric, CardFabric.Title ?? "Fabric");
        _loaderCards["LiteLoader"] = (CardLiteLoader, CardLiteLoader.Title ?? "LiteLoader");
        _loaderCards["OptiFine"] = (CardOptiFine, CardOptiFine.Title ?? "OptiFine");
        CardFabricApi.Visibility = Visibility.Collapsed;
        CardOptiFabric.Visibility = Visibility.Collapsed;
        CardMinecraft.Visibility = Visibility.Collapsed;
        BtnStart.Visibility = Visibility.Collapsed;
        CreateProgressPanel();
        BtnBack.Click += (_, _) => ExitSelectPage();
        BtnStart.Click += (_, _) => _ = StartInstallAsync();
        BtnOriginalNameRestore.Click += (_, _) => {
            _isNameEdited = false;
            UpdateInstallName();
            UpdateNameValidation();
        };
        TextSelectName.TextChanged += (_, _) => {
            if (_isUpdatingName) return;
            _isNameEdited = true;
            UpdateNameValidation();
        };
        Loaded += async (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            if (_isInitialized) return;
            _isInitialized = true;
            await RefreshVersionListAsync();
        };
    }

    public string? SelectedMinecraftVersion => _minecraftVersion;

    public async Task RefreshVersionListAsync() {
        var generation = ++_versionLoadGeneration;
        PanLoad.Visibility = Visibility.Visible;
        PanMinecraft.IsHitTestVisible = false;
        try {
            using var fetcher = new HttpStringFetcher();
            var versions = await new VersionManifestService(fetcher).GetVersionsAsync();
            if (generation != _versionLoadGeneration) return;
            _versionItems.Clear();
            PanMinecraft.Children.Clear();
            AddVersionCard("正式版", versions.Where(version => version.Type == "release").Take(80));
            AddVersionCard("预览版", versions.Where(version => version.Type == "snapshot").Take(60));
            AddVersionCard("其他版本", versions.Where(version => version.Type != "release" && version.Type != "snapshot").Take(40));
            if (PanMinecraft.Children.Count == 0) {
                AddMessage("没有从版本列表中找到可安装的 Minecraft 版本。", "Yellow");
            }
        } catch (Exception ex) {
            if (generation != _versionLoadGeneration) return;
            _versionItems.Clear();
            PanMinecraft.Children.Clear();
            AddMessage("无法加载 Minecraft 版本列表，请检查网络后重试。\n" + ex.Message, "Red");
            var retry = new MyButton {
                Text = "重新加载",
                Height = 32,
                MinWidth = 110,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 0)
            };
            retry.Click += (_, _) => _ = RefreshVersionListAsync();
            PanMinecraft.Children.Add(retry);
        } finally {
            if (generation == _versionLoadGeneration) {
                PanLoad.Visibility = Visibility.Collapsed;
                PanMinecraft.IsHitTestVisible = true;
            }
        }
    }

    private void AddVersionCard(string title, IEnumerable<(string Id, string Type, string Url)> versions) {
        var entries = versions.ToList();
        if (entries.Count == 0) return;
        var panel = new StackPanel { Margin = new Thickness(20, 38, 18, 10) };
        var card = new MyCard { Title = title, Margin = new Thickness(0, 0, 0, 15) };
        foreach (var version in entries) {
            var item = new MyListItem {
                Title = version.Id,
                Info = title,
                Tag = version.Id,
                Type = "RadioBox",
                Height = 36,
                Margin = new Thickness(0, 2, 0, 2)
            };
            item.Click += (_, _) => SelectMinecraftVersion(new VersionEntry(version.Id, version.Type));
            panel.Children.Add(item);
            _versionItems.Add((item, version.Id));
        }
        card.Children.Add(panel);
        PanMinecraft.Children.Add(card);
    }

    private void AddMessage(string text, string theme) {
        PanMinecraft.Children.Add(new MyHint {
            Text = text,
            Theme = theme,
            Margin = new Thickness(0, 5, 0, 8)
        });
    }

    private void SelectMinecraftVersion(VersionEntry version) {
        _minecraftVersion = version.Id;
        _selectedLoader = null;
        _loaderItems.Clear();
        _loaderLoadGeneration++;
        LabMinecraftVersion.Text = version.Id;
        foreach (var entry in _versionItems) {
            entry.Item.Checked = string.Equals(entry.Id, version.Id, StringComparison.Ordinal);
        }
        _isUpdatingName = true;
        _isNameEdited = false;
        TextSelectName.Text = version.Id;
        _isUpdatingName = false;
        _loggedCompletedFiles = -1;
        _progressBar.Value = 0;
        _logBox.Clear();
        PanSelect.Visibility = Visibility.Visible;
        PanSelect.Opacity = 1;
        PanSelect.RenderTransform = new System.Windows.Media.TranslateTransform();
        PanSelect.IsHitTestVisible = true;
        PanMinecraft.IsHitTestVisible = false;
        BtnStart.Visibility = Visibility.Visible;
        PanBack.ScrollToHome();
        UpdateSelectedSummary();
        UpdateNameValidation();
        _ = LoadLoadersAsync(version.Id);
    }

    private void ExitSelectPage() {
        _loaderLoadGeneration++;
        _minecraftVersion = null;
        _selectedLoader = null;
        _loaderItems.Clear();
        _isUpdatingName = true;
        _isNameEdited = false;
        TextSelectName.Text = "";
        _isUpdatingName = false;
        foreach (var entry in _versionItems) {
            entry.Item.Checked = false;
        }
        PanSelect.IsHitTestVisible = false;
        PanSelect.Visibility = Visibility.Collapsed;
        PanMinecraft.IsHitTestVisible = true;
        BtnStart.Visibility = Visibility.Collapsed;
        LabSelectName.Visibility = Visibility.Collapsed;
        HintSelectName.Visibility = Visibility.Collapsed;
        PanBack.ScrollToHome();
    }

    private async Task LoadLoadersAsync(string minecraftVersion) {
        var generation = ++_loaderLoadGeneration;
        SetLoaderCardsLoading();
        using var source = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var results = await Task.WhenAll(
            TryLoadChoicesAsync("Forge", () => LoadForgeChoicesAsync(minecraftVersion, source.Token)),
            TryLoadChoicesAsync("NeoForge", () => LoadNeoForgeChoicesAsync(minecraftVersion, source.Token)),
            TryLoadChoicesAsync("Fabric", () => LoadFabricChoicesAsync(minecraftVersion, source.Token)),
            TryLoadChoicesAsync("LiteLoader", () => LoadLiteLoaderChoicesAsync(minecraftVersion, source.Token)),
            TryLoadChoicesAsync("OptiFine", () => LoadOptiFineChoicesAsync(minecraftVersion, source.Token)));
        if (generation != _loaderLoadGeneration) return;
        ApplyLoaderResults(results);
    }

    private void SetLoaderCardsLoading() {
        _selectedLoader = null;
        _loaderItems.Clear();
        foreach (var entry in _loaderCards) {
            entry.Value.Card.Title = entry.Value.Title + " · 获取中…";
            entry.Value.Card.IsSwapped = true;
            entry.Value.Card.Visibility = Visibility.Visible;
            var panel = GetLoaderPanel(entry.Key);
            if (panel is not null) panel.Children.Clear();
            var label = GetLoaderLabel(entry.Key);
            if (label is not null) label.Text = "正在获取版本列表…";
        }
        UpdateSelectedSummary();
    }

    private void ApplyLoaderResults(IEnumerable<LoaderLoadResult> results) {
        _loaderItems.Clear();
        foreach (var result in results) {
            if (!_loaderCards.TryGetValue(result.Loader, out var cardInfo)) continue;
            var panel = GetLoaderPanel(result.Loader);
            var label = GetLoaderLabel(result.Loader);
            if (panel is null) continue;
            panel.Children.Clear();
            if (result.Error is not null) {
                cardInfo.Card.Title = cardInfo.Title + " · " + result.Error;
                if (label is not null) label.Text = result.Error;
            } else {
                cardInfo.Card.Title = cardInfo.Title + " · " + result.Choices.Count + " 个版本";
                if (label is not null) label.Text = "共 " + result.Choices.Count + " 个版本";
                AddLoaderChoice(panel, result.Loader, null);
                foreach (var choice in result.Choices) {
                    AddLoaderChoice(panel, result.Loader, choice);
                }
            }
            cardInfo.Card.IsSwapped = true;
        }
        UpdateSelectedSummary();
    }

    private void AddLoaderChoice(StackPanel panel, string loader, LoaderChoice? choice) {
        var item = new MyListItem {
            Title = choice is null ? "不安装此加载器" : choice.Display ?? choice.Version,
            Info = choice is null ? "" : loader,
            Type = "RadioBox",
            Height = 34,
            Margin = new Thickness(0, 1, 0, 1)
        };
        if (choice is not null) {
            item.Click += (_, _) => {
                foreach (var entry in _loaderItems) {
                    entry.Item.Checked = ReferenceEquals(entry.Item, item);
                }
                _selectedLoader = choice;
                UpdateSelectedSummary();
                UpdateInstallName();
                UpdateNameValidation();
            };
            _loaderItems.Add((item, choice));
        } else {
            item.Click += (_, _) => {
                foreach (var entry in _loaderItems) {
                    entry.Item.Checked = false;
                }
                _selectedLoader = null;
                UpdateSelectedSummary();
                UpdateInstallName();
                UpdateNameValidation();
            };
        }
        panel.Children.Add(item);
    }

    private StackPanel? GetLoaderPanel(string loader) => loader switch {
        "Forge" => PanForge,
        "NeoForge" => PanNeoForge,
        "Fabric" => PanFabric,
        "LiteLoader" => PanLiteLoader,
        "OptiFine" => PanOptiFine,
        _ => null
    };

    private TextBlock? GetLoaderLabel(string loader) => loader switch {
        "Forge" => LabForge,
        "NeoForge" => LabNeoForge,
        "Fabric" => LabFabric,
        "LiteLoader" => LabLiteLoader,
        "OptiFine" => LabOptiFine,
        _ => null
    };

    private static async Task<LoaderLoadResult> TryLoadChoicesAsync(string loader, Func<Task<List<LoaderChoice>>> action) {
        try {
            var choices = await action();
            return new LoaderLoadResult(loader, choices, choices.Count == 0 ? "无可用版本" : null);
        } catch (OperationCanceledException) {
            return new LoaderLoadResult(loader, Array.Empty<LoaderChoice>(), "获取超时");
        } catch (Exception ex) {
            return new LoaderLoadResult(loader, Array.Empty<LoaderChoice>(), ex.Message);
        }
    }

    private static async Task<List<LoaderChoice>> LoadForgeChoicesAsync(string minecraftVersion, CancellationToken cancellationToken) {
        using var fetcher = new HttpStringFetcher();
        var versions = await new ForgeVersionService(fetcher).GetVersionsAsync(minecraftVersion, 0, null, cancellationToken);
        return versions
            .Where(version => version.Category != "universal" && version.Category != "client")
            .OrderByDescending(version => version.IsRecommended)
            .ThenByDescending(version => version.VersionName, Comparer<string>.Create(McUtils.CompareVersion))
            .Take(12)
            .Select(version => new LoaderChoice("Forge", version.VersionName, version.IsRecommended ? version.VersionName + "（推荐）" : version.VersionName))
            .ToList();
    }

    private static async Task<List<LoaderChoice>> LoadNeoForgeChoicesAsync(string minecraftVersion, CancellationToken cancellationToken) {
        using var fetcher = new HttpStringFetcher();
        var version = await new LoaderVersionService(fetcher).FindNeoForgeAsync(minecraftVersion, cancellationToken);
        return [new LoaderChoice("NeoForge", version.VersionName)];
    }

    private static async Task<List<LoaderChoice>> LoadFabricChoicesAsync(string minecraftVersion, CancellationToken cancellationToken) {
        using var fetcher = new HttpStringFetcher();
        var result = await new FabricListService(fetcher).GetAsync(0, cancellationToken);
        var target = minecraftVersion.Replace("∞", "infinite").Replace("Combat Test 7c", "1.16_combat-3");
        var games = result.Value["game"]?.OfType<Newtonsoft.Json.Linq.JObject>();
        var gameExists = games?.Any(game => string.Equals(game["version"]?.ToString(), target, StringComparison.OrdinalIgnoreCase)) == true;
        if (!gameExists) return [];
        var loaders = result.Value["loader"]?.OfType<Newtonsoft.Json.Linq.JObject>();
        if (loaders is null) return [];
        return loaders
            .Where(loader => !string.IsNullOrWhiteSpace(loader["version"]?.ToString()))
            .OrderByDescending(loader => loader["stable"]?.ToObject<bool>() == true)
            .Take(10)
            .Select(loader => new LoaderChoice("Fabric", loader["version"]!.ToString()))
            .ToList();
    }

    private static async Task<List<LoaderChoice>> LoadLiteLoaderChoicesAsync(string minecraftVersion, CancellationToken cancellationToken) {
        using var fetcher = new HttpStringFetcher();
        var result = await new LiteLoaderListService(fetcher).GetAsync(0, cancellationToken);
        return result.Versions
            .Where(version => string.Equals(version.Inherit, minecraftVersion, StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .Select(version => new LoaderChoice("LiteLoader", version.Inherit, version.IsPreview ? version.Inherit + "（预览）" : version.Inherit))
            .ToList();
    }

    private static async Task<List<LoaderChoice>> LoadOptiFineChoicesAsync(string minecraftVersion, CancellationToken cancellationToken) {
        using var fetcher = new HttpStringFetcher();
        var result = await new OptiFineListService(fetcher).GetAsync(0, cancellationToken);
        return result.Versions
            .Where(version => string.Equals(version.Inherit, minecraftVersion, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(version => version.ReleaseTime, StringComparer.Ordinal)
            .Take(20)
            .Select(version => new LoaderChoice("OptiFine", version.DisplayName, version.DisplayName))
            .ToList();
    }

    private void CreateProgressPanel() {
        _progressBar = new System.Windows.Controls.ProgressBar {
            Height = 8,
            Minimum = 0,
            Maximum = 100,
            Margin = new Thickness(0, 6, 0, 8)
        };
        _logBox = new System.Windows.Controls.TextBox {
            Height = 120,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 11
        };
        var panel = new StackPanel { Margin = new Thickness(0, 15, 0, 10) };
        panel.Children.Add(new TextBlock {
            Text = "安装进度",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 2)
        });
        panel.Children.Add(_progressBar);
        panel.Children.Add(_logBox);
        PanSelect.Children.Insert(1, panel);
    }

    private void UpdateSelectedSummary() {
        LabSelectName.Visibility = Visibility.Visible;
        if (_selectedLoader is null) {
            LabSelectName.Text = string.IsNullOrWhiteSpace(_minecraftVersion) ? "未选择版本" : "原版 " + _minecraftVersion;
        } else {
            LabSelectName.Text = _selectedLoader.Loader + " " + (_selectedLoader.Display ?? _selectedLoader.Version);
        }
    }

    private void UpdateInstallName() {
        if (_isNameEdited) return;
        _isUpdatingName = true;
        TextSelectName.Text = BuildDefaultName();
        _isUpdatingName = false;
        _isNameEdited = false;
    }

    private string BuildDefaultName() {
        var name = _minecraftVersion ?? "";
        if (_selectedLoader is null) return name;
        var version = _selectedLoader.Version;
        var suffix = _selectedLoader.Loader switch {
            "Fabric" => "-Fabric " + version.Split('+')[0],
            "Forge" => "-Forge_" + version,
            "NeoForge" => "-NeoForge_" + version.Split('+')[0],
            "LiteLoader" => "-LiteLoader",
            "OptiFine" => "-OptiFine_" + version.Replace((_minecraftVersion ?? "") + " ", "").Replace(' ', '_'),
            _ => ""
        };
        return name + suffix;
    }

    private void UpdateNameValidation() {
        var error = ValidateCurrentName();
        HintSelectName.Text = error;
        HintSelectName.Visibility = string.IsNullOrEmpty(error) ? Visibility.Collapsed : Visibility.Visible;
        BtnOriginalNameRestore.Visibility = _isNameEdited ? Visibility.Visible : Visibility.Collapsed;
        BtnStart.IsEnabled = !_isInstalling && string.IsNullOrEmpty(error) && !string.IsNullOrWhiteSpace(_minecraftVersion);
    }

    private string ValidateCurrentName() {
        try {
            var name = TextSelectName.Text.Trim();
            var folder = TryGetMinecraftFolder();
            var versionsFolder = folder is null ? null : System.IO.Path.Combine(folder, "versions");
            return new PCL.ValidateFolderName(versionsFolder).Validate(name) ?? "";
        } catch (Exception ex) {
            return "名称检查失败：" + ex.Message;
        }
    }

    private static string? TryGetMinecraftFolder() {
        try {
            return AppServices.SelectedMinecraftFolder.Path;
        } catch {
            return null;
        }
    }

    private async Task StartInstallAsync() {
        if (_isInstalling) return;
        if (string.IsNullOrWhiteSpace(_minecraftVersion)) {
            NotificationService.Warning("请先选择一个 Minecraft 版本。", "安装");
            return;
        }
        var minecraftVersion = _minecraftVersion!;
        var name = TextSelectName.Text.Trim();
        var error = ValidateCurrentName();
        if (!string.IsNullOrEmpty(error)) {
            NotificationService.Warning(error, "安装");
            return;
        }
        var minecraftFolder = TryGetMinecraftFolder();
        if (minecraftFolder is null) {
            NotificationService.Warning("尚未找到可用的 Minecraft 文件夹。", "安装");
            return;
        }
        var targetFolder = System.IO.Path.Combine(minecraftFolder, "versions", name);
        if (Directory.Exists(targetFolder) || File.Exists(targetFolder)) {
            NotificationService.Warning("目标版本已存在：" + name, "安装");
            return;
        }
        _isInstalling = true;
        _loggedCompletedFiles = -1;
        _progressBar.Value = 0;
        _logBox.Clear();
        BtnStart.IsEnabled = false;
        AppendLog("开始安装 " + name);
        var progress = new Progress<NetLoaderProgress>(value => {
            var percent = value.TotalBytes is > 0
                ? value.BytesReceived * 100d / value.TotalBytes.Value
                : value.FilesTotal > 0 ? value.FilesCompleted * 100d / value.FilesTotal : 0;
            SetProgress(percent);
            if (value.FilesCompleted != _loggedCompletedFiles) {
                _loggedCompletedFiles = value.FilesCompleted;
                AppendLog($"已完成 {value.FilesCompleted}/{value.FilesTotal} 个文件");
            }
        });
        try {
            var selectedLoader = _selectedLoader;
            if (selectedLoader is { Loader: "LiteLoader" or "OptiFine" }) {
                throw new NotSupportedException("当前迁移版本暂不支持自动安装 " + selectedLoader.Loader + "，请手动安装或选择其他加载器。");
            }
            string installedFolder;
            if (selectedLoader is { Loader: "Fabric" }) {
                installedFolder = await new FabricInstallService().InstallAsync(minecraftFolder, minecraftVersion, selectedLoader.Version, progress);
            } else if (selectedLoader is { Loader: "Forge" or "NeoForge" }) {
                installedFolder = await InstallForgelikeAsync(minecraftFolder, minecraftVersion, selectedLoader, progress);
            } else {
                installedFolder = await new VanillaInstallService().InstallAsync(minecraftFolder, minecraftVersion, progress);
            }
            if (!string.Equals(System.IO.Path.GetFileName(installedFolder), name, StringComparison.OrdinalIgnoreCase)) {
                try {
                    installedFolder = InstanceManagementService.Rename(installedFolder, name);
                    AppendLog("已重命名为 " + name);
                } catch (Exception ex) {
                    AppendLog("安装完成，但重命名失败：" + ex.Message);
                }
            }
            SetProgress(100);
            AppendLog("安装完成：" + installedFolder);
            NotificationService.Info("安装完成：" + installedFolder, "安装");
        } catch (Exception ex) {
            AppendLog("安装失败：" + ex.Message);
            NotificationService.Error("安装失败：" + ex.Message, "安装");
        } finally {
            _isInstalling = false;
            UpdateNameValidation();
        }
    }

    private async Task<string> InstallForgelikeAsync(
        string minecraftFolder,
        string minecraftVersion,
        LoaderChoice choice,
        IProgress<NetLoaderProgress> progress) {
        var loaderName = choice.Loader;
        var isNeoForge = string.Equals(loaderName, "NeoForge", StringComparison.OrdinalIgnoreCase);
        var cacheRoot = GetCacheRoot();
        var wrapperPath = System.IO.Path.Combine(cacheRoot, "Cache", "forge_installer.jar");
        if (!File.Exists(wrapperPath)) {
            throw new InvalidOperationException("当前迁移版本缺少内置 Forge 安装器组件，无法自动安装 " + loaderName + "。");
        }
        var installerPath = System.IO.Path.Combine(cacheRoot, "Cache", $"installer-{minecraftVersion}-{choice.Version}.jar");
        if (!File.Exists(installerPath) || new FileInfo(installerPath).Length < 1000) {
            var urls = BuildInstallerUrls(loaderName, minecraftVersion, choice.Version);
            if (urls.Count == 0) throw new InvalidOperationException("无法确定 " + loaderName + " 安装器地址。");
            AppendLog("正在下载 " + loaderName + " 安装器…");
            using var downloader = new HttpDownloadService();
            var result = await downloader.DownloadAsync(new DownloadRequest {
                Url = urls[0],
                BackupUrls = urls.Skip(1).ToList(),
                TargetPath = installerPath,
                MinSize = 1000
            });
            if (!result.Success) throw new InvalidOperationException(loaderName + " 安装器下载失败。");
        }
        var java = JavaScanner.FindBest(
            JavaScanner.DefaultRoots(AppContext.BaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),
            RequiredJavaVersion(minecraftVersion));
        if (java is null) throw new InvalidOperationException("未找到可用的 Java，无法运行 " + loaderName + " 安装器。");
        AppendLog("使用 Java " + java.Version + " 运行安装器…");
        using var fetcher = new HttpStringFetcher();
        var vanillaUrl = await new VersionManifestService(fetcher).GetVersionUrlAsync(minecraftVersion);
        return await new ForgelikeInstallService(fetcher).InstallAsync(
            minecraftFolder,
            installerPath,
            java.JavaExe,
            loaderName,
            minecraftVersion,
            choice.Version,
            isNeoForge,
            cacheRoot,
            System.IO.Path.GetTempPath(),
            null,
            [vanillaUrl],
            java.Major,
            progress);
    }

    private static string GetCacheRoot() {
        var temp = AppServices.Paths.Temp;
        return System.IO.Path.GetFullPath(temp).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
    }

    private static List<string> BuildInstallerUrls(string loaderName, string minecraftVersion, string loaderVersion) {
        if (string.Equals(loaderName, "NeoForge", StringComparison.OrdinalIgnoreCase)) {
            var artifact = loaderVersion.StartsWith("1.20.1-", StringComparison.Ordinal) ? "forge" : "neoforge";
            var fileName = artifact + "-" + loaderVersion + "-installer.jar";
            var path = artifact + "/" + loaderVersion + "/" + fileName;
            return [
                "https://maven.neoforged.net/releases/net/neoforged/" + path,
                "https://bmclapi2.bangbang93.com/neoforge/maven/releases/net/neoforged/" + path
            ];
        }
        var fullVersion = minecraftVersion + "-" + loaderVersion;
        var forgePath = "net/minecraftforge/forge/" + fullVersion + "/forge-" + fullVersion + "-installer.jar";
        return [
            "https://maven.minecraftforge.net/" + forgePath,
            "https://bmclapi2.bangbang93.com/maven/" + forgePath
        ];
    }

    private static int RequiredJavaVersion(string minecraftVersion) {
        if (McUtils.CompareVersionGE(minecraftVersion, "1.20.5")) return 21;
        if (McUtils.CompareVersionGE(minecraftVersion, "1.18")) return 17;
        if (McUtils.CompareVersionGE(minecraftVersion, "1.17")) return 16;
        return 8;
    }

    private void SetProgress(double value) {
        if (!Dispatcher.CheckAccess()) {
            Dispatcher.Invoke(() => SetProgress(value));
            return;
        }
        _progressBar.Value = Math.Clamp(value, 0, 100);
    }

    private void AppendLog(string text) {
        if (!Dispatcher.CheckAccess()) {
            Dispatcher.Invoke(() => AppendLog(text));
            return;
        }
        _logBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text + Environment.NewLine);
        _logBox.ScrollToEnd();
    }

}
