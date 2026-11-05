using System.Globalization;

namespace PCL.Core.Settings;

/// <summary>
/// 实例级设置：每个实例独立存储在 <c>&lt;版本目录&gt;\PCL\Setup.ini</c>。
/// 对应 VB 版 Settings 中 <c>Sources.Instance</c> 的键。
/// </summary>
public sealed class InstanceSettings {

    /// <summary>IniFile 会自动补上 PCL 子目录与 .ini 后缀。</summary>
    private const string FileName = "Setup";

    private readonly IniFile _ini;

    public InstanceSettings(string instanceFolder) {
        if (string.IsNullOrWhiteSpace(instanceFolder)) {
            throw new ArgumentException("实例目录不能为空", nameof(instanceFolder));
        }
        InstanceFolder = NormalizeFolder(instanceFolder);
        _ini = new IniFile(InstanceFolder);
    }

    /// <summary>实例目录的绝对路径（无尾部分隔符）。</summary>
    public string InstanceFolder { get; }

    /// <summary>设置文件路径。</summary>
    public string SettingsPath => _ini.ResolvePath(FileName);

    /// <summary>读取原始字符串，未设置时返回 <c>null</c>。</summary>
    public string? GetRaw(string key) {
        if (string.IsNullOrWhiteSpace(key)) return null;
        return _ini.Read(FileName, key);
    }

    /// <summary>该键是否在实例中显式设置过。</summary>
    public bool Has(string key) => !string.IsNullOrWhiteSpace(key) && _ini.HasKey(FileName, key);

    public string GetString(string key, string fallback = "") => GetRaw(key) ?? fallback;

    public int GetInt(string key, int fallback = 0) =>
        int.TryParse(GetRaw(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    public bool GetBool(string key, bool fallback = false) =>
        bool.TryParse(GetRaw(key), out var value) ? value : fallback;

    /// <summary>写入设置。<paramref name="value"/> 为 <c>null</c> 时删除该项。</summary>
    public void Set(string key, string? value) {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("设置键不能为空", nameof(key));
        _ini.Write(FileName, key, value);
    }

    public void SetInt(string key, int value) => Set(key, value.ToString(CultureInfo.InvariantCulture));

    public void SetBool(string key, bool value) => Set(key, value ? "True" : "False");

    /// <summary>删除某一项，恢复为「未设置」。</summary>
    public void Remove(string key) => Set(key, null);

    /// <summary>
    /// 取实例设置，未设置时回退到调用方提供的全局值。
    /// </summary>
    public T GetWithFallback<T>(string key, Func<T> globalFallback) {
        if (Has(key)) {
            var raw = GetRaw(key);
            if (typeof(T) == typeof(int) && int.TryParse(raw, out var number)) return (T)(object)number;
            if (typeof(T) == typeof(bool) && bool.TryParse(raw, out var flag)) return (T)(object)flag;
            if (typeof(T) == typeof(string)) return (T)(object)(raw ?? "");
        }
        return globalFallback();
    }

    internal static string NormalizeFolder(string folder) =>
        Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

}
