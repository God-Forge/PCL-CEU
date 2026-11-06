using PCL.Core.Login;

namespace PCL.Core.Tests;

public class AccountTokenRefreshServiceTests {

    private sealed class FakeRefresher(Func<string, MicrosoftOAuthTokens?> handler) : IAccountTokenRefresher {
        public List<string> Calls { get; } = [];

        public Task<MicrosoftOAuthTokens?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) {
            Calls.Add(refreshToken);
            return Task.FromResult(handler(refreshToken));
        }
    }

    private static AccountEntry Expired(string name, string uuid, DateTimeOffset now) => new() {
        Name = name,
        Uuid = uuid,
        AccessToken = "old-token",
        UserType = "Microsoft",
        RefreshToken = "refresh-" + uuid,
        ExpiresAt = now.AddMinutes(-10)
    };

    private static AccountEntry Valid(string name, string uuid, DateTimeOffset now) => new() {
        Name = name,
        Uuid = uuid,
        AccessToken = "valid-token",
        UserType = "Microsoft",
        RefreshToken = "refresh-" + uuid,
        ExpiresAt = now.AddHours(2)
    };

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-01T00:00:00Z");

    [Test]
    public async Task 只刷新已过期的账号() {
        var store = AccountStore.Empty.Upsert(Expired("A", "1", Now)).Upsert(Valid("B", "2", Now));
        var refresher = new FakeRefresher(_ => new MicrosoftOAuthTokens {
            AccessToken = "new-token", RefreshToken = "new-refresh", ExpiresIn = 3600
        });
        var (updated, outcomes) = await new AccountTokenRefreshService(refresher).RefreshAsync(store, Now);
        await Assert.That(refresher.Calls.Count).IsEqualTo(1);
        await Assert.That(refresher.Calls[0]).IsEqualTo("refresh-1");
        await Assert.That(outcomes.Count).IsEqualTo(1);
        await Assert.That(outcomes[0].Refreshed).IsTrue();
        await Assert.That(updated.Find("1")!.AccessToken).IsEqualTo("new-token");
        await Assert.That(updated.Find("2")!.AccessToken).IsEqualTo("valid-token");
    }

    [Test]
    public async Task 刷新后更新过期时间() {
        var store = AccountStore.Empty.Upsert(Expired("A", "1", Now));
        var refresher = new FakeRefresher(_ => new MicrosoftOAuthTokens {
            AccessToken = "new", RefreshToken = "r2", ExpiresIn = 3600
        });
        var (updated, _) = await new AccountTokenRefreshService(refresher).RefreshAsync(store, Now);
        var account = updated.Find("1")!;
        await Assert.That(account.ExpiresAt).IsEqualTo(Now.AddSeconds(3600));
        await Assert.That(account.NeedsRefresh(Now, TimeSpan.FromMinutes(5))).IsFalse();
    }

    [Test]
    public async Task 没有返回新刷新令牌时沿用旧的() {
        var store = AccountStore.Empty.Upsert(Expired("A", "1", Now));
        var refresher = new FakeRefresher(_ => new MicrosoftOAuthTokens { AccessToken = "new", ExpiresIn = 60 });
        var (updated, _) = await new AccountTokenRefreshService(refresher).RefreshAsync(store, Now);
        await Assert.That(updated.Find("1")!.RefreshToken).IsEqualTo("refresh-1");
    }

    [Test]
    public async Task 刷新失败不影响其它账号() {
        var store = AccountStore.Empty.Upsert(Expired("A", "1", Now)).Upsert(Expired("B", "2", Now));
        var refresher = new FakeRefresher(token => token == "refresh-1"
            ? null
            : new MicrosoftOAuthTokens { AccessToken = "new-b", ExpiresIn = 600 });
        var (updated, outcomes) = await new AccountTokenRefreshService(refresher).RefreshAsync(store, Now);
        await Assert.That(outcomes.Count(item => item.Refreshed)).IsEqualTo(1);
        await Assert.That(outcomes.Count(item => !item.Refreshed)).IsEqualTo(1);
        await Assert.That(updated.Find("1")!.AccessToken).IsEqualTo("old-token");
        await Assert.That(updated.Find("2")!.AccessToken).IsEqualTo("new-b");
    }

    [Test]
    public async Task 刷新抛异常时保留原账号() {
        var store = AccountStore.Empty.Upsert(Expired("A", "1", Now));
        var refresher = new FakeRefresher(_ => throw new HttpRequestException("network down"));
        var (updated, outcomes) = await new AccountTokenRefreshService(refresher).RefreshAsync(store, Now);
        await Assert.That(outcomes[0].Refreshed).IsFalse();
        await Assert.That(outcomes[0].Error).Contains("network");
        await Assert.That(updated.Find("1")!.AccessToken).IsEqualTo("old-token");
    }

    [Test]
    public async Task 没有刷新令牌时不尝试() {
        var store = AccountStore.Empty.Upsert(new AccountEntry {
            Name = "Offline", Uuid = "9", AccessToken = "t", RefreshToken = null
        });
        var refresher = new FakeRefresher(_ => new MicrosoftOAuthTokens { AccessToken = "x" });
        var (_, outcomes) = await new AccountTokenRefreshService(refresher).RefreshAsync(store, Now);
        await Assert.That(refresher.Calls.Count).IsEqualTo(0);
        await Assert.That(outcomes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 提前量内也会刷新() {
        var justExpiring = new AccountEntry {
            Name = "A", Uuid = "1", AccessToken = "t",
            RefreshToken = "refresh-1", ExpiresAt = Now.AddMinutes(2)
        };
        var store = AccountStore.Empty.Upsert(justExpiring);
        var refresher = new FakeRefresher(_ => new MicrosoftOAuthTokens { AccessToken = "new", ExpiresIn = 600 });
        var (_, outcomes) = await new AccountTokenRefreshService(refresher).RefreshAsync(store, Now);
        await Assert.That(outcomes[0].Refreshed).IsTrue();
    }

    [Test]
    public async Task 不修改传入的存储实例() {
        var store = AccountStore.Empty.Upsert(Expired("A", "1", Now));
        var refresher = new FakeRefresher(_ => new MicrosoftOAuthTokens { AccessToken = "new", ExpiresIn = 60 });
        await new AccountTokenRefreshService(refresher).RefreshAsync(store, Now);
        await Assert.That(store.Find("1")!.AccessToken).IsEqualTo("old-token");
    }

}
