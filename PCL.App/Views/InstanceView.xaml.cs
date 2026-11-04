using System.Windows;
using System.Windows.Controls;
using PCL.App.Services;
using PCL.App.Views.Migrated;
using PCL.Core.Minecraft;

namespace PCL.App.Views;

public partial class InstanceView : System.Windows.Controls.UserControl {

    private IReadOnlyList<MinecraftInstanceLocation> _instances = Array.Empty<MinecraftInstanceLocation>();
    private string _page = "overall";

    public InstanceView() {
        InitializeComponent();
        InstanceBox.SelectionChanged += (_, _) => SelectInstance();
        Refresh();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh() {
        var selected = AppServices.SelectedInstanceName;
        _instances = AppServices.MinecraftFolders.ScanAll();
        InstanceBox.ItemsSource = _instances.Select(instance => $"{instance.Version.Id} · {instance.Folder.Name}").ToList();
        var index = _instances.ToList().FindIndex(instance =>
            string.Equals(instance.Version.Folder, selected, StringComparison.OrdinalIgnoreCase));
        InstanceBox.SelectedIndex = index >= 0 ? index : (_instances.Count > 0 ? 0 : -1);
        if (_instances.Count == 0) {
            AppServices.SelectedInstanceName = null;
            PageHost.Content = new TextBlock {
                Text = "没有可用的 Minecraft 实例。请先在设置中添加 Minecraft 文件夹。",
                Margin = new Thickness(20),
                Foreground = System.Windows.Media.Brushes.Gray
            };
        } else {
            ShowPage(_page);
        }
    }

    private void SelectInstance() {
        if (InstanceBox.SelectedIndex < 0 || InstanceBox.SelectedIndex >= _instances.Count) return;
        var instance = _instances[InstanceBox.SelectedIndex];
        AppServices.SelectedInstanceName = instance.Version.Folder;
        AppServices.SelectedMinecraftPath = instance.Folder.Path;
        if (_instances.Count > 0) ShowPage(_page);
    }

    private void ShowPage(string page) {
        _page = page;
        PageHost.Content = page switch {
            "setup" => new PageInstanceSetup(),
            "mod" => new PageInstanceMod(),
            "export" => new PageInstanceExport(),
            _ => new PageInstanceOverall()
        };
    }

    private void TabOverall_Click(object sender, RoutedEventArgs e) => ShowPage("overall");

    private void TabSetup_Click(object sender, RoutedEventArgs e) => ShowPage("setup");

    private void TabMod_Click(object sender, RoutedEventArgs e) => ShowPage("mod");

    private void TabExport_Click(object sender, RoutedEventArgs e) => ShowPage("export");

}
