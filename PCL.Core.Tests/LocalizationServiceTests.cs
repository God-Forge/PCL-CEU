using PCL.Core.Localization;

namespace PCL.Core.Tests;

public class LocalizationServiceTests {

    [Test]
    public async Task 切换语言与回退() {
        var service = new LocalizationService();
        await Assert.That(service.Get("common.ok")).IsEqualTo("确定");
        service.Language = "en_US";
        await Assert.That(service.Get("common.ok")).IsEqualTo("OK");
        await Assert.That(service.Get("missing.key")).IsEqualTo("missing.key");
    }

}
