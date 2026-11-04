using System.Windows;
using System.Windows.Controls;
using PCL.App.Controls;
using PCL.App.Services;

namespace PCL.App.Views.Migrated;

public partial class PageSetupLink : PCL.App.Controls.MyPageRight {

    private static readonly string[] AllowedSchemes = ["tcp", "udp", "ws", "wss"];

    private bool _initialized;
    private string _lastValidPeer = "";

    public PageSetupLink() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            if (_initialized) return;
            _initialized = true;
            PageSetupOtherSettings.EnsureRegistered();
            PageSetupOtherUi.PrepareBinding(this);
            SettingService.BindAll(this);
            _lastValidPeer = PageSetupOtherSettings.GetString("LinkCustomPeer");
            TextCustomPeer.LostFocus += (_, _) => SaveCustomPeer();
            ComboLatencyMode.SelectionChanged += (_, _) =>
                NotificationService.Info("联机模式已保存，将在下一次联机时生效。");
        };
    }

    private void SaveCustomPeer() {
        var peers = TextCustomPeer.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        if (peers.Count == 0) {
            _lastValidPeer = "";
            TextCustomPeer.Text = "";
            AppServices.Settings.Set("LinkCustomPeer", "");
            return;
        }
        foreach (var peer in peers) {
            if (!Uri.TryCreate(peer, UriKind.Absolute, out var uri) || !AllowedSchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase)) {
                NotificationService.Warning("自定义节点格式无效，应为 tcp://、udp://、ws:// 或 wss:// 地址。\r\n已恢复上一次的有效设置。");
                TextCustomPeer.Text = _lastValidPeer;
                return;
            }
        }
        var normalized = string.Join(",", peers);
        _lastValidPeer = normalized;
        if (!string.Equals(TextCustomPeer.Text, normalized, StringComparison.Ordinal)) TextCustomPeer.Text = normalized;
        AppServices.Settings.Set("LinkCustomPeer", normalized);
    }

}
