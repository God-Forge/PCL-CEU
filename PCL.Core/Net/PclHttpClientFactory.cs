using System.Net.Http;

namespace PCL.Core.Net;

/// <summary>
/// 统一创建带 PCL User-Agent、超时与可选代理的 HttpClient。
/// </summary>
public static class PclHttpClientFactory {

    public static HttpClient Create(
        HttpMessageHandler? handler = null,
        TimeSpan? timeout = null,
        string userAgent = "PCL-CEU/0.10") {
        var client = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        client.Timeout = timeout ?? TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        return client;
    }

}
