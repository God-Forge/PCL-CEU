using System.Windows;
using PCL.App.Views.Migrated;

namespace PCL.App.Views;

public partial class DownloadPageView : System.Windows.Controls.UserControl {

    private readonly PageDownloadLeft _left = new();
    private DownloadPageCategory _category = DownloadPageCategory.Install;

    public DownloadPageView() {
        InitializeComponent();
        LeftHost.Content = _left;
        _left.SelectionChanged += (_, args) => Show(args.Category);
        Show(_category);
    }

    private void Show(DownloadPageCategory category) {
        _category = category;
        RightHost.Content = category switch {
            DownloadPageCategory.Mod => new PageDownloadMod(),
            DownloadPageCategory.ModPack => new PageDownloadPack(),
            DownloadPageCategory.DataPack => new PageDownloadDataPack(),
            DownloadPageCategory.ResourcePack => new PageDownloadResourcePack(),
            DownloadPageCategory.Shader => new PageDownloadShader(),
            _ => new PageDownloadInstall()
        };
    }

}
