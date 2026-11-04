using System.Windows.Controls;
using System.Windows;
using PCL.App.Services;

namespace PCL.App.Views.Migrated;

public partial class PageLoginMsSkin : System.Windows.Controls.Grid {

    public PageLoginMsSkin() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            Skin.SetPlayer(AppServices.CurrentLogin);
        };
    }

}


