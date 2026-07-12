using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;

namespace CodexWidget;

public partial class UsageWindow : Window
{
    private const int DebugPort = 9339;
    private bool reallyClosing;
    private string provider;
    private bool lastSyncOk;
    private DispatcherTimer? loginPoll;
    private int loginPollCount;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(4) };

    public event Action<UsageSnapshot>? UsageUpdated;
    public event Action? LoginRequired;

    public UsageWindow(string initialProvider)
    {
        provider = initialProvider;
        InitializeComponent();
        ApplyLanguage();
        Closing += (_, e) => { if (!reallyClosing) { e.Cancel = true; Hide(); } };
    }

    public async Task StartAsync()
    {
        Hide();
        await TryCollectAsync(false);
    }

    public void SetProvider(string value)
    {
        provider = value;
        ApplyLanguage();
        _ = TryCollectAsync(false);
    }

    /// <summary>直接拉起浏览器跳到官方页（无指引窗口），并开始自动轮询同步。</summary>
    public bool LaunchLogin(string preferredBrowser)
    {
        var exe = FindBrowser(preferredBrowser);
        if (exe == null) return false;
        var name = Path.GetFileNameWithoutExtension(exe);
        var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexUmaruWidget", $"LoginProfile-{name}");
        Directory.CreateDirectory(profile);
        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--remote-debugging-port={DebugPort} --user-data-dir=\"{profile}\" --no-first-run --app=\"{UsageUrl}\"",
            UseShellExecute = true
        });
        StartLoginPolling();
        return true;
    }

    /// <summary>登录窗口打开后每 10 秒自动尝试同步，成功或超过 5 分钟即停。</summary>
    private void StartLoginPolling()
    {
        loginPoll?.Stop();
        loginPollCount = 0;
        loginPoll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        loginPoll.Tick += async (_, _) =>
        {
            loginPollCount++;
            await TryCollectAsync(false);
            if (lastSyncOk || loginPollCount >= 30) loginPoll?.Stop();
        };
        loginPoll.Start();
    }

    public void RefreshUsage() => _ = TryCollectAsync(false);
    public void ReallyClose() { reallyClosing = true; loginPoll?.Stop(); Close(); }

    private string UsageUrl => provider switch
    {
        "Gemini" => "https://gemini.google.com/app",
        "Claude" => "https://claude.ai/settings/usage",
        _ => "https://chatgpt.com/codex/settings/usage"
    };

    private string Domain => provider switch
    {
        "Gemini" => "gemini.google.com",
        "Claude" => "claude.ai",
        _ => "chatgpt.com"
    };

    public void ApplyLanguage()
    {
        if (ProviderTitle == null) return;
        Title = Loc.T("Login.WindowTitle");
        ProviderTitle.Text = Loc.F("Login.Title", provider);
        HintText.Text = provider switch
        {
            "Gemini" => Loc.T("Login.HintGemini"),
            "Claude" => Loc.T("Login.HintClaude"),
            _ => Loc.T("Login.HintCodex")
        };
        ModeTitle.Text = Loc.T("Login.ModeTitle");
        ModeBody.Text = Loc.T("Login.ModeBody");
        SyncStatus.Text = Loc.T("Login.NotConnected");
        BtnOpen.Content = Loc.T("Login.BtnOpen");
        BtnSync.Content = Loc.T("Login.BtnSync");
        BtnHide.Content = Loc.T("Login.BtnHide");
    }

    /// <summary>按用户偏好找浏览器：默认 Chrome，找不到退回 Edge（反之亦然）。</summary>
    private static string? FindBrowser(string preferred)
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var chrome = new[]
        {
            Path.Combine(pf, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(pf86, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe")
        };
        var edge = new[]
        {
            Path.Combine(pf86, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(pf, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(local, "Microsoft", "Edge", "Application", "msedge.exe")
        };
        var ordered = string.Equals(preferred, "Edge", StringComparison.OrdinalIgnoreCase)
            ? edge.Concat(chrome) : chrome.Concat(edge);
        return ordered.FirstOrDefault(File.Exists);
    }

    private async Task TryCollectAsync(bool showFeedback)
    {
        // Codex：优先用本机 Codex CLI 登录态直连官方接口（含重置卡），无需 Edge
        if (provider == "Codex")
        {
            var api = await CodexLocalApi.TryFetchAsync();
            if (api != null)
            {
                UsageUpdated?.Invoke(api);
                if (showFeedback) SyncStatus.Text = Loc.F("Login.SyncOk", DateTime.Now.ToString("HH:mm:ss"));
                return;
            }
        }
        try
        {
            var json = await http.GetStringAsync($"http://127.0.0.1:{DebugPort}/json/list");
            using var doc = JsonDocument.Parse(json);
            // 扫描所有匹配域名的页面，取第一个已登录且有用量内容的
            var sockets = doc.RootElement.EnumerateArray()
                .Where(x => x.TryGetProperty("url", out var url) &&
                            url.GetString()!.Contains(Domain, StringComparison.OrdinalIgnoreCase) &&
                            x.TryGetProperty("webSocketDebuggerUrl", out _))
                .Select(x => x.GetProperty("webSocketDebuggerUrl").GetString()!)
                .ToList();
            if (sockets.Count == 0)
                throw new InvalidOperationException(Loc.T("Login.NotConnected"));

            foreach (var socketUrl in sockets)
            {
                var text = await ReadBodyTextAsync(socketUrl);
                if (string.IsNullOrWhiteSpace(text) || LooksLikeLogin(text)) continue;
                lastSyncOk = true;
                UsageUpdated?.Invoke(UsageParser.Parse(text, provider));
                if (showFeedback) SyncStatus.Text = Loc.F("Login.SyncOk", DateTime.Now.ToString("HH:mm:ss"));
                return;
            }
            lastSyncOk = false;
            LoginRequired?.Invoke();
            if (showFeedback) SyncStatus.Text = Loc.T("Login.NotLoggedIn");
        }
        catch (Exception ex)
        {
            lastSyncOk = false;
            LoginRequired?.Invoke();
            if (showFeedback) SyncStatus.Text = Loc.F("Login.SyncFail", ex.Message);
        }
    }

    private static async Task<string> ReadBodyTextAsync(string websocketUrl)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(websocketUrl), CancellationToken.None);
        var command = Encoding.UTF8.GetBytes("{\"id\":1,\"method\":\"Runtime.evaluate\",\"params\":{\"expression\":\"document.body ? document.body.innerText : ''\",\"returnByValue\":true}}" );
        await socket.SendAsync(command, WebSocketMessageType.Text, true, CancellationToken.None);
        using var stream = new MemoryStream();
        var buffer = new byte[32768];
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.GetProperty("result").GetProperty("result").GetProperty("value").GetString() ?? string.Empty;
    }

    private static bool LooksLikeLogin(string text)
    {
        // 页面上有百分比就是用量页，别误判成登录页
        if (Regex.IsMatch(text, @"\d+(?:\.\d+)?\s*%")) return false;
        var hasLoginWords = text.Contains("Log in", StringComparison.OrdinalIgnoreCase) ||
                            text.Contains("Sign in", StringComparison.OrdinalIgnoreCase) ||
                            text.Contains("Continue with", StringComparison.OrdinalIgnoreCase) ||
                            text.Contains("登录");
        return hasLoginWords && text.Length < 4000;
    }

    private void OpenUsage_Click(object sender, RoutedEventArgs e) => LaunchLogin("Chrome");
    private async void Sync_Click(object sender, RoutedEventArgs e) => await TryCollectAsync(true);
    private void Done_Click(object sender, RoutedEventArgs e) => Hide();
}

/// <summary>
/// 用量快照。FiveHourRemaining / WeeklyRemaining 为“剩余可用百分比”（0~100，解析不到为 null）。
/// ExtraUsage / ExtraNote 按平台复用：Codex = 重置卡数与到期；Claude = Fable 5 额度与重置；Gemini 留空。
/// ResetCardCount 为 Codex 重置卡可用张数（解析不到为 null）。
/// </summary>
public sealed record UsageSnapshot(string FiveHourUsage, string FiveHourReset, string WeeklyUsage,
    string WeeklyReset, string ExtraUsage, string ExtraNote,
    double? FiveHourRemaining = null, double? WeeklyRemaining = null, int? ResetCardCount = null);

internal static class UsageParser
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
