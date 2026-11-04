using System.Windows.Controls;
using System.Windows;
using PCL.App.Services;

namespace PCL.App.Views.Migrated;

public partial class MyResourceItem : System.Windows.Controls.Grid {

    public MyResourceItem() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
        };
    }

}


