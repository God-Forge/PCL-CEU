using System.Text;

namespace PCL.Core.Minecraft;

/// <summary>
/// 构建统一通行证与 Authlib-Injector 的 JVM 注入参数。
/// </summary>
public static class JvmInjectionArgumentBuilder {

    public static string BuildNide(string server) {
        return "-javaagent:\"${pure_directory}\\nide8auth.jar\"=\"" + server + "\"";
    }

    public static IReadOnlyList<string> BuildAuthlib(string server, string response) {
        return [
            "-javaagent:\"${pure_directory}\\authlib-injector.jar\"=\"" + server + "\"",
            "-Dauthlibinjector.side=client",
            "-Dauthlibinjector.yggdrasil.prefetched=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(response))
        ];
    }

}
