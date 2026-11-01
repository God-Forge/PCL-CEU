using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LegacyGameArgumentBuilderTests {

    [Test]
    public async Task 追加分辨率参数() {
        var result = LegacyGameArgumentBuilder.Build("--username ${auth_player_name}");
        await Assert.That(result).IsEqualTo("--username ${auth_player_name} --height ${resolution_height} --width ${resolution_width}");
    }

}
