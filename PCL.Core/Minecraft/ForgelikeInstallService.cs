using PCL.Core.Net;

namespace PCL.Core.Minecraft;

/// <summary>
/// Forge/NeoForge 安装编排：读取安装器元数据、下载依赖、执行安装器。
/// </summary>
public sealed class ForgelikeInstallService {

    private readonly IStringFetcher _fetcher;
    private readonly IDownloadService? _single;
    private readonly IDownloadService? _parallel;

    public ForgelikeInstallService(IStringFetcher? fetcher = null, IDownloadService? single = null, IDownloadService? parallel = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
        _single = single;
        _parallel = parallel;
    }

    public async Task<string> InstallAsync(
        string mcFolder,
        string installerPath,
        string javaExePath,
        string loaderName,
        string mcVersion,
        string loaderVersion,
        bool isNeoForge,
        string cacheFolder,
        string javaTempFolder,
        string? wrapperPath = null,
        IEnumerable<string>? vanillaJsonUrls = null,
        int javaMajorVersion = 17,
        IProgress<NetLoaderProgress>? progress = null,
        CancellationToken cancellationToken = default) {
        var plan = await ForgelikeInstallPlanner.CreatePlanAsync(
            installerPath, loaderName, mcVersion, loaderVersion,
            _fetcher, vanillaJsonUrls, cancellationToken).ConfigureAwait(false);

        var libraries = LibraryResolver.Resolve(plan.Json, mcFolder);
        var tasks = libraries.Where(library => !string.IsNullOrWhiteSpace(library.Url))
            .Select(library => new NetFileTask {
                Name = library.Name,
                TargetPath = Path.Combine(mcFolder, library.LocalPath),
                Sources = [library.Url!],
                Sha1 = library.Sha1,
                MinSize = library.Size >= 0 ? library.Size : null,
                UseParallel = true,
                ParallelCount = 4
            }).ToList();
        if (tasks.Count > 0) {
            var result = await new NetLoader(_single, _parallel)
                .DownloadAsync(tasks, progress, maxConcurrency: 8, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (!result.Success) {
                var error = result.Files.FirstOrDefault(file => file.State == NetFileState.Failed)?.Error;
                throw new InvalidOperationException("Forge/NeoForge 依赖下载失败。", error);
            }
        }

        await Task.Run(() => ForgelikeInjectorRunner.Run(
            javaExePath, installerPath, mcFolder,
            wrapperPath is not null, wrapperPath,
            cacheFolder, javaTempFolder, javaMajorVersion, isNeoForge,
            null, null, null, cancellationToken), cancellationToken).ConfigureAwait(false);

        var id = plan.Json["id"]?.ToString() ?? $"{loaderName}-{loaderVersion}-{mcVersion}";
        return Path.Combine(mcFolder, "versions", id);
    }

}

