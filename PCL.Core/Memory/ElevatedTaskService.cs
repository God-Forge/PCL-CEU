namespace PCL.Core.Memory;

/// <summary>
/// 提权 helper 的白名单任务分发，避免直接暴露任意命令。
/// </summary>
public static class ElevatedTaskService {

    public static int Run(string task) {
        var kind = task.ToLowerInvariant() switch {
            "memory" => MemoryOptimizationKind.CurrentProcess,
            "memory:working-sets" => MemoryOptimizationKind.WorkingSets,
            "memory:system-working-set" => MemoryOptimizationKind.SystemWorkingSet,
            "memory:modified-page-list" => MemoryOptimizationKind.ModifiedPageList,
            "memory:standby-list" => MemoryOptimizationKind.StandbyList,
            "memory:priority0-standby-list" => MemoryOptimizationKind.Priority0StandbyList,
            _ => (MemoryOptimizationKind?)null
        };
        if (kind is null) return -2;
        return MemoryOptimizer.Optimize(kind.Value).Success ? 0 : 1;
    }

}
