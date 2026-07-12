using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CodexWidget;

/// <summary>
/// 通过本机 Codex CLI 登录态（%USERPROFILE%\.codex\auth.json，或 CODEX_HOME）
/// 直接调用官方接口获取 5 小时 / 每周窗口与重置卡数量。
/// 任何一步失败都返回 null，由调用方退回 Edge 网页解析。令牌不落日志、不外传。
/// </summary>
public static class CodexLocalApi
{
    private static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static readonly string[] FiveKeys = { "five", "5h", "primary", "session" };
    private static readonly string[] WeekKeys = { "week", "secondary", "seven_day", "7d" };

    public static async Task<UsageSnapshot?> TryFetchAsync()
    {
        try
        {
            var (token, accountId) = ReadAuth();
            if (string.IsNullOrWhiteSpace(token)) return null;

            var usageJson = await GetJsonAsync("https://chatgpt.com/backend-api/wham/usage", token!, accountId);
            var creditsJson = await GetJsonAsync("https://chatgpt.com/backend-api/wham/rate-limit-reset-credits", token!, accountId);
            if (usageJson == null && creditsJson == null) return null;

            double? fiveLeft = null, weekLeft = null;
            string fiveReset = "", weekReset = "", account = "";
            if (usageJson != null)
            {
                using var doc = JsonDocument.Parse(usageJson);
                var items = new List<(string Path, JsonElement El)>();
                Walk(doc.RootElement, "", items);
                fiveLeft = FindPercentLeft(items, FiveKeys);
                weekLeft = FindPercentLeft(items, WeekKeys);
                fiveReset = FindReset(items, FiveKeys);
                weekReset = FindReset(items, WeekKeys);
                account = items.FirstOrDefault(x => x.Path.EndsWith("/email") && x.El.ValueKind == JsonValueKind.String)
                    .El is { ValueKind: JsonValueKind.String } em ? em.GetString() ?? "" : "";
            }

            int? cardCount = null;
            var cardExpiry = "";
            DateTime? nearestExpiry = null;
            if (creditsJson != null)
            {
                using var doc = JsonDocument.Parse(creditsJson);
                (cardCount, cardExpiry, nearestExpiry) = CountCredits(doc.RootElement);
            }

            if (fiveLeft == null && weekLeft == null && cardCount == null) return null;

            string UsedText(double? left) => left is { } v ? $"{100 - v:0}%" : "";
            return new UsageSnapshot(UsedText(fiveLeft), fiveReset, UsedText(weekLeft), weekReset,
                cardCount?.ToString() ?? "", cardExpiry, fiveLeft, weekLeft, cardCount, account, nearestExpiry);
        }
        catch { return null; }
    }

    private static (string? token, string? accountId) ReadAuth()
    {
        var home = Environment.GetEnvironmentVariable("CODEX_HOME");
        var path = string.IsNullOrWhiteSpace(home)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "auth.json")
            : Path.Combine(home, "auth.json");
        if (!File.Exists(path)) return (null, null);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (!doc.RootElement.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Object)
            return (null, null);
        var token = tokens.TryGetProperty("access_token", out var t) ? t.GetString() : null;
        var account = tokens.TryGetProperty("account_id", out var a) ? a.GetString() : null;
        account ??= AccountIdFromIdToken(tokens);
        return (token, account);
    }

    /// <summary>auth.json 没有 account_id 时，从 id_token 的 JWT 载荷里找 chatgpt_account_id。</summary>
    private static string? AccountIdFromIdToken(JsonElement tokens)
    {
        try
        {
            if (!tokens.TryGetProperty("id_token", out var idToken)) return null;
            var parts = idToken.GetString()?.Split('.');
            if (parts is not { Length: 3 }) return null;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var items = new List<(string Path, JsonElement El)>();
            Walk(doc.RootElement, "", items);
            return items.FirstOrDefault(x => x.Path.Contains("chatgpt_account_id") && x.El.ValueKind == JsonValueKind.String)
                .El is { ValueKind: JsonValueKind.String } el ? el.GetString() : null;
        }
        catch { return null; }
    }

    private static async Task<string?> GetJsonAsync(string url, string token, string? accountId)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!string.IsNullOrEmpty(accountId)) req.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", accountId);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0");
            req.Headers.TryAddWithoutValidation("Origin", "https://chatgpt.com");
            req.Headers.TryAddWithoutValidation("Referer", "https://chatgpt.com/");
            var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync();
        }
        catch { return null; }
    }

    /// <summary>把 JSON 摊平成 (小写路径, 叶子值) 列表，字段名变化也能按关键词匹配。</summary>
    private static void Walk(JsonElement el, string path, List<(string Path, JsonElement El)> items)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject()) Walk(p.Value, path + "/" + p.Name.ToLowerInvariant(), items);
                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in el.EnumerateArray()) Walk(item, path + "/" + i++, items);
                break;
            default:
                items.Add((path, el));
                break;
        }
    }

    private static double? FindPercentLeft(List<(string Path, JsonElement El)> items, string[] windowKeys)
    {
        foreach (var (path, el) in items)
        {
            if (el.ValueKind != JsonValueKind.Number) continue;
            if (!path.Contains("percent") && !path.Contains("pct")) continue;
            if (!windowKeys.Any(path.Contains)) continue;
            var v = Math.Clamp(el.GetDouble(), 0, 100);
            if (path.Contains("left") || path.Contains("remaining") || path.Contains("available")) return v;
            return 100 - v; // used_percent 或含义不明时按“已用”换算
        }
        return null;
    }

    private static string FindReset(List<(string Path, JsonElement El)> items, string[] windowKeys)
    {
        foreach (var (path, el) in items)
        {
            if (!path.Contains("reset")) continue;
            if (!windowKeys.Any(path.Contains)) continue;
            if (FormatTime(el) is { } time) return time;
        }
        return "";
    }

    private static DateTime? ParseTime(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Number)
        {
            var n = el.GetDouble();
            if (n > 1e12) return DateTimeOffset.FromUnixTimeMilliseconds((long)n).LocalDateTime;
            if (n > 1e9) return DateTimeOffset.FromUnixTimeSeconds((long)n).LocalDateTime;
            if (n > 0) return DateTime.Now.AddSeconds(n); // 剩余秒数
            return null;
        }
        if (el.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(el.GetString(), out var dto))
            return dto.LocalDateTime;
        return null;
    }

    private static string? FormatTime(JsonElement el) => ParseTime(el)?.ToString("MM-dd HH:mm");

    /// <summary>统计可用重置卡张数、每张到期时间（顿号分隔）与最近一张的到期时刻。</summary>
    private static (int? count, string expiry, DateTime? nearest) CountCredits(JsonElement root)
    {
        int? count = null;
        var expiries = new List<DateTime>();

        // 首选结构化解析：credits 数组里每个对象的 status / expires_at
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("credits", out var credits) && credits.ValueKind == JsonValueKind.Array)
        {
            var available = 0;
            foreach (var credit in credits.EnumerateArray())
            {
                if (credit.ValueKind != JsonValueKind.Object) continue;
                var status = credit.TryGetProperty("status", out var st) ? st.GetString()?.ToLowerInvariant() ?? "" : "available";
                if (!(status.Contains("avail") || status.Contains("active") || status.Contains("granted") || status.Contains("unused"))) continue;
                available++;
                if (credit.TryGetProperty("expires_at", out var exp) && ParseTime(exp) is { } t) expiries.Add(t);
            }
            count = available;
        }
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("available_count", out var ac) && ac.ValueKind == JsonValueKind.Number)
            count = (int)ac.GetDouble();

        // 结构对不上时的兜底：全树扫描
        if (count == null)
        {
            var items = new List<(string Path, JsonElement El)>();
            Walk(root, "", items);
            var statuses = items.Where(x => x.Path.EndsWith("/status") && x.El.ValueKind == JsonValueKind.String)
                .Select(x => x.El.GetString()!.ToLowerInvariant()).ToList();
            if (statuses.Count > 0)
                count = statuses.Count(s => s.Contains("avail") || s.Contains("active") || s.Contains("granted") || s.Contains("unused"));
            expiries = items.Where(x => x.Path.Contains("expire"))
                .Select(x => ParseTime(x.El)).Where(t => t != null).Select(t => t!.Value).ToList();
        }

        expiries.Sort();
        expiries = expiries.Distinct().ToList();
        var text = string.Join("、", expiries.Select(t => t.ToString("MM-dd HH:mm")));
        return (count, text, expiries.Count > 0 ? expiries[0] : null);
    }
}
