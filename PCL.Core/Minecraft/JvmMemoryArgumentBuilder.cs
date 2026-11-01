using System.Text.RegularExpressions;

namespace PCL.Core.Minecraft;

/// <summary>
/// 构建 JVM 内存与 GC 参数，并移除冲突的旧参数。
/// </summary>
public static class JvmMemoryArgumentBuilder {

    private const string GcPattern = "^( )*-XX:[+-]?(Use\\w+GC|ZGenerational|UseCompactObjectHeaders|G1\\w+Percent|G1\\w+Size|(Max|Min)(GCPauseMillis|HeapFreeRatio))";

    public static bool Apply(
        List<string> arguments,
        int targetRamMb,
        int setupType,
        int javaMajorVersion,
        Version osVersion,
        bool debug) {
        arguments.Add("-Xmx" + targetRamMb + "m");
        var useG1 = false;
        if (setupType != 3) {
            useG1 = (setupType == 0 && javaMajorVersion < 15)
                    || (setupType == 1 && javaMajorVersion < 21)
                    || setupType == 2
                    || setupType == 4;
            if (osVersion.Major < 10 || osVersion.Build < 17763) useG1 = true;

            arguments.RemoveAll(argument => Regex.IsMatch(argument, GcPattern));
            arguments.Add("-XX:+UnlockExperimentalVMOptions");
            if (javaMajorVersion >= 24) arguments.Add("-XX:+UseCompactObjectHeaders");
            if (useG1 && setupType == 4) {
                arguments.Add("-XX:+UseG1GC");
                arguments.Add("-XX:G1NewSizePercent=20");
                arguments.Add("-XX:G1ReservePercent=20");
                arguments.Add("-XX:G1HeapRegionSize=32M");
                arguments.Add("-XX:MaxGCPauseMillis=50");
                if (!debug) arguments.Add("-XX:+PerfDisableSharedMem");
                if (javaMajorVersion <= 7) arguments.Add("-XX:MaxPermSize=512m");
                if (javaMajorVersion == 8) arguments.Add("-XX:+ParallelRefProcEnabled");
                if (javaMajorVersion >= 12) arguments.Add("-XX:MinHeapFreeRatio=25");
                if (javaMajorVersion >= 12) arguments.Add("-XX:MaxHeapFreeRatio=40");
            } else if (useG1) {
                arguments.Add("-XX:+UseG1GC");
                arguments.Add("-XX:G1NewSizePercent=20");
                arguments.Add("-XX:G1ReservePercent=20");
                arguments.Add("-XX:G1HeapRegionSize=32M");
                arguments.Add("-XX:MaxGCPauseMillis=50");
                if (javaMajorVersion <= 7) arguments.Add("-XX:MaxPermSize=512m");
            } else {
                arguments.Add("-XX:+UseZGC");
                if (javaMajorVersion == 21 || javaMajorVersion == 22) arguments.Add("-XX:+ZGenerational");
            }
        }
        arguments.Remove("-XX:MaxDirectMemorySize=256M");
        return useG1;
    }

}
