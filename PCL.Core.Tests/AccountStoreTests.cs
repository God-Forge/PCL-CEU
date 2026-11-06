using PCL.Core.Login;

namespace PCL.Core.Tests;

public class AccountStoreTests {

    private static AccountEntry Entry(string name, string uuid = "", string? refresh = null, DateTimeOffset? expires = null) => new() {
        Name = name,
        Uuid = uuid,
        AccessToken = "token-" + name,
        UserType = "Microsoft",
        RefreshToken = refresh,
        ExpiresAt = expires
    };

    [Test]
    public async Task 空存储没有当前账号() {
        await Assert.That(AccountStore.Empty.Current).IsNull();
        await Assert.That(AccountStore.Empty.Accounts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 新增账号并选中() {
        var store = AccountStore.Empty.Upsert(Entry("Steve", "aaa"));
        await Assert.That(store.Accounts.Count).IsEqualTo(1);
        await Assert.That(store.Current!.Name).IsEqualTo("Steve");
    }

    [Test]
    public async Task 相同键更新而不是新增() {
        var store = AccountStore.Empty
            .Upsert(Entry("Steve", "aaa"))
            .Upsert(new AccountEntry { Name = "Steve2", Uuid = "aaa", AccessToken = "new" });
        await Assert.That(store.Accounts.Count).IsEqualTo(1);
        await Assert.That(store.Current!.Name).IsEqualTo("Steve2");
    }

    [Test]
    public async Task 切换账号() {
        var store = AccountStore.Empty
            .Upsert(Entry("A", "1"))
            .Upsert(Entry("B", "2"));
        var switched = store.Select("1");
        await Assert.That(switched.Current!.Name).IsEqualTo("A");
        // 不存在的键不改变当前账号
        await Assert.That(store.Select("999").Current!.Name).IsEqualTo("B");
    }

    [Test]
    public async Task 删除当前账号自动切换() {
        var store = AccountStore.Empty
            .Upsert(Entry("A", "1"))
            .Upsert(Entry("B", "2"));
        var removed = store.Remove("2");
        await Assert.That(removed.Accounts.Count).IsEqualTo(1);
        await Assert.That(removed.Current!.Name).IsEqualTo("A");
    }

    [Test]
    public async Task 删除不存在的账号不改变() {
        var store = AccountStore.Empty.Upsert(Entry("A", "1"));
        var same = store.Remove("999");
        await Assert.That(same.Accounts.Count).IsEqualTo(1);
        await Assert.That(same.Current!.Name).IsEqualTo("A");
    }

    [Test]
    public async Task 序列化往返() {
        var store = AccountStore.Empty
            .Upsert(Entry("A", "1", "refresh-a", DateTimeOffset.Parse("2026-01-01T00:00:00Z")))
            .Upsert(Entry("B", "2"));
        var restored = AccountStore.Parse(store.Serialize());
        await Assert.That(restored.Accounts.Count).IsEqualTo(2);
        await Assert.That(restored.Current!.Name).IsEqualTo("B");
        var first = restored.Find("1")!;
        await Assert.That(first.RefreshToken).IsEqualTo("refresh-a");
        await Assert.That(first.ExpiresAt).IsNotNull();
    }

    [Test]
    public async Task 损坏内容返回空() {
        await Assert.That(AccountStore.Parse(null).Accounts.Count).IsEqualTo(0);
        await Assert.That(AccountStore.Parse("{ 不是 JSON").Accounts.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 当前账号失效时回落到第一个() {
        var json = """{"current":"不存在","accounts":[{"name":"A","uuid":"1"}]}""";
        var store = AccountStore.Parse(json);
        await Assert.That(store.Current!.Name).IsEqualTo("A");
    }

    [Test]
    public async Task 缺少用户名条目被跳过() {
        var json = """{"current":"","accounts":[{"uuid":"1"},{"name":"B","uuid":"2"}]}""";
        var store = AccountStore.Parse(json);
        await Assert.That(store.Accounts.Count).IsEqualTo(1);
        await Assert.That(store.Accounts[0].Name).IsEqualTo("B");
    }

    [Test]
    public async Task 需要刷新令牌判断() {
        var now = DateTimeOffset.Parse("2026-06-01T00:00:00Z");
        var expired = Entry("A", "1", "refresh", now.AddMinutes(-5));
        var valid = Entry("B", "2", "refresh", now.AddHours(1));
        var noRefresh = Entry("C", "3", null, now.AddMinutes(-5));
        await Assert.That(expired.NeedsRefresh(now, TimeSpan.FromMinutes(1))).IsTrue();
        await Assert.That(valid.NeedsRefresh(now, TimeSpan.FromMinutes(1))).IsFalse();
        await Assert.That(noRefresh.NeedsRefresh(now, TimeSpan.FromMinutes(1))).IsFalse();
    }

    [Test]
    public async Task 从登录态转换() {
        var profile = new LoginProfile { Name = "Steve", Uuid = "abc", AccessToken = "t", UserType = "Microsoft" };
        var entry = AccountEntry.FromProfile(profile);
        await Assert.That(entry.Name).IsEqualTo("Steve");
        await Assert.That(entry.Key).IsEqualTo("abc");
        var back = entry.ToProfile();
        await Assert.That(back.AccessToken).IsEqualTo("t");
    }

    [Test]
    public async Task 退出登录后不自动选回账号() {
        var store = AccountStore.Empty.Upsert(Entry("A", "1"));
        var deselected = store.Deselect();
        await Assert.That(deselected.Current).IsNull();
        await Assert.That(deselected.Accounts.Count).IsEqualTo(1);
        // 序列化后再读回，仍然没有当前账号
        var restored = AccountStore.Parse(deselected.Serialize());
        await Assert.That(restored.Current).IsNull();
        await Assert.That(restored.Accounts.Count).IsEqualTo(1);
    }

    [Test]
    public async Task 缺少当前字段的旧数据回落到第一个() {
        var json = """{"accounts":[{"name":"A","uuid":"1"}]}""";
        var store = AccountStore.Parse(json);
        await Assert.That(store.Current!.Name).IsEqualTo("A");
    }

    [Test]
    public async Task 没有UUID时用用户名作键() {
        var entry = Entry("OnlyName");
        await Assert.That(entry.Key).IsEqualTo("OnlyName");
    }

}
