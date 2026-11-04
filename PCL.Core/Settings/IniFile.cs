using System.Text;

namespace PCL.Core.Settings;

/// <summary>
/// 兼容 PCL 现有 Setup.ini 的 key:value 格式。
/// </summary>
public sealed class IniFile {

    private readonly string _baseDirectory;
    private readonly object _gate = new();

    public IniFile(string baseDirectory) {
        _baseDirectory = baseDirectory;
    }

    public string ResolvePath(string fileName) {
        if (fileName.Contains(":\\", StringComparison.Ordinal) || fileName.StartsWith("\\\\", StringComparison.Ordinal)) {
            return fileName;
        }
        return Path.Combine(_baseDirectory, "PCL", fileName + ".ini");
    }

    public string? Read(string fileName, string key, string? defaultValue = null) {
        lock (_gate) {
            var values = ReadAll(fileName);
            return values.TryGetValue(key, out var value) ? value : defaultValue;
        }
    }

    public bool HasKey(string fileName, string key) {
        lock (_gate) {
            return ReadAll(fileName).ContainsKey(key);
        }
    }

    public void Write(string fileName, string key, string? value) {
        lock (_gate) {
            var values = ReadAll(fileName);
            if (value is null) {
                if (!values.Remove(key)) return;
            } else {
                key = key.ReplaceLineEndings("");
                value = value.ReplaceLineEndings("");
                if (values.TryGetValue(key, out var existing) && existing == value) return;
                values[key] = value;
            }

            var path = ResolvePath(fileName);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var content = new StringBuilder();
            foreach (var pair in values) {
                content.Append(pair.Key).Append(':').Append(pair.Value).Append("\r\n");
            }
            File.WriteAllText(path, content.ToString(), new UTF8Encoding(false));
        }
    }

    private Dictionary<string, string> ReadAll(string fileName) {
        var path = ResolvePath(fileName);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return result;
        foreach (var line in File.ReadAllLines(path, Encoding.UTF8)) {
            var separator = line.IndexOf(':');
            if (separator <= 0) continue;
            result[line.Substring(0, separator)] = line.Substring(separator + 1);
        }
        return result;
    }

}
