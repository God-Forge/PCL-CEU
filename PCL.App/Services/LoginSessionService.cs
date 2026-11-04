using MeloongCore;
using PCL.Core.Login;

namespace PCL.App.Services;

/// <summary>
/// 登录会话的唯一写入口：统一更新 <see cref="AppServices.CurrentLogin"/> 并通知订阅方。
/// 之前三个登录页各自赋值，导致登录成功后启动页仍显示旧账号。
/// </summary>
public static class LoginSessionService {

    /// <summary>登录信息变化。参数为新的登录状态，已退出登录时为 <c>null</c>。</summary>
    public static event Action<LoginProfile?>? Changed;

    public static LoginProfile? Current => AppServices.CurrentLogin;

    /// <summary>切换到离线登录。</summary>
    public static void SetOffline(string? name) {
        var normalized = LoginNameValidator.Normalize(name);
        if (!LoginNameValidator.IsValid(normalized)) {
            throw new InvalidOperationException(LoginNameValidator.Validate(normalized) ?? "玩家名不合法。");
        }
        Update(OfflineLoginService.Create(normalized));
    }

    /// <summary>使用 Yggdrasil 系（Authlib / Nide）登录结果。</summary>
    public static void SetYggdrasil(YggdrasilLoginResult result, string userType) {
        ArgumentNullException.ThrowIfNull(result);
        Update(new LoginProfile {
            Name = result.Name,
            Uuid = result.Uuid,
            AccessToken = result.AccessToken,
            UserType = userType
        });
    }

    /// <summary>使用微软登录结果。</summary>
    public static void SetMicrosoft(string name, string uuid, string accessToken) {
        Update(new LoginProfile {
            Name = name,
            Uuid = uuid,
            AccessToken = accessToken,
            UserType = "Microsoft"
        });
    }

    /// <summary>退出登录（回到未登录状态）。</summary>
    public static void Clear() => Update(null);

    private static void Update(LoginProfile? profile) {
        AppServices.CurrentLogin = profile;
        Logger.Info(profile is null ? "已退出登录" : $"当前账号：{profile.Name}（{profile.UserType}）");
        try {
            Changed?.Invoke(profile);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "处理登录状态变更时出错");
        }
    }

}
