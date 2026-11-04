using System.Text;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class JavaProcessLauncherTests {

    [Test]
    public async Task 构建启动信息并追加Java目录() {
        var info = JavaProcessLauncher.BuildStartInfo(
            @"C:\java\bin\java.exe",
            @"C:\java\bin",
            @"C:\mc",
            @"C:\mc\versions\1.20.1",
            ["-cp", "a.jar", "Main"],
            Encoding.UTF8);

        await Assert.That(info.FileName).IsEqualTo(@"C:\java\bin\java.exe");
        await Assert.That(info.ArgumentList.Count).IsEqualTo(3);
        await Assert.That(info.ArgumentList[0]).IsEqualTo("-cp");
        await Assert.That(info.ArgumentList[1]).IsEqualTo("a.jar");
        await Assert.That(info.ArgumentList[2]).IsEqualTo("Main");
        await Assert.That(info.WorkingDirectory).IsEqualTo(@"C:\mc\versions\1.20.1");
        await Assert.That(info.UseShellExecute).IsFalse();
        await Assert.That(info.RedirectStandardOutput).IsTrue();
        await Assert.That(info.RedirectStandardError).IsTrue();
        await Assert.That(info.CreateNoWindow).IsTrue();
        await Assert.That(info.EnvironmentVariables["appdata"]).IsEqualTo(@"C:\mc");
        await Assert.That(info.EnvironmentVariables["Path"]).Contains(@"C:\java\bin");
    }

}
