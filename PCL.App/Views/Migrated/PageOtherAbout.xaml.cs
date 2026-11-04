using System.Reflection;
using System.Windows;
using PCL.App.Controls;
using PCL.App.Services;
using PCL.Core.Net;
using PCL.Core.Update;

namespace PCL.App.Views.Migrated;

public partial class PageOtherAbout : PCL.App.Controls.MyPageRight {

    private bool _initialized;
    private bool _checking;

    public PageOtherAbout() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            if (_initialized) return;
            _initialized = true;
            ApplyVersionInfo();
            WireInteractions();
        };
    }

    private void ApplyVersionInfo() {
        var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 24, 0, 0);
        var display = $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
        var versionCode = version.Major * 1000 + version.Minor * 10 + Math.Max(version.Build, 0);
        ItemAboutPcl.Info = (ItemAboutPcl.Info ?? "")
            .Replace("%VERSION%", display)
            .Replace("%VERSIONCODE%", versionCode.ToString())
            .Replace("%BRANCH%", "50");
    }

    private void WireInteractions() {
        CustomEventService.SetEventType(BtnAboutSource, "");
        CustomEventService.SetEventData(BtnAboutSource, "");
        BtnAboutSource.EventType = null;
        BtnAboutSource.EventData = null;
        BtnAboutSource.Click += (_, _) => PageSetupOtherUi.OpenUrl("https://github.com/Meloong-Git/PCL");

        CustomEventService.SetEventType(BtnDonateDonate, "");
        CustomEventService.SetEventData(BtnDonateDonate, "");
        BtnDonateDonate.EventType = null;
        BtnDonateDonate.EventData = null;
        BtnDonateDonate.Click += (_, _) => PageSetupOtherUi.OpenUrl("https://meloong.com/afd/a/LTCat");

        BtnDonateCopy.Visibility = Visibility.Visible;
        BtnDonateCopy.Click += (_, _) => {
            PageSetupOtherUi.CopyText(PageSetupOtherSettings.Identify);
            NotificationService.Info("识别码已复制到剪贴板。");
        };
        BtnAboutUpdate.Click += (_, _) => _ = CheckUpdateAsync();

        foreach (var button in PageSetupOtherUi.Descendants(this).OfType<LegacyButton>()) {
            var eventType = button.EventType ?? CustomEventService.GetEventType(button);
            if (eventType is not ("打开网页" or "OpenWebsite")) continue;
            var eventData = button.EventData ?? CustomEventService.GetEventData(button);
            if (string.IsNullOrWhiteSpace(eventData)) continue;
            button.EventType = null;
            button.EventData = null;
            CustomEventService.SetEventType(button, "");
            CustomEventService.SetEventData(button, "");
            button.Click += (_, _) => PageSetupOtherUi.OpenUrl(eventData);
        }
    }

    private async Task CheckUpdateAsync() {
        if (_checking) return;
        _checking = true;
        BtnAboutUpdate.IsEnabled = false;
        try {
            using var fetcher = new HttpStringFetcher();
            var info = await UpdateCheckerService.CheckAsync(fetcher, PageSetupOtherSettings.UpdateApiUrl, PageSetupOtherSettings.CurrentVersion);
            NotificationService.Info(info.HasUpdate
                ? $"发现新版本：{info.LatestVersion}\r\n{info.Url}"
                : "当前已是最新版本。");
        } catch (Exception ex) {
            NotificationService.Warning("检查更新失败：" + ex.Message);
        } finally {
            _checking = false;
            BtnAboutUpdate.IsEnabled = true;
        }
    }

}
