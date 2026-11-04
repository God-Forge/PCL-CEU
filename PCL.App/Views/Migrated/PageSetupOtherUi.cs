using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.App.Controls;
using PCL.App.Services;

using System.IO;
using Brushes = System.Windows.Media.Brushes;
using RadioButton = System.Windows.Controls.RadioButton;
using ComboBox = System.Windows.Controls.ComboBox;
using Color = System.Windows.Media.Color;
using Clipboard = System.Windows.Clipboard;
namespace PCL.App.Views.Migrated;

internal static class PageSetupOtherUi {

    public static IEnumerable<DependencyObject> Descendants(DependencyObject root) {
        var count = GetChildCount(root);
        for (var index = 0; index < count; index++) {
            DependencyObject child;
            try {
                child = VisualTreeHelper.GetChild(root, index);
            } catch {
                continue;
            }
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static int GetChildCount(DependencyObject root) {
        try {
            return VisualTreeHelper.GetChildrenCount(root);
        } catch {
            return 0;
        }
    }

    public static void PrepareBinding(DependencyObject root) {
        PrepareComboValues(root);
        PrepareRadioGroups(root);
    }

    public static int GetRadioValue(DependencyObject element) {
        return int.TryParse(SettingService.GetValue(element), out var value) ? value : 0;
    }

    public static void OpenUrl(string? url) {
        if (string.IsNullOrWhiteSpace(url)) return;
        try {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        } catch (Exception ex) {
            NotificationService.Warning("无法打开网页：" + ex.Message);
        }
    }

    public static void OpenFolder(string? path) {
        if (string.IsNullOrWhiteSpace(path)) return;
        try {
            if (!Directory.Exists(path)) {
                NotificationService.Warning("文件夹不存在：" + path);
                return;
            }
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        } catch (Exception ex) {
            NotificationService.Warning("无法打开文件夹：" + ex.Message);
        }
    }

    public static void CopyText(string? text) {
        if (string.IsNullOrEmpty(text)) return;
        try {
            Clipboard.SetText(text);
        } catch (Exception ex) {
            NotificationService.Warning("复制失败：" + ex.Message);
        }
    }

    private static void PrepareComboValues(DependencyObject root) {
        foreach (var comboBox in Descendants(root).OfType<ComboBox>()) {
            var items = comboBox.Items.OfType<ComboBoxItem>().ToList();
            if (items.Count == 0) continue;
            var values = items.Select(SettingService.GetValue).ToList();
            if (values.All(string.IsNullOrWhiteSpace)) {
                for (var index = 0; index < items.Count; index++) {
                    SettingService.SetValue(items[index], index.ToString());
                }
                continue;
            }
            for (var index = 0; index < items.Count; index++) {
                if (!string.IsNullOrWhiteSpace(values[index])) continue;
                int? inferred = null;
                for (var next = index + 1; next < items.Count; next++) {
                    if (!int.TryParse(values[next], out var nextValue)) continue;
                    inferred = nextValue - (next - index);
                    break;
                }
                if (inferred is null) {
                    for (var previous = index - 1; previous >= 0; previous--) {
                        if (!int.TryParse(values[previous], out var previousValue)) continue;
                        inferred = previousValue + (index - previous);
                        break;
                    }
                }
                if (inferred is not null) {
                    values[index] = inferred.Value.ToString();
                    SettingService.SetValue(items[index], values[index]);
                }
            }
        }
    }

    private static void PrepareRadioGroups(DependencyObject root) {
        var groups = Descendants(root).OfType<RadioButton>()
            .Where(radio => !string.IsNullOrWhiteSpace(SettingService.GetKey(radio)))
            .GroupBy(SettingService.GetKey)
            .ToList();
        foreach (var group in groups) {
            var radios = group.ToList();
            var values = radios.Select(SettingService.GetValue).ToList();
            if (values.Any(string.IsNullOrWhiteSpace)) continue;
            if (!values.All(value => int.TryParse(value, out _))) continue;
            foreach (var radio in radios) SettingService.SetKey(radio, "");
            var defaultValue = radios.Min(radio => GetRadioValue(radio));
            var current = PageSetupOtherSettings.GetInt(group.Key, defaultValue);
            foreach (var radio in radios) {
                radio.IsChecked = GetRadioValue(radio) == current;
            }
            foreach (var radio in radios) {
                radio.Checked += (_, _) => {
                    if (radio.IsChecked == true) AppServices.Settings.Set(group.Key, GetRadioValue(radio), null);
                };
            }
        }
    }

}
