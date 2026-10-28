namespace PCL;

/// <summary>
/// 输入校验规则的统一入口。
/// </summary>
public static class Validation {

    /// <summary>
    /// 进行输入验证，并返回错误原因。若没有错误则返回空字符串。
    /// </summary>
    public static string Validate(string? text, IEnumerable<Validate>? validateRules) {
        var result = "";
        if (validateRules is null) return result;
        foreach (var validateRule in validateRules) {
            result = validateRule.Validate(text);
            if (result is null) return "";
            if (result != "") return result;
        }
        return result;
    }

}

/// <summary>
/// 输入验证规则基类的实际实现。
/// 单独拆出是为了在 C# 中保留名为 <c>Validate</c> 的方法。
/// </summary>
public abstract class ValidateBase {

    /// <summary>
    /// 验证某字符串是否符合验证要求。
    /// 若符合，返回空字符串；若不符合，返回错误原因；若需要中断检查并直接通过，返回 <see langword="null"/>。
    /// </summary>
    public abstract string? Validate(string? str);

}

/// <summary>
/// 输入验证规则基类。
/// </summary>
public abstract class Validate : ValidateBase { }

/// <summary>
/// 若为空则直接通过检查。
/// </summary>
public class ValidateNullable : Validate {

    public override string? Validate(string? str) => string.IsNullOrEmpty(str) ? null : "";

}

/// <summary>
/// 不能为 <see langword="null"/> 或空字符串（不包括全空格检查）。
/// </summary>
public class ValidateNullOrEmpty : Validate {

    public override string? Validate(string? str) => string.IsNullOrEmpty(str) ? "输入内容不能为空！" : "";

}

/// <summary>
/// 不能为 <see langword="null"/> 或空字符串（包括全空格检查）。
/// </summary>
public class ValidateNullOrWhiteSpace : Validate {

    public override string? Validate(string? str) => string.IsNullOrWhiteSpace(str) ? "输入内容不能为空！" : "";

}

/// <summary>
/// 自定义的 Validate。
/// </summary>
public class ValidateFunc : Validate {

    private readonly Func<string?, string?> _func;

    public ValidateFunc(Func<string?, string?> func) {
        _func = func;
    }

    public override string? Validate(string? str) => _func(str);

}

/// <summary>
/// 必须满足正则表达式。
/// </summary>
public class ValidateRegex : Validate {

    public string Regex { get; set; } = "";
    public string? ErrorDescription { get; set; } = "正则检查失败！";

    /// <summary>用于 XAML 初始化。</summary>
    public ValidateRegex() { }

    public ValidateRegex(string regex, string errorDescription = "正则检查失败！") {
        Regex = regex;
        ErrorDescription = errorDescription;
    }

    public override string? Validate(string? str) => str is not null && str.RegexCheck(Regex) ? "" : ErrorDescription;

}

/// <summary>
/// 必须是一个完整网址。
/// </summary>
public class ValidateHttp : Validate {

    /// <summary>用于 XAML 初始化。</summary>
    public ValidateHttp() { }

    public override string? Validate(string? str) {
        if (str.EndsWithF("/")) str = str!.Substring(0, str.Length - 1);
        return str is not null && str.RegexCheck(@"^(http[s]?)\://") ? "" : "输入的网址无效！";
    }

}

/// <summary>
/// 必须为整数。
/// </summary>
public class ValidateInteger : Validate {

    public int Min { get; set; }
    public int Max { get; set; } = int.MaxValue;

    /// <summary>用于 XAML 初始化。</summary>
    public ValidateInteger() { }

    public ValidateInteger(int min, int max) {
        Min = min;
        Max = max;
    }

    public override string? Validate(string? str) {
        if (str is { Length: > 9 }) return "请输入一个大小合理的数字！";
        if (!int.TryParse(str, out var value)) return "请输入一个整数！";
        if (value > Max) return "不可超过 " + Max + "！";
        if (value < Min) return "不可低于 " + Min + "！";
        return "";
    }

}

/// <summary>
/// 长度限制。
/// </summary>
public class ValidateLength : Validate {

    public int Min { get; set; } = 0;
    public int Max { get; set; } = int.MaxValue;

    /// <summary>用于 XAML 初始化。</summary>
    public ValidateLength() { }

    public ValidateLength(int min, int max = int.MaxValue) {
        Min = min;
        Max = max;
    }

    public override string? Validate(string? str) {
        var length = str?.Length ?? 0;
        if (length != Max && Max == Min) return $"长度必须为 {Max} 个字符！";
        if (length > Max) return $"长度最长为 {Max} 个字符！";
        if (length < Min) return $"长度至少需 {Min} 个字符！";
        return "";
    }

}

/// <summary>
/// 不能包含某些特定字符串。忽略大小写。
/// </summary>
public class ValidateExcept : Validate {

    public Collection<string> Excepts { get; set; } = [];
    public string? ErrorMessage { get; set; }

    /// <summary>用于 XAML 初始化。</summary>
    public ValidateExcept() {
        ErrorMessage = "输入内容不能包含 %";
    }

    public ValidateExcept(Collection<string> excepts, string errorMessage = "输入内容不能包含 %") {
        Excepts = excepts;
        ErrorMessage = errorMessage;
    }

    public ValidateExcept(IEnumerable<string> excepts, string errorMessage = "输入内容不能包含 %")
        : this(new Collection<string>(excepts.ToList()), errorMessage) { }

    public ValidateExcept(IEnumerable<char> excepts, string errorMessage = "输入内容不能包含 %")
        : this(excepts.Select(c => c.ToString()), errorMessage) { }

    public override string? Validate(string? str) {
        if (str is null) return "";
        foreach (var ch in Excepts) {
            if (str.IndexOfF(ch, true) < 0) continue;
            ErrorMessage ??= "";
            return ErrorMessage.Replace("%", ch);
        }
        return "";
    }

}

/// <summary>
/// 不能与某些特定字符串相同。
/// </summary>
public class ValidateExceptSame : Validate {

    public Collection<string> Excepts { get; set; } = [];
    public string? ErrorMessage { get; set; }
    public bool IgnoreCase { get; set; } = false;

    /// <summary>用于 XAML 初始化。</summary>
    public ValidateExceptSame() { }

    public ValidateExceptSame(Collection<string> excepts, string errorMessage = "输入内容不能为 %", bool ignoreCase = false) {
        Excepts = excepts;
        ErrorMessage = errorMessage;
        IgnoreCase = ignoreCase;
    }

    public ValidateExceptSame(IEnumerable<string> excepts, string errorMessage = "输入内容不能为 %", bool ignoreCase = false)
        : this(new Collection<string>(excepts.ToList()), errorMessage, ignoreCase) { }

    public ValidateExceptSame(string except, string errorMessage = "输入内容不能为 %", bool ignoreCase = false)
        : this(new Collection<string> { except }, errorMessage, ignoreCase) { }

    public override string? Validate(string? str) {
        if (str is null) return (ErrorMessage ?? "").Replace("%", "null");
        foreach (var ch in Excepts) {
            if (IgnoreCase) {
                if (str.Lower() == ch.Lower()) return (ErrorMessage ?? "").Replace("%", ch);
            } else {
                if (str.Equals(ch)) return (ErrorMessage ?? "").Replace("%", ch);
            }
        }
        return "";
    }

}

/// <summary>
/// 必须为特定字符串。
/// </summary>
public class ValidateSame : Validate {

    public string? Required { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IgnoreCase { get; set; } = false;

    /// <summary>用于 XAML 初始化。</summary>
    public ValidateSame() { }

    public ValidateSame(string required, string errorMessage = "输入内容有误", bool ignoreCase = false) {
        Required = required;
        ErrorMessage = errorMessage;
        IgnoreCase = ignoreCase;
    }

    public override string? Validate(string? str) => string.Compare(str, Required, IgnoreCase) == 0 ? "" : ErrorMessage;

}

/// <summary>
/// 对文件夹名的粗略的特化检测。
/// </summary>
public class ValidateFolderName : Validate {

    public string? Folder { get; set; }
    public bool UseMinecraftCharCheck { get; set; } = true;

    private readonly IEnumerable<string>? _pathIgnore;
    private readonly List<string>? _ignoreList;

    /// <summary>用于 XAML 初始化。</summary>
    public ValidateFolderName() { }

    public ValidateFolderName(string? folder, bool useMinecraftCharCheck = true, List<string>? ignoreList = null) {
        Folder = folder;
        UseMinecraftCharCheck = useMinecraftCharCheck;
        _ignoreList = ignoreList;
        try {
            _pathIgnore = DirectoryUtils.EnumerateDirectories(folder ?? "");
        } catch {
            _pathIgnore = null;
        }
    }

    public override string? Validate(string? str) {
        try {
            var lengthCheck = new ValidateNullOrWhiteSpace().Validate(str);
            if (lengthCheck != "") return lengthCheck;
            if (str is null) return "";
            if (str.StartsWithF(" ")) return "文件夹名不能以空格开头！";
            if (str.EndsWithF(" ")) return "文件夹名不能以空格结尾！";
            lengthCheck = new ValidateLength(1, 100).Validate(str);
            if (lengthCheck != "") return lengthCheck;
            if (str.EndsWithF(".")) return "文件夹名不能以小数点结尾！";

            var charactCheck = new ValidateExcept(
                Path.GetInvalidFileNameChars().Concat(UseMinecraftCharCheck ? "!;" : ""),
                "文件夹名不可包含 % 字符！").Validate(str);
            if (charactCheck != "") return charactCheck;

            var invalidStrCheck = new ValidateExceptSame(InvalidWindowsNames, "文件夹名不可为 %！", true).Validate(str);
            if (invalidStrCheck != "") return invalidStrCheck;

            if (str.RegexCheck(@".{2,}~\d")) return "文件夹名不能包含这一特殊格式！";

            var arr = new List<string>();
            if (_pathIgnore is not null) {
                foreach (var folderPath in _pathIgnore) {
                    var folderName = PathUtils.GetLastPart(folderPath);
                    if (_ignoreList is null || !_ignoreList.Contains(folderName, StringComparer.OrdinalIgnoreCase)) {
                        arr.Add(folderName);
                    }
                }
            }
            var sameNameCheck = new ValidateExceptSame(arr, "不可与现有文件夹重名！", true).Validate(str);
            if (sameNameCheck != "") return sameNameCheck;

            return "";
        } catch (Exception ex) {
            Logger.Warn(ex, "检查文件夹名出错");
            return "错误：" + ex.Message;
        }
    }

    private static readonly string[] InvalidWindowsNames = [
        "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

}

/// <summary>
/// 对文件名的粗略的特化检测。
/// </summary>
public class ValidateFileName : Validate {

    public string? Name { get; set; }
    public bool UseMinecraftCharCheck { get; set; } = true;
    public bool IgnoreCase { get; set; } = true;
    public string? ParentFolder { get; set; }
    public bool RequireParentFolderExists { get; set; } = true;

    /// <summary>用于 XAML 初始化。</summary>
    public ValidateFileName() { }

    public ValidateFileName(string? name, bool useMinecraftCharCheck = true, bool ignoreCase = true) {
        Name = name;
        IgnoreCase = ignoreCase;
        UseMinecraftCharCheck = useMinecraftCharCheck;
    }

    public override string? Validate(string? str) {
        try {
            var lengthCheck = new ValidateNullOrWhiteSpace().Validate(str);
            if (lengthCheck != "") return lengthCheck;
            if (str is null) return "";
            if (str.StartsWithF(" ")) return "文件名不能以空格开头！";
            if (str.EndsWithF(" ")) return "文件名不能以空格结尾！";

            lengthCheck = new ValidateLength(1, 253).Validate(str + (ParentFolder ?? ""));
            if (lengthCheck != "") return lengthCheck;
            if (str.EndsWithF(".")) return "文件名不能以小数点结尾！";

            var charactCheck = new ValidateExcept(
                Path.GetInvalidFileNameChars().Concat(UseMinecraftCharCheck ? "!;" : ""),
                "文件名不可包含 % 字符！").Validate(str);
            if (charactCheck != "") return charactCheck;

            var invalidStrCheck = new ValidateExceptSame(ValidateFolderNameInvalidNames, "文件名不可为 %！", true).Validate(str);
            if (invalidStrCheck != "") return invalidStrCheck;

            if (str.RegexCheck(@".{2,}~\d")) return "文件名不能包含这一特殊格式！";

            if (ParentFolder is not null) {
                var dirInfo = DirectoryUtils.GetInfo(ParentFolder);
                if (dirInfo.Exists) {
                    var sameNameCheck = new ValidateExceptSame(
                        dirInfo.EnumerateFiles("*").Select(f => f.Name),
                        "不可与现有文件重名！", IgnoreCase).Validate(str);
                    if (sameNameCheck != "") return sameNameCheck;
                } else if (RequireParentFolderExists) {
                    return $"父文件夹不存在：{ParentFolder}";
                }
            }

            return "";
        } catch (Exception ex) {
            Logger.Warn(ex, "检查文件名出错");
            return "错误：" + ex.Message;
        }
    }

    private static readonly string[] ValidateFolderNameInvalidNames = [
        "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

}

/// <summary>
/// 要求输入一个可用的文件夹路径。
/// </summary>
public class ValidateFolderPath : Validate {

    public bool UseMinecraftCharCheck { get; set; } = true;

    /// <summary>用于 XAML 初始化。</summary>
    public ValidateFolderPath() { }

    public ValidateFolderPath(bool useMinecraftCharCheck) {
        UseMinecraftCharCheck = useMinecraftCharCheck;
    }

    public override string? Validate(string? str) {
        // 去除尾部斜线，统一为 \
        str = str?.Replace("/", "\\") ?? "";
        if (!PathUtils.RemoveSlashSuffix(str).EndsWithF(":")) str = PathUtils.RemoveSlashSuffix(str);

        var lengthCheck = new ValidateNullOrWhiteSpace().Validate(str);
        if (lengthCheck != "") return lengthCheck;

        lengthCheck = new ValidateLength(1, 254).Validate(str);
        if (lengthCheck != "") return lengthCheck;

        var match = str.RegexSeek(@".{2,}~\d");
        if (match is not null) return $"文件夹名不能包含 {match} 这一特殊格式！";

        var startsWithMac = str.StartsWithF(@"\\Mac\");
        var validHead = startsWithMac;
        if (!validHead) {
            foreach (var drive in Environment.GetLogicalDrives().Where(DirectoryUtils.Exists)) {
                var driveName = drive.Upper()[0] + @":\";
                if (str.Upper() == driveName) return "";
                if (!str.StartsWithF(driveName, true)) continue;
                validHead = true;
                break;
            }
        }
        if (!validHead) return "文件夹路径头存在错误！";

        var parts = str.Split('\\');
        for (var i = startsWithMac ? 2 : 1; i <= parts.Length - 1; i++) {
            var subStr = parts[i];

            var subLengthCheck = new ValidateNullOrWhiteSpace().Validate(subStr);
            if (subLengthCheck != "") return "文件夹路径存在错误！";

            var charactCheck = new ValidateExcept(
                Path.GetInvalidFileNameChars().Concat(UseMinecraftCharCheck ? "!;" : ""),
                "路径中存在无效字符！").Validate(subStr);
            if (charactCheck != "") return charactCheck;

            if (subStr.StartsWithF(" ")) return "文件夹名不能以空格开头！";
            if (subStr.EndsWithF(" ")) return "文件夹名不能以空格结尾！";
            if (subStr.EndsWithF(".")) return "文件夹名不能以小数点结尾！";

            var invalidStrCheck = new ValidateExceptSame(ValidateFolderNameInvalidNames, "文件夹名不可为 %！").Validate(subStr);
            if (invalidStrCheck != "") return invalidStrCheck;
        }

        return "";
    }

    private static readonly string[] ValidateFolderNameInvalidNames = [
        "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

}
