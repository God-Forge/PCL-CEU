using System.Net.Http;

namespace PCL.Core.Login;

/// <summary>
/// 把登录过程中抛出的异常翻译成用户能看懂的提示。
/// </summary>
public static class LoginErrorTranslator {

    public static string Translate(Exception? exception) {
        if (exception is null) return "登录失败，原因未知。";
        return exception switch {
            // TaskCanceledException 继承自 OperationCanceledException，需要先判断
            TaskCanceledException => "登录超时，请检查网络后重试。",
            OperationCanceledException => "登录已取消。",
            HttpRequestException => "无法连接登录服务器，请检查网络或服务器地址。",
            InvalidOperationException when !string.IsNullOrWhiteSpace(exception.Message) => exception.Message,
            UriFormatException => "登录服务器地址格式不正确。",
            _ => "登录失败：" + exception.Message
        };
    }

}
