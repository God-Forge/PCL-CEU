namespace PCL.Core.Minecraft;

public sealed class LaunchTokenContext {

    public string NativesDirectory { get; init; } = "";
    public string LibrariesDirectory { get; init; } = "";
    public string PureDirectory { get; init; } = "";
    public string LauncherVersion { get; init; } = "";
    public string VersionName { get; init; } = "";
    public string GameDirectory { get; init; } = "";
    public string AssetsRoot { get; init; } = "";
    public string AuthPlayerName { get; init; } = "";
    public string AuthUuid { get; init; } = "";
    public string AccessToken { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string Xuid { get; init; } = "";
    public string UserType { get; init; } = "";
    public string PrimaryJar { get; init; } = "";
    public string GameAssets { get; init; } = "";
    public string AssetsIndexName { get; init; } = "";
    public string VersionType { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    public string Classpath { get; init; } = "";

}

/// <summary>
/// 构建启动参数中的替换标记，并处理空的 version_type。
/// </summary>
public static class LaunchTokenBuilder {

    public static List<(string Key, string Value)> Build(LaunchTokenContext context, List<string> arguments) {
        var tokens = new List<(string Key, string Value)> {
            ("${classpath_separator}", ";"),
            ("${natives_directory}", context.NativesDirectory),
            ("${library_directory}", context.LibrariesDirectory),
            ("${libraries_directory}", context.LibrariesDirectory),
            ("${pure_directory}", context.PureDirectory),
            ("${launcher_name}", "PCL"),
            ("${launcher_version}", context.LauncherVersion),
            ("${version_name}", context.VersionName),
            ("${game_directory}", context.GameDirectory),
            ("${assets_root}", context.AssetsRoot),
            ("${user_properties}", "{}"),
            ("${auth_player_name}", context.AuthPlayerName),
            ("${auth_uuid}", context.AuthUuid),
            ("${auth_access_token}", context.AccessToken),
            ("${access_token}", context.AccessToken),
            ("${auth_session}", context.AccessToken),
            ("${clientid}", context.ClientId),
            ("${auth_xuid}", context.Xuid),
            ("${user_type}", string.IsNullOrEmpty(context.UserType) ? "msa" : context.UserType),
            ("${primary_jar}", context.PrimaryJar),
            ("${game_assets}", context.GameAssets),
            ("${assets_index_name}", context.AssetsIndexName)
        };

        if (string.IsNullOrEmpty(context.VersionType)) {
            var index = arguments.IndexOf("--versionType");
            if (index >= 0) {
                arguments.RemoveAt(index);
                if (index < arguments.Count) arguments.RemoveAt(index);
            }
            tokens.Add(("${version_type}", "\"\""));
        } else {
            tokens.Add(("${version_type}", context.VersionType));
        }

        tokens.Add(("${resolution_width}", context.Width.ToString()));
        tokens.Add(("${resolution_height}", context.Height.ToString()));
        tokens.Add(("${classpath}", context.Classpath));
        return tokens;
    }

}
