using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Minecraft;

/// <summary>
/// 根据版本 JSON 与资源索引生成原版安装下载任务，不执行安装。
/// </summary>
public static class VanillaInstallPlanner {

    public static IReadOnlyList<NetFileTask> Plan(
        JObject versionJson,
        string mcFolder,
        string assetIndexJson,
        string? assetBaseUrl = null) {
        var result = new List<NetFileTask>();
        var versionId = versionJson["id"]?.ToString() ?? throw new InvalidDataException("版本 JSON 缺少 id。");
        var versionFolder = System.IO.Path.Combine(mcFolder, "versions", versionId);

        var client = versionJson["downloads"]?["client"];
        if (client is not null) {
            var url = client["url"]?.ToString();
            if (!string.IsNullOrWhiteSpace(url)) {
                result.Add(new NetFileTask {
                    Name = versionId + ".jar",
                    TargetPath = System.IO.Path.Combine(versionFolder, versionId + ".jar"),
                    Sources = [url!],
                    Sha256 = null,
                    Sha1 = client["sha1"]?.ToString(),
                    MinSize = client["size"]?.ToObject<long>(),
                    UseParallel = true,
                    ParallelCount = 4
                });
            }
        }

        foreach (var library in LibraryResolver.Resolve(versionJson, mcFolder)) {
            if (string.IsNullOrWhiteSpace(library.Url)) continue;
            result.Add(new NetFileTask {
                Name = library.Name,
                TargetPath = System.IO.Path.Combine(mcFolder, library.LocalPath),
                Sources = [library.Url!],
                Sha256 = null,
                Sha1 = library.Sha1,
                MinSize = library.Size >= 0 ? library.Size : null,
                UseParallel = true,
                ParallelCount = 4
            });
        }

        var assetIndex = versionJson["assetIndex"];
        var assetIndexId = assetIndex?["id"]?.ToString();
        if (!string.IsNullOrWhiteSpace(assetIndexId)) {
            var url = assetIndex?["url"]?.ToString();
            if (!string.IsNullOrWhiteSpace(url)) {
                result.Add(new NetFileTask {
                    Name = "asset-index-" + assetIndexId,
                    TargetPath = System.IO.Path.Combine(mcFolder, "assets", "indexes", assetIndexId + ".json"),
                    Sources = [url!],
                    MinSize = assetIndex?["size"]?.ToObject<long>(),
                    Sha1 = assetIndex?["sha1"]?.ToString(),
                    UseParallel = false
                });
            }
        }

        var baseUrl = string.IsNullOrWhiteSpace(assetBaseUrl)
            ? "https://resources.download.minecraft.net/"
            : assetBaseUrl!.TrimEnd('/') + "/";
        var mirrorBase = "https://bmclapi2.bangbang93.com/assets/";
        if (assetIndexJson is not null) {
            var index = JObject.Parse(assetIndexJson);
            if (index["objects"] is JObject objects) {
                foreach (var property in objects.Properties()) {
                    var hash = property.Value["hash"]?.ToString();
                    if (string.IsNullOrWhiteSpace(hash) || hash!.Length < 2) continue;
                    var prefix = hash.Substring(0, 2);
                    result.Add(new NetFileTask {
                        Name = property.Name,
                        TargetPath = System.IO.Path.Combine(mcFolder, "assets", "objects", prefix, hash),
                        Sources = [
                            baseUrl + prefix + "/" + hash,
                            mirrorBase + prefix + "/" + hash
                        ],
                        MinSize = property.Value["size"]?.ToObject<long>(),
                        Sha1 = hash,
                        UseParallel = false
                    });
                }
            }
        }

        return result;
    }

}
