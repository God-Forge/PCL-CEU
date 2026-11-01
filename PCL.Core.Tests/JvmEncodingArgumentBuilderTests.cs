using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class JvmEncodingArgumentBuilderTests {

    [Test]
    public async Task 老版本使用sun编码参数() {
        var args = new List<string>();
        JvmEncodingArgumentBuilder.Apply(args, "GBK", 17, false);
        await Assert.That(args.Contains("-Dsun.stdout.encoding=GBK")).IsTrue();
        await Assert.That(args.Contains("-Dsun.stderr.encoding=GBK")).IsTrue();
        await Assert.That(args.Contains("-Dfile.encoding=COMPAT")).IsFalse();
    }

    [Test]
    public async Task 新版本使用std编码参数并添加COMPAT() {
        var args = new List<string>();
        JvmEncodingArgumentBuilder.Apply(args, "UTF-8", 21, true);
        await Assert.That(args.Contains("-Dstdout.encoding=UTF-8")).IsTrue();
        await Assert.That(args.Contains("-Dstderr.encoding=UTF-8")).IsTrue();
        await Assert.That(args.Contains("-Dfile.encoding=COMPAT")).IsTrue();
    }

    [Test]
    public async Task 已有参数时不重复添加() {
        var args = new List<string> { "-Dstdout.encoding=GBK", "-Dfile.encoding=UTF-8" };
        JvmEncodingArgumentBuilder.Apply(args, "GBK", 21, true);
        await Assert.That(args.Count(argument => argument == "-Dstdout.encoding=GBK")).IsEqualTo(1);
        await Assert.That(args.Count(argument => argument == "-Dfile.encoding=UTF-8")).IsEqualTo(1);
    }

}
