namespace PCL.Core.Minecraft;

/// <summary>
/// 解析 Natives 文件夹路径，必要时回退到 ASCII 路径并提前创建目录。
/// </summary>
public static class NativesFolderResolver {

    public static string Resolve(
        string versionPath,
        string name,
        bool isGbkEncoding,
        string appData,
        string osDrive,
        Func<string, string> toShortPath) {
        var result = AddSlashSuffix(toShortPath(versionPath)) + name + "-natives\\";
        if (!isGbkEncoding && !IsAsciiOnly(result)) {
            result = appData + ".minecraft\\bin\\natives\\";
            if (!IsAsciiOnly(result)) {
                result = osDrive + "ProgramData\\PCL\\natives\\";
            }
        }
        Directory.CreateDirectory(result);
        return AddSlashSuffix(toShortPath(result));
    }

    private static string AddSlashSuffix(string path) {
        return path.EndsWith("\\", StringComparison.Ordinal) ? path : path + "\\";
    }

    private static bool IsAsciiOnly(string value) {
        return value.All(character => character <= 127);
    }

}
