using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class CrashAutoFixPlannerTests {

    private static CrashAnalysisReport Report(params string[] reasons) => new() {
        Diagnoses = reasons.Select(reason => new CrashDiagnosis { Reason = reason }).ToList()
    };

    [Test]
    public async Task 空报告没有动作() {
        await Assert.That(CrashAutoFixPlanner.Plan(null).Count).IsEqualTo(0);
        await Assert.That(CrashAutoFixPlanner.Plan(new CrashAnalysisReport()).Count).IsEqualTo(0);
        await Assert.That(CrashAutoFixPlanner.HasAutoApplicable([])).IsFalse();
    }

    [Test]
    public async Task 堆栈溢出可自动修复() {
        var actions = CrashAutoFixPlanner.Plan(Report("堆栈溢出"));
        await Assert.That(actions.Count).IsEqualTo(1);
        await Assert.That(actions[0].Kind).IsEqualTo(CrashFixKind.AutoApplicable);
        await Assert.That(actions[0].JvmArgument).IsEqualTo("-Xss4m");
        await Assert.That(CrashAutoFixPlanner.HasAutoApplicable(actions)).IsTrue();
    }

    [Test]
    public async Task 其他原因只提示() {
        var actions = CrashAutoFixPlanner.Plan(Report("Mod互不兼容"));
        await Assert.That(actions[0].Kind).IsEqualTo(CrashFixKind.SuggestOnly);
        await Assert.That(actions[0].JvmArgument).IsNull();
        await Assert.That(actions[0].Description).IsNotEmpty();
    }

    [Test]
    public async Task 混合情况分别处理() {
        var actions = CrashAutoFixPlanner.Plan(Report("堆栈溢出", "内存不足"));
        await Assert.That(actions.Count).IsEqualTo(2);
        await Assert.That(actions.Count(action => action.Kind == CrashFixKind.AutoApplicable)).IsEqualTo(1);
        await Assert.That(actions.Count(action => action.Kind == CrashFixKind.SuggestOnly)).IsEqualTo(1);
    }

    [Test]
    public async Task 合并参数到空设置() {
        var actions = CrashAutoFixPlanner.Plan(Report("堆栈溢出"));
        await Assert.That(CrashAutoFixPlanner.MergeArguments(null, actions)).IsEqualTo("-Xss4m");
        await Assert.That(CrashAutoFixPlanner.MergeArguments("", actions)).IsEqualTo("-Xss4m");
    }

    [Test]
    public async Task 追加到已有参数后面() {
        var actions = CrashAutoFixPlanner.Plan(Report("堆栈溢出"));
        var merged = CrashAutoFixPlanner.MergeArguments("-Xmx4G", actions);
        await Assert.That(merged).IsEqualTo("-Xmx4G -Xss4m");
    }

    [Test]
    public async Task 已有相同参数时不重复追加() {
        var actions = CrashAutoFixPlanner.Plan(Report("堆栈溢出"));
        var merged = CrashAutoFixPlanner.MergeArguments("-Xss4m -Xmx4G", actions);
        await Assert.That(merged).IsEqualTo("-Xss4m -Xmx4G");
    }

    [Test]
    public async Task 没有可自动项时原样返回() {
        var actions = CrashAutoFixPlanner.Plan(Report("Mod互不兼容"));
        await Assert.That(CrashAutoFixPlanner.MergeArguments("-Xmx4G", actions)).IsEqualTo("-Xmx4G");
    }

}
