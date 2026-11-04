using System.Windows;
using System.Windows.Controls;
using PCL.App.Services;

namespace PCL.App.Views;

public partial class SettingsView : System.Windows.Controls.UserControl {

    public SettingsView() {
        InitializeComponent();
        LanguageBox.SelectedIndex = AppServices.Localization.Language == "en_US" ? 1 : 0;
        PathText.Text = $"PCL 数据目录：{AppServices.Paths.Pcl}\r\n默认 Minecraft 目录：{AppServices.Paths.Minecraft}\r\n缓存目录：{AppServices.Paths.Temp}";
        RefreshFolders();
    }

    private void RefreshFolders() {
        FolderList.Text = string.Join("\r\n", AppServices.MinecraftFolders.GetFolders()
            .Select(folder => $"{folder.Name}：{folder.Path}"));
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e) {
        var folder = MeloongCore.Wpf.Dialogs.SelectFolder("选择 Minecraft 文件夹（包含 versions/libraries/assets）", false, AppServices.Paths.Minecraft).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(folder)) return;
        AppServices.MinecraftFolders.AddFolder(System.IO.Path.GetFileName(folder.TrimEnd(System.IO.Path.DirectorySeparatorChar)), folder);
        AppServices.SelectedMinecraftPath = folder;
        RefreshFolders();
        NotificationService.Info("已添加 Minecraft 文件夹。");
    }

    private void Apply_Click(object sender, RoutedEventArgs e) {
        var language = LanguageBox.SelectedIndex == 1 ? "en_US" : "zh_CN";
        AppServices.Localization.Language = language;
        AppServices.Settings.Set("UiLanguage", language);
        PathText.Text = $"已切换语言：{language}\r\nPCL 数据目录：{AppServices.Paths.Pcl}";
    }

}



