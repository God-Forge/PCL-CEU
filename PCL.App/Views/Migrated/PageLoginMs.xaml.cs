using System.Windows.Controls;
using System.Windows;
using PCL.App.Services;
using PCL.Core.Login;

namespace PCL.App.Views.Migrated;

public partial class PageLoginMs : System.Windows.Controls.StackPanel {

    public PageLoginMs() {
        InitializeComponent();
        Loaded += (_, _) => {
            PageAnimationService.FadeIn(this);
            PCL.App.Controls.SettingService.BindAll(this);
            BtnLogin.Click += async (_, _) => await LoginAsync();
        };
    }

    private async Task LoginAsync() {
        var clientId = Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID") ?? "";
        if (string.IsNullOrWhiteSpace(clientId)) {
            NotificationService.Warning("未配置 PCL_MS_CLIENT_ID，无法使用微软登录。", "微软登录");
            return;
        }
        BtnLogin.IsEnabled = false;
        try {
            var service = new MicrosoftLoginService(clientId);
            var code = await service.BeginDeviceCodeAsync();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName = code.VerificationUri,
                UseShellExecute = true
            });
            System.Windows.MessageBox.Show(
                $"请在浏览器中输入代码：{code.UserCode}\r\n{code.VerificationUri}",
                "微软登录", MessageBoxButton.OK, MessageBoxImage.Information);
            MicrosoftOAuthTokens? tokens = null;
            var deadline = DateTime.UtcNow.AddSeconds(code.ExpiresIn);
            while (DateTime.UtcNow < deadline) {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, code.Interval)));
                tokens = await service.PollTokenAsync(code.DeviceCode);
                if (tokens is not null) break;
            }
            if (tokens is null) throw new TimeoutException("设备码登录超时。");
            var xbl = await service.AuthenticateXboxLiveAsync(tokens.AccessToken);
            var (xsts, uhs) = await service.AuthorizeXstsAsync(xbl);
            var minecraft = await service.LoginMinecraftAsync(xsts, uhs);
            var (uuid, name) = await service.GetProfileAsync(minecraft.AccessToken);
            LoginSessionService.SetMicrosoft(name, uuid, minecraft.AccessToken);
            NotificationService.Info("微软登录成功：" + name, "微软登录");
            NavigationService.NavigateHome();
        } catch (Exception ex) {
            NotificationService.Error(LoginErrorTranslator.Translate(ex), "微软登录");
        } finally {
            BtnLogin.IsEnabled = true;
        }
    }

}
