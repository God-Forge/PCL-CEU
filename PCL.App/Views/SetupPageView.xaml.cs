using System.Windows;
using PCL.App.Views.Migrated;

namespace PCL.App.Views;

public partial class SetupPageView : System.Windows.Controls.UserControl {

    private readonly PageSetupLeft _left = new();
    private int _index;

    public SetupPageView() {
        InitializeComponent();
        LeftHost.Content = _left;
        _left.SelectionChanged += (_, _) => Show(_left.SelectedIndex);
        Show(0);
    }

    private void Show(int index) {
        _index = index;
        RightHost.Content = index switch {
            1 => new PageSetupLink(),
            2 => new PageSetupUI(),
            3 => new PageSetupSystem(),
            _ => new PageSetupLaunch()
        };
    }

}
