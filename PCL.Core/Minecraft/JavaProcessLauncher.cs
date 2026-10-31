using System.Diagnostics;
using System.Text;

namespace PCL.Core.Minecraft;

/// <summary>
/// 构建 Minecraft Java 进程的启动信息（环境变量、编码、工作目录与重定向）。
/// </summary>
public static class JavaProcessLauncher {

    public static ProcessStartInfo BuildStartInfo(
        string javaExePath,
        string javaFolder,
        string mcFolder,
        string workingDirectory,
        string arguments,
        Encoding encoding) {
        var startInfo = new ProcessStartInfo(javaExePath);
        var path = startInfo.EnvironmentVariables["Path"] ?? "";
        var paths = path.Split([';'], StringSplitOptions.RemoveEmptyEntries).ToList();
        paths.Add(javaFolder);
        startInfo.EnvironmentVariables["Path"] = string.Join(";", paths.Distinct(StringComparer.OrdinalIgnoreCase));
        startInfo.EnvironmentVariables["appdata"] = mcFolder;
        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.StandardOutputEncoding = encoding;
        startInfo.StandardErrorEncoding = encoding;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;
        startInfo.Arguments = arguments;
        return startInfo;
    }

}
