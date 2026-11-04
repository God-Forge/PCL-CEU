using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PCL.Core.Localization;
using PCL.Core.Paths;
using PCL.Core.Settings;

namespace PCL.App.Services;

public static class AppServices {

    public static PclPathService Paths { get; private set; } = null!;
    public static SettingsService Settings { get; private set; } = null!;
    public static LocalizationService Localization { get; private set; } = null!;
    public static PCL.Core.Minecraft.MinecraftFolderService MinecraftFolders { get; private set; } = null!;
    public static string? SelectedInstanceName { get; set; }
    public static string? SelectedMinecraftPath { get; set; }
    public static PCL.Core.Minecraft.MinecraftFolder SelectedMinecraftFolder =>
        MinecraftFolders.GetFolders().FirstOrDefault(folder => string.Equals(folder.Path, SelectedMinecraftPath, StringComparison.OrdinalIgnoreCase))
        ?? MinecraftFolders.Default;
    public static PCL.Core.Login.LoginProfile? CurrentLogin { get; set; }
    public static string? SelectedInstancePath => SelectedInstanceName is null
        ? null
        : System.IO.Path.Combine(SelectedMinecraftFolder.Versions, SelectedInstanceName);

    public static void Initialize() {
        Paths = new PclPathService(AppContext.BaseDirectory);
        Paths.EnsureDirectories();
        Settings = new SettingsService(AppContext.BaseDirectory);
        Settings.Register("SystemDebugMode", false);
        Settings.Register("SystemSystemCache", "");
        Settings.Register("UiLanguage", "zh_CN");
        Settings.Register("LoginLegacyName", "");
        Settings.Register("VersionServerAuthServer", "");
        Settings.Register("VersionServerNide", "");
        Settings.Register("LaunchFolders", "");
        Settings.Register("UiMusicAuto", false);
        Settings.Register("UiMusicRandom", false);
        Settings.Register("UiMusicVolume", 60);
        MinecraftFolders = new PCL.Core.Minecraft.MinecraftFolderService(Paths, Settings);
        Localization = new LocalizationService();
        Localization.Language = Settings.Get<string>("UiLanguage") ?? "zh_CN";
    }

}

public static class DispatcherService {

    public static void Invoke(Action action) {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    public static Task InvokeAsync(Action action) {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) {
            action();
            return Task.CompletedTask;
        }
        return dispatcher.InvokeAsync(action).Task;
    }

    public static Task RunAsync(Func<Task> action) => Task.Run(action);

}

public static class NotificationService {

    public static void Info(string message, string? title = null) {
        Show(message, title ?? AppServices.Localization.Get("common.info"), MessageBoxImage.Information);
    }

    public static void Warning(string message, string? title = null) {
        Show(message, title ?? AppServices.Localization.Get("common.warning"), MessageBoxImage.Warning);
    }

    public static void Error(string message, string? title = null) {
        Show(message, title ?? AppServices.Localization.Get("common.error"), MessageBoxImage.Error);
    }

    public static void Show(string message, string title, MessageBoxImage image = MessageBoxImage.Information) {
        DispatcherService.Invoke(() => System.Windows.MessageBox.Show(message, title, MessageBoxButton.OK, image));
    }

}

public static class ClipboardService {

    public static void SetText(string text) => DispatcherService.Invoke(() => System.Windows.Clipboard.SetText(text));

    public static string GetText() {
        var result = "";
        DispatcherService.Invoke(() => result = System.Windows.Clipboard.GetText());
        return result;
    }

}

public sealed class SingleInstanceService : IDisposable {

    private Mutex? _mutex;

    public bool TryAcquire(string name = "PCL-CEU-CSharp") {
        _mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        return createdNew;
    }

    public void Dispose() {
        try {
            _mutex?.ReleaseMutex();
        } catch {
        }
        _mutex?.Dispose();
        _mutex = null;
    }

}

public static class ThemeService {

    public static void Apply(System.Windows.Application application) {
        application.Resources["PclAccentBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
        application.Resources["PclAccentDarkBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 90, 158));
        application.Resources["PclTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 32));
        application.Resources["PclSubtleTextBrush"] = new SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102));
        application.Resources["PclFontFamily"] = new System.Windows.Media.FontFamily("Microsoft YaHei UI");
    }

    public static double GetDpi(Visual visual) {
        return VisualTreeHelper.GetDpi(visual).PixelsPerDip;
    }

}







