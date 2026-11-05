using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PCL.App.Controls;
using PCL.App.Services;
using PCL.Core.Java;
using PCL.Core.Memory;
using PCL.Core.Minecraft;
using PCL.Core.Settings;

namespace PCL.App.Views.Migrated;

public partial class PageInstanceSetup : PCL.App.Controls.MyPageRight {

    private readonly DispatcherTimer _ramTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _loading;
    private bool _updatingJava;
    private bool _updatingRam;
    private string? _instancePath;
    private IReadOnlyList<JavaInstallation> _javaList = Array.Empty<JavaInstallation>();
    private int _requiredJavaMajor = 17;
    private int _modCount;
    private int _lastIndieIndex = -1;
    private int _lastServerLoginIndex = -1;

    public event EventHandler? GlobalSettingsRequested;

    public PageInstanceSetup() {
        InitializeComponent();
        PCL.App.Controls.SettingService.SetKey(ComboArgumentJava, "");
        PCL.App.Controls.SettingService.SetKey(RadioRamType2, "");
        PCL.App.Controls.SettingService.SetKey(RadioRamType0, "");
        PCL.App.Controls.SettingService.SetKey(RadioRamType1, "");

        ComboArgumentIndieV2.SelectionChanged += Indie_Changed;
        ComboArgumentJava.SelectionChanged += JavaMode_Changed;
        TextArgumentJavaRange.TextChanged += (_, _) => UpdateJavaHint();
        ComboArgumentJavaSelect.SelectionChanged += JavaSelection_Changed;
        BtnArgumentJavaSearch.Click += async (_, _) => await ScanJavaAsync();

        RadioRamType0.Click += RamType_Changed;
        RadioRamType1.Click += RamType_Changed;
        RadioRamType2.Click += RamType_Changed;
        SliderRamCustom.ValueChanged += RamSlider_Changed;

        ComboServerLogin.SelectionChanged += ServerLogin_Changed;
        TextServerNide.TextChanged += (_, _) => SaveServerLogin();
        TextServerAuthServer.TextChanged += (_, _) => SaveServerLogin();
        TextServerEnter.TextChanged += ServerEnter_Changed;
        BtnServerAuthLittle.Click += LittleSkin_Click;

        TextAdvanceRun.TextChanged += (_, _) => UpdateRunWaitVisibility();
        BtnSwitch.Click += Switch_Click;

        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            RegisterSettings();
            _loading = true;
            try {
                PCL.App.Controls.SettingService.BindAll(this);
                LoadInstance();
            } catch (Exception ex) {
                NotificationService.Warning("加载版本设置失败：" + ex.Message);
            } finally {
                _loading = false;
            }
            _ramTimer.Start();
        };
        Unloaded += (_, _) => _ramTimer.Stop();
        _ramTimer.Tick += (_, _) => RefreshRam(updateSliderMaximum: false);
    }

    private void RegisterSettings() {
        // 实例级设置统一由 AppServices.Initialize 按 InstanceSettingKeys.Definitions 注册，
        // 这里不再重复注册，以免默认值与注册表不一致。
    }

    private void LoadInstance() {
        _instancePath = ResolveInstancePath();
        if (_instancePath is null) {
            CardArgument.IsEnabled = false;
            CardServer.IsEnabled = false;
            CardAdvance.IsEnabled = false;
            RadioRamType0.IsEnabled = false;
            RadioRamType1.IsEnabled = false;
            RadioRamType2.IsEnabled = false;
            SliderRamCustom.IsEnabled = false;
            ComboRamOptimize.IsEnabled = false;
            BtnArgumentJavaSearch.IsEnabled = false;
            ComboArgumentJavaSelect.IsEnabled = false;
            LabArgumentJava.Text = "请先选择版本";
            UpdateJavaUi();
            RefreshRam();
            return;
        }

        _modCount = CountMods(_instancePath);
        _requiredJavaMajor = ReadRequiredJavaMajor(_instancePath);

        var indie = GetInstanceSetting("VersionArgumentIndieV2", true);
        _lastIndieIndex = indie ? 0 : 1;
        ComboArgumentIndieV2.SelectedIndex = _lastIndieIndex;

        var javaMode = GetInstanceSetting("VersionArgumentJavaV2", 0);
        _updatingJava = true;
        ComboArgumentJava.SelectedIndex = Math.Clamp(javaMode, 0, Math.Max(ComboArgumentJava.Items.Count - 1, 0));
        _updatingJava = false;

        var login = GetInstanceSetting("VersionServerLogin", 0);
        if (login < 0 || login >= ComboServerLogin.Items.Count) login = 0;
        _lastServerLoginIndex = login;
        ComboServerLogin.SelectedIndex = login;
        UpdateServerLoginUi();

        LoadMemorySettings();
        UpdateRunWaitVisibility();
        UpdateJavaUi();
        RefreshRam();
        _ = ScanJavaAsync();
    }

    private static string? ResolveInstancePath() {
        try {
            if (!string.IsNullOrWhiteSpace(AppServices.SelectedInstanceName)) return AppServices.SelectedInstancePath;
            var first = AppServices.MinecraftFolders.ScanAll().FirstOrDefault();
            if (first is null) return null;
            AppServices.SelectedInstanceName = first.Version.Folder;
            AppServices.SelectedMinecraftPath = first.Folder.Path;
            return System.IO.Path.Combine(first.Folder.Versions, first.Version.Folder);
        } catch {
            return null;
        }
    }

    private T GetInstanceSetting<T>(string key, T fallback) {
        if (_instancePath is null) return fallback;
        try {
            var value = AppServices.Settings.Get(key, _instancePath);
            if (value is null) return fallback;
            if (value is T typed) return typed;
            if (typeof(T) == typeof(bool)) {
                var text = Convert.ToString(value) ?? "";
                return (T)(object)(bool.TryParse(text, out var parsed) ? parsed : text == "1");
            }
            return (T)Convert.ChangeType(value, typeof(T));
        } catch {
            return fallback;
        }
    }

    private void SetInstanceSetting(string key, object value) {
        if (_instancePath is null) return;
        try {
            AppServices.Settings.Set(key, value, _instancePath);
        } catch {
        }
    }

    private void Indie_Changed(object sender, SelectionChangedEventArgs e) {
        if (_loading || _updatingRam) return;
        var index = ComboArgumentIndieV2.SelectedIndex;
        if (index < 0) return;
        if (_lastIndieIndex >= 0 && index != _lastIndieIndex) {
            var result = System.Windows.MessageBox.Show(
                "调整版本隔离设置后，你需要将游戏存档、Mod 等文件手动迁移到新的游戏文件夹中。\r\n" +
                "如果发现存档消失，把这项设置改回来就能恢复。\r\n\r\n" +
                "如果你不会迁移存档，不建议修改这项设置！",
                "版本隔离", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes) {
                var previous = _lastIndieIndex;
                _loading = true;
                ComboArgumentIndieV2.SelectedIndex = previous;
                _loading = false;
                return;
            }
        }
        _lastIndieIndex = index;
        SetInstanceSetting("VersionArgumentIndieV2", index == 0);
    }

    private async Task ScanJavaAsync() {
        try {
            BtnArgumentJavaSearch.IsEnabled = false;
            LabArgumentJava.Text = "搜索中…";
            var roots = BuildJavaRoots();
            var result = await Task.Run(() => JavaScanner.Scan(roots));
            _javaList = result;
            PopulateJavaList();
            UpdateJavaHint();
        } catch (Exception ex) {
            _javaList = Array.Empty<JavaInstallation>();
            PopulateJavaList();
            LabArgumentJava.Text = "搜索 Java 失败：" + ex.Message;
        } finally {
            BtnArgumentJavaSearch.IsEnabled = true;
        }
    }

    private IEnumerable<string> BuildJavaRoots() {
        var roots = new List<string>();
        try {
            roots.AddRange(JavaScanner.DefaultRoots(AppContext.BaseDirectory, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)));
            roots.Add(AppServices.Paths.Java);
            if (_instancePath is not null) roots.Add(_instancePath);
        } catch {
        }
        return roots.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private void PopulateJavaList() {
        _updatingJava = true;
        try {
            ComboArgumentJavaSelect.Items.Clear();
            foreach (var java in _javaList) {
                ComboArgumentJavaSelect.Items.Add(new ComboBoxItem {
                    Content = $"{java.Version}  ·  {java.JavaHome}",
                    Tag = java.JavaHome
                });
            }
            if (_javaList.Count == 0) {
                ComboArgumentJavaSelect.Items.Add(new ComboBoxItem { Content = "未找到 Java，可手动安装后重新扫描" });
                ComboArgumentJavaSelect.IsEnabled = false;
                LabArgumentJava.Text = "未找到 Java";
                return;
            }

            ComboArgumentJavaSelect.IsEnabled = true;
            var saved = GetInstanceSetting(PCL.Core.Settings.InstanceSettingKeys.JavaSelect, PCL.Core.Settings.InstanceSettingKeys.JavaSelectGlobal);
            if (saved == PCL.Core.Settings.InstanceSettingKeys.JavaSelectGlobal) saved = "";
            var selected = ComboArgumentJavaSelect.Items.OfType<ComboBoxItem>().FirstOrDefault(item =>
                               string.Equals(item.Tag as string, saved, StringComparison.OrdinalIgnoreCase))
                           ?? ComboArgumentJavaSelect.Items[0] as ComboBoxItem;
            ComboArgumentJavaSelect.SelectedItem = selected;
            LabArgumentJava.Text = selected?.Content?.ToString() ?? "";
        } finally {
            _updatingJava = false;
        }
    }

    private void JavaSelection_Changed(object sender, SelectionChangedEventArgs e) {
        if (_loading || _updatingJava) return;
        if (ComboArgumentJavaSelect.SelectedItem is not ComboBoxItem item || item.Tag is not string path) return;
        SetInstanceSetting(PCL.Core.Settings.InstanceSettingKeys.JavaSelect, path);
        LabArgumentJava.Text = item.Content?.ToString() ?? "";
        UpdateJavaHint();
    }

    private void JavaMode_Changed(object sender, SelectionChangedEventArgs e) {
        if (_loading || _updatingJava) return;
        SetInstanceSetting("VersionArgumentJavaV2", Math.Max(ComboArgumentJava.SelectedIndex, 0));
        UpdateJavaUi();
    }

    private void UpdateJavaUi() {
        var mode = ComboArgumentJava.SelectedIndex;
        if (mode < 0) mode = 0;
        var showRange = mode == 1;
        var showSelect = mode == 3;

        TextArgumentJavaRange.Visibility = showRange ? Visibility.Visible : Visibility.Collapsed;
        PanArgumentJavaSelect.Visibility = showSelect ? Visibility.Visible : Visibility.Collapsed;
        HintArgumentJava.Visibility = showSelect ? Visibility.Collapsed : Visibility.Visible;

        if (showRange && string.IsNullOrWhiteSpace(TextArgumentJavaRange.Text) && _instancePath is not null) {
            TextArgumentJavaRange.Text = $"[{_requiredJavaMajor}, )";
        }
        UpdateJavaHint();
    }

    private void UpdateJavaHint() {
        if (ComboArgumentJava.SelectedIndex == 3) return;
        var best = _javaList.FirstOrDefault(java => java.Major >= _requiredJavaMajor) ?? _javaList.FirstOrDefault();
        if (ComboArgumentJava.SelectedIndex == 1) {
            if (string.IsNullOrWhiteSpace(TextArgumentJavaRange.Text)) {
                HintArgumentJava.Text = "请输入 Java 版本范围，例如 [17, )。";
                HintArgumentJava.Theme = "Red";
            } else if (best is null) {
                HintArgumentJava.Text = "没有找到符合该范围的 Java，启动游戏时可能会自动下载。";
                HintArgumentJava.Theme = "Yellow";
            } else {
                HintArgumentJava.Text = $"将按版本范围选择 Java；当前可用：{best.Version}";
                HintArgumentJava.Theme = "Blue";
            }
            return;
        }

        if (_javaList.Count == 0) {
            HintArgumentJava.Text = "未找到可用的 Java，启动游戏时会尝试自动下载。";
            HintArgumentJava.Theme = "Yellow";
        } else if (best is null) {
            HintArgumentJava.Text = "未找到符合该版本要求的 Java。";
            HintArgumentJava.Theme = "Yellow";
        } else {
            HintArgumentJava.Text = $"将会使用：{best.Version}（{best.JavaHome}）";
            HintArgumentJava.Theme = "Blue";
        }
    }

    private void LoadMemorySettings() {
        var type = GetInstanceSetting("VersionRamType", 2);
        if (type is < 0 or > 2) type = 2;
        _updatingRam = true;
        try {
            RadioRamType0.IsChecked = type == 0;
            RadioRamType1.IsChecked = type == 1;
            RadioRamType2.IsChecked = type == 2;
            SliderRamCustom.IsEnabled = type == 1;
        } finally {
            _updatingRam = false;
        }
    }

    private void RamType_Changed(object sender, RoutedEventArgs e) {
        if (_loading || _updatingRam) return;
        var type = RadioRamType1.IsChecked == true ? 1 : RadioRamType0.IsChecked == true ? 0 : 2;
        SetInstanceSetting("VersionRamType", type);
        SliderRamCustom.IsEnabled = type == 1;
        RefreshRam();
    }

    private void RamSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) {
        if (_loading || _updatingRam) return;
        RefreshRam(updateSliderMaximum: false);
    }

    private int CurrentRamType() {
        if (RadioRamType1.IsChecked == true) return 1;
        if (RadioRamType0.IsChecked == true) return 0;
        return 2;
    }

    private double CalculateRam(int type, double availableGb) {
        if (_instancePath is null) return 0;
        if (type == 1) return CustomValueToGb(SliderRamCustom.Value);
        if (type == 0) return CalculateAutoRam(availableGb);
        var globalType = ToInt(ReadNormalSetting("LaunchRamType", 0), 0);
        if (globalType == 1) return CustomValueToGb(ToDouble(ReadNormalSetting("LaunchRamCustom", 13d), 13d));
        return CalculateAutoRam(availableGb);
    }

    private double CalculateAutoRam(double availableGb) {
        double minimum;
        double target1;
        double target2;
        double target3;
        if (_modCount > 0) {
            minimum = 0.5 + _modCount / 150d;
            target1 = 1.5 + _modCount / 90d;
            target2 = 2.7 + _modCount / 50d;
            target3 = 4.5 + _modCount / 25d;
        } else {
            minimum = 0.5;
            target1 = 1.5;
            target2 = 2.5;
            target3 = 4;
        }

        var give = 0d;
        var available = Math.Max(availableGb, 0);
        give += Math.Min(available, target1);
        available -= target1;
        if (available >= 0.1) {
            give += Math.Min(available * 0.7, target2 - target1);
            available -= (target2 - target1) / 0.7;
        }
        if (available >= 0.1) {
            give += Math.Min(available * 0.4, target3 - target2);
            available -= (target3 - target2) / 0.4;
        }
        if (available >= 0.1) give += Math.Min(available * 0.15, target3);
        return Math.Round(Math.Max(give, minimum), 1);
    }

    private static double CustomValueToGb(double value) {
        if (value <= 12) return value * 0.1 + 0.3;
        if (value <= 25) return (value - 12) * 0.5 + 1.5;
        if (value <= 33) return (value - 25) + 8;
        return (value - 33) * 2 + 16;
    }

    private void RefreshRam(bool updateSliderMaximum = true) {
        try {
            var status = MemoryOptimizer.GetStatus();
            var total = status.TotalMB / 1024d;
            var available = Math.Max(status.AvailableMB / 1024d, 0);
            if (updateSliderMaximum) UpdateSliderMaximum(total);

            var requested = Math.Max(CalculateRam(CurrentRamType(), available), 0);
            var actual = Math.Min(requested, available);
            var used = Math.Max(total - available, 0);
            var empty = Math.Max(total - used - actual, 0);

            ColumnRamUsed.Width = new GridLength(Math.Max(used, 0.01), GridUnitType.Star);
            ColumnRamGame.Width = new GridLength(Math.Max(actual, 0.01), GridUnitType.Star);
            ColumnRamEmpty.Width = new GridLength(Math.Max(empty, 0.01), GridUnitType.Star);

            LabRamGame.Text = FormatGb(actual) + " GB" +
                              (requested - actual > 0.05 ? $"（可用 {FormatGb(actual)} GB）" : "");
            LabRamUsed.Text = FormatGb(used) + " GB";
            LabRamTotal.Text = " / " + FormatGb(total) + " GB";
            SliderRamCustom.IsEnabled = _instancePath is not null && CurrentRamType() == 1;
        } catch {
            LabRamGame.Text = "不可用";
            LabRamUsed.Text = "不可用";
            LabRamTotal.Text = "";
            SliderRamCustom.IsEnabled = false;
        }
    }

    private void UpdateSliderMaximum(double totalGb) {
        var maximum = 1d;
        if (totalGb <= 1.5) {
            maximum = Math.Max(Math.Floor((totalGb - 0.3) / 0.1), 1);
        } else if (totalGb <= 8) {
            maximum = Math.Floor((totalGb - 1.5) / 0.5) + 12;
        } else if (totalGb <= 16) {
            maximum = Math.Floor(totalGb - 8) + 25;
        } else {
            maximum = Math.Floor((totalGb - 16) / 2) + 33;
        }
        maximum = Math.Max(maximum, 1);
        if (Math.Abs(SliderRamCustom.MaxValue - maximum) < 0.01) return;
        _updatingRam = true;
        try {
            SliderRamCustom.MaxValue = maximum;
        } finally {
            _updatingRam = false;
        }
    }

    private static string FormatGb(double value) => value.ToString("0.0");

    private static object? ReadNormalSetting(string key, object defaultValue) {
        try {
            return AppServices.Settings.Get(key);
        } catch (KeyNotFoundException) {
            try {
                AppServices.Settings.Register(key, defaultValue);
                return AppServices.Settings.Get(key);
            } catch {
                return defaultValue;
            }
        } catch {
            return defaultValue;
        }
    }

    private static int ToInt(object? value, int fallback) {
        try {
            if (value is bool flag) return flag ? 1 : 0;
            return Convert.ToInt32(value);
        } catch {
            return fallback;
        }
    }

    private static double ToDouble(object? value, double fallback) {
        try {
            return Convert.ToDouble(value);
        } catch {
            return fallback;
        }
    }

    private static int CountMods(string instancePath) {
        try {
            var folder = InstanceManagementService.GetFolders(instancePath).Mods;
            if (!Directory.Exists(folder)) return 0;
            return Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly).Count(file => {
                var extension = System.IO.Path.GetExtension(file).ToLowerInvariant();
                return (extension is ".jar" or ".zip" or ".litemod")
                       || file.EndsWith(".jar.disabled", StringComparison.OrdinalIgnoreCase);
            });
        } catch {
            return 0;
        }
    }

    private static int ReadRequiredJavaMajor(string instancePath) {
        try {
            var major = MinecraftVersionScanner.ScanVersion(instancePath).JavaVersionMajor;
            return major > 0 ? major : 17;
        } catch {
            return 17;
        }
    }

    private void ServerLogin_Changed(object sender, SelectionChangedEventArgs e) {
        if (_loading) return;
        UpdateServerLoginUi();
        SaveServerLogin();
    }

    private void UpdateServerLoginUi() {
        var type = ComboServerLogin.SelectedIndex;
        var nide = type == 3;
        var auth = type == 4;
        LabServerNide.Visibility = nide ? Visibility.Visible : Visibility.Collapsed;
        TextServerNide.Visibility = nide ? Visibility.Visible : Visibility.Collapsed;
        PanServerNide.Visibility = nide ? Visibility.Visible : Visibility.Collapsed;
        LabServerAuthServer.Visibility = auth ? Visibility.Visible : Visibility.Collapsed;
        TextServerAuthServer.Visibility = auth ? Visibility.Visible : Visibility.Collapsed;
        LabServerAuthRegister.Visibility = auth ? Visibility.Visible : Visibility.Collapsed;
        TextServerAuthRegister.Visibility = auth ? Visibility.Visible : Visibility.Collapsed;
        LabServerAuthName.Visibility = auth ? Visibility.Visible : Visibility.Collapsed;
        TextServerAuthName.Visibility = auth ? Visibility.Visible : Visibility.Collapsed;
        BtnServerAuthLittle.Visibility = auth ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveServerLogin() {
        if (_loading || _instancePath is null) return;
        var type = ComboServerLogin.SelectedIndex;
        if (type == 3 && string.IsNullOrWhiteSpace(TextServerNide.Text)) return;
        if (type == 4 && string.IsNullOrWhiteSpace(TextServerAuthServer.Text)) return;
        if (type == _lastServerLoginIndex && _lastServerLoginIndex >= 0) return;
        _lastServerLoginIndex = type;
        SetInstanceSetting("VersionServerLogin", type);
    }

    private void ServerEnter_Changed(object sender, TextChangedEventArgs e) {
        if (_loading) return;
        var normalized = TextServerEnter.Text.Replace('：', ':').Replace('。', '.');
        if (string.Equals(normalized, TextServerEnter.Text, StringComparison.Ordinal)) return;
        var caret = TextServerEnter.CaretIndex;
        TextServerEnter.Text = normalized;
        TextServerEnter.CaretIndex = Math.Min(caret, normalized.Length);
    }

    private void LittleSkin_Click(object sender, RoutedEventArgs e) {
        if (!string.IsNullOrWhiteSpace(TextServerAuthServer.Text)
            && !string.Equals(TextServerAuthServer.Text, "https://littleskin.cn/api/yggdrasil", StringComparison.OrdinalIgnoreCase)) {
            var result = System.Windows.MessageBox.Show(
                "即将把第三方登录设置覆盖为 LittleSkin 登录。\r\n" +
                "除非你是服主，或者服主要求你这样做，否则请不要继续。\r\n\r\n" +
                "是否确实需要覆盖当前设置？",
                "设置覆盖确认", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (result != MessageBoxResult.Yes) return;
        }
        ComboServerLogin.SelectedIndex = 4;
        TextServerAuthServer.Text = "https://littleskin.cn/api/yggdrasil";
        TextServerAuthRegister.Text = "https://littleskin.cn/auth/register";
        TextServerAuthName.Text = "LittleSkin 登录";
        SaveServerLogin();
    }

    private void UpdateRunWaitVisibility() {
        CheckAdvanceRunWait.Visibility = string.IsNullOrWhiteSpace(TextAdvanceRun.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void Switch_Click(object sender, RoutedEventArgs e) {
        if (GlobalSettingsRequested is not null) {
            GlobalSettingsRequested(this, EventArgs.Empty);
            return;
        }
        NotificationService.Info("请在主界面进入全局设置。");
    }

}
