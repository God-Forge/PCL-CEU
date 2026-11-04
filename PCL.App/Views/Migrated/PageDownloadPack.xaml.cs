using PCL.App.Controls;
using System.Windows;
using PCL.App.Services;

namespace PCL.App.Views.Migrated;

public partial class PageDownloadPack : PCL.App.Controls.MyPageRight {

    public PageDownloadPack() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
        };
    }

}


