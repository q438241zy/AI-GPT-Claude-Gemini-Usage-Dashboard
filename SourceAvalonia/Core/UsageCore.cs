using System.Text.RegularExpressions;

namespace CodexWidget;

/// <summary>
/// 用量快照。FiveHourRemaining / WeeklyRemaining 为“剩余可用百分比”（0~100，解析不到为 null）。
/// ExtraUsage / ExtraNote 按平台复用：Codex = 重置卡数与到期；Claude = Fable 5 额度与重置；Gemini 留空。
/// ResetCardCount 为 Codex 重置卡可用张数（解析不到为 null）。
/// </summary>
public sealed record UsageSnapshot(string FiveHourUsage, string FiveHourReset, string WeeklyUsage,
    string WeeklyReset, string ExtraUsage, string ExtraNote,
    double? FiveHourRemaining = null, double? WeeklyRemaining = null, int? ResetCardCount = null);

public static class UsageParser
{
    private static readonly string[] RemainKeys = { "remaining", "left", "available", "剩余", "可用", "残り" };
    private static readonly string[] UsedKeys = { "used", "已使用", "已用", "使用済" };

    public static UsageSnapshot Parse(string source, string provider)
    {
        var lines = source.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => Regex.Replace(x.Trim(), @"\s+", " ")).Where(x => x.Length > 0).ToArray();
        var five = FindSection(lines, new[] { "5 hour", "5-hour", "five hour", "5 小时", "5小时", "session", "5時間" });
        var week = FindSection(lines, new[] { "weekly", "week limit", "周用量", "每周", "周限制", "all models", "週間" });

        string extraUsage = "", extraNote = "";
        int? cardCount = null;
        if (provider == "Codex")
        {
            var cards = lines.FirstOrDefault(x => ContainsAny(x, "reset available", "resets available", "可用重置", "重置卡", "重置次数") && Regex.IsMatch(x, @"\d+"));
            var expiry = lines.FirstOrDefault(x => ContainsAny(x, "expires", "expire on", "expiration", "到期", "有效期"));
            if (cards != null)
            {
                var m = Regex.Match(cards, @"\d+");
                if (m.Success && int.TryParse(m.Value, out var n)) cardCount = n;
            }
            extraUsage = CleanLabel(cards);
            extraNote = CleanLabel(expiry);
        }
        else if (provider == "Claude")
        {
            // Claude 没有重置卡的说法，改抓 Fable 5 模型额度
            var fable = FindSection(lines, new[] { "fable" });
            extraUsage = CleanLabel(fable.usage);
            extraNote = CleanLabel(fable.reset);
        }

        return new(CleanLabel(five.usage), CleanLabel(five.reset),
            CleanLabel(week.usage), CleanLabel(week.reset),
            extraUsage, extraNote,
            ExtractRemaining(five.usage, five.block), ExtractRemaining(week.usage, week.block), cardCount);
    }

    /// <summary>把 “X% used / X% remaining / 剩余 X%” 统一换算成剩余可用百分比；方向词可在同区块其他行。</summary>
    internal static double? ExtractRemaining(string? line, string[]? block = null)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        var match = Regex.Match(line, @"(\d+(?:\.\d+)?)\s*%");
        if (!match.Success) return null;
        var value = double.Parse(match.Groups[1].Value);
        var isRemain = ContainsAny(line, RemainKeys);
        var isUsed = ContainsAny(line, UsedKeys);
        if (!isRemain && !isUsed && block != null)
            isRemain = block.Any(b => ContainsAny(b, RemainKeys)) && !block.Any(b => ContainsAny(b, UsedKeys));
        var remaining = isRemain ? value : 100 - value;
        return Math.Clamp(remaining, 0, 100);
    }

    /// <summary>去掉行首的“重置时间 / Resets”等标签词，避免与界面前缀重复显示。</summary>
    private static string CleanLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return Regex.Replace(value.Trim(),
            @"^(重置时间|重置|刷新时间|刷新|リセット|Resets?|Refreshes?|Reset time)\s*[:：]?\s*", "",
            RegexOptions.IgnoreCase);
    }

    private static (string? usage, string? reset, string[] block) FindSection(string[] lines, string[] keys)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            if (!keys.Any(k => lines[i].Contains(k, StringComparison.OrdinalIgnoreCase))) continue;
            var block = lines[i..Math.Min(lines.Length, i + 10)];
            var usage = block.FirstOrDefault(x => Regex.IsMatch(x, @"\d+(?:\.\d+)?\s*%"))
                        ?? block.FirstOrDefault(x => ContainsAny(x, RemainKeys) || ContainsAny(x, UsedKeys));
            var reset = block.FirstOrDefault(x => ContainsAny(x, "reset", "resets", "refresh", "重置", "刷新"));
            return (usage, reset, block);
        }
        return (null, null, Array.Empty<string>());
    }

    private static bool ContainsAny(string text, params string[] keys) => keys.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
