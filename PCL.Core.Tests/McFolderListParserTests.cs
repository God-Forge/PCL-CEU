using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class McFolderListParserTests {

    [Test]
    public async Task 解析有效与无效文件夹() {
        var result = McFolderListParser.Parse(@"A>C:\mc\|B>D:\game\bad|C>E:\ok\");
        await Assert.That(result.Entries.Count).IsEqualTo(2);
        await Assert.That(result.Entries[0].Name).IsEqualTo("A");
        await Assert.That(result.Entries[0].Path).IsEqualTo(@"C:\mc\");
        await Assert.That(result.Entries[1].Name).IsEqualTo("C");
        await Assert.That(result.Invalid.Count).IsEqualTo(1);
        await Assert.That(result.Invalid[0]).IsEqualTo(@"B>D:\game\bad");
    }

    [Test]
    public async Task 空设置返回空列表() {
        var result = McFolderListParser.Parse("");
        await Assert.That(result.Entries.Count).IsEqualTo(0);
        await Assert.That(result.Invalid.Count).IsEqualTo(0);
    }

}
