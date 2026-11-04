using PCL.App.Controls;
using System.Windows;
using System.Windows.Threading;
using PCL.App.Services;

namespace PCL.App.Views.Migrated;

public partial class PageSetupLeft : PCL.App.Controls.MyPageLeft {

    private readonly MyListItem[] _items;
    private bool _initialized;

    public int SelectedIndex { get; private set; }
    public string? SelectedTag { get; private set; }
    public event EventHandler? SelectedIndexChanged;
    public event EventHandler? SelectionChanged;

    public PageSetupLeft() {
        InitializeComponent();
        _items = [ItemLaunch, ItemLink, ItemUI, ItemSystem];
        foreach (var item in _items) item.Click += Item_Click;
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            if (_initialized) return;
            _initialized = true;
            PageSetupOtherSettings.EnsureRegistered();
            RefreshVisibility();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(SelectDefault));
        };
    }

    public void SelectIndex(int index, bool raiseEvent = true) {
        if (index < 0 || index >= _items.Length) return;
        if (_items[index].Visibility != Visibility.Visible) return;
        for (var i = 0; i < _items.Length; i++) _items[i].Checked = i == index;
        SelectedIndex = index;
        SelectedTag = _items[index].Tag?.ToString();
        if (!raiseEvent) return;
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Item_Click(object sender, RoutedEventArgs e) {
        if (sender is not MyListItem item) return;
        var index = Array.IndexOf(_items, item);
        if (index >= 0) SelectIndex(index);
    }

    private void SelectDefault() {
        var visible = _items.Select((item, index) => (item, index))
            .Where(pair => pair.item.Visibility == Visibility.Visible)
            .ToList();
        if (visible.Count == 0) {
            ItemLaunch.Visibility = Visibility.Visible;
            SelectIndex(0);
            return;
        }
        SelectIndex(visible[0].index);
    }

    private void RefreshVisibility() {
        var forceShow = PageSetupUI.HiddenForceShow;
        ItemLaunch.Visibility = forceShow || !PageSetupOtherSettings.GetBool("UiHiddenSetupLaunch") ? Visibility.Visible : Visibility.Collapsed;
        ItemUI.Visibility = forceShow || !PageSetupOtherSettings.GetBool("UiHiddenSetupUi") ? Visibility.Visible : Visibility.Collapsed;
        ItemSystem.Visibility = forceShow || !PageSetupOtherSettings.GetBool("UiHiddenSetupSystem") ? Visibility.Visible : Visibility.Collapsed;
        ItemLink.Visibility = forceShow || !PageSetupOtherSettings.GetBool("UiHiddenSetupLink") ? Visibility.Visible : Visibility.Collapsed;
        var available = _items.Count(item => item.Visibility == Visibility.Visible);
        PanItem.Visibility = forceShow || available >= 2 ? Visibility.Visible : Visibility.Collapsed;
    }

}
