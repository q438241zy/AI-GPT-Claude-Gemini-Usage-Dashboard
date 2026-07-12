using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodexWidget;

/// <summary>
/// 跨平台用量采集器（无界面）：
/// - Codex：优先读本机 Codex CLI 登录态直连官方接口；
/// - 其余：通过带调试端口的 Chrome / Edge / Chromium 读取官方用量页文字再解析。
/// 浏览器路径按 Windows / macOS / Linux 分别探测。
/// </summary>
public sealed class UsageCollector
{
    private const int DebugPort = 9339;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(4) };

    public string Provider { get; set; } = "Codex";

    /// <summary>用户退出登录后置 false，停止读取本机 Codex CLI 登录态。</summary>
    public bool UseCodexLocalApi { get; set; } = true;

    private string UsageUrl => Provider switch
    {
        "Gemini" => "https://gemini.google.com/app",
        "Claude" => "https://claude.ai/settings/usage",
        _ => "https://chatgpt.com/codex/settings/usage"
    };

    private string Domain => Provider switch
    {
        "Gemini" => "gemini.google.com",
        "Claude" => "claude.ai",
        _ => "chatgpt.com"
    };

    /// <summary>采集一次。返回 null 表示未登录 / 未连接到官方页面。</summary>
    public async Task<UsageSnapshot?> CollectAsync()
    {
        if (Provider == "Codex" && UseCodexLocalApi)
        {
            var api = await CodexLocalApi.TryFetchAsync();
            if (api != null) return api;
        }
        try
        {
            var json = await http.GetStringAsync($"http://127.0.0.1:{DebugPort}/json/list");
            using var doc = JsonDocument.Parse(json);
            var sockets = doc.RootElement.EnumerateArray()
                .Where(x => x.TryGetProperty("url", out var url) &&
                            url.GetString()!.Contains(Domain, StringComparison.OrdinalIgnoreCase) &&
                            x.TryGetProperty("webSocketDebuggerUrl", out _))
                .Select(x => x.GetProperty("webSocketDebuggerUrl").GetString()!)
                .ToList();
            foreach (var socketUrl in sockets)
            {
                var text = await ReadBodyTextAsync(socketUrl);
                if (string.IsNullOrWhiteSpace(text) || LooksLikeLogin(text)) continue;
                return UsageParser.Parse(text, Provider);
            }
        }
        catch { }
        return null;
    }

    /// <summary>拉起浏览器直跳官方页；返回 false 表示没找到可用浏览器。</summary>
    public bool LaunchLogin(string preferredBrowser)
    {
        var exe = FindBrowser(preferredBrowser);
        if (exe == null) return false;
        var name = Path.GetFileNameWithoutExtension(exe).Replace(' ', '-');
        var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexUmaruWidget", $"LoginProfile-{name}");
        Directory.CreateDirectory(profile);
        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            Arguments = $"--remote-debugging-port={DebugPort} --user-data-dir=\"{profile}\" --no-first-run --app=\"{UsageUrl}\"",
            UseShellExecute = false
        });
        return true;
    }

    /// <summary>按系统探测 Chrome / Edge / Chromium，优先用户偏好。</summary>
    private static string? FindBrowser(string preferred)
    {
        string[] chrome, edge;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            chrome = new[]
            {
                Path.Combine(pf, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(pf86, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe")
            };
            edge = new[]
            {
                Path.Combine(pf86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(pf, "Microsoft", "Edge", "Application", "msedge.exe")
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            chrome = new[]
            {
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                "/Applications/Chromium.app/Contents/MacOS/Chromium"
            };
            edge = new[] { "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge" };
        }
        else
        {
            chrome = FromPath("google-chrome", "google-chrome-stable", "chromium", "chromium-browser");
            edge = FromPath("microsoft-edge", "microsoft-edge-stable");
        }
        var ordered = string.Equals(preferred, "Edge", StringComparison.OrdinalIgnoreCase)
            ? edge.Concat(chrome) : chrome.Concat(edge);
        return ordered.FirstOrDefault(File.Exists);
    }

    /// <summary>Linux：在 PATH 里找可执行文件。</summary>
    private static string[] FromPath(params string[] names)
    {
        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        return names.SelectMany(n => dirs.Select(d => Path.Combine(d, n))).ToArray();
    }

    private static async Task<string> ReadBodyTextAsync(string websocketUrl)
    {
        using var socket = new ClientWebSocket();
        await socket.ConnectAsync(new Uri(websocketUrl), CancellationToken.None);
        var command = Encoding.UTF8.GetBytes("{\"id\":1,\"method\":\"Runtime.evaluate\",\"params\":{\"expression\":\"document.body ? document.body.innerText : ''\",\"returnByValue\":true}}");
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
}
