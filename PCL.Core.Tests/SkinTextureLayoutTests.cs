using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class SkinTextureLayoutTests {

    [Test]
    public async Task 识别合法尺寸() {
        await Assert.That(SkinTextureLayout.IsValidSize(64, 64)).IsTrue();
        await Assert.That(SkinTextureLayout.IsValidSize(64, 32)).IsTrue();
        await Assert.That(SkinTextureLayout.IsValidSize(128, 128)).IsFalse();
        await Assert.That(SkinTextureLayout.IsValidSize(64, 63)).IsFalse();
    }

    [Test]
    public async Task 旧版尺寸判定() {
        await Assert.That(SkinTextureLayout.IsLegacy(64, 32)).IsTrue();
        await Assert.That(SkinTextureLayout.IsLegacy(64, 64)).IsFalse();
    }

    [Test]
    public async Task 现代皮肤全身有六块() {
        var placements = SkinTextureLayout.BodyPlacements(legacy: false);
        await Assert.That(placements.Count).IsEqualTo(6);
        await Assert.That(placements.Any(item => item.Name == "leftArm")).IsTrue();
        await Assert.That(placements.Any(item => item.Name == "leftLeg")).IsTrue();
    }

    [Test]
    public async Task 旧版皮肤左手左腿复用右侧() {
        var placements = SkinTextureLayout.BodyPlacements(legacy: true);
        var rightArm = placements.First(item => item.Name == "rightArm");
        var leftArm = placements.First(item => item.Name == "leftArm");
        await Assert.That(leftArm.Source).IsEqualTo(rightArm.Source);
        // 但摆放位置不同
        await Assert.That(leftArm.DestX).IsNotEqualTo(rightArm.DestX);
    }

    [Test]
    public async Task 部件不超出画布() {
        foreach (var placement in SkinTextureLayout.BodyPlacements(false)) {
            await Assert.That(placement.DestX + placement.Source.Width)
                .IsLessThanOrEqualTo(SkinTextureLayout.BodyPixelsWidth);
            await Assert.That(placement.DestY + placement.Source.Height)
                .IsLessThanOrEqualTo(SkinTextureLayout.BodyPixelsHeight);
        }
    }

    [Test]
    public async Task 现代皮肤区域都在纹理内() {
        foreach (var placement in SkinTextureLayout.BodyPlacements(false)) {
            await Assert.That(SkinTextureLayout.IsWithinBounds(placement, 64, 64)).IsTrue();
        }
    }

    [Test]
    public async Task 旧版皮肤不使用超出高度32的区域() {
        foreach (var placement in SkinTextureLayout.BodyPlacements(legacy: true)) {
            await Assert.That(SkinTextureLayout.IsWithinBounds(placement, 64, 32)).IsTrue();
        }
    }

    [Test]
    public async Task 头部与帽子区域坐标() {
        await Assert.That(SkinTextureLayout.HeadFace).IsEqualTo(new PixelRegion(8, 8, 8, 8));
        await Assert.That(SkinTextureLayout.HeadOverlay).IsEqualTo(new PixelRegion(40, 8, 8, 8));
        await Assert.That(SkinTextureLayout.AvatarPixels).IsEqualTo(8);
    }

}
