using System.IO.Compression;
using System.Text;
using Newtonsoft.Json.Linq;
using PCL.Core.Settings;

namespace PCL.Core.Transfer;

/// <summary>搬机包的导出选项。</summary>
public sealed class MachineTransferOptions {

    /// <summary>要打包的实例目录（<c>versions/&lt;实例名&gt;</c>）。</summary>
    public string InstanceFolder { get; init; } = "";

    /// <summary>输出的 <c>.pcltransfer</c> 文件路径。</summary>
    public string OutputPath { get; init; } = "";

    /// <summary>是否把 Java 运行时一起打包（体积会明显变大）。</summary>
    public bool IncludeJava { get; init; }

    /// <summary><see cref="IncludeJava"/> 为 true 时必须提供的 Java 主目录。</summary>
    public string? JavaHome { get; init; }

    public bool IncludeSaves { get; init; } = true;

    public bool IncludeScreenshots { get; init; }

    public bool IncludeLogs { get; init; }

    /// <summary>要一起带走的实例级设置（只接受 <see cref="InstanceSettingKeys.All"/> 中的键）。</summary>
    public IReadOnlyDictionary<string, object?>? Settings { get; init; }

}

/// <summary>搬机包的清单，保存为包内 <c>transfer.json</c>。</summary>
public sealed class MachineTransferManifest {

    public const string FormatName = "pcl-machine-transfer";
    public const int CurrentVersion = 1;

    public string Format { get; init; } = FormatName;

    public int Version { get; init; } = CurrentVersion;

    public string InstanceName { get; init; } = "";

    /// <summary>实例文件夹里的版本 id（版本 JSON 的 id）。</summary>
    public string VersionId { get; init; } = "";

    public string MinecraftVersion { get; init; } = "";

    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

    public bool IncludesJava { get; init; }

    public string JavaVersion { get; init; } = "";

    public string JavaType { get; init; } = "jre";

    public int FileCount { get; init; }

    public long Size { get; init; }

    public IReadOnlyDictionary<string, object?> Settings { get; init; } = new Dictionary<string, object?>();

}

/// <summary>导出或导入后的结果。</summary>
public sealed class MachineTransferResult {

    public string InstanceName { get; init; } = "";

    public string InstancePath { get; init; } = "";

    public string? JavaHome { get; init; }

    public int FileCount { get; init; }

    public long Size { get; init; }

    public IReadOnlyDictionary<string, object?> Settings { get; init; } = new Dictionary<string, object?>();

}

/// <summary>导入前的预览信息。</summary>
public sealed class MachineTransferPreview {

    public MachineTransferManifest Manifest { get; init; } = new();

    public string PackagePath { get; init; } = "";

    public long PackageSize { get; init; }

}

/// <summary>
/// 一键搬机：把「实例 + 可选 Java + 实例级设置」打成一个包，在另一台机器上导入即可继续玩。
/// 包内不会包含任何账号令牌，只带走白名单内的实例设置。
/// </summary>
public static class MachineTransferService {

    public const string ManifestEntry = "transfer.json";
    public const string DefaultExtension = ".pcltransfer";
    public const string InstanceFolderEntry = "instance/";
    public const string JavaFolderEntry = "java/";

    /// <summary>导出时永远跳过的文件（相对实例目录，使用 / 分隔）。</summary>
    private static readonly string[] AlwaysExcludedFiles = {
        "PCL/LoginCache.json",
        "PCL/AccountStore.json",
        "launcher_accounts.json"
    };

    /// <summary>把实例打包成搬机包。</summary>
    public static MachineTransferResult Export(MachineTransferOptions options) {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.InstanceFolder)) throw new ArgumentException("实例目录不能为空。", nameof(options));
        if (string.IsNullOrWhiteSpace(options.OutputPath)) throw new ArgumentException("输出路径不能为空。", nameof(options));
        if (!Directory.Exists(options.InstanceFolder)) throw new DirectoryNotFoundException("实例目录不存在：" + options.InstanceFolder);

        var instanceFolder = Path.GetFullPath(options.InstanceFolder)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var instanceName = Path.GetFileName(instanceFolder);
        if (string.IsNullOrWhiteSpace(instanceName)) throw new InvalidOperationException("无法从目录名推断实例名。");

        var output = Path.GetFullPath(options.OutputPath);
        if (output.StartsWith(instanceFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidOperationException("输出文件不能放在实例目录里，否则会把自身打包进去。");
        }
        var outputDirectory = Path.GetDirectoryName(output);
        if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);

        if (options.IncludeJava) {
            if (string.IsNullOrWhiteSpace(options.JavaHome)) throw new InvalidOperationException("打包 Java 时必须指定 Java 主目录。");
            if (!File.Exists(Path.Combine(options.JavaHome, "bin", "java.exe"))) {
                throw new DirectoryNotFoundException("指定的 Java 目录无效（缺少 bin/java.exe）：" + options.JavaHome);
            }
        }

        var settings = FilterSettings(options.Settings);
        var temp = Path.Combine(Path.GetTempPath(), "pcl-transfer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        var fileCount = 0;
        try {
            var instanceTarget = Path.Combine(temp, "instance");
            Directory.CreateDirectory(instanceTarget);
            foreach (var file in EnumerateExportFiles(instanceFolder, options)) {
                var relative = GetRelativePath(instanceFolder, file);
                var target = Path.Combine(instanceTarget, relative);
                var parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                File.Copy(file, target, true);
                fileCount++;
            }

            var versionId = ReadVersionId(instanceFolder, instanceName);
            var includesJava = false;
            var javaVersion = "";
            var javaType = "jre";
            if (options.IncludeJava && !string.IsNullOrWhiteSpace(options.JavaHome)) {
                var javaTarget = Path.Combine(temp, "java");
                Directory.CreateDirectory(javaTarget);
                foreach (var file in Directory.EnumerateFiles(options.JavaHome!, "*", SearchOption.AllDirectories)) {
                    var relative = GetRelativePath(options.JavaHome!, file);
                    var target = Path.Combine(javaTarget, relative);
                    var parent = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    File.Copy(file, target, true);
                    fileCount++;
                }
                includesJava = true;
                javaVersion = ReadJavaVersion(options.JavaHome!);
                javaType = Directory.Exists(Path.Combine(options.JavaHome!, "jmods"))
                           || File.Exists(Path.Combine(options.JavaHome!, "bin", "javac.exe"))
                    ? "jdk"
                    : "jre";
            }

            var manifest = new MachineTransferManifest {
                InstanceName = instanceName,
                VersionId = versionId,
                MinecraftVersion = GuessMinecraftVersion(versionId),
                CreatedUtc = DateTime.UtcNow,
                IncludesJava = includesJava,
                JavaVersion = javaVersion,
                JavaType = javaType,
                FileCount = fileCount,
                Settings = settings
            };
            File.WriteAllText(Path.Combine(temp, ManifestEntry), BuildManifestJson(manifest).ToString(Newtonsoft.Json.Formatting.Indented), new UTF8Encoding(false));

            if (File.Exists(output)) File.Delete(output);
            ZipFile.CreateFromDirectory(temp, output, CompressionLevel.Optimal, false);
            var size = new FileInfo(output).Length;

            return new MachineTransferResult {
                InstanceName = instanceName,
                InstancePath = instanceFolder,
                FileCount = fileCount,
                Size = size,
                Settings = settings
            };
        } catch {
            try { if (File.Exists(output)) File.Delete(output); } catch { }
            throw;
        } finally {
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    /// <summary>读取搬机包清单，不修改任何文件。</summary>
    public static MachineTransferPreview Inspect(string packagePath) {
        if (!File.Exists(packagePath)) throw new FileNotFoundException("搬机包不存在。", packagePath);
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.GetEntry(ManifestEntry) ?? throw new InvalidDataException("不是 PCL 搬机包（缺少 transfer.json）。");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        var manifest = ParseManifest(JObject.Parse(reader.ReadToEnd()));
        return new MachineTransferPreview {
            Manifest = manifest,
            PackagePath = Path.GetFullPath(packagePath),
            PackageSize = new FileInfo(packagePath).Length
        };
    }

    /// <summary>
    /// 导入搬机包。<paramref name="minecraftFolder"/> 是 <c>.minecraft</c> 目录；
    /// <paramref name="javaRoot"/> 为 <c>PCL/Java</c>，包内带 Java 时会释放到这里。
    /// 目标实例已存在时直接抛异常，不会覆盖。
    /// </summary>
    public static MachineTransferResult Import(
        string packagePath,
        string minecraftFolder,
        string? targetName = null,
        string? javaRoot = null) {
        if (!File.Exists(packagePath)) throw new FileNotFoundException("搬机包不存在。", packagePath);
        if (string.IsNullOrWhiteSpace(minecraftFolder)) throw new ArgumentException("Minecraft 目录不能为空。", nameof(minecraftFolder));

        var preview = Inspect(packagePath);
        var manifest = preview.Manifest;
        var instanceName = string.IsNullOrWhiteSpace(targetName) ? manifest.InstanceName : targetName!.Trim();
        ValidateInstanceName(instanceName);

        var versions = Path.Combine(minecraftFolder, "versions");
        Directory.CreateDirectory(versions);
        var target = Path.Combine(versions, instanceName);
        if (Directory.Exists(target)) throw new IOException("实例已存在，已取消导入：" + instanceName);

        var temp = Path.Combine(Path.GetTempPath(), "pcl-transfer-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var fileCount = 0;
        string? javaHome = null;
        try {
            using (var archive = ZipFile.OpenRead(packagePath)) {
                foreach (var entry in archive.Entries) {
                    if (string.Equals(entry.FullName, ManifestEntry, StringComparison.OrdinalIgnoreCase)) continue;
                    if (entry.FullName.Length == 0) continue;
                    var relative = entry.FullName.Replace('\\', '/').TrimStart('/');
                    if (relative.Length == 0) continue;
                    if (!relative.StartsWith(InstanceFolderEntry, StringComparison.OrdinalIgnoreCase)
                        && !relative.StartsWith(JavaFolderEntry, StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }
                    var safe = relative.Replace('/', Path.DirectorySeparatorChar);
                    var destination = Path.GetFullPath(Path.Combine(temp, safe));
                    var root = Path.GetFullPath(temp).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) {
                        throw new InvalidDataException("搬机包包含不安全的路径：" + entry.FullName);
                    }
                    if (relative.EndsWith("/", StringComparison.Ordinal)) {
                        Directory.CreateDirectory(destination);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    using var input = entry.Open();
                    using var output = File.Create(destination);
                    input.CopyTo(output);
                    if (relative.StartsWith(InstanceFolderEntry, StringComparison.OrdinalIgnoreCase)) fileCount++;
                }
            }

            var instanceTemp = Path.Combine(temp, "instance");
            if (!Directory.Exists(instanceTemp)) throw new InvalidDataException("搬机包里没有实例内容。");
            if (FindVersionJson(instanceTemp, instanceName, manifest.VersionId) is null) {
                throw new InvalidDataException("搬机包里的实例缺少版本 JSON。");
            }

            Directory.Move(instanceTemp, target);
            NormalizeJsonName(target, manifest.VersionId, instanceName);

            var javaTemp = Path.Combine(temp, "java");
            if (manifest.IncludesJava && Directory.Exists(javaTemp) && !string.IsNullOrWhiteSpace(javaRoot)) {
                var javaFolderName = $"{manifest.JavaVersion}-{manifest.JavaType}".Replace(":", "_");
                var javaTarget = Path.Combine(javaRoot!, javaFolderName);
                if (!Directory.Exists(javaTarget)) {
                    Directory.CreateDirectory(javaRoot!);
                    Directory.Move(javaTemp, javaTarget);
                    javaHome = javaTarget;
                }
            }

            return new MachineTransferResult {
                InstanceName = instanceName,
                InstancePath = target,
                JavaHome = javaHome,
                FileCount = fileCount,
                Size = new FileInfo(packagePath).Length,
                Settings = manifest.Settings
            };
        } catch {
            if (Directory.Exists(target)) {
                try { Directory.Delete(target, true); } catch { }
            }
            throw;
        } finally {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
        }
    }

    /// <summary>把设置字典限制在实例级白名单内，并忽略空键。</summary>
    public static IReadOnlyDictionary<string, object?> FilterSettings(IReadOnlyDictionary<string, object?>? settings) {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (settings is null) return result;
        foreach (var pair in settings) {
            if (string.IsNullOrWhiteSpace(pair.Key)) continue;
            if (!InstanceSettingKeys.IsInstanceLevel(pair.Key)) continue;
            result[pair.Key] = pair.Value;
        }
        return result;
    }

    public static string DefaultFileName(string instanceName) =>
        Sanitize(instanceName) + DefaultExtension;

    private static IEnumerable<string> EnumerateExportFiles(string instanceFolder, MachineTransferOptions options) {
        var root = instanceFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)) {
            var relative = GetRelativePath(root, file).Replace('\\', '/');
            if (IsExcluded(relative, options)) continue;
            yield return file;
        }
    }

    private static bool IsExcluded(string relativePath, MachineTransferOptions options) {
        if (AlwaysExcludedFiles.Any(item => string.Equals(relativePath, item, StringComparison.OrdinalIgnoreCase))) return true;

        // 日志目录默认不带；勾选「包含日志」后才打包
        if (IsUnder(relativePath, "logs") || IsUnder(relativePath, "PCL/Logs")) return !options.IncludeLogs;
        if (IsUnder(relativePath, "crash-reports") || IsUnder(relativePath, ".cache")) return true;

        if (!options.IncludeSaves && IsUnder(relativePath, "saves")) return true;
        if (!options.IncludeScreenshots && IsUnder(relativePath, "screenshots")) return true;
        return false;
    }

    private static bool IsUnder(string relativePath, string folder) =>
        string.Equals(relativePath, folder, StringComparison.OrdinalIgnoreCase)
        || relativePath.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase);

    private static string ReadVersionId(string instanceFolder, string instanceName) {
        var preferred = Path.Combine(instanceFolder, instanceName + ".json");
        var jsonPath = File.Exists(preferred)
            ? preferred
            : Directory.EnumerateFiles(instanceFolder, "*.json", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (jsonPath is null) return instanceName;
        try {
            var json = JObject.Parse(File.ReadAllText(jsonPath));
            var id = json["id"]?.ToString();
            return string.IsNullOrWhiteSpace(id) ? instanceName : id!;
        } catch {
            return instanceName;
        }
    }

    private static string ReadJavaVersion(string javaHome) {
        var release = Path.Combine(javaHome, "release");
        if (File.Exists(release)) {
            foreach (var line in File.ReadAllLines(release)) {
                if (!line.StartsWith("JAVA_VERSION=", StringComparison.OrdinalIgnoreCase)) continue;
                return line.Substring("JAVA_VERSION=".Length).Trim().Trim('"');
            }
        }
        return "unknown";
    }

    /// <summary>从版本 id 里猜出 Minecraft 版本，例如 <c>1.20.1-Forge_47.4.8</c> → <c>1.20.1</c>。</summary>
    private static string GuessMinecraftVersion(string versionId) {
        if (string.IsNullOrWhiteSpace(versionId)) return "";
        var match = System.Text.RegularExpressions.Regex.Match(versionId, @"^\d+\.\d+(?:\.\d+)?");
        return match.Success ? match.Value : versionId;
    }

    private static JObject BuildManifestJson(MachineTransferManifest manifest) {
        var settings = new JObject();
        foreach (var pair in manifest.Settings) {
            settings[pair.Key] = pair.Value is null ? JValue.CreateNull() : JToken.FromObject(pair.Value);
        }
        return new JObject {
            ["format"] = manifest.Format,
            ["version"] = manifest.Version,
            ["instanceName"] = manifest.InstanceName,
            ["versionId"] = manifest.VersionId,
            ["minecraftVersion"] = manifest.MinecraftVersion,
            ["createdUtc"] = manifest.CreatedUtc.ToString("O"),
            ["includesJava"] = manifest.IncludesJava,
            ["javaVersion"] = manifest.JavaVersion,
            ["javaType"] = manifest.JavaType,
            ["fileCount"] = manifest.FileCount,
            ["settings"] = settings
        };
    }

    private static MachineTransferManifest ParseManifest(JObject json) {
        if (!string.Equals(json["format"]?.ToString(), MachineTransferManifest.FormatName, StringComparison.OrdinalIgnoreCase)) {
            throw new InvalidDataException("不是 PCL 搬机包。");
        }
        var version = json["version"]?.ToObject<int>() ?? 0;
        if (version != MachineTransferManifest.CurrentVersion) {
            throw new InvalidDataException("不支持的搬机包版本：" + version);
        }
        var settings = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (json["settings"] is JObject source) {
            foreach (var pair in source.Properties()) {
                settings[pair.Name] = pair.Value.Type == JTokenType.Null ? null : ToPlainValue(pair.Value);
            }
        }
        return new MachineTransferManifest {
            InstanceName = json["instanceName"]?.ToString() ?? "",
            VersionId = json["versionId"]?.ToString() ?? "",
            MinecraftVersion = json["minecraftVersion"]?.ToString() ?? "",
            CreatedUtc = DateTime.TryParse(json["createdUtc"]?.ToString(), null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var created)
                ? created.ToUniversalTime()
                : DateTime.UtcNow,
            IncludesJava = json["includesJava"]?.ToObject<bool>() ?? false,
            JavaVersion = json["javaVersion"]?.ToString() ?? "",
            JavaType = json["javaType"]?.ToString() ?? "jre",
            FileCount = json["fileCount"]?.ToObject<int>() ?? 0,
            Settings = FilterSettings(settings)
        };
    }

    private static object? ToPlainValue(JToken token) => token.Type switch {
        JTokenType.Boolean => token.ToObject<bool>(),
        JTokenType.Integer => token.ToObject<long>(),
        JTokenType.Float => token.ToObject<double>(),
        _ => token.ToString()
    };

    private static string? FindVersionJson(string instanceTemp, string instanceName, string versionId) {
        var candidates = new List<string> { Path.Combine(instanceTemp, instanceName + ".json") };
        if (!string.IsNullOrWhiteSpace(versionId)) candidates.Add(Path.Combine(instanceTemp, versionId + ".json"));
        foreach (var candidate in candidates) {
            if (File.Exists(candidate)) return candidate;
        }
        return Directory.EnumerateFiles(instanceTemp, "*.json", SearchOption.TopDirectoryOnly).FirstOrDefault();
    }

    /// <summary>导入后的实例名可能与包内 JSON 文件名不同，这里统一改名，避免启动器找不到版本 JSON。</summary>
    private static void NormalizeJsonName(string instanceFolder, string versionId, string instanceName) {
        var jsonPath = FindVersionJson(instanceFolder, instanceName, versionId);
        if (jsonPath is null) return;
        var fileName = Path.GetFileName(jsonPath);
        if (string.Equals(Path.GetFileNameWithoutExtension(fileName), instanceName, StringComparison.OrdinalIgnoreCase)) return;
        var target = Path.Combine(instanceFolder, instanceName + ".json");
        if (File.Exists(target)) return;
        File.Move(jsonPath, target);
        try {
            var json = JObject.Parse(File.ReadAllText(target));
            if (json["id"] is not null) {
                json["id"] = instanceName;
                File.WriteAllText(target, json.ToString(), new UTF8Encoding(false));
            }
        } catch {
            // 改名失败不影响启动，保持原内容
        }
    }

    private static void ValidateInstanceName(string name) {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("实例名为空。");
        if (name is "." or "..") throw new InvalidDataException("实例名无效：" + name);
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new InvalidDataException("实例名包含非法字符：" + name);
    }

    private static string Sanitize(string name) {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var character in name) {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }
        return builder.ToString();
    }

    private static string GetRelativePath(string root, string path) {
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var pathFull = Path.GetFullPath(path);
        if (pathFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) {
            return pathFull.Substring(rootFull.Length + 1);
        }
        return Path.GetFileName(pathFull);
    }

}
