using System.Windows;
using PCL.Core.Ui;

namespace PCL.App.Services;

/// <summary>
/// 对话框入口：统一走 <see cref="DialogWindow"/>，并保证在 UI 线程上弹出。
/// 对应 VB 版 MyMsgBox / MyMsgBoxInput / MyMsgBoxSelect。
/// </summary>
public static class DialogService {

    private static readonly DialogQueue Queue = new();

    /// <summary>当前是否有对话框正在显示。</summary>
    public static bool IsBusy => Queue.IsBusy;

    /// <summary>显示一个对话框并等待结果。</summary>
    public static PCL.Core.Ui.DialogResult Show(DialogRequest request) {
        ArgumentNullException.ThrowIfNull(request);
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) return ShowCore(request);
        return dispatcher.Invoke(() => ShowCore(request));
    }

    /// <summary>显示纯文本提示，返回点击的按钮编号。</summary>
    public static int ShowText(
        string caption,
        string title = "提示",
        string button1 = "确定",
        string button2 = "",
        string button3 = "",
        bool isWarn = false) {
        return Show(new DialogRequest {
            Kind = DialogKind.Text,
            Title = title,
            Caption = caption,
            Button1 = button1,
            Button2 = button2,
            Button3 = button3,
            IsWarn = isWarn
        }).ButtonIndex;
    }

    /// <summary>
    /// 显示输入框。点击第一个按钮返回输入内容，点击其余按钮返回 <c>null</c>。
    /// </summary>
    public static string? ShowInput(
        string title,
        string caption = "",
        string defaultInput = "",
        string hintText = "",
        string button1 = "确定",
        string button2 = "取消",
        bool isWarn = false,
        Func<string, string?>? validator = null) {
        var result = Show(new DialogRequest {
            Kind = DialogKind.Input,
            Title = title,
            Caption = caption,
            DefaultInput = defaultInput,
            HintText = hintText,
            Button1 = button1,
            Button2 = button2,
            IsWarn = isWarn,
            Validator = validator
        });
        return result.IsPrimaryButton ? result.Input : null;
    }

    /// <summary>
    /// 显示单选列表。点击第一个按钮返回选中下标，点击其余按钮返回 <c>null</c>。
    /// </summary>
    public static int? ShowSelect(
        IReadOnlyList<string> options,
        string title = "提示",
        string button1 = "确定",
        string button2 = "",
        bool isWarn = false) {
        var result = Show(new DialogRequest {
            Kind = DialogKind.Select,
            Title = title,
            Options = options,
            Button1 = button1,
            Button2 = button2,
            IsWarn = isWarn
        });
        return result.IsPrimaryButton ? result.Selection : null;
    }

    private static PCL.Core.Ui.DialogResult ShowCore(DialogRequest request) {
        Queue.Enqueue(request);
        var current = Queue.Dequeue() ?? request;
        var window = new Dialogs.DialogWindow(current);
        if (System.Windows.Application.Current?.MainWindow is { IsLoaded: true } owner) {
            window.Owner = owner;
        }
        try {
            window.ShowDialog();
            return window.Result;
        } finally {
            Queue.Complete();
        }
    }

}
