using PCL.Core.Minecraft;
using PCL.Core.Net;

namespace PCL.Core.Tests;

public class LoaderInstallOrchestratorTests {

    private sealed class FakeFetcher(string? json) : IStringFetcher {
        public Task<string> GetStringAsync(string url, CancellationToken cancellationToken = default) =>
            Task.FromResult(json ?? throw new HttpRequestException("no network"));
    }

    private const string FabricProfile = """
    { "id": "fabric-loader-0.15.0-26.3", "inheritsFrom": "26.3",
      "libraries": [], "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient" }
    """;

    private static string TempMc() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-loader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    [Test]
    public async Task 没有加载器时直接成功() {
        var mc = TempMc();
        try {
            var result = await new LoaderInstallOrchestrator(new FakeFetcher(FabricProfile))
                .InstallAsync(new InstallTarget { MinecraftVersion = "26.3" }, mc);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Message).Contains("没有需要安装");
        } finally {
            Directory.Delete(mc, true);
        }
    }

    [Test]
    public async Task 冲突组合直接失败() {
        var mc = TempMc();
        try {
            var result = await new LoaderInstallOrchestrator(new FakeFetcher(FabricProfile))
                .InstallAsync(new InstallTarget { FabricVersion = "0.15.0", ForgeVersion = "47.2.0" }, mc);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Message).Contains("不能同时安装");
        } finally {
            Directory.Delete(mc, true);
        }
    }

    [Test]
    public async Task Fabric步骤会被处理() {
        var mc = TempMc();
        try {
            var result = await new LoaderInstallOrchestrator(new FakeFetcher(FabricProfile))
                .InstallAsync(new InstallTarget { MinecraftVersion = "26.3", FabricVersion = "0.15.0" }, mc);
            // 真实下载库需要网络；这里验证编排把 Fabric 当作可自动步骤处理并给出结果
            var handled = result.Installed.Concat(result.Pending).ToList();
            await Assert.That(handled.Count).IsEqualTo(1);
            await Assert.That(handled[0]).Contains("Fabric");
        } finally {
            Directory.Delete(mc, true);
        }
    }

    [Test]
    public async Task Forge列为待手动完成() {
        var mc = TempMc();
        try {
            var result = await new LoaderInstallOrchestrator(new FakeFetcher(FabricProfile))
                .InstallAsync(new InstallTarget { MinecraftVersion = "26.3", ForgeVersion = "47.2.0" }, mc);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Pending.Count).IsEqualTo(1);
            await Assert.That(result.Pending[0]).Contains("Forge");
        } finally {
            Directory.Delete(mc, true);
        }
    }

    [Test]
    public async Task Fabric下载失败时转为待办() {
        var mc = TempMc();
        try {
            var result = await new LoaderInstallOrchestrator(new FakeFetcher(null))
                .InstallAsync(new InstallTarget { MinecraftVersion = "26.3", FabricVersion = "0.15.0" }, mc);
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Pending.Count).IsEqualTo(1);
            await Assert.That(result.Pending[0]).Contains("安装失败");
        } finally {
            Directory.Delete(mc, true);
        }
    }

    [Test]
    public async Task 目录不存在时失败() {
        var result = await new LoaderInstallOrchestrator(new FakeFetcher(FabricProfile))
            .InstallAsync(new InstallTarget { FabricVersion = "0.15.0" },
                Path.Combine(Path.GetTempPath(), "pcl-none-" + Guid.NewGuid().ToString("N")));
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Message).Contains("目录不存在");
    }

}
