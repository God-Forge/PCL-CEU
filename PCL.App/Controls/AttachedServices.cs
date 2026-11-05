using System.Windows;
using System.Windows.Media;
using MeloongCore;
using PCL.App.Services;
using PCL.Core.Settings;
using PCL.Core.Ui;

namespace PCL.App.Controls;

public static class SettingService {

    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.RegisterAttached("Key", typeof(string), typeof(SettingService), new PropertyMetadata(""));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.RegisterAttached("Value", typeof(string), typeof(SettingService), new PropertyMetadata(""));

    public static string GetKey(DependencyObject element) => (string)element.GetValue(KeyProperty);

    public static void BindAll(DependencyObject root) {
        var instancePath = AppServices.SelectedInstancePath;
        BindElement(root, instancePath);
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) {
            BindAll(VisualTreeHelper.GetChild(root, i));
        }
    }

    private static void BindElement(DependencyObject element, string? instancePath) {
        var key = GetKey(element);
        if (string.IsNullOrWhiteSpace(key)) return;
        try {
            switch (element) {
                case System.Windows.Controls.TextBox textBox: {
                    var value = GetOrRegister(key, textBox.Text, instancePath);
                    textBox.Text = Convert.ToString(value) ?? "";
                    textBox.TextChanged += (_, _) => AppServices.Settings.Set(key, textBox.Text, instancePath);
                    break;
                }
                case System.Windows.Controls.CheckBox checkBox: {
                    var value = GetOrRegister(key, checkBox.IsChecked == true, instancePath);
                    checkBox.IsChecked = value is bool flag && flag;
                    checkBox.Checked += (_, _) => AppServices.Settings.Set(key, true, instancePath);
                    checkBox.Unchecked += (_, _) => AppServices.Settings.Set(key, false, instancePath);
                    break;
                }
                case System.Windows.Controls.ComboBox comboBox: {
                    var defaultValue = comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
                        ? (GetValue(item) ?? item.Content?.ToString() ?? "")
                        : (comboBox.Text ?? "");
                    var value = GetOrRegister(key, defaultValue, instancePath);
                    comboBox.SelectedItem = FindComboItem(comboBox, Convert.ToString(value));
                    comboBox.SelectionChanged += (_, _) => {
                        var selected = comboBox.SelectedItem is System.Windows.Controls.ComboBoxItem item
                            ? (GetValue(item) ?? item.Content?.ToString() ?? "")
                            : (comboBox.Text ?? "");
                        AppServices.Settings.Set(key, selected, instancePath);
                    };
                    break;
                }
                case System.Windows.Controls.Slider slider: {
                    var value = GetOrRegister(key, slider.Value, instancePath);
                    slider.Value = Convert.ToDouble(value);
                    slider.ValueChanged += (_, e) => AppServices.Settings.Set(key, e.NewValue, instancePath);
                    break;
                }
                case System.Windows.Controls.RadioButton radioButton: {
                    var value = GetOrRegister(key, radioButton.IsChecked == true, instancePath);
                    radioButton.IsChecked = value is bool flag && flag;
                    radioButton.Checked += (_, _) => AppServices.Settings.Set(key, true, instancePath);
                    radioButton.Unchecked += (_, _) => AppServices.Settings.Set(key, false, instancePath);
                    break;
                }
            }
        } catch {
        }
    }

    private static object? GetOrRegister(string key, object? defaultValue, string? instancePath) {
        try {
            return AppServices.Settings.Get(key, instancePath);
        } catch (KeyNotFoundException) {
            var source = key.StartsWith("Version", StringComparison.Ordinal) ? SettingSource.Instance : SettingSource.Normal;
            AppServices.Settings.Register(key, defaultValue, source);
            return AppServices.Settings.Get(key, instancePath);
        }
    }

    private static System.Windows.Controls.ComboBoxItem? FindComboItem(System.Windows.Controls.ComboBox comboBox, string? value) {
        return comboBox.Items.OfType<System.Windows.Controls.ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(GetValue(item) ?? item.Content?.ToString(), value, StringComparison.Ordinal));
    }
    public static void SetKey(DependencyObject element, string value) => element.SetValue(KeyProperty, value);
    public static string GetValue(DependencyObject element) => (string)element.GetValue(ValueProperty);
    public static void SetValue(DependencyObject element, string value) => element.SetValue(ValueProperty, value);

}

public static class CustomEventService {

    public static readonly DependencyProperty EventTypeProperty =
        DependencyProperty.RegisterAttached("EventType", typeof(string), typeof(CustomEventService), new PropertyMetadata(""));

    public static readonly DependencyProperty EventDataProperty =
        DependencyProperty.RegisterAttached("EventData", typeof(string), typeof(CustomEventService), new PropertyMetadata(""));

    public static string GetEventType(DependencyObject element) => (string)element.GetValue(EventTypeProperty);
    public static void SetEventType(DependencyObject element, string value) => element.SetValue(EventTypeProperty, value);
    public static string GetEventData(DependencyObject element) => (string)element.GetValue(EventDataProperty);
    public static void SetEventData(DependencyObject element, string value) => element.SetValue(EventDataProperty, value);

    public static void Raise(string? type, string? data) {
        var eventType = CustomEventParser.ParseType(type);
        if (eventType == CustomEventType.None) return;
        if (CustomEventParser.IsRestricted(eventType)) {
            Logger.Warn($"自定义事件「{type}」出于安全考虑未执行");
            return;
        }
        var arguments = CustomEventParser.SplitArguments(data);
        var target = arguments.FirstOrDefault() ?? "";
        try {
            switch (eventType) {
                case CustomEventType.OpenWebsite:
                    if (!CustomEventParser.IsValidUrl(target, out var urlError)) {
                        NotificationService.Warning(urlError ?? "网址无效。", "自定义事件");
                        break;
                    }
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                        FileName = target.Replace('\\', '/'),
                        UseShellExecute = true
                    });
                    break;
                case CustomEventType.OpenFile:
                    if (System.IO.File.Exists(target) || System.IO.Directory.Exists(target)) {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = target, UseShellExecute = true });
                    } else {
                        NotificationService.Warning("目标不存在：" + target, "自定义事件");
                    }
                    break;
                case CustomEventType.OpenHelp:
                    NavigationService.Navigate(new Views.Migrated.PageOtherHelp(), "help");
                    break;
                case CustomEventType.CopyText:
                    if (!string.IsNullOrWhiteSpace(target)) ClipboardService.SetText(target);
                    break;
                case CustomEventType.ShowHint:
                    if (!string.IsNullOrWhiteSpace(target)) HintService.Show(target);
                    break;
                case CustomEventType.ShowWindow:
                    if (!string.IsNullOrWhiteSpace(target)) DialogService.ShowText(target, "PCL CEU");
                    break;
                case CustomEventType.SwitchPage:
                    NavigationService.NavigateTo(target);
                    break;
                default:
                    Logger.Info($"自定义事件「{type}」暂未实现，已忽略。");
                    break;
            }
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "执行自定义事件失败：" + type);
        }
    }

}



