using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.Net;
using PCL.Core.Tasks;

namespace PCL.Core.Tests;

public class DownloadServiceTests {

    private sealed class SimpleHttpServer : IDisposable {

        private readonly TcpListener _listener;
        private readonly Dictionary<string, byte[]> _routes;
        private readonly CancellationTokenSource _cts = new();

        public SimpleHttpServer(Dictionary<string, byte[]> routes) {
            _routes = routes;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _ = Task.Run(AcceptLoopAsync);
        }

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public string BaseUrl => $"http://127.0.0.1:{Port}/";

        private async Task AcceptLoopAsync() {
            while (!_cts.IsCancellationRequested) {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false); }
                catch { return; }
                _ = Task.Run(() => HandleAsync(client));
            }
        }

        private async Task HandleAsync(TcpClient client) {
            using (client) {
                try {
                    var stream = client.GetStream();
                    var headerBytes = new List<byte>();
                    var buffer = new byte[1];
                    while (headerBytes.Count < 16384) {
                        var read = await stream.ReadAsync(buffer, 0, 1).ConfigureAwait(false);
                        if (read == 0) break;
                        headerBytes.Add(buffer[0]);
                        if (headerBytes.Count >= 4
                            && headerBytes[headerBytes.Count - 4] == '\r' && headerBytes[headerBytes.Count - 3] == '\n'
                            && headerBytes[headerBytes.Count - 2] == '\r' && headerBytes[headerBytes.Count - 1] == '\n') break;
                    }
                    var header = Encoding.ASCII.GetString(headerBytes.ToArray());
                    var headerLines = header.Split(new[] { "\r\n" }, StringSplitOptions.None);
                    var firstLine = headerLines[0];
                    var parts = firstLine.Split(' ');
                    var path = parts.Length >= 2 ? parts[1] : "/";
                    var rangeStart = 0;
                    var rangeEnd = -1;
                    var hasRange = false;
                    foreach (var line in headerLines) {
                        if (!line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase)) continue;
                        var value = line.Substring("Range:".Length).Trim();
                        if (!value.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) continue;
                        var range = value.Substring("bytes=".Length).Split('-');
                        int.TryParse(range[0], out rangeStart);
                        if (range.Length > 1) int.TryParse(range[1], out rangeEnd);
                        hasRange = true;
                    }

                    if (!_routes.TryGetValue(path, out var body)) {
                        await WriteResponseAsync(stream, "404 Not Found", Array.Empty<byte>(), 0, -1, 0, false).ConfigureAwait(false);
                        return;
                    }

                    if (hasRange && rangeStart < body.Length) {
                        var end = rangeEnd >= 0 ? Math.Min(rangeEnd, body.Length - 1) : body.Length - 1;
                        await WriteResponseAsync(stream, "206 Partial Content", body, rangeStart, end, body.Length, true).ConfigureAwait(false);
                    } else if (hasRange && rangeStart >= body.Length) {
                        await WriteResponseAsync(stream, "416 Range Not Satisfiable", Array.Empty<byte>(), 0, -1, body.Length, false).ConfigureAwait(false);
                    } else {
                        await WriteResponseAsync(stream, "200 OK", body, 0, body.Length - 1, body.Length, true).ConfigureAwait(false);
                    }
                } catch {
                    // 测试服务器忽略单个连接错误。
                }
            }
        }

        private static async Task WriteResponseAsync(Stream stream, string status, byte[] body, int start, int end, int total, bool ranges) {
            var length = Math.Max(0, end - start + 1);
            var builder = new StringBuilder();
            builder.Append("HTTP/1.1 ").Append(status).Append("\r\n");
            builder.Append("Content-Length: ").Append(length).Append("\r\n");
            builder.Append("Connection: close\r\n");
            if (ranges) {
                builder.Append("Accept-Ranges: bytes\r\n");
                if (status.StartsWith("206", StringComparison.Ordinal)) {
                    builder.Append("Content-Range: bytes ").Append(start).Append('-').Append(end).Append('/').Append(total).Append("\r\n");
                }
            }
            builder.Append("\r\n");
            var headerBytes = Encoding.ASCII.GetBytes(builder.ToString());
            await stream.WriteAsync(headerBytes, 0, headerBytes.Length).ConfigureAwait(false);
            if (length > 0) await stream.WriteAsync(body, start, length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        public void Dispose() {
            _cts.Cancel();
            _listener.Stop();
            _cts.Dispose();
        }

    }

    private static string CreateRoot() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-download-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task 下载文件并校验哈希() {
        var root = CreateRoot();
        var content = Encoding.UTF8.GetBytes("hello download service");
        using var server = new SimpleHttpServer(new Dictionary<string, byte[]> { ["/file"] = content });
        try {
            var target = Path.Combine(root, "file.bin");
            using var sha = SHA256.Create();
            var hash = BitConverter.ToString(sha.ComputeHash(content)).Replace("-", "").ToLowerInvariant();
            using var service = new HttpDownloadService();
            var result = await service.DownloadAsync(new DownloadRequest {
                Url = server.BaseUrl + "file",
                TargetPath = target,
                Sha256 = hash
            });
            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.ReadAllBytes(target).SequenceEqual(content)).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 多源回退与断点续传() {
        var root = CreateRoot();
        var content = Encoding.UTF8.GetBytes("0123456789abcdefghijklmnopqrstuvwxyz");
        using var server = new SimpleHttpServer(new Dictionary<string, byte[]> { ["/file"] = content });
        try {
            var target = Path.Combine(root, "file.bin");
            var partial = target + ".part";
            File.WriteAllBytes(partial, content.Take(10).ToArray());

            using var service = new HttpDownloadService();
            var result = await service.DownloadAsync(new DownloadRequest {
                Url = server.BaseUrl + "missing",
                BackupUrls = [server.BaseUrl + "file"],
                TargetPath = target,
                MaxRetries = 0
            });
            await Assert.That(result.SourceUrl).IsEqualTo(server.BaseUrl + "file");
            await Assert.That(File.ReadAllBytes(target).SequenceEqual(content)).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 使用任务状态对象下载() {
        var root = CreateRoot();
        var content = Encoding.UTF8.GetBytes("task download");
        using var server = new SimpleHttpServer(new Dictionary<string, byte[]> { ["/file"] = content });
        try {
            var target = Path.Combine(root, "file.bin");
            using var service = new HttpDownloadService();
            var task = new ProgressTask();
            var result = await service.DownloadAsync(new DownloadRequest {
                Url = server.BaseUrl + "file",
                TargetPath = target
            }, task);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(task.State).IsEqualTo(TaskState.Finished);
            await Assert.That(task.Progress).IsEqualTo(1d);
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 多分片下载() {
        var root = CreateRoot();
        var content = Enumerable.Range(0, 65536).Select(value => (byte)(value % 251)).ToArray();
        using var server = new SimpleHttpServer(new Dictionary<string, byte[]> { ["/file"] = content });
        try {
            var target = Path.Combine(root, "file.bin");
            using var service = new ParallelDownloadService();
            var result = await service.DownloadAsync(new DownloadRequest {
                Url = server.BaseUrl + "file",
                TargetPath = target,
                ParallelCount = 4,
                MinPartSize = 8192
            });
            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.ReadAllBytes(target).SequenceEqual(content)).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 哈希不匹配时失败() {
        var root = CreateRoot();
        using var server = new SimpleHttpServer(new Dictionary<string, byte[]> { ["/file"] = Encoding.UTF8.GetBytes("abc") });
        try {
            var target = Path.Combine(root, "file.bin");
            using var service = new HttpDownloadService();
            var threw = false;
            try {
                await service.DownloadAsync(new DownloadRequest {
                    Url = server.BaseUrl + "file",
                    TargetPath = target,
                    Sha256 = new string('0', 64),
                    MaxRetries = 0
                });
            } catch (HttpRequestException) {
                threw = true;
            }
            await Assert.That(threw).IsTrue();
            await Assert.That(File.Exists(target)).IsFalse();
        } finally {
            Directory.Delete(root, true);
        }
    }

}
