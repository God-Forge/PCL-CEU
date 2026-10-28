namespace PCL.Core.Tools;

public sealed class DailyLuckResult {

    public DateTime Date { get; }
    public int Value { get; }
    public string Comment { get; }

    public DailyLuckResult(DateTime date, int value, string comment) {
        Date = date;
        Value = value;
        Comment = comment;
    }

}

/// <summary>
/// 今日人品：以当地日期对应的 Unix 时间戳（天）为种子，生成每天固定的人品值。
/// </summary>
public static class DailyLuck {

    /// <summary>
    /// 返回今天的人品值（0~100）与对应评语。
    /// </summary>
    public static DailyLuckResult GetToday() {
        var date = DateTime.Now.Date;
        var unixDay = new DateTimeOffset(date).ToUnixTimeSeconds() / 86400;
        var seed = (int)(unixDay % int.MaxValue);
        var value = new Random(seed).Next(0, 101);

        var comment = value switch {
            <= 9 => "今天运气不太行，出门小心……",
            <= 29 => "普普通通的一天。",
            <= 59 => "还不错，保持好心情！",
            <= 89 => "运气不错，适合做点大事！",
            _ => "欧皇附体！今天想做什么都可以！"
        };

        return new DailyLuckResult(date, value, comment);
    }

}
