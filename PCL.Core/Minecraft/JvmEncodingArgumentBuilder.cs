namespace PCL.Core.Minecraft;

/// <summary>
/// 添加 Java 标准输出/错误与 file.encoding 参数。
/// </summary>
public static class JvmEncodingArgumentBuilder {

    public static void Apply(List<string> arguments, string encodingName, int javaMajorVersion, bool useCompatEncoding) {
        var stdoutName = javaMajorVersion < 19 ? "sun.stdout.encoding" : "stdout.encoding";
        var stderrName = javaMajorVersion < 19 ? "sun.stderr.encoding" : "stderr.encoding";
        if (!arguments.Any(argument => argument.StartsWith("-D" + stdoutName + "=", StringComparison.Ordinal))) {
            arguments.Add("-D" + stdoutName + "=" + encodingName);
        }
        if (!arguments.Any(argument => argument.StartsWith("-D" + stderrName + "=", StringComparison.Ordinal))) {
            arguments.Add("-D" + stderrName + "=" + encodingName);
        }
        if (useCompatEncoding
            && !arguments.Any(argument => argument.StartsWith("-Dfile.encoding=", StringComparison.Ordinal))) {
            arguments.Add("-Dfile.encoding=COMPAT");
        }
    }

}
