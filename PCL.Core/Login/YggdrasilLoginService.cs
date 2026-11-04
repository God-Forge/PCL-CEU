using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Login;

public sealed class YggdrasilLoginResult {

    public string Name { get; init; } = "";
    public string Uuid { get; init; } = "";
    public string AccessToken { get; init; } = "";
    public string ClientToken { get; init; } = "";

}

/// <summary>
/// Authlib-Injector / Nide 使用的 Yggdrasil 登录协议。
/// </summary>
public sealed class YggdrasilLoginService {

    private readonly HttpClient _client;

    public YggdrasilLoginService(HttpClient? client = null) {
        _client = client ?? PCL.Core.Net.PclHttpClientFactory.Create();
    }

    public async Task<YggdrasilLoginResult> LoginAsync(
        string baseUrl,
        string username,
        string password,
        CancellationToken cancellationToken = default) {
        var body = new JObject {
            ["agent"] = new JObject { ["name"] = "Minecraft", ["version"] = 1 },
            ["username"] = username,
            ["password"] = password,
            ["requestUser"] = true
        };
        var json = await PostJsonAsync(baseUrl.TrimEnd('/') + "/authenticate", body, cancellationToken).ConfigureAwait(false);
        var profile = json["selectedProfile"] ?? json["availableProfiles"]?[0]
                      ?? throw new InvalidOperationException("登录成功，但账号没有可用角色。");
        return new YggdrasilLoginResult {
            Name = profile["name"]?.ToString() ?? "",
            Uuid = profile["id"]?.ToString() ?? "",
            AccessToken = json["accessToken"]?.ToString() ?? "",
            ClientToken = json["clientToken"]?.ToString() ?? ""
        };
    }

    public async Task ValidateAsync(string baseUrl, string accessToken, string clientToken, CancellationToken cancellationToken = default) {
        var body = new JObject {
            ["accessToken"] = accessToken,
            ["clientToken"] = clientToken,
            ["requestUser"] = true
        };
        await PostJsonAsync(baseUrl.TrimEnd('/') + "/validate", body, cancellationToken).ConfigureAwait(false);
    }

    public async Task<YggdrasilLoginResult> RefreshAsync(
        string baseUrl,
        string accessToken,
        string clientToken,
        string name,
        string uuid,
        CancellationToken cancellationToken = default) {
        var body = new JObject {
            ["selectedProfile"] = new JObject { ["name"] = name, ["id"] = uuid },
            ["accessToken"] = accessToken,
            ["clientToken"] = clientToken,
            ["requestUser"] = true
        };
        var json = await PostJsonAsync(baseUrl.TrimEnd('/') + "/refresh", body, cancellationToken).ConfigureAwait(false);
        var profile = json["selectedProfile"] ?? throw new InvalidOperationException("刷新登录未返回角色。");
        return new YggdrasilLoginResult {
            Name = profile["name"]?.ToString() ?? name,
            Uuid = profile["id"]?.ToString() ?? uuid,
            AccessToken = json["accessToken"]?.ToString() ?? accessToken,
            ClientToken = json["clientToken"]?.ToString() ?? clientToken
        };
    }

    private async Task<JObject> PostJsonAsync(string url, JObject body, CancellationToken cancellationToken) {
        using var content = new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Yggdrasil 请求失败：" + text);
        return string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text);
    }

}
