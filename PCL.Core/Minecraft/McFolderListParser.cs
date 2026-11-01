namespace PCL.Core.Minecraft;

public sealed class McFolderEntry {

    public string Name { get; init; } = "";
    public string Path { get; init; } = "";

}

public sealed class McFolderListParseResult {

    public IReadOnlyList<McFolderEntry> Entries { get; init; } = Array.Empty<McFolderEntry>();
    public IReadOnlyList<string> Invalid { get; init; } = Array.Empty<string>();

}

/// <summary>
/// 解析 LaunchFolders 设置：名称>路径|名称>路径。
/// </summary>
public static class McFolderListParser {

    public static McFolderListParseResult Parse(string? value) {
        var entries = new List<McFolderEntry>();
        var invalid = new List<string>();
        foreach (var item in (value ?? "").Split(['|'], StringSplitOptions.RemoveEmptyEntries)) {
            var separator = item.IndexOf('>');
            if (separator < 0 || !item.EndsWith("\\", StringComparison.Ordinal)) {
                invalid.Add(item);
                continue;
            }
            entries.Add(new McFolderEntry {
                Name = item.Substring(0, separator),
                Path = item.Substring(separator + 1)
            });
        }
        return new McFolderListParseResult {
            Entries = entries,
            Invalid = invalid
        };
    }

}
