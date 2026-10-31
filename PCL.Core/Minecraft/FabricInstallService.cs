using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Minecraft;

/// <summary>
/// Fabric 安装：确保原版存在，获取 Fabric profile，下载依赖并写入版本 JSON。
/// </summary>
public sealed class FabricInstallService {

    private readonly IStringFetcher _fetcher;
    private readonly IDownloadService? _single;
    private readonly IDownloadService? _parallel;

    public FabricInstallService(IStringFetcher? fetcher = null, IDownloadService? single = null, IDownloadService? parallel = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
        _single = single;
        _parallel = parallel;
    }

    public async Task<string> InstallAsync(
        string mcFolder,
        string mcVersion,
        string loaderVersion,
        IProgress<NetLoaderProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        var profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/profile/json";
        var profileText = await _fetcher.GetStringAsync(profileUrl, cancellationToken).ConfigureAwait(false);
        var profile = JObject.Parse(profileText);
        var id = profile["id"]?.ToString() ?? $"fabric-loader-{loaderVersion}-{mcVersion}";
        var versionFolder = System.IO.Path.Combine(mcFolder, "versions", id);
        if (File.Exists(System.IO.Path.Combine(versionFolder, id + ".json"))) {
            throw new InvalidOperationException("实例已存在：" + id);
        }

        var vanillaJson = System.IO.Path.Combine(mcFolder, "versions", mcVersion, mcVersion + ".json");
        if (!File.Exists(vanillaJson)) {
            await new VanillaInstallService(_fetcher, _single, _parallel)
                .InstallAsync(mcFolder, mcVersion, null, cancellationToken).ConfigureAwait(false);
        }

        var tasks = new List<NetFileTask>();
        foreach (var library in LibraryResolver.Resolve(profile, mcFolder)) {
            if (string.IsNullOrWhiteSpace(library.Url)) continue;
            tasks.Add(new NetFileTask {
                Name = library.Name,
                TargetPath = System.IO.Path.Combine(mcFolder, library.LocalPath),
                Sources = [library.Url!],
                Sha1 = library.Sha1,
                MinSize = library.Size >= 0 ? library.Size : null,
                UseParallel = true,
                ParallelCount = 4
            });
        }

        var loader = new NetLoader(_single, _parallel);
        var result = await loader.DownloadAsync(tasks, progress, maxConcurrency: 8, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!result.Success) {
            var error = result.Files.FirstOrDefault(file => file.State == NetFileState.Failed)?.Error;
            throw new InvalidOperationException("Fabric 依赖下载失败。", error);
        }

        Directory.CreateDirectory(versionFolder);
        File.WriteAllText(System.IO.Path.Combine(versionFolder, id + ".json"), profileText);
        return versionFolder;
    }

}
