using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace PCL.Core.Tools;

/// <summary>
/// 调用内置 Payload 编解码工具，并在首次使用时释放到 runtime。
/// </summary>
public static class PayloadService {

    private const string ToolsVersion = "2";
    private static readonly object Gate = new();

    public static (string Encode, string Decode) EnsureRuntimeTools(string baseDirectory) {
        lock (Gate) {
            var runtime = Path.Combine(baseDirectory, "runtime");
            Directory.CreateDirectory(runtime);
            var encode = Path.Combine(runtime, "encode.exe");
            var decode = Path.Combine(runtime, "decode.exe");
            var versionFile = Path.Combine(runtime, "payload-tools.version");
            if (File.Exists(encode) && File.Exists(decode) && File.Exists(versionFile)
                && File.ReadAllText(versionFile).Trim() == ToolsVersion) {
                return (encode, decode);
            }

            Extract("PCL.Resources.Payload.encode.exe", encode);
            Extract("PCL.Resources.Payload.decode.exe", decode);
            File.WriteAllText(versionFile, ToolsVersion);
            return (encode, decode);
        }
    }

    public static string Encode(string baseDirectory, string inputFolder, string outputFile, string? name = null) {
        if (!Directory.Exists(inputFolder)) throw new DirectoryNotFoundException("输入文件夹不存在：" + inputFolder);
        var tools = EnsureRuntimeTools(baseDirectory);
        var arguments = Quote(inputFolder.TrimEnd('\\', '/')) + " " + Quote(outputFile);
        if (!string.IsNullOrWhiteSpace(name)) {
            arguments += " --name " + Quote(name!.Replace("\"", ""));
        }
        return Run(tools.Encode, arguments, "打包");
    }

    public static string Decode(string baseDirectory, string inputFile, string outputFolder) {
        if (!File.Exists(inputFile)) throw new FileNotFoundException("Payload 文件不存在。", inputFile);
        var tools = EnsureRuntimeTools(baseDirectory);
        Directory.CreateDirectory(outputFolder);
        return Run(tools.Decode, Quote(inputFile) + " " + Quote(outputFolder.TrimEnd('\\', '/')), "解包");
    }

    public static string Import(string baseDirectory, string payloadFile, string mcFolder, string? name = null) {
        if (!File.Exists(payloadFile)) throw new FileNotFoundException("Payload 文件不存在。", payloadFile);
        var temp = Path.Combine(Path.GetTempPath(), "PCL", "PayloadImport", Guid.NewGuid().ToString("N"));
        string? target = null;
        try {
            Decode(baseDirectory, payloadFile, temp);
            var jsonFile = Directory.EnumerateFiles(temp, "*.json", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(file => {
                    try {
                        var json = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(file));
                        return json["id"] is not null;
                    } catch {
                        return false;
                    }
                }) ?? throw new InvalidDataException("Payload 中没有找到有效的版本 JSON。");
            var jsonObject = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(jsonFile));
            var id = jsonObject["id"]?.ToString() ?? "";
            var instanceName = string.IsNullOrWhiteSpace(name)
                ? (string.IsNullOrWhiteSpace(id) ? Path.GetFileNameWithoutExtension(payloadFile) : id)
                : name!;
            instanceName = instanceName.Trim();
            if (string.IsNullOrWhiteSpace(instanceName) || instanceName is "." or ".."
                || instanceName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                throw new InvalidDataException("实例名无效：" + instanceName);
            }

            target = Path.Combine(mcFolder, "versions", instanceName);
            if (Directory.Exists(target)) throw new IOException("实例已存在：" + instanceName);
            CopyDirectory(temp, target);

            var oldJson = Path.Combine(target, Path.GetFileName(jsonFile));
            var oldBase = Path.GetFileNameWithoutExtension(jsonFile);
            if (!string.Equals(Path.GetFileName(jsonFile), instanceName + ".json", StringComparison.OrdinalIgnoreCase)
                && File.Exists(oldJson)) {
                File.Move(oldJson, Path.Combine(target, instanceName + ".json"));
            }
            if (!string.Equals(oldBase, instanceName, StringComparison.OrdinalIgnoreCase)) {
                var oldJar = Path.Combine(target, oldBase + ".jar");
                if (File.Exists(oldJar)) File.Move(oldJar, Path.Combine(target, instanceName + ".jar"));
            }
            return instanceName;
        } catch {
            if (target is not null && Directory.Exists(target)) {
                try { Directory.Delete(target, true); } catch { }
            }
            throw;
        } finally {
            try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
        }
    }

    private static void Extract(string resourceName, string target) {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
                           ?? throw new InvalidOperationException("内置 Payload 工具资源缺失：" + resourceName);
        using var output = File.Create(target);
        stream.CopyTo(output);
    }

    private static string Run(string exe, string arguments, string action) {
        var info = new ProcessStartInfo {
            FileName = exe,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var process = Process.Start(info) ?? throw new InvalidOperationException("无法启动 Payload 工具。");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        var text = (output + "\r\n" + error).Trim();
        if (process.ExitCode != 0) throw new InvalidOperationException($"{action}失败（退出码 {process.ExitCode}）：{text}");
        return text;
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "") + "\"";

    private static void CopyDirectory(string source, string target) {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) {
            var relative = file.Substring(source.Length + 1);
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
        }
    }

}
