using PCL.App.Controls;
using System.Windows;
using PCL.App.Services;

namespace PCL.App.Views.Migrated;

public enum DownloadPageCategory {
    Install = 0,
    Mod = 2,
    ModPack = 3,
    DataPack = 4,
    ResourcePack = 5,
    Shader = 6
}

public sealed class DownloadPageSelectionChangedEventArgs : EventArgs {

    public DownloadPageCategory Category { get; init; }
    public int Index => (int)Category;
    public string DisplayName { get; init; } = "";

}

public partial class PageDownloadLeft : PCL.App.Controls.MyPageLeft {

    private readonly Dictionary<DownloadPageCategory, (MyListItem Item, string Name)> _items = new();

    private DownloadPageCategory _selectedCategory = DownloadPageCategory.Install;

    public DownloadPageCategory SelectedCategory => _selectedCategory;
    public int SelectedIndex => (int)_selectedCategory;
    public string SelectedDisplayName => _items.TryGetValue(_selectedCategory, out var entry) ? entry.Name : "";

    public event EventHandler<DownloadPageSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<int>? SelectedIndexChanged;
    public event EventHandler<DownloadPageSelectionChangedEventArgs>? RefreshRequested;

    public PageDownloadLeft() {
        InitializeComponent();
        _items[DownloadPageCategory.Install] = (ItemInstall, "原版游戏");
        _items[DownloadPageCategory.Mod] = (ItemMod, "Mod");
        _items[DownloadPageCategory.ModPack] = (ItemPack, "整合包");
        _items[DownloadPageCategory.DataPack] = (ItemDataPack, "数据包");
        _items[DownloadPageCategory.ResourcePack] = (ItemResourcePack, "资源包");
        _items[DownloadPageCategory.Shader] = (ItemShader, "光影包");
        foreach (var entry in _items) {
            entry.Value.Item.Click += (_, _) => SelectCategory(entry.Key);
            AttachRefreshHandlers(entry.Value.Item, entry.Key);
        }
        ItemInstall.Checked = true;
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
        };
    }

    public void SelectCategory(DownloadPageCategory category) {
        if (!_items.ContainsKey(category)) return;
        foreach (var entry in _items) {
            entry.Value.Item.Checked = entry.Key == category;
        }
        if (_selectedCategory == category) return;
        _selectedCategory = category;
        RaiseSelectionChanged();
    }

    public void SelectCategory(int index) {
        if (Enum.IsDefined(typeof(DownloadPageCategory), index)) {
            SelectCategory((DownloadPageCategory)index);
        }
    }

    public void Refresh() {
        var args = CreateEventArgs();
        RefreshRequested?.Invoke(this, args);
    }

    private void RaiseSelectionChanged() {
        var args = CreateEventArgs();
        SelectionChanged?.Invoke(this, args);
        SelectedIndexChanged?.Invoke(this, args.Index);
    }

    private DownloadPageSelectionChangedEventArgs CreateEventArgs() => new() {
        Category = _selectedCategory,
        DisplayName = SelectedDisplayName
    };

    private void AttachRefreshHandlers(DependencyObject root, DownloadPageCategory category) {
        if (root is MyIconButton button) {
            button.Click += (_, _) => {
                var args = new DownloadPageSelectionChangedEventArgs {
                    Category = category,
                    DisplayName = _items[category].Name
                };
                RefreshRequested?.Invoke(this, args);
            };
        }
        if (root is not System.Windows.Controls.Panel panel) return;
        foreach (System.Windows.UIElement child in panel.Children) {
            AttachRefreshHandlers(child, category);
        }
    }

}
