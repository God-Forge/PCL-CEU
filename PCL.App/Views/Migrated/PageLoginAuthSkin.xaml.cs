using System.Windows.Controls;
using System.Windows;
using PCL.App.Services;

namespace PCL.App.Views.Migrated;

public partial class PageLoginAuthSkin : System.Windows.Controls.Grid {

    public PageLoginAuthSkin() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            Skin.SetPlayer(AppServices.CurrentLogin);
        };
    }

}


