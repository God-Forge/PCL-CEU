using System.Text;

namespace PCL.Core.Minecraft;

public static class LaunchLogService {

    private const int MaximumCharacters = 200_000;
    private static readonly object SyncRoot = new();
    private static readonly StringBuilder Buffer = new();

    public static event Action<string>? LineWritten;
    public static event Action? Cleared;

    public static string Text {
        get {
            lock (SyncRoot) return Buffer.ToString();
        }
    }

    public static void Clear() {
        lock (SyncRoot) Buffer.Clear();
        try {
            Cleared?.Invoke();
        } catch (Exception ex) {
            Logger.Warn(ex, "广播启动日志清空事件失败");
        }
    }

    public static void WriteLine(string? line) {
        if (line is null) return;
        lock (SyncRoot) {
            if (Buffer.Length > 0) Buffer.AppendLine();
            Buffer.Append(line);
            if (Buffer.Length > MaximumCharacters) {
                Buffer.Remove(0, Buffer.Length - MaximumCharacters);
            }
        }
        try {
            LineWritten?.Invoke(line);
        } catch (Exception ex) {
            Logger.Warn(ex, "广播启动日志失败");
        }
    }

}
