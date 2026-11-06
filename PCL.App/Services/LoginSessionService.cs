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

    /// <summary>旧的单账号缓存键（仅用于迁移）。</summary>
    public const string CacheKey = "LoginCacheJson";

    /// <summary>多账号列表的设置键。</summary>
    public const string AccountsKey = "AccountsJson";

    private static AccountStore _store = AccountStore.Empty;

    /// <summary>已保存的账号列表。</summary>
    public static IReadOnlyList<AccountEntry> Accounts => _store.Accounts;

    /// <summary>当前账号的标识。</summary>
    public static string? CurrentKey => _store.CurrentKey;

    /// <summary>从设置中恢复账号列表与当前登录状态。启动时调用。</summary>
    public static void Restore() {
        try {
            var json = AppServices.Settings.Get<string>(AccountsKey);
            _store = AccountStore.Parse(json);
            if (_store.Accounts.Count == 0) {
                // 兼容旧版本的单账号缓存
                var legacy = LoginCache.Deserialize(AppServices.Settings.Get<string>(CacheKey));
                if (legacy is not null) {
                    _store = AccountStore.Empty.Upsert(AccountEntry.FromProfile(legacy));
                    Persist();
                    Logger.Info("已把旧的单账号缓存迁移为账号列表");
                }
            }
            var current = _store.Current?.ToProfile();
            if (current is null) return;
            AppServices.CurrentLogin = current;
            Logger.Info($"已恢复登录状态：{current.Name}（{current.UserType}），共 {_store.Accounts.Count} 个账号");
            Changed?.Invoke(current);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "恢复登录状态失败");
        }
    }

    /// <summary>切换到指定账号。</summary>
    public static bool SwitchTo(string? key) {
        var next = _store.Select(key);
        if (ReferenceEquals(next, _store)) return false;
        _store = next;
        var profile = _store.Current?.ToProfile();
        AppServices.CurrentLogin = profile;
        Persist();
        Changed?.Invoke(profile);
        Logger.Info($"已切换账号：{profile?.Name ?? "（无）"}");
        return true;
    }

    /// <summary>删除账号；删的是当前账号时自动切到下一个。</summary>
    public static bool RemoveAccount(string? key) {
        var next = _store.Remove(key);
        if (ReferenceEquals(next, _store)) return false;
        _store = next;
        var profile = _store.Current?.ToProfile();
        AppServices.CurrentLogin = profile;
        Persist();
        Changed?.Invoke(profile);
        Logger.Info($"已删除账号 {key}，当前账号：{profile?.Name ?? "（无）"}");
        return true;
    }

    /// <summary>后台刷新已过期的账号令牌，成功后更新当前登录态。</summary>
    public static async Task RefreshExpiredAccountsAsync() {
        if (_store.Accounts.Count == 0) return;
        try {
            var service = new AccountTokenRefreshService(new MicrosoftTokenRefresher());
            var (updated, outcomes) = await service.RefreshAsync(_store, DateTimeOffset.Now);
            if (outcomes.Count == 0) return;
            var refreshed = outcomes.Count(item => item.Refreshed);
            Logger.Info($"账号令牌刷新：成功 {refreshed} 个，失败 {outcomes.Count - refreshed} 个");
            if (refreshed == 0) return;
            _store = updated;
            Persist();
            var profile = _store.Current?.ToProfile();
            if (profile is null) return;
            AppServices.CurrentLogin = profile;
            Changed?.Invoke(profile);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "刷新账号令牌失败");
        }
    }

    /// <summary>把微软登录服务适配成刷新接口；失败一律返回 null 交给上层处理。</summary>
    private sealed class MicrosoftTokenRefresher : IAccountTokenRefresher {
        private readonly MicrosoftLoginService? _service;

        public MicrosoftTokenRefresher() {
            // 与登录页一致：client id 通过环境变量提供，未配置时无法续期
            var clientId = Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID") ?? "";
            _service = string.IsNullOrWhiteSpace(clientId) ? null : new MicrosoftLoginService(clientId);
        }

        public async Task<MicrosoftOAuthTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) {
            if (_service is null) {
                Logger.Info("未配置 PCL_MS_CLIENT_ID，跳过账号令牌刷新");
                return null;
            }
            try {
                return await _service.RefreshAsync(refreshToken, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                Logger.Warn(ex, "微软账号令牌刷新失败");
                return null;
            }
        }
    }

    private static void Persist() {
        try {
            AppServices.Settings.Set(AccountsKey, _store.Serialize());
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "保存账号列表失败");
        }
    }

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
        try {
            if (profile is null) {
                // 退出登录：保留账号列表，只取消当前选择
                _store = _store.Deselect();
            } else {
                _store = _store.Upsert(AccountEntry.FromProfile(profile));
            }
            Persist();
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "保存登录状态失败");
        }
        Logger.Info(profile is null ? "已退出登录" : $"当前账号：{profile.Name}（{profile.UserType}）");
        try {
            Changed?.Invoke(profile);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Error(ex, "处理登录状态变更时出错");
        }
    }

}
