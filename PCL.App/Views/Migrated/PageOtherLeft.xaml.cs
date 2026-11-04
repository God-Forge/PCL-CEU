using PCL.App.Controls;
using System.Windows;
using System.Windows.Threading;
using PCL.App.Services;

namespace PCL.App.Views.Migrated;

public partial class PageOtherLeft : PCL.App.Controls.MyPageLeft {

    private const string FeedbackUrl = "https://github.com/Meloong-Git/PCL/issues/";
    private const string VoteUrl = "https://github.com/Meloong-Git/PCL/discussions/categories/%E5%8A%9F%E8%83%BD%E6%8A%95%E7%A5%A8";

    private readonly MyListItem[] _items;
    private bool _initialized;

    public int SelectedIndex { get; private set; }
    public string? SelectedTag { get; private set; }
    public event EventHandler? SelectedIndexChanged;
    public event EventHandler? SelectionChanged;

    public PageOtherLeft() {
        InitializeComponent();
        _items = [ItemHelp, ItemAbout, ItemTest, ItemFeedback, ItemVote];
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
        if (index < 0 || index >= 3) return;
        if (_items[index].Visibility != Visibility.Visible) return;
        for (var i = 0; i < 3; i++) _items[i].Checked = i == index;
        SelectedIndex = index;
        SelectedTag = _items[index].Tag?.ToString();
        if (!raiseEvent) return;
        SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Item_Click(object sender, RoutedEventArgs e) {
        if (sender is not MyListItem item) return;
        var index = Array.IndexOf(_items, item);
        if (index < 0) return;
        if (index == 3) {
            item.Checked = false;
            PageSetupOtherUi.OpenUrl(FeedbackUrl);
            return;
        }
        if (index == 4) {
            item.Checked = false;
            PageSetupOtherUi.OpenUrl(VoteUrl);
            return;
        }
        SelectIndex(index);
    }

    private void SelectDefault() {
        var visible = _items.Take(3).Select((item, index) => (item, index))
            .Where(pair => pair.item.Visibility == Visibility.Visible)
            .ToList();
        if (visible.Count == 0) {
            ItemHelp.Visibility = Visibility.Visible;
            SelectIndex(0);
            return;
        }
        SelectIndex(visible[0].index);
    }

    private void RefreshVisibility() {
        var forceShow = PageSetupUI.HiddenForceShow;
        ItemHelp.Visibility = forceShow || !PageSetupOtherSettings.GetBool("UiHiddenOtherHelp") ? Visibility.Visible : Visibility.Collapsed;
        ItemAbout.Visibility = forceShow || !PageSetupOtherSettings.GetBool("UiHiddenOtherAbout") ? Visibility.Visible : Visibility.Collapsed;
        ItemTest.Visibility = forceShow || !PageSetupOtherSettings.GetBool("UiHiddenOtherTest") ? Visibility.Visible : Visibility.Collapsed;
        ItemFeedback.Visibility = forceShow || !PageSetupOtherSettings.GetBool("UiHiddenOtherFeedback") ? Visibility.Visible : Visibility.Collapsed;
        ItemVote.Visibility = forceShow || !PageSetupOtherSettings.GetBool("UiHiddenOtherVote") ? Visibility.Visible : Visibility.Collapsed;
        var available = _items.Count(item => item.Visibility == Visibility.Visible);
        PanItem.Visibility = forceShow || available >= 2 ? Visibility.Visible : Visibility.Collapsed;
    }

}
