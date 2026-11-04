using PCL.App.Controls;
using System.Windows;
using PCL.App.Services;

namespace PCL.App.Views.Migrated;

public partial class PageDownloadDataPack : PCL.App.Controls.MyPageRight {

    public PageDownloadDataPack() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
        };
    }

}


