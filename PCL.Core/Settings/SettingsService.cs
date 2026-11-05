using System.Security.Cryptography;
using MeloongCore;
using Microsoft.Win32;

namespace PCL.Core.Settings;

public enum SettingSource {
    Normal,
    Registry,
    Instance
}

public sealed class SettingDefinition {

    public string Key { get; init; } = "";
    public object? DefaultValue { get; init; }
    public SettingSource Source { get; init; }
    public bool Encrypted { get; init; }
    public Type Type { get; init; } = typeof(string);
    public Action<object?>? OnChanged { get; init; }
    internal Dictionary<string, object?> Cache { get; } = new(StringComparer.OrdinalIgnoreCase);

    public object? GetCache(string instanceKey) {
        return Cache.TryGetValue(instanceKey, out var value) ? value : null;
    }

    public void SetCache(string instanceKey, object? value) {
        Cache[instanceKey] = value;
    }

}

/// <summary>
/// 兼容 PCL 现有设置项：Setup.ini、注册表、实例 PCL/Setup.ini、加密值与变更事件。
/// </summary>
public sealed class SettingsService {

    private const string RegistryFolder = @"Software\PCLDebug";
    private static readonly byte[] InitialVector = "95168702"u8.ToArray();
    private static readonly Dictionary<string, object> ForcedSettings = new() {
        ["UiHiddenPageLink"] = true,
        ["UiHiddenSetupLink"] = true
    };

    private readonly Dictionary<string, SettingDefinition> _definitions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IniFile _ini;
    private readonly string _identify;

    public SettingsService(string baseDirectory, string? identify = null) {
        _ini = new IniFile(baseDirectory);
        _identify = identify ?? ReadRegistryValue("Identify", "") ?? "";
    }

    public void Register(SettingDefinition definition) {
        _definitions[definition.Key] = definition;
    }

    public void Register(string key, object? defaultValue, SettingSource source = SettingSource.Normal,
        bool encrypted = false, Action<object?>? onChanged = null) {
        Register(new SettingDefinition {
            Key = key,
            DefaultValue = defaultValue,
            Source = source,
            Encrypted = encrypted,
            Type = defaultValue?.GetType() ?? typeof(string),
            OnChanged = onChanged
        });
    }

    public object? Get(string key, string? instancePath = null) {
        var definition = GetDefinition(key);
        var instanceKey = GetInstanceKey(definition, instancePath);
        var cached = definition.GetCache(instanceKey);
        if (cached is not null) return cached;

        if (ForcedSettings.TryGetValue(key, out var forced)) {
            definition.SetCache(instanceKey, forced);
            return forced;
        }

        try {
            var defaultValue = Convert.ToString(definition.DefaultValue) ?? "";
            var value = ReadRaw(definition, instancePath) ?? defaultValue;
            if (definition.Encrypted && value != defaultValue) {
                try {
                    value = Decrypt(value);
                } catch (Exception ex) {
                    Logger.Warn(ex, "解密设置失败：" + key);
                    value = defaultValue;
                }
            }
            var converted = ConvertValue(value, definition.Type);
            definition.SetCache(instanceKey, converted);
            return converted;
        } catch (Exception ex) {
            Logger.Error(ex, "读取设置失败：" + key);
            var fallback = ConvertValue(definition.DefaultValue, definition.Type);
            definition.SetCache(instanceKey, fallback);
            return fallback;
        }
    }

    public T? Get<T>(string key, string? instancePath = null) {
        return (T?)Get(key, instancePath);
    }

    public void Set(string key, object? value, string? instancePath = null) {
        var definition = GetDefinition(key);
        var instanceKey = GetInstanceKey(definition, instancePath);
        try {
            var converted = ConvertValue(value, definition.Type);
            if (definition.GetCache(instanceKey) is null) Get(key, instancePath);
            if (Equals(definition.GetCache(instanceKey), converted)) return;

            definition.SetCache(instanceKey, converted);
            Save(definition, converted, instancePath);
            definition.OnChanged?.Invoke(converted);
        } catch (Exception ex) {
            Logger.Error(ex, $"设置设置项失败：{key}");
        }
    }

    public void Reset(string key, string? instancePath = null) {
        var definition = GetDefinition(key);
        var instanceKey = GetInstanceKey(definition, instancePath);
        try {
            if (definition.GetCache(instanceKey) is null) Get(key, instancePath);
            definition.SetCache(instanceKey, ConvertValue(definition.DefaultValue, definition.Type));
            DeleteRaw(definition, instancePath);
        } catch (Exception ex) {
            Logger.Error(ex, "重置设置失败：" + key);
        }
    }

    public bool HasSaved(string key, string? instancePath = null) {
        var definition = GetDefinition(key);
        return definition.Source switch {
            SettingSource.Normal => _ini.HasKey("Setup", key),
            SettingSource.Instance => instancePath is not null && _ini.HasKey(Path.Combine(instancePath, "PCL", "Setup.ini"), key),
            _ => ReadRegistryValue(key, null) is not null
        };
    }

    /// <summary>
    /// 丢弃某个实例的设置缓存。实例重命名或删除后必须调用，
    /// 否则会一直读到旧路径的缓存值。
    /// </summary>
    public void InvalidateInstanceCache(string? instancePath) {
        if (string.IsNullOrWhiteSpace(instancePath)) return;
        var normalized = NormalizeInstancePath(instancePath);
        foreach (var definition in _definitions.Values) {
            var staleKeys = definition.Cache.Keys
                .Where(cached => string.Equals(NormalizeInstancePath(cached), normalized, StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var stale in staleKeys) definition.Cache.Remove(stale);
        }
    }

    private static string NormalizeInstancePath(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private SettingDefinition GetDefinition(string key) {
        return _definitions.TryGetValue(key, out var definition)
            ? definition
            : throw new KeyNotFoundException("未找到设置项：" + key);
    }

    private static string GetInstanceKey(SettingDefinition definition, string? instancePath) {
        return definition.Source == SettingSource.Instance
            ? instancePath ?? throw new ArgumentException("读取版本设置时必须提供实例路径。")
            : "";
    }

    private string? ReadRaw(SettingDefinition definition, string? instancePath) {
        var defaultValue = Convert.ToString(definition.DefaultValue) ?? "";
        return definition.Source switch {
            SettingSource.Normal => _ini.Read("Setup", definition.Key, defaultValue),
            SettingSource.Instance => _ini.Read(Path.Combine(instancePath!, "PCL", "Setup.ini"), definition.Key, defaultValue),
            _ => ReadRegistryValue(definition.Key, defaultValue)
        };
    }

    private void Save(SettingDefinition definition, object? value, string? instancePath) {
        var text = Convert.ToString(value) ?? "";
        if (definition.Encrypted) text = Encrypt(text);
        switch (definition.Source) {
            case SettingSource.Normal:
                _ini.Write("Setup", definition.Key, text);
                break;
            case SettingSource.Instance:
                if (instancePath is null) throw new ArgumentException("保存版本设置时必须提供实例路径。");
                _ini.Write(Path.Combine(instancePath, "PCL", "Setup.ini"), definition.Key, text);
                break;
            case SettingSource.Registry:
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryFolder)) {
                    key?.SetValue(definition.Key, text, RegistryValueKind.String);
                }
                break;
        }
    }

    private void DeleteRaw(SettingDefinition definition, string? instancePath) {
        switch (definition.Source) {
            case SettingSource.Normal:
                _ini.Write("Setup", definition.Key, null);
                break;
            case SettingSource.Instance:
                if (instancePath is null) return;
                _ini.Write(Path.Combine(instancePath, "PCL", "Setup.ini"), definition.Key, null);
                break;
            case SettingSource.Registry:
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryFolder, writable: true)) {
                    key?.DeleteValue(definition.Key, throwOnMissingValue: false);
                }
                break;
        }
    }

    private static string? ReadRegistryValue(string key, string? defaultValue) {
        using var registry = Registry.CurrentUser.OpenSubKey(RegistryFolder);
        return registry?.GetValue(key)?.ToString() ?? defaultValue;
    }

    private static object? ConvertValue(object? value, Type type) {
        if (value is null) return type.IsValueType ? Activator.CreateInstance(type) : null;
        if (type == typeof(string)) return Convert.ToString(value);
        if (type == typeof(bool)) {
            var text = Convert.ToString(value);
            return bool.TryParse(text, out var parsed) ? parsed : text == "1";
        }
        if (type.IsEnum) return Enum.Parse(type, Convert.ToString(value) ?? "", ignoreCase: true);
        return Convert.ChangeType(value, type);
    }

    private string Encrypt(string value) {
        using var des = DES.Create();
        des.Key = NormalizeKey("PCL" + _identify);
        des.IV = InitialVector;
        using var encryptor = des.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(encryptor.TransformFinalBlock(bytes, 0, bytes.Length));
    }

    private string Decrypt(string value) {
        using var des = DES.Create();
        des.Key = NormalizeKey("PCL" + _identify);
        des.IV = InitialVector;
        using var decryptor = des.CreateDecryptor();
        var bytes = Convert.FromBase64String(value);
        return Encoding.UTF8.GetString(decryptor.TransformFinalBlock(bytes, 0, bytes.Length));
    }

    private static byte[] NormalizeKey(string key) {
        var bytes = Encoding.UTF8.GetBytes(key);
        var result = new byte[8];
        Array.Copy(bytes, result, Math.Min(bytes.Length, result.Length));
        return result;
    }

}

