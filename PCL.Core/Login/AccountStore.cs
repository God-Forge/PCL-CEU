using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Login;

/// <summary>账号条目：比登录态多一份刷新令牌与过期时间。</summary>
public sealed class AccountEntry {

    public string Name { get; init; } = "";
    public string Uuid { get; init; } = "";
    public string AccessToken { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string Xuid { get; init; } = "";
    public string UserType { get; init; } = "legacy";

    /// <summary>用于续期的刷新令牌；离线账号为空。</summary>
    public string? RefreshToken { get; init; }

    /// <summary>访问令牌的过期时间；未知为 <c>null</c>。</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>转换为登录态。</summary>
    public LoginProfile ToProfile() => new() {
        Name = Name,
        Uuid = Uuid,
        AccessToken = AccessToken,
        ClientId = ClientId,
        Xuid = Xuid,
        UserType = UserType
    };

    public static AccountEntry FromProfile(LoginProfile profile, string? refreshToken = null, DateTimeOffset? expiresAt = null) => new() {
        Name = profile.Name,
        Uuid = profile.Uuid,
        AccessToken = profile.AccessToken,
        ClientId = profile.ClientId,
        Xuid = profile.Xuid,
        UserType = profile.UserType,
        RefreshToken = refreshToken,
        ExpiresAt = expiresAt
    };

    /// <summary>是否具备续期条件（有刷新令牌且已知过期时间）。</summary>
    public bool NeedsRefresh(DateTimeOffset now, TimeSpan margin) =>
        !string.IsNullOrWhiteSpace(RefreshToken)
        && ExpiresAt is { } expires
        && expires - margin <= now;

    /// <summary>账号标识：优先 UUID，缺失时退化为用户名。</summary>
    public string Key => string.IsNullOrWhiteSpace(Uuid) ? Name : Uuid;

}

/// <summary>
/// 多账号存储：账号列表、当前选择与序列化。
/// 纯数据操作，不涉及网络与界面，便于测试。
/// </summary>
public sealed class AccountStore {

    private readonly List<AccountEntry> _accounts;

    private AccountStore(List<AccountEntry> accounts, string? currentKey) {
        _accounts = accounts;
        CurrentKey = currentKey;
    }

    public static AccountStore Empty { get; } = new([], null);

    public IReadOnlyList<AccountEntry> Accounts => _accounts;

    /// <summary>当前选中账号的标识（UUID 或用户名）。</summary>
    public string? CurrentKey { get; private init; }

    /// <summary>当前账号；没有或找不到时为 <c>null</c>。</summary>
    public AccountEntry? Current => Find(CurrentKey);

    public AccountEntry? Find(string? key) {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return _accounts.FirstOrDefault(entry =>
            string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>新增或更新账号（按 Key 去重），并设为当前账号。</summary>
    public AccountStore Upsert(AccountEntry entry, bool select = true) {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.Key)) return this;
        var accounts = _accounts
            .Where(existing => !string.Equals(existing.Key, entry.Key, StringComparison.OrdinalIgnoreCase))
            .ToList();
        accounts.Add(entry);
        return new AccountStore(accounts, select ? entry.Key : CurrentKey ?? entry.Key);
    }

    /// <summary>移除账号；移除的是当前账号时自动切到剩下的第一个。</summary>
    public AccountStore Remove(string? key) {
        if (string.IsNullOrWhiteSpace(key)) return this;
        var accounts = _accounts
            .Where(existing => !string.Equals(existing.Key, key, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (accounts.Count == _accounts.Count) return this;
        var current = string.Equals(CurrentKey, key, StringComparison.OrdinalIgnoreCase)
            ? accounts.FirstOrDefault()?.Key
            : CurrentKey;
        return new AccountStore(accounts, current);
    }

    /// <summary>取消当前选择，但保留账号列表（用于退出登录）。</summary>
    public AccountStore Deselect() => new(_accounts, null);

    /// <summary>切换当前账号；目标不存在时保持不变。</summary>
    public AccountStore Select(string? key) {
        var target = Find(key);
        return target is null ? this : new AccountStore(_accounts, target.Key);
    }

    /// <summary>序列化为 JSON。</summary>
    public string Serialize() {
        var json = new JObject {
            ["current"] = CurrentKey ?? "",
            ["accounts"] = new JArray(_accounts.Select(entry => new JObject {
                ["name"] = entry.Name,
                ["uuid"] = entry.Uuid,
                ["accessToken"] = entry.AccessToken,
                ["clientId"] = entry.ClientId,
                ["xuid"] = entry.Xuid,
                ["userType"] = entry.UserType,
                ["refreshToken"] = entry.RefreshToken ?? "",
                ["expiresAt"] = entry.ExpiresAt?.ToString("O") ?? ""
            }))
        };
        return json.ToString(Formatting.None);
    }

    /// <summary>从 JSON 还原；损坏或为空时返回 <see cref="Empty"/>。</summary>
    public static AccountStore Parse(string? json) {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        try {
            var root = JObject.Parse(json);
            var accounts = new List<AccountEntry>();
            foreach (var item in root["accounts"] as JArray ?? []) {
                var name = item["name"]?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                accounts.Add(new AccountEntry {
                    Name = name,
                    Uuid = item["uuid"]?.ToString() ?? "",
                    AccessToken = item["accessToken"]?.ToString() ?? "",
                    ClientId = item["clientId"]?.ToString() ?? "",
                    Xuid = item["xuid"]?.ToString() ?? "",
                    UserType = item["userType"]?.ToString() ?? "legacy",
                    RefreshToken = NullIfEmpty(item["refreshToken"]?.ToString()),
                    ExpiresAt = ParseTime(item["expiresAt"]?.ToString())
                });
            }
            // current 字段缺失时视为旧数据，回落到第一个；
            // 明确写空字符串表示「用户主动退出」，保持无当前账号
            var hasCurrentField = root["current"] is not null;
            var current = NullIfEmpty(root["current"]?.ToString());
            var store = new AccountStore(accounts, current);
            if (store.Current is null && accounts.Count > 0) {
                // 字段缺失或指向不存在的账号：回落到第一个
                // 明确写空字符串（用户主动退出）则保持无当前账号
                if (!hasCurrentField || current is not null) {
                    return new AccountStore(accounts, accounts[0].Key);
                }
            }
            return store;
        } catch (JsonException ex) {
            Logger.Warn(ex, "解析账号列表失败，已重置");
            return Empty;
        }
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static DateTimeOffset? ParseTime(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;

}
