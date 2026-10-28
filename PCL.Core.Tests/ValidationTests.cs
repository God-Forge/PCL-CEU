using PCL;

namespace PCL.Core.Tests;

public class ValidationTests {

    private static string CreateTempDir() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-core-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [Test]
    public async Task Nullable_为空时中断后续检查() {
        await Assert.That(new ValidateNullable().Validate(null)).IsNull();
        await Assert.That(new ValidateNullable().Validate("")).IsNull();
        await Assert.That(new ValidateNullable().Validate("x")).IsEqualTo("");
    }

    [Test]
    public async Task NullOrEmpty_与NullOrWhiteSpace_的区别() {
        await Assert.That(new ValidateNullOrEmpty().Validate(" ")).IsEqualTo("");
        await Assert.That(new ValidateNullOrEmpty().Validate("")).IsEqualTo("输入内容不能为空！");
        await Assert.That(new ValidateNullOrWhiteSpace().Validate(" ")).IsEqualTo("输入内容不能为空！");
        await Assert.That(new ValidateNullOrWhiteSpace().Validate("x")).IsEqualTo("");
    }

    [Test]
    public async Task Http_只接受_http_或_https() {
        await Assert.That(new ValidateHttp().Validate("https://example.com/")).IsEqualTo("");
        await Assert.That(new ValidateHttp().Validate("http://example.com")).IsEqualTo("");
        await Assert.That(new ValidateHttp().Validate("ftp://example.com")).IsEqualTo("输入的网址无效！");
        await Assert.That(new ValidateHttp().Validate(null)).IsEqualTo("输入的网址无效！");
    }

    [Test]
    public async Task Integer_检查范围与格式() {
        var rule = new ValidateInteger(1, 10);
        await Assert.That(rule.Validate("1")).IsEqualTo("");
        await Assert.That(rule.Validate("10")).IsEqualTo("");
        await Assert.That(rule.Validate("0")).IsEqualTo("不可低于 1！");
        await Assert.That(rule.Validate("11")).IsEqualTo("不可超过 10！");
        await Assert.That(rule.Validate("abc")).IsEqualTo("请输入一个整数！");
        await Assert.That(rule.Validate("1234567890")).IsEqualTo("请输入一个大小合理的数字！");
    }

    [Test]
    public async Task Length_检查固定长度与上下限() {
        await Assert.That(new ValidateLength(3, 3).Validate("abc")).IsEqualTo("");
        await Assert.That(new ValidateLength(3, 3).Validate("ab")).IsEqualTo("长度必须为 3 个字符！");
        await Assert.That(new ValidateLength(3, 3).Validate("abcd")).IsEqualTo("长度必须为 3 个字符！");
        await Assert.That(new ValidateLength(2).Validate("a")).IsEqualTo("长度至少需 2 个字符！");
    }

    [Test]
    public async Task Regex_与_Func_规则() {
        await Assert.That(new ValidateRegex(@"^\d+$").Validate("123")).IsEqualTo("");
        await Assert.That(new ValidateRegex(@"^\d+$").Validate("abc")).IsEqualTo("正则检查失败！");
        await Assert.That(new ValidateFunc(s => s == "ok" ? "" : "bad").Validate("ok")).IsEqualTo("");
        await Assert.That(new ValidateFunc(s => s == "ok" ? "" : "bad").Validate("no")).IsEqualTo("bad");
    }

    [Test]
    public async Task Except_忽略大小写并替换占位符() {
        var rule = new ValidateExcept(["a"], "不能包含 %");
        await Assert.That(rule.Validate("ABC")).IsEqualTo("不能包含 a");
        await Assert.That(rule.Validate("BCD")).IsEqualTo("");
    }

    [Test]
    public async Task ExceptSame_支持忽略大小写() {
        await Assert.That(new ValidateExceptSame(["CON"], "不能为 %", true).Validate("con")).IsEqualTo("不能为 CON");
        await Assert.That(new ValidateExceptSame(["CON"], "不能为 %", true).Validate("other")).IsEqualTo("");
        await Assert.That(new ValidateExceptSame(["CON"], "不能为 %", false).Validate("con")).IsEqualTo("");
    }

    [Test]
    public async Task Same_支持忽略大小写() {
        await Assert.That(new ValidateSame("abc", "不匹配", true).Validate("ABC")).IsEqualTo("");
        await Assert.That(new ValidateSame("abc", "不匹配", false).Validate("ABC")).IsEqualTo("不匹配");
    }

    [Test]
    public async Task FolderName_检查空格特殊字符保留名与重名() {
        var dir = CreateTempDir();
        try {
            Directory.CreateDirectory(Path.Combine(dir, "existing"));
            var rule = new ValidateFolderName(dir);
            await Assert.That(rule.Validate(" ")).IsEqualTo("输入内容不能为空！");
            await Assert.That(rule.Validate(" leading")).IsEqualTo("文件夹名不能以空格开头！");
            await Assert.That(rule.Validate("trailing ")).IsEqualTo("文件夹名不能以空格结尾！");
            await Assert.That(rule.Validate("CON")).IsEqualTo("文件夹名不可为 CON！");
            await Assert.That(rule.Validate("bad:name")!.Contains("文件夹名不可包含")).IsTrue();
            await Assert.That(rule.Validate("existing")).IsEqualTo("不可与现有文件夹重名！");
            await Assert.That(rule.Validate("normal")).IsEqualTo("");
        } finally {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task FileName_检查重名与父文件夹不存在() {
        var dir = CreateTempDir();
        try {
            File.WriteAllText(Path.Combine(dir, "existing.txt"), "");
            var rule = new ValidateFileName("x") { ParentFolder = dir };
            await Assert.That(rule.Validate("existing.txt")).IsEqualTo("不可与现有文件重名！");
            await Assert.That(rule.Validate("new.txt")).IsEqualTo("");
            var missingParent = new ValidateFileName("x") { ParentFolder = Path.Combine(dir, "missing") };
            await Assert.That(missingParent.Validate("new.txt")!.Contains("父文件夹不存在")).IsTrue();
        } finally {
            Directory.Delete(dir, true);
        }
    }

    [Test]
    public async Task FolderPath_检查驱动器与非法字符() {
        var dir = CreateTempDir();
        try {
            await Assert.That(new ValidateFolderPath().Validate(dir)).IsEqualTo("");
            await Assert.That(new ValidateFolderPath().Validate("NOTADRIVE:\\foo")).IsEqualTo("文件夹路径头存在错误！");
            await Assert.That(new ValidateFolderPath().Validate(dir + "\\bad:name")!.Contains("路径中存在无效字符")).IsTrue();
        } finally {
            Directory.Delete(dir, true);
        }
    }

}
