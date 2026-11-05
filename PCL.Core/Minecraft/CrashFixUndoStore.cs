using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

/// <summary>
/// 一键修复的撤销记录：保存应用修复前的实例设置值，供用户回滚。
/// 记录写在 PCL 目录下的一个小文件里，避免污染设置项。
/// </summary>
public static class CrashFixUndoStore {

    private const string FileName = "CrashFixUndo.json";

    /// <summary>保存撤销信息（覆盖旧的记录）。</summary>
    public static string Save(string instancePath, string previousValue, string pclRoot) {
        var path = ResolvePath(pclRoot);
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = new JObject {
                ["instance"] = instancePath,
                ["previousValue"] = previousValue,
                ["time"] = DateTime.Now.ToString("O")
            };
            File.WriteAllText(path, json.ToString(), new System.Text.UTF8Encoding(false));
            return path;
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "保存修复撤销信息失败");
            return "";
        }
    }

    /// <summary>读取撤销信息；没有记录时返回 <c>null</c>。</summary>
    public static (string InstancePath, string PreviousValue)? Load(string pclRoot) {
        var path = ResolvePath(pclRoot);
        if (!File.Exists(path)) return null;
        try {
            var json = JObject.Parse(File.ReadAllText(path));
            var instance = json["instance"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(instance)) return null;
            return (instance, json["previousValue"]?.ToString() ?? "");
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "读取修复撤销信息失败");
            return null;
        }
    }

    /// <summary>清除撤销记录。</summary>
    public static void Clear(string pclRoot) {
        try {
            var path = ResolvePath(pclRoot);
            if (File.Exists(path)) File.Delete(path);
        } catch (Exception ex) when (ex is not OutOfMemoryException) {
            Logger.Warn(ex, "清除修复撤销信息失败");
        }
    }

    private static string ResolvePath(string pclRoot) => Path.Combine(pclRoot, "PCL", FileName);

}
