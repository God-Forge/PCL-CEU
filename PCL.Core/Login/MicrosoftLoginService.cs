using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Login;

public sealed class MicrosoftDeviceCode {

    public string DeviceCode { get; init; } = "";
    public string UserCode { get; init; } = "";
    public string VerificationUri { get; init; } = "";
    public int ExpiresIn { get; init; }
    public int Interval { get; init; } = 5;

}

public sealed class MicrosoftOAuthTokens {

    public string AccessToken { get; init; } = "";
    public string RefreshToken { get; init; } = "";
    public int ExpiresIn { get; init; }

}

public sealed class MicrosoftLoginResult {

    public string AccessToken { get; init; } = "";
    public string RefreshToken { get; init; } = "";
    public string Uuid { get; init; } = "";
    public string Name { get; init; } = "";
    public DateTime ExpiresAt { get; init; }

}

/// <summary>
/// Microsoft OAuth 设备码登录与 Xbox Live / Minecraft Services 认证链。
/// 设备码展示与轮询节奏由调用方控制。
/// </summary>
public sealed class MicrosoftLoginService {

    private const string DeviceCodeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string XboxLiveUrl = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string MinecraftLoginUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string MinecraftProfileUrl = "https://api.minecraftservices.com/minecraft/profile";
    private const string Scope = "XboxLive.signin offline_access";

    private readonly HttpClient _client;
    private readonly string _clientId;

    public MicrosoftLoginService(string clientId, HttpClient? client = null) {
        if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("缺少 Microsoft OAuth Client ID。", nameof(clientId));
        _clientId = clientId;
        _client = client ?? PCL.Core.Net.PclHttpClientFactory.Create();
    }

    public async Task<MicrosoftDeviceCode> BeginDeviceCodeAsync(CancellationToken cancellationToken = default) {
        var content = new FormUrlEncodedContent(new Dictionary<string, string> {
            ["client_id"] = _clientId,
            ["tenant"] = "/consumers",
            ["scope"] = Scope
        });
        var json = await PostJsonAsync(DeviceCodeUrl, content, cancellationToken).ConfigureAwait(false);
        return new MicrosoftDeviceCode {
            DeviceCode = json["device_code"]?.ToString() ?? "",
            UserCode = json["user_code"]?.ToString() ?? "",
            VerificationUri = json["verification_uri"]?.ToString() ?? "",
            ExpiresIn = json["expires_in"]?.ToObject<int>() ?? 0,
            Interval = json["interval"]?.ToObject<int>() ?? 5
        };
    }

    public async Task<MicrosoftOAuthTokens?> PollTokenAsync(string deviceCode, CancellationToken cancellationToken = default) {
        var content = new FormUrlEncodedContent(new Dictionary<string, string> {
            ["client_id"] = _clientId,
            ["device_code"] = deviceCode,
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
        });
        var response = await _client.PostAsync(TokenUrl, content, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) {
            if (text.Contains("authorization_pending", StringComparison.Ordinal)
                || text.Contains("slow_down", StringComparison.Ordinal)) return null;
            throw new InvalidOperationException("Microsoft 设备码登录失败：" + text);
        }
        return ParseTokens(JObject.Parse(text));
    }

    public async Task<MicrosoftOAuthTokens> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) {
        var content = new FormUrlEncodedContent(new Dictionary<string, string> {
            ["client_id"] = _clientId,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
            ["scope"] = Scope
        });
        var response = await _client.PostAsync(TokenUrl, content, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Microsoft Token 刷新失败：" + text);
        return ParseTokens(JObject.Parse(text));
    }

    public async Task<string> AuthenticateXboxLiveAsync(string accessToken, CancellationToken cancellationToken = default) {
        var ticket = accessToken.StartsWith("d=", StringComparison.Ordinal) ? accessToken : "d=" + accessToken;
        var body = new JObject {
            ["Properties"] = new JObject {
                ["AuthMethod"] = "RPS",
                ["SiteName"] = "user.auth.xboxlive.com",
                ["RpsTicket"] = ticket
            },
            ["RelyingParty"] = "http://auth.xboxlive.com",
            ["TokenType"] = "JWT"
        };
        var json = await PostJsonAsync(XboxLiveUrl, JsonContent(body), cancellationToken).ConfigureAwait(false);
        return json["Token"]?.ToString() ?? throw new InvalidOperationException("Xbox Live 认证未返回 Token。");
    }

    public async Task<(string Token, string UserHash)> AuthorizeXstsAsync(string xblToken, CancellationToken cancellationToken = default) {
        var body = new JObject {
            ["Properties"] = new JObject {
                ["SandboxId"] = "RETAIL",
                ["UserTokens"] = new JArray(xblToken)
            },
            ["RelyingParty"] = "rp://api.minecraftservices.com/",
            ["TokenType"] = "JWT"
        };
        var json = await PostJsonAsync(XstsUrl, JsonContent(body), cancellationToken).ConfigureAwait(false);
        var token = json["Token"]?.ToString() ?? throw new InvalidOperationException("XSTS 认证未返回 Token。");
        var userHash = json["DisplayClaims"]?["xui"]?[0]?["uhs"]?.ToString()
                       ?? throw new InvalidOperationException("XSTS 认证未返回 UHS。");
        return (token, userHash);
    }

    public async Task<MicrosoftOAuthTokens> LoginMinecraftAsync(string xstsToken, string userHash, CancellationToken cancellationToken = default) {
        var body = new JObject { ["identityToken"] = $"XBL3.0 x={userHash};{xstsToken}" };
        var json = await PostJsonAsync(MinecraftLoginUrl, JsonContent(body), cancellationToken).ConfigureAwait(false);
        return new MicrosoftOAuthTokens {
            AccessToken = json["access_token"]?.ToString() ?? "",
            ExpiresIn = json["expires_in"]?.ToObject<int>() ?? 0
        };
    }

    public async Task<(string Uuid, string Name)> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default) {
        using var request = new HttpRequestMessage(HttpMethod.Get, MinecraftProfileUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("获取 Minecraft 档案失败：" + text);
        var json = JObject.Parse(text);
        return (json["id"]?.ToString() ?? "", json["name"]?.ToString() ?? "");
    }

    private static MicrosoftOAuthTokens ParseTokens(JObject json) {
        return new MicrosoftOAuthTokens {
            AccessToken = json["access_token"]?.ToString() ?? "",
            RefreshToken = json["refresh_token"]?.ToString() ?? "",
            ExpiresIn = json["expires_in"]?.ToObject<int>() ?? 0
        };
    }

    private static StringContent JsonContent(JObject body) {
        return new StringContent(body.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
    }

    private async Task<JObject> PostJsonAsync(string url, HttpContent content, CancellationToken cancellationToken) {
        using var response = await _client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("请求失败：" + text);
        return JObject.Parse(text);
    }

}
