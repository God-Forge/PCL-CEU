using System.IO;
using MeloongCore;
using Newtonsoft.Json.Linq;
using PCL.Core.Login;

namespace PCL.Core.Minecraft;

public sealed class LaunchPlan {

    public string MainClass { get; init; } = "";
    public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    public string Classpath { get; init; } = "";
    public string NativesFolder { get; init; } = "";

}

/// <summary>
/// 从版本 JSON 构建一次启动所需的参数、classpath 与 Natives 目录。
/// </summary>
public static class LaunchPlanService {

    public static LaunchPlan Build(
        JObject versionJson,
        string mcFolder,
        string instanceFolder,
        string versionName,
        LoginProfile login,
        int width = 854,
        int height = 480,
        int ramMb = 4096,
        int windowType = WindowSizeResolver.TypeDefault,
        IReadOnlyList<string>? extraJvmArguments = null) {
        var mergedJson = ResolveVersionJson(mcFolder, versionName, versionJson);
        var libraries = LibraryResolver.Resolve(mergedJson, mcFolder).ToList();
        var classpathEntries = libraries.Select(library => new ClasspathEntry {
            Name = library.Name,
            LocalPath = Path.Combine(mcFolder, library.LocalPath),
            IsNative = library.IsNative
        }).ToList();
        var mainJars = new[] { versionJson["jar"]?.ToString(), versionName, mergedJson["jar"]?.ToString() }
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(id => {
                var path = Path.Combine(instanceFolder, id! + ".jar");
                if (File.Exists(path)) return path;
                path = Path.Combine(mcFolder, "versions", id!, id! + ".jar");
                return File.Exists(path) ? path : null;
            })
            .Where(path => path is not null)
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var jar in mainJars) {
            classpathEntries.Add(new ClasspathEntry { Name = "main", LocalPath = jar });
        }
        var classpath = ClasspathBuilder.Build(classpathEntries);
        var natives = libraries.Where(library => library.IsNative)
            .Select(library => Path.Combine(mcFolder, library.LocalPath))
            .ToList();
        var nativesFolder = NativesFolderResolver.Resolve(
            instanceFolder, versionName, false,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetLogicalDrives().First(Directory.Exists),
            PathUtils.ToShortPath);
        if (natives.Count > 0) {
            NativesExtractor.Extract(natives, nativesFolder);
        }

        var arguments = new List<string>();
        JvmMemoryArgumentBuilder.Apply(arguments, ramMb, 0, Environment.Version.Major, Environment.OSVersion.Version, false);
        arguments.AddRange(JvmArgumentCollector.Collect(mergedJson, Environment.OSVersion.Version.ToString(), extraJvmArguments));
        if (!arguments.Any(argument => argument.StartsWith("-Djava.library.path=", StringComparison.OrdinalIgnoreCase))) {
            arguments.Add("-Djava.library.path=" + PathUtils.RemoveSlashSuffix(nativesFolder));
        }
        if (!arguments.Any(argument => argument is "-cp" or "-classpath")) {
            arguments.Add("-cp");
            arguments.Add(classpath);
        }

        var gameArguments = GameArgumentCollector.Collect(mergedJson, Environment.OSVersion.Version.ToString()).ToList();
        if (gameArguments.Count == 0 && mergedJson["minecraftArguments"] is not null) {
            gameArguments.AddRange(JavaArgumentParser.Split(
                LegacyGameArgumentBuilder.Build(mergedJson["minecraftArguments"]!.ToString())));
        }
        arguments.Add(mergedJson["mainClass"]?.ToString() ?? "net.minecraft.client.main.Main");
        arguments.AddRange(gameArguments);
        if (WindowSizeResolver.IsFullscreen(windowType)) arguments.Add("--fullscreen");

        var context = new LaunchTokenContext {
            NativesDirectory = PathUtils.RemoveSlashSuffix(nativesFolder),
            LibrariesDirectory = Path.Combine(mcFolder, "libraries"),
            PureDirectory = AppContext.BaseDirectory,
            LauncherVersion = "0.10",
            VersionName = versionName,
            GameDirectory = instanceFolder,
            AssetsRoot = Path.Combine(mcFolder, "assets"),
            AuthPlayerName = login.Name,
            AuthUuid = login.Uuid,
            AccessToken = login.AccessToken,
            ClientId = login.ClientId,
            Xuid = login.Xuid,
            UserType = login.UserType,
            PrimaryJar = Path.Combine(instanceFolder, versionName + ".jar"),
            GameAssets = Path.Combine(mcFolder, "assets", "virtual", "legacy"),
            AssetsIndexName = mergedJson["assetIndex"]?["id"]?.ToString() ?? "legacy",
            VersionType = mergedJson["type"]?.ToString() ?? "",
            Width = width,
            Height = height,
            Classpath = classpath
        };
        var tokens = LaunchTokenBuilder.Build(context, arguments);
        return new LaunchPlan {
            MainClass = mergedJson["mainClass"]?.ToString() ?? "net.minecraft.client.main.Main",
            Arguments = LaunchArgumentFormatter.Replace(arguments, tokens),
            Classpath = classpath,
            NativesFolder = nativesFolder
        };
    }

    private static JObject ResolveVersionJson(string mcFolder, string versionName, JObject versionJson) {
        var current = (JObject)versionJson.DeepClone();
        var parent = current["inheritsFrom"]?.ToString();
        var guard = 0;
        while (!string.IsNullOrWhiteSpace(parent) && guard++ < 16) {
            var path = Path.Combine(mcFolder, "versions", parent!, parent! + ".json");
            if (!File.Exists(path)) break;
            var parentJson = JObject.Parse(File.ReadAllText(path));
            parentJson.Merge(current);
            current = parentJson;
            parent = current["inheritsFrom"]?.ToString();
        }
        return current;
    }

}


