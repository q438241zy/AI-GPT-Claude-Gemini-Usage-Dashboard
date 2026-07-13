using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace CodexWidget;

public partial class MainWindow : Window
{
    private UsageWindow? usageWindow;
    private WidgetSettings settings = WidgetSettings.Load();
    private HotkeyManager? hotkeys;
    private readonly DispatcherTimer refreshTimer = new();
    private Storyboard? mascotStoryboard;
    private UsageSnapshot? lastSnapshot;
    private bool clickThroughApplied;

    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        ContentRendered += (_, _) => MoveToBottomRight();
        SourceInitialized += (_, _) => InitializeHotkeys();
        Deactivated += (_, _) => { if (settings.AlwaysOnTop) Topmost = true; };
        refreshTimer.Tick += (_, _) => usageWindow?.RefreshUsage();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplySettings(false);
        if (TryApplyDemo()) return;
        usageWindow = new UsageWindow(settings.Provider) { UseCodexLocalApi = !settings.DisableCodexLocalApi };
        usageWindow.UsageUpdated += ApplyUsage;
        usageWindow.LoginRequired += () => Dispatcher.Invoke(() => UpdatedAt.Text = Loc.T("Usage.NeedLogin"));
        await usageWindow.StartAsync();
        refreshTimer.Start();
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
        demoRun = true;
        var expiries = string.Join("、", Enumerable.Range(0, cards)
            .Select(i => DateTime.Now.AddDays(2 + i * 5).ToString("MM-dd HH:mm")));
        ApplyUsage(new UsageSnapshot($"{100 - five:0}% used", "12:00", $"{100 - week:0}% used", "07-15 08:00",
            cards.ToString(), expiries, five, week, cards,
            "demo@example.com", cards > 0 ? DateTime.Now.AddDays(2) : null));
        return true;
    }

    private void InitializeHotkeys()
    {
        hotkeys = new HotkeyManager(this);
        hotkeys.Pressed += id => Dispatcher.Invoke(() =>
        {
            switch (id)
            {
                case 1: if (IsVisible) Hide(); else { Show(); Activate(); Topmost = settings.AlwaysOnTop; } break;
                case 2: ToggleClickThrough(false); Show(); Activate(); break;
                case 3: RefreshNow(); break;
                case 4: ShowSettings(); break;
                case 5: ExitApp(); break;
                case 6: ToggleClickThrough(false); Show(); Activate(); break;
            }
        });
    }

    private void ApplySettings(bool providerMayHaveChanged)
    {
        Loc.Lang = settings.Language;
        Width = 380 * settings.Scale;
        Height = 802 * settings.Scale;
        Opacity = settings.Opacity;
        Topmost = settings.AlwaysOnTop;
        if (!paceActive) refreshTimer.Interval = TimeSpan.FromMinutes(Math.Clamp(settings.RefreshMinutes, 1, 120));
        ClickThroughMenu.IsChecked = settings.ClickThrough;
        if (usageWindow != null) usageWindow.UseCodexLocalApi = !settings.DisableCodexLocalApi;
        ApplyLocalization();
        ApplyMascot();
        ApplyAnimation();
        RefreshCharacterMenu();
        MoveToBottomRight();
        ToggleClickThrough(settings.ClickThrough);
        if (providerMayHaveChanged) usageWindow?.SetProvider(settings.Provider);
    }

    private void ApplyLocalization()
    {
        DashboardTitle.Text = Loc.F("Board.Title", settings.Provider.ToUpperInvariant());
        MenuSettings.Header = Loc.T("Menu.Settings");
        MenuRefresh.Header = Loc.T("Menu.Refresh");
        MenuLogin.Header = Loc.T("Menu.Login");
        MenuProvider.Header = Loc.T("Menu.Provider");
        MenuCharacter.Header = Loc.T("Menu.Character");
        ClickThroughMenu.Header = Loc.T("Menu.ClickThrough");
        MenuHotkeys.Header = Loc.T("Menu.Hotkeys");
        MenuExit.Header = Loc.T("Menu.Exit");
        CardBadge.ToolTip = Loc.T("Badge.Tooltip");
        RandomButton.ToolTip = Loc.T("Menu.Random");
        UpdateLoginButton();
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
        usageWindow?.ApplyLanguage();
    }

    /// <summary>角色子菜单：内置角色 + 已装角色包 + 自定义图片 + 创作者入口。</summary>
    private void RefreshCharacterMenu()
    {
        MenuCharacter.Items.Clear();
        AddCharacterItem("多啦A梦", "Doraemon");
        AddCharacterItem("小埋", "Umaru");
        foreach (var pack in CharacterPack.LoadAll()) AddCharacterItem(pack.DisplayName, pack.Key);
        AddCharacterItem(Loc.T("Menu.CustomImage"), "Custom");
        MenuCharacter.Items.Add(new Separator());
        var import = new System.Windows.Controls.MenuItem { Header = Loc.T("Menu.ImportPack") };
        import.Click += (_, _) => ImportCharacterPack();
        MenuCharacter.Items.Add(import);
        var open = new System.Windows.Controls.MenuItem { Header = Loc.T("Menu.OpenPackDir") };
        open.Click += (_, _) => OpenCharacterFolder();
        MenuCharacter.Items.Add(open);
    }

    private void AddCharacterItem(string header, string key)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header, Tag = key, IsChecked = settings.Character == key };
        item.Click += CharacterMenu_Click;
        MenuCharacter.Items.Add(item);
    }

    private void ImportCharacterPack()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var pack = CharacterPack.Import(dialog.SelectedPath);
        if (pack == null) { UpdatedAt.Text = Loc.T("Msg.PackInvalid"); return; }
        settings.Character = pack.Key;
        settings.Save();
        ApplySettings(false);
        UpdatedAt.Text = Loc.F("Msg.PackImported", pack.DisplayName);
    }

    internal static void OpenCharacterFolder()
    {
        Directory.CreateDirectory(CharacterPack.Root);
        Process.Start(new ProcessStartInfo { FileName = CharacterPack.Root, UseShellExecute = true });
    }

    /// <summary>
    /// 按 5 小时/每周余量刷新角色图与姿势。表情/姿势优先用图片集里的真实立绘：
    /// 姿势图（sit/crushed）优先于表情图（happy/nervous/crying/sleeping）；
    /// 都没有时退回默认立绘并用变形动画表现姿势。
    /// </summary>
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
                path = $"pack://application:,,,/Assets/Umaru/stand/{MascotState.FileName(expression)}.png";
                boardArt = expression == MascotExpression.Happy;
            }
            else
            {
                path = $"pack://application:,,,/Assets/Umaru/{MascotState.FileName(pose)}/default.png";
                poseFromImage = true;
            }
        }
        else if (settings.Character == "Custom")
        {
            path = settings.CustomCharacterPath;
        }
        else
        {
            path = "pack://application:,,,/Assets/Doraemon/default.png";
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

    private double mascotHeight = 460;

    /// <summary>
    /// 原版立绘铺满全窗（看板画在图里）；状态图模式则是“看板模块 + 人物模块”上下紧贴叠放：
    /// 人物贴底，看板卡片直接压在人物头顶上方，中间不留空隙。
    /// </summary>
    private void ApplyMascotLayout(bool boardArt)
    {
        if (boardArt)
        {
            MascotImage.Stretch = Stretch.Fill;
            MascotImage.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            MascotImage.VerticalAlignment = VerticalAlignment.Stretch;
            MascotImage.Margin = new Thickness(0);
            MascotImage.Height = double.NaN;
            BoardBorder.Margin = new Thickness(31, 38, 31, 0);
        }
        else
        {
            MascotImage.Stretch = Stretch.Uniform;
            MascotImage.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            MascotImage.VerticalAlignment = VerticalAlignment.Bottom;
            MascotImage.Height = mascotHeight;
            MascotImage.Margin = new Thickness(14, 0, 14, 8);
            var boardTop = Math.Max(16, 802 - 8 - mascotHeight - 281 - 4);
            BoardBorder.Margin = new Thickness(31, boardTop, 31, 0);
        }
    }

    private void ApplyPose(MascotPose pose, bool poseFromImage, bool boardArt)
    {
        // 文字面板：只有“原版立绘 + 站立”时才透明叠加在画中看板上，其余用实体卡片
        var solidCard = !(boardArt && pose == MascotPose.Stand);
        BoardBorder.Background = solidCard
            ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xF2, 0xFF, 0xF6, 0xE0))
            : new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x08, 0xFF, 0xF9, 0xE7));
        BoardBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD9, 0x9A, 0x55));
        BoardBorder.BorderThickness = new Thickness(solidCard ? 2.5 : 0);

        // 姿势变形只在没有对应姿势图时兜底
        var squash = !poseFromImage;
        switch (pose)
        {
            case MascotPose.Sit:
                MascotScale.ScaleX = squash ? 1.05 : 1; MascotScale.ScaleY = squash ? 0.74 : 1;
                BoardTranslate.Y = boardArt && squash ? 170 : 0; BoardRotate.Angle = 0;
                break;
            case MascotPose.Crushed:
                // 看板卡片砸落在角色身上（叠放布局下按人物高度下压）
                MascotScale.ScaleX = squash ? 1.16 : 1; MascotScale.ScaleY = squash ? 0.30 : 1;
                BoardTranslate.Y = boardArt ? 330 : Math.Max(70, mascotHeight * 0.55);
                BoardRotate.Angle = -5;
                break;
            default:
                MascotScale.ScaleX = 1; MascotScale.ScaleY = 1;
                BoardTranslate.Y = 0; BoardRotate.Angle = 0;
                break;
        }
    }

    // 解码结果缓存：状态频繁切换时不重复解码，降低内存/CPU 抖动
    private static readonly Dictionary<string, BitmapImage> imageCache = new();

    private (int Width, int Height)? LoadMascotImage(string? path)
    {
        ImageBehavior.SetAnimatedSource(MascotImage, null);
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            if (!imageCache.TryGetValue(path, out var image))
            {
                image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = path.StartsWith("pack:") ? new Uri(path) : new Uri(Path.GetFullPath(path));
                image.EndInit();
                image.Freeze();
                if (imageCache.Count > 24) imageCache.Clear();
                imageCache[path] = image;
            }
            if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                ImageBehavior.SetRepeatBehavior(MascotImage, RepeatBehavior.Forever);
                ImageBehavior.SetAnimatedSource(MascotImage, image);
            }
            else MascotImage.Source = image;
            return (image.PixelWidth, image.PixelHeight);
        }
        catch { UpdatedAt.Text = Loc.T("Msg.CharacterLoadFail"); return null; }
    }

    private void ApplyAnimation()
    {
        mascotStoryboard?.Stop(this);
        MascotTranslate.Y = 0;
        MascotRotate.Angle = 0;
        if (!settings.Animate) return;
        var duration = TimeSpan.FromSeconds(1.8 / Math.Max(0.2, settings.AnimationSpeed));
        var bob = new DoubleAnimation(-4, 4, new Duration(duration)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
        var sway = new DoubleAnimation(-0.7, 0.7, new Duration(duration + TimeSpan.FromMilliseconds(350))) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase() };
        mascotStoryboard = new Storyboard();
        mascotStoryboard.Children.Add(bob);
        mascotStoryboard.Children.Add(sway);
        Storyboard.SetTarget(bob, MascotTranslate);
        Storyboard.SetTargetProperty(bob, new PropertyPath(TranslateTransform.YProperty));
        Storyboard.SetTarget(sway, MascotRotate);
        Storyboard.SetTargetProperty(sway, new PropertyPath(RotateTransform.AngleProperty));
        mascotStoryboard.Begin(this, true);
    }

    private void MoveToBottomRight()
    {
        var area = System.Windows.Forms.Screen.PrimaryScreen!.WorkingArea;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = Math.Max(200, (int)Math.Round(Width * dpi.DpiScaleX));
        var height = Math.Max(300, (int)Math.Round(Height * dpi.DpiScaleY));
        var x = Math.Max(area.Left + 8, area.Right - width - 14);
        var y = Math.Max(area.Top + 8, area.Bottom - height - 8);
        SetWindowPos(hwnd, settings.AlwaysOnTop ? new IntPtr(-1) : new IntPtr(-2), x, y, width, height,
            SwpNoActivate | SwpShowWindow);
    }

    private void ApplyUsage(UsageSnapshot s)
    {
        Dispatcher.Invoke(() =>
        {
            if (testMode) return; // 测试模式期间不让真实数据覆盖预览
            UpdateRefreshPace(s);
            lastSnapshot = s;
            RenderUsage(s);
            ApplyMascot();
        });
    }

    private double? paceLastFive;
    private bool paceActive;
    private int paceStillChecks;
    private bool testMode;
    private bool demoRun;

    /// <summary>测试模式：用假余量驱动看板娘，供预览角色状态。</summary>
    public void ApplyTestUsage(double five, double week, int cards)
    {
        testMode = true;
        refreshTimer.Stop();
        var expiries = string.Join("、", Enumerable.Range(0, cards)
            .Select(i => DateTime.Now.AddDays(10 + i * 4).ToString("MM-dd HH:mm")));
        lastSnapshot = new UsageSnapshot($"{100 - five:0}%", DateTime.Now.AddHours(2).ToString("HH:mm"),
            $"{100 - week:0}%", DateTime.Now.AddDays(3).ToString("MM-dd HH:mm"),
            cards.ToString(), expiries, five, week, cards);
        RenderUsage(lastSnapshot);
        ApplyMascot();
        UpdatedAt.Text = Loc.T("Test.Active");
    }

    public void EndTestUsage()
    {
        if (!testMode) return;
        testMode = false;
        lastSnapshot = null;
        ApplyLocalization();
        ApplyMascot();
        refreshTimer.Start();
        usageWindow?.RefreshUsage();
    }

    /// <summary>
    /// 动态刷新：5 小时进度有变动（正在使用）→ 30 秒一刷；
    /// 连续 6 次（约 3 分钟）没变化 → 回到设置的间隔（默认 5 分钟）。
    /// </summary>
    private void UpdateRefreshPace(UsageSnapshot s)
    {
        var current = s.FiveHourRemaining;
        if (paceLastFive is { } prev && current is { } cur && Math.Abs(cur - prev) >= 0.5)
        {
            paceActive = true;
            paceStillChecks = 0;
        }
        else if (paceActive && ++paceStillChecks >= 6)
        {
            paceActive = false;
        }
        if (current != null) paceLastFive = current;
        refreshTimer.Interval = paceActive
            ? TimeSpan.FromSeconds(30)
            : TimeSpan.FromMinutes(Math.Clamp(settings.RefreshMinutes, 1, 120));
    }

    private static readonly SolidColorBrush AlertBrush = new(System.Windows.Media.Color.FromRgb(0xD8, 0x30, 0x2E));
    private static readonly SolidColorBrush NormalBrush = new(System.Windows.Media.Color.FromRgb(0x5C, 0x34, 0x1E));
    private static readonly SolidColorBrush NormalDimBrush = new(System.Windows.Media.Color.FromRgb(0x8B, 0x62, 0x4A));

    private void RenderUsage(UsageSnapshot s)
    {
        string Show(string v) => string.IsNullOrWhiteSpace(v) ? Loc.T("Usage.NotShown") : v;
        // 解析到余量百分比时直接显示“余量 X%”，避免“0%”歧义
        FiveUsage.Text = s.FiveHourRemaining is { } fr
            ? Loc.F("Usage.FiveLeft", fr.ToString("0"))
            : Loc.F("Usage.Five", Show(s.FiveHourUsage));
        FiveReset.Text = Loc.F("Usage.Reset", Show(s.FiveHourReset));
        WeekUsage.Text = s.WeeklyRemaining is { } wr
            ? Loc.F("Usage.WeekLeft", wr.ToString("0"))
            : Loc.F("Usage.Week", Show(s.WeeklyUsage));
        WeekReset.Text = Loc.F("Usage.Reset", Show(s.WeeklyReset));
        // 低余量红字警示：5 小时 ≤20%，周 <10%
        FiveUsage.Foreground = s.FiveHourRemaining is <= 20 ? AlertBrush : NormalBrush;
        WeekUsage.Foreground = s.WeeklyRemaining is < 10 ? AlertBrush : NormalBrush;
        ApplyProviderRows(s);
        UpdateLoginButton();
        UpdatedAt.Text = Loc.F("Usage.UpdatedAt", DateTime.Now.ToString("HH:mm"));
        MaybeNotifyCardExpiry(s);
    }

    /// <summary>已登录时按钮显示账号，点击可退出；未登录显示「登录」。</summary>
    private void UpdateLoginButton()
    {
        if (lastSnapshot == null)
        {
            LoginButton.Content = Loc.T("Board.LoginBtn");
            return;
        }
        var account = string.IsNullOrWhiteSpace(lastSnapshot.Account) ? Loc.T("Login.LoggedIn") : lastSnapshot.Account;
        if (account.Length > 20) account = account[..19] + "…";
        LoginButton.Content = "👤 " + account;
    }

    /// <summary>重置卡到期前 3 天：高亮到期行，并（每张卡仅一次）弹窗提醒。</summary>
    private void MaybeNotifyCardExpiry(UsageSnapshot s)
    {
        var expiring = s.CardExpiry is { } exp && exp > DateTime.Now && exp - DateTime.Now <= TimeSpan.FromDays(3);
        ExtraNoteText.Foreground = expiring ? AlertBrush : NormalDimBrush;
        ExtraNoteText.FontWeight = expiring ? FontWeights.Bold : FontWeights.SemiBold;
        if (!expiring || testMode || demoRun) return;
        var key = s.CardExpiry!.Value.ToString("yyyy-MM-dd HH:mm");
        if (settings.NotifiedCardExpiries.Contains(key)) return;
        settings.NotifiedCardExpiries.Add(key);
        while (settings.NotifiedCardExpiries.Count > 10) settings.NotifiedCardExpiries.RemoveAt(0);
        settings.Save();
        System.Windows.MessageBox.Show(this, Loc.F("Alert.CardExpiring", s.CardExpiry.Value.ToString("MM-dd HH:mm")),
            Loc.T("Alert.Title"), MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>平台差异化栏位：Codex = 重置卡行（张数圆圈 + 每张到期日）；Claude = Fable 5 额度行；Gemini 无。</summary>
    private void ApplyProviderRows(UsageSnapshot? s)
    {
        var isCodex = settings.Provider == "Codex";
        var isClaude = settings.Provider == "Claude";
        ExtraSeparator.Visibility = isCodex || isClaude ? Visibility.Visible : Visibility.Collapsed;
        ExtraRow.Visibility = isCodex || isClaude ? Visibility.Visible : Visibility.Collapsed;
        CardBadge.Visibility = isCodex ? Visibility.Visible : Visibility.Collapsed;
        ExtraNoteText.Visibility = Visibility.Collapsed;
        if (isCodex)
        {
            ExtraUsage.Text = Loc.T("Usage.CardsLabel");
            CardBadgeText.Text = s?.ResetCardCount?.ToString() ?? "—";
            CardBadge.ToolTip = Loc.T("Badge.Tooltip");
            if (!string.IsNullOrWhiteSpace(s?.ExtraNote))
            {
                ExtraNoteText.Text = Loc.F("Usage.CardExpiry", s!.ExtraNote);
                ExtraNoteText.Visibility = Visibility.Visible;
            }
        }
        else if (isClaude)
        {
            var text = string.IsNullOrWhiteSpace(s?.ExtraUsage) ? Loc.T("Usage.NotShown") : s!.ExtraUsage;
            ExtraUsage.Text = Loc.F("Usage.Fable", text);
            if (!string.IsNullOrWhiteSpace(s?.ExtraNote))
            {
                ExtraNoteText.Text = Loc.F("Usage.Reset", s!.ExtraNote);
                ExtraNoteText.Visibility = Visibility.Visible;
            }
        }
    }

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is System.Windows.Controls.Button || e.OriginalSource is System.Windows.Controls.TextBox) return;
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Root_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        WidgetMenu.IsOpen = true;
        e.Handled = true;
    }

    private void RefreshNow() { UpdatedAt.Text = Loc.T("Usage.Refreshing"); usageWindow?.RefreshUsage(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshNow();

    /// <summary>未登录：拉起浏览器登录并自动轮询；已登录：显示账号并询问是否退出。</summary>
    private void Login_Click(object sender, RoutedEventArgs e)
    {
        if (usageWindow == null) return;
        if (lastSnapshot != null && !testMode)
        {
            var account = string.IsNullOrWhiteSpace(lastSnapshot.Account) ? Loc.T("Login.LoggedIn") : lastSnapshot.Account;
            var msg = Loc.F("Login.LogoutAsk", account);
            if (settings.Provider == "Codex" && !settings.DisableCodexLocalApi)
                msg += "\n\n" + Loc.T("Login.CodexCliNote");
            if (System.Windows.MessageBox.Show(this, msg, Loc.T("Login.LogoutTitle"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                Logout();
            return;
        }
        settings.DisableCodexLocalApi = false;
        settings.Save();
        usageWindow.UseCodexLocalApi = true;
        UpdatedAt.Text = usageWindow.LaunchLogin(settings.Browser)
            ? Loc.T("Login.Opened")
            : Loc.T("Login.NoBrowser");
    }

    /// <summary>退出登录：清除挂件的浏览器登录资料；Codex 另外停止读取本机 CLI 登录态。</summary>
    private void Logout()
    {
        if (settings.Provider == "Codex")
        {
            settings.DisableCodexLocalApi = true;
            if (usageWindow != null) usageWindow.UseCodexLocalApi = false;
        }
        settings.Save();
        try
        {
            var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUmaruWidget");
            foreach (var dir in Directory.GetDirectories(root, "LoginProfile-*").Append(Path.Combine(root, "EdgeLoginProfile")))
                if (Directory.Exists(dir))
                    try { Directory.Delete(dir, true); } catch { /* 浏览器占用时跳过 */ }
        }
        catch { }
        lastSnapshot = null;
        ApplyLocalization();
        ApplyMascot();
        UpdatedAt.Text = Loc.T("Login.LogoutDone");
    }
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();

    private static readonly Random rng = new();

    /// <summary>🎲 在内置角色和已装角色包之间随机换一个（不重复当前角色）。</summary>
    private void RandomCharacter_Click(object sender, RoutedEventArgs e)
    {
        var keys = new List<string> { "Doraemon", "Umaru" };
        keys.AddRange(CharacterPack.LoadAll().Select(p => p.Key));
        keys.Remove(settings.Character);
        if (keys.Count == 0) return;
        settings.Character = keys[rng.Next(keys.Count)];
        settings.Save();
        ApplySettings(false);
    }

    private void ShowSettings()
    {
        ToggleClickThrough(false);
        var original = settings.Clone();
        var dialog = new SettingsWindow(settings.Clone(), preview =>
        {
            // 实时预览：设置窗口每一次调整都立即作用到挂件
            settings = preview;
            ApplySettings(false);
        }, ApplyTestUsage, EndTestUsage) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            settings = dialog.Value.Clone();
            settings.Save();
            if (original.Provider != settings.Provider) { lastSnapshot = null; paceLastFive = null; paceActive = false; }
            ApplySettings(original.Provider != settings.Provider);
        }
        else
        {
            settings = original;
            ApplySettings(false);
        }
    }

    private void ProviderMenu_Click(object sender, RoutedEventArgs e)
    {
        settings.Provider = ((System.Windows.Controls.MenuItem)sender).Tag!.ToString()!;
        settings.Save();
        lastSnapshot = null; // 清掉上一平台的旧数据，避免残留/重叠
        paceLastFive = null; paceActive = false;
        ApplySettings(true);
    }

    private void CharacterMenu_Click(object sender, RoutedEventArgs e)
    {
        var character = ((System.Windows.Controls.MenuItem)sender).Tag!.ToString()!;
        if (character == "Custom")
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "图片或动图|*.png;*.jpg;*.jpeg;*.gif|所有文件|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            settings.CustomCharacterPath = dialog.FileName;
        }
        settings.Character = character;
        settings.Save();
        ApplySettings(false);
    }

    private void ClickThroughMenu_Click(object sender, RoutedEventArgs e)
    {
        settings.ClickThrough = ClickThroughMenu.IsChecked;
        settings.Save();
        ToggleClickThrough(settings.ClickThrough);
    }

    private void ToggleClickThrough(bool enabled)
    {
        settings.ClickThrough = enabled;
        ClickThroughMenu.IsChecked = enabled;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var style = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(hwnd, GwlExStyle, enabled ? style | WsExTransparent : style & ~WsExTransparent);
        if (!enabled && clickThroughApplied) UpdatedAt.Text = Loc.T("Msg.ClickThroughOff");
        clickThroughApplied = enabled;
    }

    private void Hotkeys_Click(object sender, RoutedEventArgs e) => System.Windows.MessageBox.Show(
        Loc.T("Hotkeys.Info"), Loc.T("Hotkeys.Title"), MessageBoxButton.OK, MessageBoxImage.Information);

    private void Close_Click(object sender, RoutedEventArgs e) => ExitApp();
    private void ExitApp()
    {
        refreshTimer.Stop();
        hotkeys?.Dispose();
        usageWindow?.ReallyClose();
        Close();
    }

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter,
        int x, int y, int width, int height, uint flags);
}
