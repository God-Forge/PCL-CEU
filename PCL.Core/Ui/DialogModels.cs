namespace PCL.Core.Ui;

/// <summary>对话框类型，对应 VB 版 MyMsgBoxType。</summary>
public enum DialogKind {
    /// <summary>纯文本，返回点击的按钮编号。</summary>
    Text,
    /// <summary>输入框，返回输入的文本。</summary>
    Input,
    /// <summary>单选列表，返回选中项下标。</summary>
    Select
}

/// <summary>
/// 一个对话框请求的完整描述，与具体 UI 框架无关。
/// </summary>
public sealed class DialogRequest {

    public DialogKind Kind { get; init; } = DialogKind.Text;
    public string Title { get; init; } = "提示";
    public string Caption { get; init; } = "";

    public string Button1 { get; init; } = "确定";
    public string Button2 { get; init; } = "";
    public string Button3 { get; init; } = "";

    /// <summary>警告配色。</summary>
    public bool IsWarn { get; init; }

    /// <summary>输入框默认内容。</summary>
    public string DefaultInput { get; init; } = "";

    /// <summary>输入框提示文本。</summary>
    public string HintText { get; init; } = "";

    /// <summary>选择项。</summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>校验委托：返回 <c>null</c> 表示通过，否则返回错误提示。</summary>
    public Func<string, string?>? Validator { get; init; }

    /// <summary>实际显示的按钮文案（空按钮会被剔除）。</summary>
    public IReadOnlyList<string> Buttons => NormalizeButtons(Button1, Button2, Button3);

    /// <summary>是否存在多个按钮。</summary>
    public bool HasMultipleButtons => Buttons.Count > 1;

    /// <summary>去掉空按钮文案，并保证至少有一个按钮。</summary>
    public static IReadOnlyList<string> NormalizeButtons(string? button1, string? button2, string? button3) {
        var buttons = new[] { button1, button2, button3 }
            .Where(button => !string.IsNullOrWhiteSpace(button))
            .Select(button => button!.Trim())
            .ToList();
        if (buttons.Count == 0) buttons.Add("确定");
        return buttons;
    }

}

/// <summary>
/// 对话框的返回值。
/// </summary>
public sealed class DialogResult {

    /// <summary>点击的按钮编号，从 1 开始。</summary>
    public int ButtonIndex { get; init; } = 1;

    /// <summary>输入内容，仅在输入模式下有效；点击非第一个按钮时为 <c>null</c>。</summary>
    public string? Input { get; init; }

    /// <summary>选中项下标，仅在选择模式下有效；点击非第一个按钮时为 <c>null</c>。</summary>
    public int? Selection { get; init; }

    public bool IsPrimaryButton => ButtonIndex == 1;

    public static DialogResult Cancel(int index) => new() { ButtonIndex = index };

}

/// <summary>
/// 对话框串行队列：同一时刻只显示一个，完成后自动交给下一个。
/// </summary>
public sealed class DialogQueue {

    private readonly Queue<DialogRequest> _pending = new();

    /// <summary>当前正在显示的请求，没有时为 <c>null</c>。</summary>
    public DialogRequest? Current { get; private set; }

    /// <summary>是否有对话框正在显示或有排队。</summary>
    public bool IsBusy => Current is not null || _pending.Count > 0;

    public int PendingCount => _pending.Count;

    /// <summary>入队一个请求。</summary>
    public void Enqueue(DialogRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        _pending.Enqueue(request);
    }

    /// <summary>取下一个待显示的请求。已有当前请求时返回 <c>null</c>。</summary>
    public DialogRequest? Dequeue() {
        if (Current is not null) return null;
        Current = _pending.Count > 0 ? _pending.Dequeue() : null;
        return Current;
    }

    /// <summary>标记当前请求已完成，返回下一个待显示的请求。</summary>
    public DialogRequest? Complete() {
        Current = null;
        return Dequeue();
    }

    public void Clear() {
        _pending.Clear();
        Current = null;
    }

}
