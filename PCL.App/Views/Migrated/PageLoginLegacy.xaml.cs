using System.Windows.Controls;
using System.Windows;
using PCL.App.Services;
using PCL.Core.Login;

namespace PCL.App.Views.Migrated;

public partial class PageLoginLegacy : System.Windows.Controls.Grid {

    public PageLoginLegacy() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            var saved = AppServices.Settings.Get<string>("LoginLegacyName") ?? "";
            ComboName.Text = saved.Split('¨').FirstOrDefault() ?? "";
            ComboName.LostFocus += (_, _) => AppServices.Settings.Set("LoginLegacyName", ComboName.Text);
            BtnLogin.Click += (_, _) => {
                var name = ComboName.Text.Trim();
                if (string.IsNullOrWhiteSpace(name)) {
                    System.Windows.MessageBox.Show("请输入游戏用户名。", "离线登录", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                AppServices.Settings.Set("LoginLegacyName", name);
                AppServices.CurrentLogin = OfflineLoginService.Create(name);
                System.Windows.MessageBox.Show($"离线登录成功：{name}\r\nUUID：{AppServices.CurrentLogin.Uuid}", "离线登录", MessageBoxButton.OK, MessageBoxImage.Information);
            };
        };
    }

}





