using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LaunchTokenBuilderTests {

    private static LaunchTokenContext CreateContext(string versionType = "") {
        return new LaunchTokenContext {
            NativesDirectory = @"C:\mc\natives",
            LibrariesDirectory = @"C:\mc\libraries",
            PureDirectory = @"C:\pcl",
            LauncherVersion = "800",
            VersionName = "1.20.1",
            GameDirectory = @"C:\mc\versions\1.20.1",
            AssetsRoot = @"C:\mc\assets",
            AuthPlayerName = "Steve",
            AuthUuid = "uuid",
            AccessToken = "token",
            PrimaryJar = @"C:\mc\versions\1.20.1\1.20.1.jar",
            GameAssets = @"C:\mc\assets\virtual\legacy",
            AssetsIndexName = "5",
            VersionType = versionType,
            Width = 854,
            Height = 480,
            Classpath = "a.jar;b.jar"
        };
    }

    [Test]
    public async Task 构建替换标记() {
        var tokens = LaunchTokenBuilder.Build(CreateContext("release"), []);
        await Assert.That(tokens.Count).IsEqualTo(24);
        await Assert.That(tokens.First(token => token.Key == "${auth_player_name}").Value).IsEqualTo("Steve");
        await Assert.That(tokens.First(token => token.Key == "${version_type}").Value).IsEqualTo("release");
        await Assert.That(tokens.First(token => token.Key == "${resolution_width}").Value).IsEqualTo("854");
        await Assert.That(tokens.First(token => token.Key == "${classpath}").Value).IsEqualTo("a.jar;b.jar");
    }

    [Test]
    public async Task 空版本类型时移除参数并输出空引号() {
        var arguments = new List<string> { "--versionType", "release", "--foo" };
        var tokens = LaunchTokenBuilder.Build(CreateContext(), arguments);
        await Assert.That(arguments.Count).IsEqualTo(1);
        await Assert.That(arguments[0]).IsEqualTo("--foo");
        await Assert.That(tokens.First(token => token.Key == "${version_type}").Value).IsEqualTo("\"\"");
    }

}
