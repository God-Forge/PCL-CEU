using System.Text;
using Newtonsoft.Json;

namespace PCL.Core.IO;

/// <summary>
/// PCL 常用文件操作封装，统一 UTF-8 与异常处理。
/// </summary>
public static class PclFileService {

    private static readonly UTF8Encoding Utf8 = new(false);

    public static string ReadText(string path) => File.ReadAllText(path, Utf8);

    public static void WriteText(string path, string content) {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, content, Utf8);
    }

    public static T? ReadJson<T>(string path) {
        return JsonConvert.DeserializeObject<T>(ReadText(path));
    }

    public static void WriteJson<T>(string path, T value) {
        WriteText(path, JsonConvert.SerializeObject(value, Formatting.Indented));
    }

    public static bool TryDelete(string path) {
        try {
            if (File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path)) Directory.Delete(path, true);
            return true;
        } catch {
            return false;
        }
    }

    public static void CopyDirectory(string source, string target, Func<string, bool>? include = null) {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) {
            if (include is not null && !include(file)) continue;
            var relative = file.Substring(Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar).Length + 1);
            var destination = Path.Combine(target, relative);
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.Copy(file, destination, true);
        }
    }

    public static string ComputeSha256(string path) {
        using var stream = File.OpenRead(path);
        using var sha = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }

}
