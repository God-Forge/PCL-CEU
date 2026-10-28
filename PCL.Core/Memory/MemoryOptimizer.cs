using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;

namespace PCL.Core.Memory;

/// <summary>
/// 可执行的内存回收操作，对应 RAMMap 的 Empty 菜单。
/// </summary>
public enum MemoryOptimizationKind {
    /// <summary>清理当前进程（PCL）的工作集。</summary>
    CurrentProcess,
    /// <summary>清空所有工作集。</summary>
    WorkingSets,
    /// <summary>清空系统工作集（系统文件缓存）。</summary>
    SystemWorkingSet,
    /// <summary>清空已修改页列表。</summary>
    ModifiedPageList,
    /// <summary>清空待机列表。</summary>
    StandbyList,
    /// <summary>清空优先级 0（低优先级）待机列表。</summary>
    Priority0StandbyList,
    /// <summary>清空系统文件缓存。</summary>
    SystemFileCache
}

public sealed class MemoryStatus {

    public ulong TotalBytes { get; }
    public ulong AvailableBytes { get; }

    public double TotalMB => TotalBytes / 1024d / 1024d;
    public double AvailableMB => AvailableBytes / 1024d / 1024d;

    public MemoryStatus(ulong totalBytes, ulong availableBytes) {
        TotalBytes = totalBytes;
        AvailableBytes = availableBytes;
    }

}

public sealed class MemoryOptimizationResult {

    public bool Success { get; }
    public string Message { get; }
    public long FreedBytes { get; }
    public MemoryStatus Before { get; }
    public MemoryStatus After { get; }

    public double FreedMB => FreedBytes / 1024d / 1024d;

    public MemoryOptimizationResult(bool success, string message, long freedBytes, MemoryStatus before, MemoryStatus after) {
        Success = success;
        Message = message;
        FreedBytes = freedBytes;
        Before = before;
        After = after;
    }

}

/// <summary>
/// Windows 物理内存回收工具。部分操作需要管理员权限。
/// </summary>
public static class MemoryOptimizer {

    private const int SystemMemoryListInformation = 0x50;
    private const int MemoryEmptyWorkingSets = 2;
    private const int MemoryFlushModifiedList = 3;
    private const int MemoryPurgeStandbyList = 4;
    private const int MemoryPurgeLowPriorityStandbyList = 5;

    private const uint ProcessQueryInformation = 0x0400;
    private const uint ProcessSetQuota = 0x0100;
    private const uint TokenAdjustPrivileges = 0x0020;
    private const uint TokenQuery = 0x0008;
    private const uint SePrivilegeEnabled = 0x0002;
    private const int ErrorNotAllAssigned = 1300;

    public static bool IsAdministrator {
        get {
            try {
                using var identity = WindowsIdentity.GetCurrent();
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            } catch {
                return false;
            }
        }
    }

    public static MemoryStatus GetStatus() {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new MemoryStatus(status.TotalPhys, status.AvailPhys);
    }

    /// <summary>
    /// 执行单项内存回收，并返回前后可用物理内存。
    /// </summary>
    public static MemoryOptimizationResult Optimize(MemoryOptimizationKind kind) {
        var before = GetStatus();
        var (success, message) = Perform(kind);
        Thread.Sleep(250);
        var after = GetStatus();
        var freed = CalculateFreedBytes(before, after);
        return new MemoryOptimizationResult(success, BuildMessage(message, freed), freed, before, after);
    }

    /// <summary>
    /// 执行一组内存回收。包含待机列表的操作通常需要管理员权限。
    /// </summary>
    public static MemoryOptimizationResult OptimizeAll(bool includeStandbyLists) {
        var before = GetStatus();
        var allSuccess = true;
        var messages = new List<string>();

        foreach (var kind in GetKinds(includeStandbyLists)) {
            var (success, message) = Perform(kind);
            allSuccess &= success;
            messages.Add(message);
        }

        Thread.Sleep(300);
        var after = GetStatus();
        var freed = CalculateFreedBytes(before, after);
        return new MemoryOptimizationResult(
            allSuccess,
            BuildMessage(string.Join("；", messages), freed),
            freed,
            before,
            after);
    }

    private static IEnumerable<MemoryOptimizationKind> GetKinds(bool includeStandbyLists) {
        yield return MemoryOptimizationKind.WorkingSets;
        yield return MemoryOptimizationKind.SystemWorkingSet;
        yield return MemoryOptimizationKind.ModifiedPageList;
        if (!includeStandbyLists) yield break;
        yield return MemoryOptimizationKind.StandbyList;
        yield return MemoryOptimizationKind.Priority0StandbyList;
    }

    private static (bool Success, string Message) Perform(MemoryOptimizationKind kind) {
        try {
            return kind switch {
                MemoryOptimizationKind.CurrentProcess => EmptyCurrentProcess(),
                MemoryOptimizationKind.WorkingSets => EmptyWorkingSets(),
                MemoryOptimizationKind.SystemWorkingSet => EmptySystemWorkingSet("已清空系统工作集"),
                MemoryOptimizationKind.SystemFileCache => EmptySystemWorkingSet("已清空系统文件缓存"),
                MemoryOptimizationKind.ModifiedPageList => ExecuteNtMemoryCommand(MemoryFlushModifiedList, "已清空已修改页列表"),
                MemoryOptimizationKind.StandbyList => ExecuteNtMemoryCommand(MemoryPurgeStandbyList, "已清空待机列表"),
                MemoryOptimizationKind.Priority0StandbyList => ExecuteNtMemoryCommand(MemoryPurgeLowPriorityStandbyList, "已清空优先级 0 待机列表"),
                _ => (false, "未知的内存优化类型")
            };
        } catch (Exception ex) {
            return (false, "内存优化失败：" + ex.Message);
        }
    }

    private static (bool Success, string Message) EmptyCurrentProcess() {
        var success = EmptyWorkingSet(GetCurrentProcess());
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return success
            ? (true, "已清理 PCL 自身工作集")
            : (false, "清理 PCL 自身工作集失败：" + new Win32Exception(Marshal.GetLastWin32Error()).Message);
    }

    private static (bool Success, string Message) EmptyWorkingSets() {
        if (EnablePrivilege("SeProfileSingleProcessPrivilege")) {
            var command = MemoryEmptyWorkingSets;
            var status = NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int));
            if (status == 0) return (true, "已清空所有工作集");
        }

        // 非管理员时退化为逐进程 EmptyWorkingSet，能清理多少算多少。
        var successes = 0;
        var failures = 0;
        foreach (var process in Process.GetProcesses()) {
            try {
                var handle = OpenProcess(ProcessQueryInformation | ProcessSetQuota, false, process.Id);
                if (handle == IntPtr.Zero) {
                    failures++;
                    continue;
                }
                try {
                    if (EmptyWorkingSet(handle)) successes++;
                    else failures++;
                } finally {
                    CloseHandle(handle);
                }
            } catch {
                failures++;
            } finally {
                process.Dispose();
            }
        }

        if (successes == 0) {
            return (false, "未能清理任何进程的工作集，请以管理员身份运行 PCL");
        }
        var suffix = failures > 0 ? $"（{failures} 个进程无权限）" : "";
        return (true, $"已清理 {successes} 个进程的工作集{suffix}");
    }

    private static (bool Success, string Message) EmptySystemWorkingSet(string successMessage) {
        if (!EnablePrivilege("SeIncreaseQuotaPrivilege")) {
            return (false, "清空系统工作集需要管理员权限");
        }
        if (SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0)) {
            return (true, successMessage);
        }
        return (false, "清空系统工作集失败：" + new Win32Exception(Marshal.GetLastWin32Error()).Message);
    }

    private static (bool Success, string Message) ExecuteNtMemoryCommand(int command, string successMessage) {
        if (!EnablePrivilege("SeProfileSingleProcessPrivilege")) {
            return (false, "该操作需要管理员权限");
        }
        var value = command;
        var status = NtSetSystemInformation(SystemMemoryListInformation, ref value, sizeof(int));
        return status == 0
            ? (true, successMessage)
            : (false, $"操作失败：{NtStatusMessage(status)}");
    }

    private static long CalculateFreedBytes(MemoryStatus before, MemoryStatus after) {
        return after.AvailableBytes > before.AvailableBytes
            ? (long)(after.AvailableBytes - before.AvailableBytes)
            : 0;
    }

    private static string BuildMessage(string message, long freedBytes) {
        if (freedBytes <= 0) return message + "，可用物理内存无明显变化";
        return message + $"，可用物理内存增加约 {freedBytes / 1024d / 1024d:F0} MB";
    }

    private static string NtStatusMessage(int status) {
        return status switch {
            0 => "成功",
            unchecked((int)0xC0000061) => "权限不足，需要以管理员身份运行 PCL",
            unchecked((int)0xC0000022) => "访问被拒绝，需要以管理员身份运行 PCL",
            _ => $"NTSTATUS 0x{status:X8}"
        };
    }

    private static bool EnablePrivilege(string privilege) {
        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token)) return false;
        try {
            if (!LookupPrivilegeValue(null, privilege, out var luid)) return false;
            var privileges = new TokenPrivileges {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SePrivilegeEnabled
            };
            if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero)) return false;
            return Marshal.GetLastWin32Error() != ErrorNotAllAssigned;
        } finally {
            CloseHandle(token);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TokenPrivileges {
        public uint PrivilegeCount;
        public Luid Luid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid {
        public uint LowPart;
        public int HighPart;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges, ref TokenPrivileges newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetSystemFileCacheSize(IntPtr minimumFileCacheSize, IntPtr maximumFileCacheSize, uint flags);

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int systemInformationClass, ref int systemInformation, int systemInformationLength);

}
