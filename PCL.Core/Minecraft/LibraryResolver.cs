using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

public sealed class ResolvedLibrary {

    public string Name { get; init; } = "";
    public string LocalPath { get; init; } = "";
    public string? Url { get; init; }
    public string? Sha1 { get; init; }
    public long Size { get; init; } = -1;
    public bool IsNative { get; init; }

}

/// <summary>
/// 将版本 JSON 的 libraries 解析为本地路径与下载地址。
/// </summary>
public static class LibraryResolver {

    public static IReadOnlyList<ResolvedLibrary> Resolve(JObject versionJson, string mcFolder, RuleContext? context = null) {
        context ??= RuleContext.Current();
        var result = new List<ResolvedLibrary>();
        if (versionJson["libraries"] is not JArray libraries) return result;

        foreach (var library in libraries.OfType<JObject>()) {
            if (!RuleEvaluator.IsAllowed(library["rules"], context)) continue;

            var name = library["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var artifact = library["downloads"]?["artifact"] as JObject;
            var natives = library["natives"] as JObject;
            var path = artifact?["path"]?.ToString();
            if (string.IsNullOrWhiteSpace(path)) path = MavenPath(name!);
            if (string.IsNullOrWhiteSpace(path)) continue;

            var rootUrl = library["url"]?.ToString();
            var url = artifact?["url"]?.ToString();
            if (string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(rootUrl)) {
                url = rootUrl!.TrimEnd('/') + "/" + path!.Replace('\\', '/');
            }

            if (artifact is not null || natives is null) {
                result.Add(new ResolvedLibrary {
                    Name = name!,
                    LocalPath = System.IO.Path.Combine("libraries", path!.Replace('/', System.IO.Path.DirectorySeparatorChar)),
                    Url = url,
                    Sha1 = artifact?["sha1"]?.ToString(),
                    Size = artifact?["size"]?.ToObject<long>() ?? -1
                });
            }

            if (natives is null) continue;
            var classifier = natives[context.OsName]?.ToString();
            if (string.IsNullOrWhiteSpace(classifier)) continue;
            classifier = classifier!.Replace("${arch}", context.Arch == "x86" ? "32" : "64");
            var nativeArtifact = library["downloads"]?["classifiers"]?[classifier] as JObject;
            var nativePath = nativeArtifact?["path"]?.ToString();
            if (string.IsNullOrWhiteSpace(nativePath)) nativePath = MavenPath(name!, classifier);
            if (string.IsNullOrWhiteSpace(nativePath)) continue;
            var nativeUrl = nativeArtifact?["url"]?.ToString();
            if (string.IsNullOrWhiteSpace(nativeUrl) && !string.IsNullOrWhiteSpace(rootUrl)) {
                nativeUrl = rootUrl!.TrimEnd('/') + "/" + nativePath!.Replace('\\', '/');
            }
            result.Add(new ResolvedLibrary {
                Name = name + ":" + classifier,
                LocalPath = System.IO.Path.Combine("libraries", nativePath!.Replace('/', System.IO.Path.DirectorySeparatorChar)),
                Url = nativeUrl,
                Sha1 = nativeArtifact?["sha1"]?.ToString(),
                Size = nativeArtifact?["size"]?.ToObject<long>() ?? -1,
                IsNative = true
            });
        }

        return result;
    }

    public static string? GetMavenPath(string name, string? classifier = null) => MavenPath(name, classifier);

    public static string GetLocalPath(string name, string mcFolder, bool withHead = true) {
        var path = MavenPath(name) ?? throw new ArgumentException("无效的 Maven 名称：" + name, nameof(name));
        var local = path.Replace('/', System.IO.Path.DirectorySeparatorChar);
        return withHead
            ? System.IO.Path.Combine(mcFolder, "libraries", local)
            : local;
    }

    private static string? MavenPath(string name, string? classifier = null) {
        var parts = name.Split(':');
        if (parts.Length < 3) return null;
        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts[2];
        var fileClassifier = classifier ?? (parts.Length >= 4 ? parts[3] : null);
        var fileName = artifact + "-" + version + (string.IsNullOrWhiteSpace(fileClassifier) ? "" : "-" + fileClassifier) + ".jar";
        return $"{group}/{artifact}/{version}/{fileName}";
    }

}
