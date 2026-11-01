namespace PCL.Core.Minecraft;

/// <summary>
/// 根据窗口设置生成全屏参数。
/// </summary>
public static class FullscreenArgumentBuilder {

    public static IReadOnlyList<string> Build(int windowType) {
        return windowType == 0 ? ["--fullscreen"] : Array.Empty<string>();
    }

}
