using System.Net;
using System.Net.Http;

namespace PCL.Core.Net;

/// <summary>
/// 可替换的字符串下载接口，便于测试与后续迁移 ModNet。
/// </summary>
public interface IStringFetcher {
    Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// 基于 HttpClient 的默认实现。
/// </summary>
public sealed class HttpStringFetcher : IStringFetcher, IDisposable {

    private readonly HttpClient _client;

    public HttpStringFetcher() {
        var handler = new HttpClientHandler {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _client = new HttpClient(handler) {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("PCL-CEU/1.0");
    }

    public async Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) {
        using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    }

    public void Dispose() => _client.Dispose();

}
