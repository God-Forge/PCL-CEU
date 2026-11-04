using System.Windows.Controls;
using System.Windows;
using PCL.App.Services;
using PCL.Core.Login;

namespace PCL.App.Views.Migrated;

public partial class PageLoginNide : System.Windows.Controls.Grid {

    public PageLoginNide() {
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
                    var server = AppServices.Settings.Get<string>("VersionServerNide") ?? "";
                    if (string.IsNullOrWhiteSpace(server)) throw new InvalidOperationException("尚未配置登录服务器地址。");
                    var result = await new YggdrasilLoginService().LoginAsync(server, name, password);
                    LoginSessionService.SetYggdrasil(result, "Nide");
                    NotificationService.Info("登录成功：" + result.Name, "登录");
                } catch (Exception ex) {
                    NotificationService.Error(LoginErrorTranslator.Translate(ex), "登录");
                } finally {
                    BtnLogin.IsEnabled = true;
                }
            };
        };
    }

}



