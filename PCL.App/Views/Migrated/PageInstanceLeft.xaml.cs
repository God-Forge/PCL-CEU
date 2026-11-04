using System.IO;
using System.Windows;
using System.Windows.Media;
using PCL.App.Controls;
using PCL.App.Services;

namespace PCL.App.Views.Migrated;

public enum InstancePageCategory {
    Overall = 0,
    Setup = 1,
    Modify = 2,
    Mod = 3,
    ModDisabled = 4,
    Export = 5
}

public sealed class InstancePageSelectionChangedEventArgs : EventArgs {

    public InstancePageCategory Category { get; }
    public int Index => (int)Category;

    public InstancePageSelectionChangedEventArgs(InstancePageCategory category) {
        Category = category;
    }

}

public partial class PageInstanceLeft : PCL.App.Controls.MyPageLeft {

    private readonly Dictionary<MyListItem, InstancePageCategory> _items = new();

    public event EventHandler<InstancePageSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<InstancePageSelectionChangedEventArgs>? SelectedIndexChanged;

    public InstancePageCategory SelectedCategory { get; private set; } = InstancePageCategory.Overall;
    public int SelectedIndex => (int)SelectedCategory;

    public PageInstanceLeft() {
        InitializeComponent();
        Wire(ItemOverall, InstancePageCategory.Overall);
        Wire(ItemSetup, InstancePageCategory.Setup);
        Wire(ItemModify, InstancePageCategory.Modify);
        Wire(ItemMod, InstancePageCategory.Mod);
        Wire(ItemModDisabled, InstancePageCategory.ModDisabled);
        Wire(ItemExport, InstancePageCategory.Export);
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            RefreshModAvailability();
            ApplySelection(SelectedCategory, raiseEvent: false);
        };
    }

    public void SelectCategory(InstancePageCategory category, bool raiseEvent = true) {
        ApplySelection(category, raiseEvent);
    }

    public void Refresh() {
        RefreshModAvailability();
        ApplySelection(SelectedCategory, raiseEvent: false);
    }

    private void Wire(MyListItem item, InstancePageCategory category) {
        _items[item] = category;
        item.Click += Category_Click;
    }

    private void Category_Click(object sender, RoutedEventArgs e) {
        if (sender is not MyListItem item || !_items.TryGetValue(item, out var category)) return;
        if (IsFromIconButton(e.OriginalSource as DependencyObject)) return;
        ApplySelection(category, raiseEvent: true);
    }

    private void ApplySelection(InstancePageCategory category, bool raiseEvent) {
        SelectedCategory = category;
        foreach (var pair in _items) pair.Key.Checked = pair.Value == category;
        if (raiseEvent) {
            var args = new InstancePageSelectionChangedEventArgs(category);
            SelectionChanged?.Invoke(this, args);
            SelectedIndexChanged?.Invoke(this, args);
        }
    }

    private static bool IsFromIconButton(DependencyObject? source) {
        var guard = 0;
        while (source is not null && guard++ < 32) {
            if (source is MyIconButton) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private void RefreshModAvailability() {
        var modable = IsModable(AppServices.SelectedInstancePath);
        ItemMod.Visibility = modable ? Visibility.Visible : Visibility.Collapsed;
        ItemModDisabled.Visibility = modable ? Visibility.Collapsed : Visibility.Visible;
        if (SelectedCategory == InstancePageCategory.Mod && !modable) {
            ApplySelection(InstancePageCategory.ModDisabled, raiseEvent: false);
        } else if (SelectedCategory == InstancePageCategory.ModDisabled && modable) {
            ApplySelection(InstancePageCategory.Mod, raiseEvent: false);
        }
    }

    private static bool IsModable(string? instancePath) {
        if (string.IsNullOrWhiteSpace(instancePath) || !Directory.Exists(instancePath)) return false;
        try {
            var json = Directory.EnumerateFiles(instancePath, "*.json", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (json is null) return false;
            var text = File.ReadAllText(json);
            return text.Contains("fabric", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("forge", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("quilt", StringComparison.OrdinalIgnoreCase)
                   || text.Contains("launchwrapper", StringComparison.OrdinalIgnoreCase);
        } catch {
            return false;
        }
    }

}
