using System.Reflection;
using System.Windows;
using MeloongCore;
using PCL.App.Services;
using PCL.App.Views;
using PCL.App.Views.Migrated;
using PCL.Core.Import;

namespace PCL.App;

public partial class MainWindow : Window {

    public MainWindow() {
        InitializeComponent();
        HintService.Initialize(PanHint);
        NavigationService.Initialize(ShowView);
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        VersionText.Text = $"版本 0.24.0\n程序集 {version}";
        ShowView(new LaunchPageView());
        AllowDrop = true;
        Drop += OnFileDrop;
    }

    private void ShowView(UIElement view) => ContentHost.Content = view;

    /// <summary>处理从资源管理器拖入的文件：识别类型后引导或直接导入。</summary>
    private void OnFileDrop(object sender, DragEventArgs e) {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;
        e.Handled = true;
        var kind = DropImportClassifier.Classify(paths[0]);
        Logger.Info($"拖入 {paths.Length} 项，首项类型：{DropImportClassifier.Describe(kind)}");
        switch (kind) {
            case DropImportKind.ModFile:
                ImportModFiles(paths);
                break;
            case DropImportKind.Modpack:
                NavigationService.NavigateTo("instance");
                NotificationService.Info("检测到整合包，请在实例页使用「导入整合包」。", "拖拽导入");
                break;
            case DropImportKind.ResourcePack:
                NavigationService.NavigateTo("instance");
                NotificationService.Info("检测到资源包，可在实例页的资源包管理中导入。", "拖拽导入");
                break;
            default:
                NotificationService.Info(
                    $"无法识别拖入的内容（{DropImportClassifier.Describe(kind)}）。支持：整合包 zip、Mod jar/litemod、资源包 zip。",
                    "拖拽导入");
                break;
        }
    }

    private void ImportModFiles(string[] paths) {
        var instancePath = AppServices.SelectedInstancePath;
        if (string.IsNullOrEmpty(instancePath)) {
            NotificationService.Warning("请先在实例页选择一个实例，再拖入 Mod。", "拖拽导入");
            return;
        }
        var modsFolder = System.IO.Path.Combine(instancePath, "mods");
        var copied = 0;
        foreach (var path in paths) {
            if (DropImportClassifier.Classify(path) != DropImportKind.ModFile) continue;
            try {
                System.IO.Directory.CreateDirectory(modsFolder);
                System.IO.File.Copy(path, System.IO.Path.Combine(modsFolder, System.IO.Path.GetFileName(path)), true);
                copied++;
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                Logger.Warn(ex, "拖入复制 Mod 失败：" + path);
            }
        }
        if (copied > 0) {
            NotificationService.Info($"已导入 {copied} 个 Mod 文件。", "拖拽导入");
        } else {
            NotificationService.Warning("没有可导入的 Mod 文件。", "拖拽导入");
        }
    }

    private void NavHome_Click(object sender, RoutedEventArgs e) => ShowView(new LaunchPageView());

    private void NavDownload_Click(object sender, RoutedEventArgs e) => ShowView(new DownloadPageView());

    private void NavInstance_Click(object sender, RoutedEventArgs e) => ShowView(new InstanceView());

    private void NavTools_Click(object sender, RoutedEventArgs e) => ShowView(new PageOtherTest());

    private void NavSettings_Click(object sender, RoutedEventArgs e) => ShowView(new SetupPageView());

    private void NavPages_Click(object sender, RoutedEventArgs e) => ShowView(new PageCatalogView());

    private void NavHelp_Click(object sender, RoutedEventArgs e) => ShowView(new PCL.App.Views.Migrated.PageOtherHelp());

    private void NavAbout_Click(object sender, RoutedEventArgs e) => ShowView(new PageOtherAbout());

}





















