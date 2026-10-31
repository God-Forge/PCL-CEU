using Newtonsoft.Json.Linq;
using PCL.Core.Download;
using PCL.Core.Net;

namespace PCL.Core.Minecraft;

/// <summary>
/// 原版安装：获取版本 JSON、资源索引，下载并写入版本文件夹。
/// </summary>
public sealed class VanillaInstallService {

    private readonly IStringFetcher _fetcher;
    private readonly NetLoader _loader;

    public VanillaInstallService(IStringFetcher? fetcher = null, IDownloadService? single = null, IDownloadService? parallel = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
        _loader = new NetLoader(single, parallel);
    }

    public async Task<string> InstallAsync(
        string mcFolder,
        string versionId,
        IProgress<NetLoaderProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        var jsonText = await new VersionManifestService(_fetcher).GetVersionJsonAsync(versionId, cancellationToken).ConfigureAwait(false);
        var versionJson = JObject.Parse(jsonText);
        var actualId = versionJson["id"]?.ToString() ?? versionId;
        var versionFolder = System.IO.Path.Combine(mcFolder, "versions", actualId);
        if (File.Exists(System.IO.Path.Combine(versionFolder, actualId + ".json"))) {
            throw new InvalidOperationException("实例已存在：" + actualId);
        }

        var assetIndexUrl = versionJson["assetIndex"]?["url"]?.ToString();
        var assetIndexJson = "";
        if (!string.IsNullOrWhiteSpace(assetIndexUrl)) {
            assetIndexJson = await _fetcher.GetStringAsync(assetIndexUrl!, cancellationToken).ConfigureAwait(false);
        }

        var tasks = VanillaInstallPlanner.Plan(versionJson, mcFolder, assetIndexJson);
        var result = await _loader.DownloadAsync(tasks, progress, maxConcurrency: 8, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Success) {
            var error = result.Files.FirstOrDefault(file => file.State == NetFileState.Failed)?.Error;
            throw new InvalidOperationException("原版文件下载失败。", error);
        }

        Directory.CreateDirectory(versionFolder);
        File.WriteAllText(System.IO.Path.Combine(versionFolder, actualId + ".json"), jsonText);
        return versionFolder;
    }

}
