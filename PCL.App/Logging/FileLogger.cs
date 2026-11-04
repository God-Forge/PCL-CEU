using System.IO;
using System.Text;
using MeloongCore;

namespace PCL.App;

/// <summary>
/// 将 PCL 日志写入本地文件，保留迁移期的诊断能力。
/// </summary>
public sealed class FileLogger : BaseLogger {

    private readonly string _path;
    private StreamWriter? _writer;

    public FileLogger(string path) {
        _path = path;
    }

    public override void Init() {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        _writer = new StreamWriter(_path, append: true, Encoding.UTF8) { AutoFlush = true };
    }

    public override void Output(LogEntry entry) {
        _writer?.WriteLine(entry.message);
    }

}

