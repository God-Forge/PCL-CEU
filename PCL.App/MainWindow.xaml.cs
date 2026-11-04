using System.Reflection;
using System.Windows;
using PCL.App.Services;
using PCL.App.Views;
using PCL.App.Views.Migrated;

namespace PCL.App;

public partial class MainWindow : Window {

    public MainWindow() {
        InitializeComponent();
        HintService.Initialize(PanHint);
        NavigationService.Initialize(ShowView);
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
        VersionText.Text = $"版本 0.24.0\n程序集 {version}";
        ShowView(new LaunchPageView());
    }

    private void ShowView(UIElement view) => ContentHost.Content = view;

    private void NavHome_Click(object sender, RoutedEventArgs e) => ShowView(new LaunchPageView());

    private void NavDownload_Click(object sender, RoutedEventArgs e) => ShowView(new DownloadPageView());

    private void NavInstance_Click(object sender, RoutedEventArgs e) => ShowView(new InstanceView());

    private void NavTools_Click(object sender, RoutedEventArgs e) => ShowView(new PageOtherTest());

    private void NavSettings_Click(object sender, RoutedEventArgs e) => ShowView(new SetupPageView());

    private void NavPages_Click(object sender, RoutedEventArgs e) => ShowView(new PageCatalogView());

    private void NavHelp_Click(object sender, RoutedEventArgs e) => ShowView(new PCL.App.Views.Migrated.PageOtherHelp());

    private void NavAbout_Click(object sender, RoutedEventArgs e) => ShowView(new PageOtherAbout());

}





















