namespace PCL.Core.Login;

/// <summary>刷新令牌的能力抽象，便于在没有网络时替换实现。</summary>
public interface IAccountTokenRefresher {

    /// <summary>用刷新令牌换取新的访问令牌。失败时返回 <c>null</c>。</summary>
    Task<MicrosoftOAuthTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

}

/// <summary>单次刷新的结果，便于日志与界面展示。</summary>
public sealed class TokenRefreshOutcome {

    public bool Refreshed { get; init; }
    public string? AccountName { get; init; }
    public string? Error { get; init; }

}

/// <summary>
/// 按需刷新账号令牌：只处理「有刷新令牌且已过期」的账号，
/// 刷新失败时保留原有账号信息，不影响其它账号。
/// </summary>
public sealed class AccountTokenRefreshService {

    private readonly IAccountTokenRefresher _refresher;

    public AccountTokenRefreshService(IAccountTokenRefresher refresher) {
        _refresher = refresher ?? throw new ArgumentNullException(nameof(refresher));
    }

    /// <summary>默认提前量：距离过期不足 5 分钟就刷新。</summary>
    public static readonly TimeSpan DefaultMargin = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 检查并刷新需要续期的账号，返回更新后的存储。
    /// 不修改传入的存储实例。
    /// </summary>
    public async Task<(AccountStore Store, IReadOnlyList<TokenRefreshOutcome> Outcomes)> RefreshAsync(
        AccountStore store,
        DateTimeOffset now,
        TimeSpan? margin = null,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(store);
        var effectiveMargin = margin ?? DefaultMargin;
        var outcomes = new List<TokenRefreshOutcome>();
        var current = store;
        foreach (var account in store.Accounts) {
            cancellationToken.ThrowIfCancellationRequested();
            if (!account.NeedsRefresh(now, effectiveMargin)) continue;
            if (string.IsNullOrWhiteSpace(account.RefreshToken)) continue;
            try {
                var tokens = await _refresher
                    .RefreshAsync(account.RefreshToken!, cancellationToken)
                    .ConfigureAwait(false);
                if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken)) {
                    outcomes.Add(new TokenRefreshOutcome {
                        Refreshed = false,
                        AccountName = account.Name,
                        Error = "刷新返回空令牌"
                    });
                    continue;
                }
                var updated = new AccountEntry {
                    Name = account.Name,
                    Uuid = account.Uuid,
                    AccessToken = tokens.AccessToken,
                    ClientId = account.ClientId,
                    Xuid = account.Xuid,
                    UserType = account.UserType,
                    // 部分服务不返回新的刷新令牌，此时沿用旧的
                    RefreshToken = string.IsNullOrWhiteSpace(tokens.RefreshToken)
                        ? account.RefreshToken
                        : tokens.RefreshToken,
                    ExpiresAt = tokens.ExpiresIn > 0 ? now.AddSeconds(tokens.ExpiresIn) : account.ExpiresAt
                };
                current = current.Upsert(updated, select: false);
                outcomes.Add(new TokenRefreshOutcome { Refreshed = true, AccountName = account.Name });
                Logger.Info($"已刷新账号令牌：{account.Name}");
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                Logger.Warn(ex, "刷新账号令牌失败：" + account.Name);
                outcomes.Add(new TokenRefreshOutcome {
                    Refreshed = false,
                    AccountName = account.Name,
                    Error = ex.Message
                });
            }
        }
        return (current, outcomes);
    }

}
