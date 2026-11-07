using System.IO.Compression;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class InstanceHealthServiceTests {

    private static string CreateHealthyInstance(out string minecraft) {
        minecraft = Path.Combine(Path.GetTempPath(), "pcl-health-" + Guid.NewGuid().ToString("N"));
        var instance = Path.Combine(minecraft, "versions", "test");
        Directory.CreateDirectory(instance);
        File.WriteAllText(Path.Combine(instance, "test.json"), """
            {"id":"test","mainClass":"net.minecraft.client.main.Main","assetIndex":{"id":"1.20"},"libraries":[]}
            """);
        File.WriteAllText(Path.Combine(instance, "test.jar"), "jar");
        Directory.CreateDirectory(Path.Combine(minecraft, "assets", "indexes"));
        File.WriteAllText(Path.Combine(minecraft, "assets", "indexes", "1.20.json"), """{"objects":{}}""");
        return instance;
    }

    private static void CreateFabricJar(string path, string id, string marker = "") {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("fabric.mod.json");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("{\"id\":\"" + id + "\",\"name\":\"" + id + "\",\"version\":\"1.0.0\",\"description\":\"" + marker + "\"}");
    }

    [Test]
    public async Task 健康实例得满分() {
        var instance = CreateHealthyInstance(out var minecraft);
        try {
            var report = InstanceHealthService.Evaluate(instance, minecraft);

            await Assert.That(report.Score).IsEqualTo(100);
            await Assert.That(report.Grade).IsEqualTo(InstanceHealthGrade.S);
            await Assert.That(report.ErrorCount).IsEqualTo(0);
            await Assert.That(report.FixActions.Count).IsEqualTo(0);
            await Assert.That(report.InstanceName).IsEqualTo("test");
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 警告会扣分并降级() {
        var instance = CreateHealthyInstance(out var minecraft);
        try {
            File.WriteAllText(Path.Combine(instance, "test.json"), """
                {"id":"other","mainClass":"net.minecraft.client.main.Main","assetIndex":{"id":"1.20"},"libraries":[]}
                """);

            var report = InstanceHealthService.Evaluate(instance, minecraft);

            await Assert.That(report.WarningCount).IsEqualTo(1);
            await Assert.That(report.Score).IsEqualTo(94);
            await Assert.That(report.Grade).IsEqualTo(InstanceHealthGrade.A);
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 缺少版本JSON会给出修复建议() {
        var instance = CreateHealthyInstance(out var minecraft);
        try {
            File.Delete(Path.Combine(instance, "test.json"));

            var report = InstanceHealthService.Evaluate(instance, minecraft);

            await Assert.That(report.Score).IsEqualTo(75);
            await Assert.That(report.Grade).IsEqualTo(InstanceHealthGrade.B);
            await Assert.That(report.FixActions.Any(action => action.Kind == InstanceFixKind.RepairBaseFiles)).IsTrue();
            await Assert.That(report.CanAutoFix).IsTrue();
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 缺少客户端JAR建议修复基础文件() {
        var instance = CreateHealthyInstance(out var minecraft);
        try {
            File.Delete(Path.Combine(instance, "test.jar"));

            var report = InstanceHealthService.Evaluate(instance, minecraft);

            await Assert.That(report.Score).IsEqualTo(94);
            var action = report.FixActions.Single(item => item.Kind == InstanceFixKind.RepairBaseFiles);
            await Assert.That(action.CanAutoFix).IsTrue();
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 重复ModID按错误扣分() {
        var instance = CreateHealthyInstance(out var minecraft);
        try {
            var mods = Path.Combine(instance, "mods");
            Directory.CreateDirectory(mods);
            CreateFabricJar(Path.Combine(mods, "sodium-1.jar"), "sodium", "one");
            CreateFabricJar(Path.Combine(mods, "sodium-2.jar"), "sodium", "two");

            var report = InstanceHealthService.Evaluate(instance, minecraft);

            await Assert.That(report.ErrorCount).IsEqualTo(1);
            await Assert.That(report.Score).IsEqualTo(80);
            await Assert.That(report.Grade).IsEqualTo(InstanceHealthGrade.B);
            await Assert.That(report.FixActions.Any(action => action.Kind == InstanceFixKind.ResolveModConflicts)).IsTrue();
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 缺少前置Mod会给出建议() {
        var instance = CreateHealthyInstance(out var minecraft);
        try {
            var mods = Path.Combine(instance, "mods");
            Directory.CreateDirectory(mods);
            using (var zip = ZipFile.Open(Path.Combine(mods, "lib.jar"), ZipArchiveMode.Create)) {
                var entry = zip.CreateEntry("fabric.mod.json");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("""{"id":"lib","name":"Lib","version":"1.0.0","depends":{"fabricloader":">=0.14","corelib":">=2.0"}}""");
            }

            var report = InstanceHealthService.Evaluate(instance, minecraft);

            var action = report.FixActions.SingleOrDefault(item => item.Kind == InstanceFixKind.InstallMissingMods);
            await Assert.That(action).IsNotNull();
            await Assert.That(action!.Detail.Contains("corelib")).IsTrue();
            await Assert.That(action.CanAutoFix).IsFalse();
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 分级边界正确() {
        await Assert.That(InstanceHealthService.GradeOf(100)).IsEqualTo(InstanceHealthGrade.S);
        await Assert.That(InstanceHealthService.GradeOf(95)).IsEqualTo(InstanceHealthGrade.S);
        await Assert.That(InstanceHealthService.GradeOf(94)).IsEqualTo(InstanceHealthGrade.A);
        await Assert.That(InstanceHealthService.GradeOf(85)).IsEqualTo(InstanceHealthGrade.A);
        await Assert.That(InstanceHealthService.GradeOf(84)).IsEqualTo(InstanceHealthGrade.B);
        await Assert.That(InstanceHealthService.GradeOf(75)).IsEqualTo(InstanceHealthGrade.B);
        await Assert.That(InstanceHealthService.GradeOf(74)).IsEqualTo(InstanceHealthGrade.C);
        await Assert.That(InstanceHealthService.GradeOf(60)).IsEqualTo(InstanceHealthGrade.C);
        await Assert.That(InstanceHealthService.GradeOf(59)).IsEqualTo(InstanceHealthGrade.D);
        await Assert.That(InstanceHealthService.GradeOf(0)).IsEqualTo(InstanceHealthGrade.D);
    }

    [Test]
    public async Task 报告文本包含分数与建议() {
        var instance = CreateHealthyInstance(out var minecraft);
        try {
            File.Delete(Path.Combine(instance, "test.jar"));
            var report = InstanceHealthService.Evaluate(instance, minecraft);
            var text = InstanceHealthService.FormatReport(report);

            await Assert.That(text.Contains("健康分：94")).IsTrue();
            await Assert.That(text.Contains("修复建议")).IsTrue();
        } finally {
            Directory.Delete(minecraft, true);
        }
    }

    [Test]
    public async Task 历史记录与趋势() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-health-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var path = Path.Combine(root, "HealthHistory.json");
            var first = InstanceHealthService.Evaluate(CreateHealthyInstance(out var mc1), mc1);
            InstanceHealthHistoryService.Append(path, first);
            var second = new InstanceHealthReport {
                InstanceName = first.InstanceName,
                Score = 70,
                Grade = InstanceHealthGrade.B,
                EvaluatedUtc = first.EvaluatedUtc.AddMinutes(1)
            };
            var entries = InstanceHealthHistoryService.Append(path, second);

            var trend = InstanceHealthHistoryService.GetTrend(path, first.InstanceName);
            await Assert.That(entries.Count).IsEqualTo(2);
            await Assert.That(trend.Count).IsEqualTo(2);
            await Assert.That(InstanceHealthHistoryService.FormatTrend(trend).Contains("↓30")).IsTrue();
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 单次记录也能格式化趋势() {
        var entries = new[] {
            new InstanceHealthHistoryEntry { Instance = "a", Score = 88, Grade = InstanceHealthGrade.A, EvaluatedUtc = DateTime.UtcNow }
        };
        var text = InstanceHealthHistoryService.FormatTrend(entries);
        await Assert.That(text).IsEqualTo("88");
        await Assert.That(InstanceHealthHistoryService.FormatTrend([])).IsEqualTo("还没有历史记录。");
    }

}
