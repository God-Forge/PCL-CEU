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

    public async Task<string> RepairAsync(
        string mcFolder,
        string instanceFolder,
        string versionId,
        IProgress<NetLoaderProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        var jsonPath = System.IO.Path.Combine(instanceFolder, versionId + ".json");
        if (!File.Exists(jsonPath)) {
            jsonPath = Directory.EnumerateFiles(instanceFolder, "*.json", SearchOption.TopDirectoryOnly).FirstOrDefault()
                       ?? throw new FileNotFoundException("实例缺少版本 JSON。", jsonPath);
        }

        var versionJson = JObject.Parse(File.ReadAllText(jsonPath));
        var assetIndexJson = "{}";
        var assetIndex = versionJson["assetIndex"];
        var assetIndexId = assetIndex?["id"]?.ToString();
        if (!string.IsNullOrWhiteSpace(assetIndexId)) {
            var localIndex = System.IO.Path.Combine(mcFolder, "assets", "indexes", assetIndexId + ".json");
            if (File.Exists(localIndex)) {
                assetIndexJson = File.ReadAllText(localIndex);
            } else {
                var assetIndexUrl = assetIndex?["url"]?.ToString();
                if (!string.IsNullOrWhiteSpace(assetIndexUrl)) {
                    assetIndexJson = await _fetcher.GetStringAsync(assetIndexUrl!, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        var planned = VanillaInstallPlanner.Plan(versionJson, mcFolder, assetIndexJson);
        var missing = planned.Where(task => !IsSatisfied(task)).ToList();
        if (missing.Count == 0) return "基础文件完整，无需修复。";

        var result = await _loader.DownloadAsync(missing, progress, maxConcurrency: 8, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (!result.Success) {
            var error = result.Files.FirstOrDefault(file => file.State == NetFileState.Failed)?.Error;
            throw new InvalidOperationException($"修复下载失败，仍有 {result.Files.Count(file => file.State != NetFileState.Finished)} 个文件未完成。", error);
        }

        return $"修复完成：检查 {planned.Count} 个文件，补全 {missing.Count} 个。";
    }

    private static bool IsSatisfied(NetFileTask task) {
        if (!File.Exists(task.TargetPath)) return false;
        if (task.MinSize is > 0 && new FileInfo(task.TargetPath).Length < task.MinSize.Value) return false;
        return true;
    }

}
