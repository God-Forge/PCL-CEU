using System.Text;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

public sealed class LauncherProfilesResult {

    public bool Success { get; init; }
    public Exception? Error { get; init; }

}

/// <summary>
/// 创建与更新 launcher_profiles.json。
/// </summary>
public static class LauncherProfilesService {

    public static void EnsureCreated(string mcFolder) {
        var path = Path.Combine(mcFolder, "launcher_profiles.json");
        if (File.Exists(path)) return;
        var now = DateTime.Now;
        var json = new JObject {
            ["profiles"] = new JObject {
                ["PCL"] = new JObject {
                    ["icon"] = "Grass",
                    ["name"] = "PCL",
                    ["lastVersionId"] = "latest-release",
                    ["type"] = "latest-release",
                    ["lastUsed"] = now.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.0000Z'")
                }
            },
            ["selectedProfile"] = "PCL",
            ["clientToken"] = "23323323323323323323323323323333"
        };
        File.WriteAllText(path, json.ToString(), Encoding.GetEncoding("GB18030"));
    }

    public static LauncherProfilesResult Update(string mcFolder, string name, string clientToken) {
        try {
            Apply(mcFolder, name, clientToken);
            return new LauncherProfilesResult { Success = true };
        } catch (Exception first) {
            try {
                File.Delete(Path.Combine(mcFolder, "launcher_profiles.json"));
                Apply(mcFolder, name, clientToken);
                return new LauncherProfilesResult { Success = true };
            } catch (Exception second) {
                return new LauncherProfilesResult { Success = false, Error = new AggregateException(first, second) };
            }
        }
    }

    private static void Apply(string mcFolder, string name, string clientToken) {
        EnsureCreated(mcFolder);
        var path = Path.Combine(mcFolder, "launcher_profiles.json");
        var profiles = JObject.Parse(ReadText(path));
        var replacement = new JObject {
            ["authenticationDatabase"] = new JObject {
                ["00000111112222233333444445555566"] = new JObject {
                    ["username"] = name.Replace("\"", "-"),
                    ["profiles"] = new JObject {
                        ["66666555554444433333222221111100"] = new JObject {
                            ["displayName"] = name
                        }
                    }
                }
            },
            ["clientToken"] = clientToken,
            ["selectedUser"] = new JObject {
                ["account"] = "00000111112222233333444445555566",
                ["profile"] = "66666555554444433333222221111100"
            }
        };
        profiles.Merge(replacement);
        File.WriteAllText(path, profiles.ToString(), Encoding.GetEncoding("GB18030"));
    }

    private static string ReadText(string path) {
        var bytes = File.ReadAllBytes(path);
        try {
            return new UTF8Encoding(false, true).GetString(bytes);
        } catch (DecoderFallbackException) {
            return Encoding.GetEncoding("GB18030").GetString(bytes);
        }
    }

}
