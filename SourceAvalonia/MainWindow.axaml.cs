using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CodexWidget;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CodexWidgetCross;

public partial class MainWindow : Window
{
    private WidgetSettings settings = WidgetSettings.Load();
    private readonly UsageCollector collector = new();
    private readonly DispatcherTimer refreshTimer = new();
    private DispatcherTimer? loginPoll;
    private int loginPollCount;
    private UsageSnapshot? lastSnapshot;
    private bool demoMode;

    private readonly ScaleTransform mascotScale = new();
    private readonly TranslateTransform mascotTranslate = new();
    private readonly RotateTransform boardRotate = new();
    private readonly TranslateTransform boardTranslate = new();
    private double mascotHeight = 460;
    private static readonly Dictionary<string, Bitmap> imageCache = new();

    public MainWindow()
    {
        InitializeComponent();
        MascotImage.RenderTransform = new TransformGroup { Children = { mascotScale, mascotTranslate } };
        BoardBorder.RenderTransform = new TransformGroup { Children = { boardRotate, boardTranslate } };
        RootGrid.PointerPressed += Root_PointerPressed;
        refreshTimer.Tick += async (_, _) => await CollectAndRenderAsync();
        Opened += async (_, _) =>
        {
            ApplySettings(false);
            if (TryApplyDemo()) { demoMode = true; return; }
            await CollectAndRenderAsync();
            refreshTimer.Start();
        };
    }

    /// <summary>命令行 --demo [小时余量%] [周余量%] [重置卡数]，供创作者调试角色状态。</summary>
    private bool TryApplyDemo()
    {
        var args = Environment.GetCommandLineArgs();
        var idx = Array.IndexOf(args, "--demo");
        if (idx < 0) return false;
        double Arg(int offset, double fallback) =>
            args.Length > idx + offset && double.TryParse(args[idx + offset], out var v) ? v : fallback;
        var five = Arg(1, 100);
        var week = Arg(2, 100);
        var cards = (int)Arg(3, 3);
        lastSnapshot = new UsageSnapshot($"{100 - five:0}%", "12:00", $"{100 - week:0}%", "07-15 08:00",
            cards.ToString(), "07-27 08:05", five, week, cards);
        RenderUsage(lastSnapshot);
        ApplyMascot();
        return true;
    }

    private async Task CollectAndRenderAsync()
    {
        collector.Provider = settings.Provider;
        var snapshot = await collector.CollectAsync();
        if (snapshot == null)
        {
            if (lastSnapshot == null) UpdatedAt.Text = Loc.T("Usage.NeedLogin");
            return;
        }
        lastSnapshot = snapshot;
        RenderUsage(snapshot);
        ApplyMascot();
    }

    private void ApplySettings(bool providerChanged)
    {
        Loc.Lang = settings.Language;
        Width = 380 * settings.Scale;
        Height = 802 * settings.Scale;
        Opacity = settings.Opacity;
        Topmost = settings.AlwaysOnTop;
        refreshTimer.Interval = TimeSpan.FromMinutes(Math.Clamp(settings.RefreshMinutes, 1, 120));
        ApplyLocalization();
        ApplyMascot();
        RefreshCharacterMenu();
        PositionBottomRight();
        if (providerChanged && !demoMode) _ = CollectAndRenderAsync();
    }

    private void PositionBottomRight()
    {
        var screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen == null) return;
        var wa = screen.WorkingArea;
        var scale = screen.Scaling;
        var w = (int)Math.Round(Width * scale);
        var h = (int)Math.Round(Height * scale);
        Position = new PixelPoint(Math.Max(wa.X + 8, wa.Right - w - 14), Math.Max(wa.Y + 8, wa.Bottom - h - 8));
    }

    private void ApplyLocalization()
    {
        DashboardTitle.Text = Loc.F("Board.Title", settings.Provider.ToUpperInvariant());
        MenuSettings.Header = Loc.T("Menu.Settings");
        MenuRefresh.Header = Loc.T("Menu.Refresh");
        MenuLogin.Header = Loc.T("Menu.Login");
        MenuProvider.Header = Loc.T("Menu.Provider");
        MenuCharacter.Header = Loc.T("Menu.Character");
        MenuExit.Header = Loc.T("Menu.Exit");
        LoginButton.Content = Loc.T("Board.LoginBtn");
        ToolTip.SetTip(CardBadge, Loc.T("Badge.Tooltip"));
        if (lastSnapshot != null) RenderUsage(lastSnapshot);
        else
        {
            FiveUsage.Text = Loc.F("Usage.Five", Loc.T("Usage.Syncing"));
            FiveReset.Text = Loc.F("Usage.Reset", "—");
            WeekUsage.Text = Loc.F("Usage.Week", Loc.T("Usage.Syncing"));
            WeekReset.Text = Loc.F("Usage.Reset", "—");
            ApplyProviderRows(null);
            UpdatedAt.Text = Loc.T("Usage.Waiting");
        }
    }

    private void RenderUsage(UsageSnapshot s)
    {
        string Show(string v) => string.IsNullOrWhiteSpace(v) ? Loc.T("Usage.NotShown") : v;
        FiveUsage.Text = s.FiveHourRemaining is { } fr
            ? Loc.F("Usage.FiveLeft", fr.ToString("0"))
            : Loc.F("Usage.Five", Show(s.FiveHourUsage));
        FiveReset.Text = Loc.F("Usage.Reset", Show(s.FiveHourReset));
        WeekUsage.Text = s.WeeklyRemaining is { } wr
            ? Loc.F("Usage.WeekLeft", wr.ToString("0"))
            : Loc.F("Usage.Week", Show(s.WeeklyUsage));
        WeekReset.Text = Loc.F("Usage.Reset", Show(s.WeeklyReset));
        ApplyProviderRows(s);
        UpdatedAt.Text = Loc.F("Usage.UpdatedAt", DateTime.Now.ToString("HH:mm"));
    }

    /// <summary>平台差异化栏位：Codex 显示重置卡行 + 圆圈徽章 + 到期；Claude 显示 Fable 5 行；Gemini 皆无。</summary>
    private void ApplyProviderRows(UsageSnapshot? s)
    {
        var isCodex = settings.Provider == "Codex";
        var isClaude = settings.Provider == "Claude";
        CardsRow.IsVisible = isCodex;
        CardExpiry.IsVisible = isCodex && !string.IsNullOrWhiteSpace(s?.ExtraNote);
        ExtraSeparator.IsVisible = isCodex || isClaude;
        ExtraUsage.IsVisible = isClaude;
        if (isCodex)
        {
            CardsLabel.Text = Loc.T("Usage.CardsLabel");
            CardBadgeText.Text = s?.ResetCardCount?.ToString() ?? "—";
            if (!string.IsNullOrWhiteSpace(s?.ExtraNote))
                CardExpiry.Text = Loc.F("Usage.CardExpiry", s!.ExtraNote);
        }
        if (isClaude)
        {
            var text = string.IsNullOrWhiteSpace(s?.ExtraUsage) ? Loc.T("Usage.NotShown") : s!.ExtraUsage;
            if (!string.IsNullOrWhiteSpace(s?.ExtraNote)) text += "｜" + s!.ExtraNote;
            ExtraUsage.Text = Loc.F("Usage.Fable", text);
        }
    }

    /// <summary>角色子菜单：内置角色 + 已装角色包 + 自定义图片 + 创作者入口。</summary>
    private void RefreshCharacterMenu()
    {
        MenuCharacter.Items.Clear();
        AddCharacterItem("多啦A梦", "Doraemon");
        AddCharacterItem("小埋", "Umaru");
        foreach (var pack in CharacterPack.LoadAll()) AddCharacterItem(pack.DisplayName, pack.Key);
        var custom = new MenuItem { Header = Loc.T("Menu.CustomImage") };
        custom.Click += async (_, _) => await PickCustomImageAsync();
        MenuCharacter.Items.Add(custom);
        MenuCharacter.Items.Add(new Separator());
        var import = new MenuItem { Header = Loc.T("Menu.ImportPack") };
        import.Click += async (_, _) => await ImportCharacterPackAsync();
        MenuCharacter.Items.Add(import);
        var open = new MenuItem { Header = Loc.T("Menu.OpenPackDir") };
        open.Click += (_, _) => OpenCharacterFolder();
        MenuCharacter.Items.Add(open);
    }

    private void AddCharacterItem(string header, string key)
    {
        var item = new MenuItem { Header = header, Tag = key };
        item.Click += (_, _) =>
        {
            settings.Character = key;
            settings.Save();
            ApplySettings(false);
        };
        MenuCharacter.Items.Add(item);
    }

    private async Task PickCustomImageAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("图片") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif" } } }
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path == null) return;
        settings.CustomCharacterPath = path;
        settings.Character = "Custom";
        settings.Save();
        ApplySettings(false);
    }

    private async Task ImportCharacterPackAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path == null) return;
        var pack = CharacterPack.Import(path);
        if (pack == null) { UpdatedAt.Text = Loc.T("Msg.PackInvalid"); return; }
        settings.Character = pack.Key;
        settings.Save();
        ApplySettings(false);
        UpdatedAt.Text = Loc.F("Msg.PackImported", pack.DisplayName);
    }

    internal static void OpenCharacterFolder()
    {
        Directory.CreateDirectory(CharacterPack.Root);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo { FileName = CharacterPack.Root, UseShellExecute = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", $"\"{CharacterPack.Root}\"");
        else
            Process.Start("xdg-open", $"\"{CharacterPack.Root}\"");
    }

    /// <summary>按 5 小时/每周余量刷新角色图与姿势（与 Windows 版同一套规则）。</summary>
    private void ApplyMascot()
    {
        var expression = MascotState.ExpressionFor(lastSnapshot?.FiveHourRemaining);
        var pose = MascotState.PoseFor(lastSnapshot?.WeeklyRemaining);

        string? path = null;
        var poseFromImage = false;
        var boardArt = false; // 图片里画了看板（原版立绘），文字直接叠上去

        if (settings.Character.StartsWith("Pack:", StringComparison.Ordinal))
        {
            var pack = CharacterPack.LoadAll().FirstOrDefault(p => p.Key == settings.Character);
            if (pack != null) (path, poseFromImage, _) = pack.Resolve(pose, expression);
        }
        else if (settings.Character == "Umaru")
        {
            if (pose == MascotPose.Stand)
            {
                path = $"avares://CodexWidgetCross/Assets/Umaru/stand/{MascotState.FileName(expression)}.png";
                boardArt = expression == MascotExpression.Happy;
            }
            else
            {
                path = $"avares://CodexWidgetCross/Assets/Umaru/{MascotState.FileName(pose)}/default.png";
                poseFromImage = true;
            }
        }
        else if (settings.Character == "Custom")
        {
            path = settings.CustomCharacterPath;
        }
        else
        {
            path = "avares://CodexWidgetCross/Assets/Doraemon/default.png";
            boardArt = true;
        }

        var size = LoadMascotImage(path);
        if (!boardArt)
        {
            const double areaWidth = 352; // 380 - 左右 14 边距
            var aspect = size is { Width: > 0 } s2 ? s2.Height / (double)s2.Width : 1.0;
            mascotHeight = Math.Min(465, areaWidth * aspect);
        }
        ApplyMascotLayout(boardArt);
        ApplyPose(pose, poseFromImage, boardArt);
    }

    /// <summary>原版立绘铺满全窗；状态图模式则“看板模块 + 人物模块”上下紧贴叠放。</summary>
    private void ApplyMascotLayout(bool boardArt)
    {
        if (boardArt)
        {
            MascotImage.Stretch = Stretch.Fill;
            MascotImage.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            MascotImage.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            MascotImage.Margin = new Thickness(0);
            MascotImage.Height = double.NaN;
            BoardBorder.Margin = new Thickness(31, 38, 31, 0);
            BoardBorder.Background = new SolidColorBrush(Color.FromArgb(0x08, 0xFF, 0xF9, 0xE7));
            BoardBorder.BorderBrush = null;
            BoardBorder.BorderThickness = new Thickness(0);
        }
        else
        {
            MascotImage.Stretch = Stretch.Uniform;
            MascotImage.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            MascotImage.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom;
            MascotImage.Height = mascotHeight;
            MascotImage.Margin = new Thickness(14, 0, 14, 8);
            var boardTop = Math.Max(16, 802 - 8 - mascotHeight - 281 - 4);
            BoardBorder.Margin = new Thickness(31, boardTop, 31, 0);
            BoardBorder.Background = new SolidColorBrush(Color.FromArgb(0xF2, 0xFF, 0xF6, 0xE0));
            BoardBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD9, 0x9A, 0x55));
            BoardBorder.BorderThickness = new Thickness(2.5);
        }
    }

    private void ApplyPose(MascotPose pose, bool poseFromImage, bool boardArt)
    {
        var squash = !poseFromImage;
        switch (pose)
        {
            case MascotPose.Sit:
                mascotScale.ScaleX = squash ? 1.05 : 1; mascotScale.ScaleY = squash ? 0.74 : 1;
                boardTranslate.Y = boardArt && squash ? 170 : 0; boardRotate.Angle = 0;
                break;
            case MascotPose.Crushed:
                mascotScale.ScaleX = squash ? 1.16 : 1; mascotScale.ScaleY = squash ? 0.30 : 1;
                boardTranslate.Y = boardArt ? 330 : Math.Max(70, mascotHeight * 0.55);
                boardRotate.Angle = -5;
                break;
            default:
                mascotScale.ScaleX = 1; mascotScale.ScaleY = 1;
                boardTranslate.Y = 0; boardRotate.Angle = 0;
                break;
        }
    }

    private (int Width, int Height)? LoadMascotImage(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            if (!imageCache.TryGetValue(path, out var bitmap))
            {
                bitmap = path.StartsWith("avares:", StringComparison.Ordinal)
                    ? new Bitmap(AssetLoader.Open(new Uri(path)))
                    : new Bitmap(path);
                if (imageCache.Count > 24) imageCache.Clear();
                imageCache[path] = bitmap;
            }
            MascotImage.Source = bitmap;
            return (bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        }
        catch { UpdatedAt.Text = Loc.T("Msg.CharacterLoadFail"); return null; }
    }

    private void Root_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Button) return;
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (demoMode) return;
        UpdatedAt.Text = Loc.T("Usage.Refreshing");
        _ = CollectAndRenderAsync();
    }

    private void Login_Click(object? sender, RoutedEventArgs e)
    {
        if (demoMode) return;
        if (!collector.LaunchLogin(settings.Browser)) { UpdatedAt.Text = Loc.T("Login.NoBrowser"); return; }
        UpdatedAt.Text = Loc.T("Login.Opened");
        StartLoginPolling();
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
            var before = lastSnapshot;
            await CollectAndRenderAsync();
            if (!ReferenceEquals(before, lastSnapshot) || loginPollCount >= 30) loginPoll?.Stop();
        };
        loginPoll.Start();
    }

    private void ProviderMenu_Click(object? sender, RoutedEventArgs e)
    {
        settings.Provider = ((MenuItem)sender!).Tag!.ToString()!;
        settings.Save();
        lastSnapshot = null;
        ApplySettings(true);
    }

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var original = settings.Clone();
        var dialog = new SettingsWindow(settings.Clone(), preview =>
        {
            settings = preview;
            ApplySettings(false);
        });
        var saved = await dialog.ShowDialog<bool?>(this);
        if (saved == true)
        {
            settings = dialog.Value.Clone();
            settings.Save();
            if (original.Provider != settings.Provider) lastSnapshot = null;
            ApplySettings(original.Provider != settings.Provider);
        }
        else
        {
            settings = original;
            ApplySettings(false);
        }
    }

    private void Exit_Click(object? sender, RoutedEventArgs e)
    {
        refreshTimer.Stop();
        loginPoll?.Stop();
        Close();
    }
}
