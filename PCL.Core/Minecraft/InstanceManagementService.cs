using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using PCL.Core.Settings;

namespace PCL.Core.Minecraft;

public sealed class InstanceSetup {

    public int DisplayType { get; init; }
    public string CustomInfo { get; init; } = "";
    public bool IsStar { get; init; }
    public string Logo { get; init; } = "";
    public bool LogoCustom { get; init; }

}

public sealed class InstanceFolderSet {

    public string Version { get; init; } = "";
    public string Saves { get; init; } = "";
    public string Mods { get; init; } = "";
    public string Screenshots { get; init; } = "";

}

/// <summary>
/// 实例目录层面的读写：PCL/Setup.ini 元数据、版本重命名与快捷目录。
/// </summary>
public static class InstanceManagementService {

    private const string SetupFile = "Setup";

    public static InstanceSetup ReadSetup(string instanceFolder) {
        var ini = new IniFile(Normalize(instanceFolder));
        return new InstanceSetup {
            DisplayType = ReadInt(ini, "DisplayType"),
            CustomInfo = ini.Read(SetupFile, "CustomInfo") ?? "",
            IsStar = ReadBool(ini, "IsStar"),
            Logo = ini.Read(SetupFile, "Logo") ?? "",
            LogoCustom = ReadBool(ini, "LogoCustom")
        };
    }

    public static void WriteSetup(string instanceFolder, InstanceSetup setup) {
        var ini = new IniFile(Normalize(instanceFolder));
        ini.Write(SetupFile, "DisplayType", setup.DisplayType.ToString());
        ini.Write(SetupFile, "CustomInfo", setup.CustomInfo);
        ini.Write(SetupFile, "IsStar", setup.IsStar ? "True" : "False");
        ini.Write(SetupFile, "Logo", setup.Logo);
        ini.Write(SetupFile, "LogoCustom", setup.LogoCustom ? "True" : "False");
    }

    public static void SetCustomInfo(string instanceFolder, string customInfo) {
        var setup = ReadSetup(instanceFolder);
        WriteSetup(instanceFolder, new InstanceSetup {
            DisplayType = setup.DisplayType,
            CustomInfo = customInfo.Trim(),
            IsStar = setup.IsStar,
            Logo = setup.Logo,
            LogoCustom = setup.LogoCustom
        });
    }

    public static void SetDisplayType(string instanceFolder, int displayType) {
        var setup = ReadSetup(instanceFolder);
        WriteSetup(instanceFolder, new InstanceSetup {
            DisplayType = displayType,
            CustomInfo = setup.CustomInfo,
            IsStar = setup.IsStar,
            Logo = setup.Logo,
            LogoCustom = setup.LogoCustom
        });
    }

    public static void ToggleStar(string instanceFolder) {
        var setup = ReadSetup(instanceFolder);
        WriteSetup(instanceFolder, new InstanceSetup {
            DisplayType = setup.DisplayType,
            CustomInfo = setup.CustomInfo,
            IsStar = !setup.IsStar,
            Logo = setup.Logo,
            LogoCustom = setup.LogoCustom
        });
    }

    public static InstanceFolderSet GetFolders(string instanceFolder) {
        var root = Normalize(instanceFolder) + Path.DirectorySeparatorChar;
        return new InstanceFolderSet {
            Version = root,
            Saves = Path.Combine(root, "saves") + Path.DirectorySeparatorChar,
            Mods = Path.Combine(root, "mods") + Path.DirectorySeparatorChar,
            Screenshots = Path.Combine(root, "screenshots") + Path.DirectorySeparatorChar
        };
    }

    public static string Rename(string instanceFolder, string newName) {
        var source = Normalize(instanceFolder);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException("实例不存在：" + source);
        newName = (newName ?? "").Trim();
        ValidateFolderName(newName);

        var parent = Path.GetDirectoryName(source)
                     ?? throw new InvalidOperationException("无法获取实例的父目录。");
        var target = Path.Combine(parent, newName);
        if (Directory.Exists(target) || File.Exists(target)) throw new IOException("目标实例已存在：" + newName);

        var oldName = Path.GetFileName(source);
        var oldJson = FindJson(source, oldName);
        JObject? json = null;
        if (oldJson is not null) {
            try {
                json = JObject.Parse(File.ReadAllText(oldJson));
            } catch {
                json = null;
            }
        }

        Directory.Move(source, target);

        MoveIfExists(Path.Combine(target, oldName + "-natives"), Path.Combine(target, newName + "-natives"));
        MoveIfExists(Path.Combine(target, oldName + ".jar"), Path.Combine(target, newName + ".jar"));

        var setupPath = Path.Combine(target, "PCL", SetupFile + ".ini");
        if (File.Exists(setupPath)) {
            var text = File.ReadAllText(setupPath, Encoding.UTF8);
            var replaced = text.Replace(source, target, StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(text, replaced, StringComparison.Ordinal)) {
                File.WriteAllText(setupPath, replaced, new UTF8Encoding(false));
            }
        }

        if (oldJson is not null) {
            var oldJsonName = Path.GetFileName(oldJson);
            var oldJsonInTarget = Path.Combine(target, oldJsonName);
            if (File.Exists(oldJsonInTarget)) File.Delete(oldJsonInTarget);
            var targetJson = Path.Combine(target, newName + ".json");
            if (json is not null) {
                json["id"] = newName;
                File.WriteAllText(targetJson, json.ToString(), new UTF8Encoding(false));
            } else if (!string.Equals(oldJsonInTarget, targetJson, StringComparison.OrdinalIgnoreCase)) {
                File.Move(oldJsonInTarget, targetJson);
            }
        }

        return target;
    }

    public static void Delete(string instanceFolder) {
        var path = Normalize(instanceFolder);
        if (!Directory.Exists(path)) return;
        Directory.Delete(path, true);
    }

    private static string Normalize(string path) {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string? FindJson(string folder, string name) {
        var expected = Path.Combine(folder, name + ".json");
        if (File.Exists(expected)) return expected;
        return Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly).FirstOrDefault();
    }

    private static void MoveIfExists(string source, string target) {
        if (Directory.Exists(source)) Directory.Move(source, target);
        else if (File.Exists(source)) File.Move(source, target);
    }

    private static void ValidateFolderName(string name) {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("版本名不能为空。", nameof(name));
        if (name is "." or "..") throw new ArgumentException("版本名不能是 . 或 ..。", nameof(name));
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) throw new ArgumentException("版本名包含非法字符。", nameof(name));
        if (name.EndsWith(' ') || name.EndsWith('.')) throw new ArgumentException("版本名不能以空格或句点结尾。", nameof(name));
    }

    private static int ReadInt(IniFile ini, string key) {
        return int.TryParse(ini.Read(SetupFile, key), out var value) ? value : 0;
    }

    private static bool ReadBool(IniFile ini, string key) {
        var value = ini.Read(SetupFile, key);
        return bool.TryParse(value, out var parsed) ? parsed : value == "1";
    }

}
