using PCL.Core.Login;

namespace PCL.Core.Tests;

public class LoginNameValidatorTests {

    [Test]
    public async Task 正常玩家名通过() {
        await Assert.That(LoginNameValidator.Validate("Steve")).IsNull();
        await Assert.That(LoginNameValidator.Validate("玩家一")).IsNull();
        await Assert.That(LoginNameValidator.IsValid("a_b")).IsTrue();
    }

    [Test]
    public async Task 空名被拒绝() {
        await Assert.That(LoginNameValidator.Validate("")).IsNotNull();
        await Assert.That(LoginNameValidator.Validate("   ")).IsNotNull();
        await Assert.That(LoginNameValidator.Validate(null)).IsNotNull();
    }

    [Test]
    public async Task 超长名被拒绝() {
        await Assert.That(LoginNameValidator.Validate(new string('a', 16))).IsNull();
        await Assert.That(LoginNameValidator.Validate(new string('a', 17))).IsNotNull();
    }

    [Test]
    public async Task 控制字符被拒绝() {
        // 首尾换行会被 Trim 掉，这里测名字中间的控制字符
        var message = LoginNameValidator.Validate("Ste\tve");
        await Assert.That(message).IsNotNull();
    }

    [Test]
    public async Task 首尾空白被忽略() {
        await Assert.That(LoginNameValidator.Validate("  Steve  ")).IsNull();
        await Assert.That(LoginNameValidator.Normalize("  Steve  ")).IsEqualTo("Steve");
    }

}
