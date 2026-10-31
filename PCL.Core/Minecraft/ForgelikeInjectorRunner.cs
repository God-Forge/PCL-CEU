using System.Diagnostics;

namespace PCL.Core.Minecraft;

/// <summary>
/// 解析 Forge/NeoForge 安装器输出，并执行安装器进程。
/// </summary>
public static class ForgelikeInstallerOutputParser {

    public static bool TryGetProgress(string content, double current, out double progress) {
        switch (content) {
            case "Extracting json":
                progress = 0.27;
                return true;
            case "Downloading libraries":
                progress = 0.28;
                return true;
            case "  File exists: Checksum validated.":
                progress = current + 0.003;
                return true;
            case "Building Processors":
                progress = 0.38;
                return true;
            case "Task: DOWNLOAD_MOJMAPS":
                progress = 0.4;
                return true;
            case "Task: MERGE_MAPPING":
                progress = 0.5;
                return true;
            case "Splitting: ":
                progress = 0.55;
                return true;
            case "Parameter Annotations":
                progress = 0.6;
                return true;
            case "Processing Complete":
                progress = 0.67;
                return true;
            case "log: null":
                progress = 0.67;
                return true;
            case "Sorting":
                progress = 0.8;
                return true;
            case "Remapping final jar":
                progress = 0.85;
                return true;
            case "Remapping jar... 50%":
                progress = 0.9;
                return true;
            case "Remapping jar... 100%":
                progress = 0.95;
                return true;
            case "Injecting profile":
                progress = 0.98;
                return true;
            default:
                progress = current;
                return false;
        }
    }

}

public static class ForgelikeInjectorRunner {

    public static string BuildArguments(
        string installerPath,
        string mcFolder,
        bool useJavaWrapper,
        string? wrapperPath,
        string cacheFolder,
        string javaTempFolder,
        int javaMajorVersion) {
        var installer = Path.Combine(cacheFolder, "Cache", "forge_installer.jar");
        var classPath = installer + ";" + installerPath;
        var arguments = useJavaWrapper && !string.IsNullOrWhiteSpace(wrapperPath)
            ? $"-Doolloo.jlw.tmpdir=\"{javaTempFolder.TrimEnd(Path.DirectorySeparatorChar)}\" -cp \"{classPath}\" -jar \"{wrapperPath}\" com.bangbang93.ForgeInstaller \"{mcFolder}\""
            : $"-cp \"{classPath}\" com.bangbang93.ForgeInstaller \"{mcFolder}\"";
        return javaMajorVersion >= 9
            ? "--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED " + arguments
            : arguments;
    }

    public static void Run(
        string javaExePath,
        string installerPath,
        string mcFolder,
        bool useJavaWrapper,
        string? wrapperPath,
        string cacheFolder,
        string javaTempFolder,
        int javaMajorVersion,
        bool isNeoForge,
        Action<double>? progress = null,
        Action<string>? info = null,
        Action<string>? trace = null,
        CancellationToken cancellationToken = default) {
        var loaderName = isNeoForge ? "NeoForge" : "Forge";
        var arguments = BuildArguments(installerPath, mcFolder, useJavaWrapper, wrapperPath, cacheFolder, javaTempFolder, javaMajorVersion);
        info?.Invoke($"开始安装 {loaderName}：{arguments}");

        var startInfo = new ProcessStartInfo {
            FileName = javaExePath,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        var lastResults = new Queue<string>();
        var gate = new object();
        var currentProgress = 0d;
        using var process = new Process { StartInfo = startInfo };
        using var outputDone = new ManualResetEvent(false);
        using var errorDone = new ManualResetEvent(false);

        void HandleLine(string? line, ManualResetEvent done) {
            if (line is null) {
                done.Set();
                return;
            }
            lock (gate) {
                lastResults.Enqueue(line);
                if (lastResults.Count > 100) lastResults.Dequeue();
            }
            try {
                bool known;
                double next;
                lock (gate) {
                    known = ForgelikeInstallerOutputParser.TryGetProgress(line, currentProgress, out next);
                    if (known) currentProgress = next;
                }
                if (known) {
                    progress?.Invoke(next);
                    info?.Invoke(line);
                } else {
                    trace?.Invoke(line);
                }
            } catch {
                // 输出回调不应中断安装进程。
            }
            if (cancellationToken.IsCancellationRequested && !process.HasExited) {
                try {
                    info?.Invoke($"由于任务取消，已中止 {loaderName} 安装");
                    process.Kill();
                } catch {
                    // 进程可能已经退出。
                }
            }
        }

        process.OutputDataReceived += (_, e) => HandleLine(e.Data, outputDone);
        process.ErrorDataReceived += (_, e) => HandleLine(e.Data, errorDone);

        using var registration = cancellationToken.Register(() => {
            try {
                if (!process.HasExited) process.Kill();
            } catch {
                // 进程可能已经退出。
            }
        });

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        outputDone.WaitOne(10000);
        errorDone.WaitOne(10000);

        string[] lines;
        lock (gate) lines = lastResults.ToArray();
        if (lines.Reverse().Take(5).Any(line => line == "true")) return;

        trace?.Invoke(string.Join(Environment.NewLine, lines));
        var tail = string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - 5)));
        throw new InvalidOperationException($"{loaderName} 安装器出错，日志结束部分为：" + Environment.NewLine + tail);
    }

}
