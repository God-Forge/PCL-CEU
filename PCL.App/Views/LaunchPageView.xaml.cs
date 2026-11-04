using PCL.App.Views.Migrated;

namespace PCL.App.Views;

public partial class LaunchPageView : System.Windows.Controls.UserControl {

    public LaunchPageView() {
        InitializeComponent();
        LeftHost.Content = new PageLaunchLeft();
        RightHost.Content = new PageLaunchRight();
    }

}
