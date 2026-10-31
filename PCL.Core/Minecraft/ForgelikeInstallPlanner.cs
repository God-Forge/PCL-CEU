using System.IO.Compression;
using System.Text;
using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Minecraft;

public sealed class ForgelikeInstallPlan {

    public JObject Json { get; init; } = new();

}

/// <summary>
/// 读取 Forge/NeoForge 安装器内的 install_profile.json 与 version.json，
/// 合并元数据、补充 Mappings 并剔除安装器稍后自行生成的 Forgelike 主库。
/// </summary>
public static class ForgelikeInstallPlanner {

    public static async Task<ForgelikeInstallPlan> CreatePlanAsync(
        string installerPath,
        string loaderName,
        string mcVersion,
        string loaderVersion,
        IStringFetcher? fetcher = null,
        string? vanillaJsonUrl = null,
        CancellationToken cancellationToken = default) {
        JObject profile;
        JObject version;
        using (var archive = ZipFile.OpenRead(installerPath)) {
            profile = ReadEntry(archive, "install_profile.json");
            version = ReadEntry(archive, "version.json");
        }
        profile.Merge(version);

        if (profile["libraries"] is not JArray libraries) {
            throw new InvalidDataException("安装器缺少 libraries 字段。");
        }

        var loaderLower = loaderName.ToLowerInvariant();
        var generatedNames = new[] {
            $"{loaderLower}-{mcVersion}-{loaderVersion}.jar",
            $"{loaderLower}-{mcVersion}-{loaderVersion}-client.jar"
        };
        for (var i = libraries.Count - 1; i >= 0; i--) {
            var name = libraries[i]?["name"]?.ToString();
            var path = string.IsNullOrWhiteSpace(name) ? null : LibraryResolver.GetMavenPath(name!);
            if (path is not null && generatedNames.Any(value => path.EndsWith(value, StringComparison.OrdinalIgnoreCase))) {
                libraries.RemoveAt(i);
            }
        }

        var mappings = profile["data"]?["MOJMAPS"]?["client"]?.ToString();
        if (!string.IsNullOrWhiteSpace(mappings)) {
            if (string.IsNullOrWhiteSpace(vanillaJsonUrl)) {
                throw new InvalidDataException("安装器需要 Mappings，但未提供原版 JSON 地址。");
            }
            var vanillaText = await (fetcher ?? new HttpStringFetcher())
                .GetStringAsync(vanillaJsonUrl!, cancellationToken).ConfigureAwait(false);
            var vanilla = JObject.Parse(vanillaText);
            var clientMappings = vanilla["downloads"]?["client_mappings"] as JObject
                                 ?? throw new InvalidDataException("原版 JSON 缺少 client_mappings。");
            var token = mappings!.Trim('[', ']');
            var separator = token.LastIndexOf('@');
            var originalName = separator >= 0 ? token.Substring(0, separator) : token;
            var extension = separator >= 0 ? token.Substring(separator + 1) : "txt";
            var mavenPath = LibraryResolver.GetMavenPath(originalName)
                            ?? throw new InvalidDataException("Mappings 名称无效：" + originalName);
            libraries.Add(new JObject {
                ["name"] = originalName,
                ["downloads"] = new JObject {
                    ["artifact"] = new JObject {
                        ["path"] = Path.ChangeExtension(mavenPath, "." + extension),
                        ["url"] = clientMappings["url"],
                        ["sha1"] = clientMappings["sha1"],
                        ["size"] = clientMappings["size"]
                    }
                }
            });
        }

        return new ForgelikeInstallPlan { Json = profile };
    }

    private static JObject ReadEntry(ZipArchive archive, string name) {
        var entry = archive.GetEntry(name) ?? throw new InvalidDataException("安装器缺少 " + name);
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return JObject.Parse(reader.ReadToEnd());
    }

}
