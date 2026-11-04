using System.Windows.Controls;
using System.Windows;
using PCL.App.Services;
using PCL.Core.Login;

namespace PCL.App.Views.Migrated;

public partial class PageLoginAuth : System.Windows.Controls.Grid {

    public PageLoginAuth() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            BtnLogin.Click += async (_, _) => {
                var name = ComboName.Text.Trim();
                var password = TextPass.Password;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(password)) {
                    System.Windows.MessageBox.Show("请输入账号和密码。", "登录", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                BtnLogin.IsEnabled = false;
                try {
                    var server = AppServices.Settings.Get<string>("VersionServerAuthServer") ?? "";
                    if (string.IsNullOrWhiteSpace(server)) throw new InvalidOperationException("尚未配置登录服务器地址。");
                    var result = await new YggdrasilLoginService().LoginAsync(server, name, password);
                    AppServices.CurrentLogin = new LoginProfile { Name = result.Name, Uuid = result.Uuid, AccessToken = result.AccessToken, UserType = "Auth" };
                    System.Windows.MessageBox.Show("登录成功：" + result.Name, "登录", MessageBoxButton.OK, MessageBoxImage.Information);
                } catch (Exception ex) {
                    System.Windows.MessageBox.Show("登录失败：" + ex.Message, "登录", MessageBoxButton.OK, MessageBoxImage.Error);
                } finally {
                    BtnLogin.IsEnabled = true;
                }
            };
        };
    }

}



