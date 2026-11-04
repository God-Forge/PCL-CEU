using PCL.App.Services;
using PCL.Core.Settings;

namespace PCL.App.Views.Migrated;

internal static class PageSetupOtherSettings {

    public const string DefaultJvm =
        "-XX:-OmitStackTraceInFastThrow -Djdk.lang.Process.allowAmbiguousCommands=True -Dfml.ignoreInvalidMinecraftCertificates=True -Dfml.ignorePatchDiscrepancies=True";

    public const string Identify = "0000-0000-0000-0000";

    public const string CurrentVersion = "0.23.0";

    public const string UpdateApiUrl = "https://api.github.com/repos/God-Forge/PCL-CEU/releases/latest";

    private static SettingsService? _owner;

    public static void EnsureRegistered() {
        if (ReferenceEquals(_owner, AppServices.Settings)) return;
        _owner = AppServices.Settings;
        RegisterLaunch();
        RegisterSystem();
        RegisterUi();
        RegisterLink();
    }

    public static bool GetBool(string key, bool fallback = false) {
        try {
            return AppServices.Settings.Get(key) switch {
                bool value => value,
                int value => value != 0,
                string value when bool.TryParse(value, out var parsed) => parsed,
                string value => value == "1",
                _ => fallback
            };
        } catch (KeyNotFoundException) {
            AppServices.Settings.Register(key, fallback);
            try {
                return Convert.ToBoolean(AppServices.Settings.Get(key));
            } catch {
                return fallback;
            }
        } catch {
            return fallback;
        }
    }

    public static int GetInt(string key, int fallback = 0) {
        try {
            return Convert.ToInt32(AppServices.Settings.Get(key));
        } catch (KeyNotFoundException) {
            AppServices.Settings.Register(key, fallback);
            try {
                return Convert.ToInt32(AppServices.Settings.Get(key));
            } catch {
                return fallback;
            }
        } catch {
            return fallback;
        }
    }

    public static string GetString(string key, string fallback = "") {
        try {
            return Convert.ToString(AppServices.Settings.Get(key)) ?? fallback;
        } catch (KeyNotFoundException) {
            AppServices.Settings.Register(key, fallback);
            try {
                return Convert.ToString(AppServices.Settings.Get(key)) ?? fallback;
            } catch {
                return fallback;
            }
        } catch {
            return fallback;
        }
    }

    private static void RegisterLaunch() {
        var settings = AppServices.Settings;
        settings.Register("LaunchArgumentIndieV2", 4);
        settings.Register("LaunchArgumentTitle", "");
        settings.Register("LaunchArgumentInfo", "PCL");
        settings.Register("LaunchArgumentVisible", 5, SettingSource.Registry);
        settings.Register("LaunchArgumentPriority", 1, SettingSource.Registry);
        settings.Register("LaunchArgumentWindowType", 1);
        settings.Register("LaunchArgumentWindowWidth", 854);
        settings.Register("LaunchArgumentWindowHeight", 480);
        settings.Register("LaunchArgumentRam", false, SettingSource.Registry);
        settings.Register("LaunchRamType", 0);
        settings.Register("LaunchRamCustom", 15);
        settings.Register("LaunchSkinType", 0, SettingSource.Registry);
        settings.Register("LaunchSkinID", "", SettingSource.Registry);
        settings.Register("LaunchSkinSlim", false, SettingSource.Registry);
        settings.Register("LaunchAdvanceJvm", DefaultJvm);
        settings.Register("LaunchAdvanceGame", "");
        settings.Register("LaunchAdvanceRun", "");
        settings.Register("LaunchAdvanceRunWait", true);
        settings.Register("LaunchAdvanceDisableJLW", false);
        settings.Register("LaunchAdvanceDisableLUA", false);
        settings.Register("LaunchAdvanceGraphicCard", true, SettingSource.Registry);
        settings.Register("LaunchAdvanceGC", 4);
        settings.Register("ToolDownloadCert", false, SettingSource.Registry);
    }

    private static void RegisterSystem() {
        var settings = AppServices.Settings;
        settings.Register("SystemDebugMode", false, SettingSource.Registry);
        settings.Register("SystemDebugAnim", 9, SettingSource.Registry);
        settings.Register("SystemDebugSkipCopy", false, SettingSource.Registry);
        settings.Register("SystemDebugDelay", false, SettingSource.Registry);
        settings.Register("SystemSystemCache", "", SettingSource.Registry);
        settings.Register("SystemSystemUpdate", 0);
        settings.Register("SystemSystemActivity", 0);
        settings.Register("SystemSystemTelemetry", true, SettingSource.Registry);
        settings.Register("ToolDownloadSource", 1, SettingSource.Registry);
        settings.Register("ToolDownloadVersion", 1, SettingSource.Registry);
        settings.Register("ToolDownloadThread", 63, SettingSource.Registry);
        settings.Register("ToolDownloadSpeed", 42, SettingSource.Registry);
        settings.Register("ToolDownloadMod", 2, SettingSource.Registry);
        settings.Register("ToolDownloadTranslateV2", 1, SettingSource.Registry);
        settings.Register("ToolModLocalNameStyle", 0, SettingSource.Registry);
        settings.Register("ToolUpdateRelease", false, SettingSource.Registry);
        settings.Register("ToolUpdateSnapshot", false, SettingSource.Registry);
        settings.Register("ToolHelpChinese", true, SettingSource.Registry);
        settings.Register("HintDownloadThread", false);
    }

    private static void RegisterUi() {
        var settings = AppServices.Settings;
        settings.Register("UiLauncherTransparent", 600);
        settings.Register("UiLauncherHue", 180);
        settings.Register("UiLauncherSat", 80);
        settings.Register("UiLauncherDelta", 90);
        settings.Register("UiLauncherLight", 20);
        settings.Register("UiLauncherTheme", 0);
        settings.Register("UiLauncherLogo", true);
        settings.Register("UiLauncherEmail", false);
        settings.Register("UiBackgroundColorful", true);
        settings.Register("UiBackgroundOpacity", 1000);
        settings.Register("UiBackgroundBlur", 0);
        settings.Register("UiBackgroundSuit", 0);
        settings.Register("UiCustomType", 0);
        settings.Register("UiCustomPreset", 0);
        settings.Register("UiCustomNet", "");
        settings.Register("UiLogoType", 1);
        settings.Register("UiLogoText", "");
        settings.Register("UiLogoLeft", false);
        settings.Register("UiMusicVolume", 500);
        settings.Register("UiMusicStop", false);
        settings.Register("UiMusicStart", false);
        settings.Register("UiMusicRandom", true);
        settings.Register("UiMusicAuto", true);
        settings.Register("UiHiddenPageDownload", false);
        settings.Register("UiHiddenPageLink", false);
        settings.Register("UiHiddenPageSetup", false);
        settings.Register("UiHiddenPageOther", false);
        settings.Register("UiHiddenFunctionSelect", false);
        settings.Register("UiHiddenFunctionModUpdate", false);
        settings.Register("UiHiddenFunctionInstanceModify", false);
        settings.Register("UiHiddenFunctionHidden", false);
        settings.Register("UiHiddenSetupLaunch", false);
        settings.Register("UiHiddenSetupUi", false);
        settings.Register("UiHiddenSetupLink", false);
        settings.Register("UiHiddenSetupSystem", false);
        settings.Register("UiHiddenOtherHelp", false);
        settings.Register("UiHiddenOtherFeedback", false);
        settings.Register("UiHiddenOtherVote", false);
        settings.Register("UiHiddenOtherAbout", false);
        settings.Register("UiHiddenOtherTest", false);
    }

    private static void RegisterLink() {
        var settings = AppServices.Settings;
        settings.Register("LinkLatencyMode", 0, SettingSource.Registry);
        settings.Register("LinkCustomPeer", "");
    }

}
