namespace CodexWidget;

/// <summary>简单三语文案表：zh / en / ja。</summary>
public static class Loc
{
    public static string Lang { get; set; } = "zh";

    private static int Index => Lang switch { "en" => 1, "ja" => 2, _ => 0 };

    public static string T(string key) => Table.TryGetValue(key, out var v) ? v[Index] : key;
    public static string F(string key, params object[] args) => string.Format(T(key), args);

    private static readonly Dictionary<string, string[]> Table = new()
    {
        // 看板
        ["Board.Title"] = new[] { "{0} 仪表盘", "{0} Dashboard", "{0} ダッシュボード" },
        ["Usage.Five"] = new[] { "⏱ 5小时用量：{0}", "⏱ 5-hour usage: {0}", "⏱ 5時間使用量：{0}" },
        ["Usage.FiveLeft"] = new[] { "⏱ 5小时余量：{0}%", "⏱ 5-hour left: {0}%", "⏱ 5時間残量：{0}%" },
        ["Usage.WeekLeft"] = new[] { "📅 周余量：{0}%", "📅 Weekly left: {0}%", "📅 週間残量：{0}%" },
        ["Usage.Fable"] = new[] { "🤖 Fable 5 额度：{0}", "🤖 Fable 5: {0}", "🤖 Fable 5 クォータ：{0}" },
        ["Usage.Reset"] = new[] { "   重置时间：{0}", "   Resets: {0}", "   リセット時刻：{0}" },
        ["Usage.Week"] = new[] { "📅 周用量：{0}", "📅 Weekly usage: {0}", "📅 週間使用量：{0}" },
        ["Usage.Cards"] = new[] { "🎟 可用重置卡：{0}", "🎟 Reset cards: {0}", "🎟 リセットカード：{0}" },
        ["Usage.Extra"] = new[] { "📊 额外额度：{0}", "📊 Extra quota: {0}", "📊 追加クォータ：{0}" },
        ["Usage.Expiry"] = new[] { "   到期/说明：{0}", "   Expiry/Note: {0}", "   期限/説明：{0}" },
        ["Usage.UpdatedAt"] = new[] { "更新于 {0}", "Updated at {0}", "更新 {0}" },
        ["Usage.Waiting"] = new[] { "等待同步", "Waiting for sync", "同期待ち" },
        ["Usage.Syncing"] = new[] { "同步中…", "Syncing…", "同期中…" },
        ["Usage.Refreshing"] = new[] { "正在刷新…", "Refreshing…", "更新中…" },
        ["Usage.NeedLogin"] = new[] { "请点登录，在官方窗口完成登录", "Click Login and sign in via the official window", "ログインを押して公式ウィンドウでサインインしてください" },
        ["Usage.NotShown"] = new[] { "未同步到", "Not synced", "未同期" },
        ["Badge.Tooltip"] = new[] { "重置卡可用张数", "Reset cards available", "利用可能なリセットカード数" },
        ["Board.LoginBtn"] = new[] { "登录", "Login", "ログイン" },
        ["Usage.CardsLabel"] = new[] { "🎟 可用重置卡", "🎟 Reset cards", "🎟 リセットカード" },
        ["Usage.CardExpiry"] = new[] { "   到期：{0}", "   Expires: {0}", "   期限：{0}" },
        ["Login.Opened"] = new[] { "浏览器已打开，登录后会自动同步", "Browser opened — will sync after sign-in", "ブラウザを開きました。ログイン後自動同期します" },
        ["Login.NoBrowser"] = new[] { "找不到 Chrome / Edge 浏览器", "Chrome / Edge not found", "Chrome / Edge が見つかりません" },
        ["Login.LoggedIn"] = new[] { "已登录", "Signed in", "ログイン済み" },
        ["Login.LogoutTitle"] = new[] { "退出登录", "Sign out", "ログアウト" },
        ["Login.LogoutAsk"] = new[] { "当前账号：{0}\n\n要退出登录吗？", "Current account: {0}\n\nSign out?", "現在のアカウント：{0}\n\nログアウトしますか？" },
        ["Login.CodexCliNote"] = new[]
        {
            "说明：Codex 数据来自本机 Codex CLI 登录。退出后挂件将不再读取它（随时点「登录」恢复）；如需彻底退出 CLI 请在终端执行 codex logout。",
            "Note: Codex data comes from the local Codex CLI login. After sign-out this widget stops reading it (click Login anytime to restore); to fully sign out run codex logout in a terminal.",
            "注：Codex データはローカル Codex CLI のログインから取得。ログアウト後は読み取りを停止します（「ログイン」で復元可）。CLI 自体は codex logout で。"
        },
        ["Login.LogoutDone"] = new[] { "已退出登录", "Signed out", "ログアウトしました" },
        ["Alert.Title"] = new[] { "重置卡到期提醒", "Reset card expiry reminder", "リセットカード期限のお知らせ" },
        ["Alert.CardExpiring"] = new[]
        {
            "有重置卡将在 3 天内到期（{0}），记得及时使用！",
            "A reset card expires within 3 days ({0}) — use it before it's gone!",
            "リセットカードが 3 日以内に期限切れになります（{0}）。お早めに！"
        },

        // 右键菜单
        ["Menu.Settings"] = new[] { "⚙ 设置…", "⚙ Settings…", "⚙ 設定…" },
        ["Menu.Refresh"] = new[] { "↻ 立即刷新", "↻ Refresh now", "↻ 今すぐ更新" },
        ["Menu.Login"] = new[] { "🔐 登录", "🔐 Login", "🔐 ログイン" },
        ["Menu.Provider"] = new[] { "平台", "Provider", "プラットフォーム" },
        ["Menu.Character"] = new[] { "角色", "Character", "キャラクター" },
        ["Menu.CustomImage"] = new[] { "自定义图片 / GIF…", "Custom image / GIF…", "カスタム画像 / GIF…" },
        ["Menu.ImportPack"] = new[] { "导入角色包（文件夹）…", "Import character pack (folder)…", "キャラパックを取り込む…" },
        ["Menu.OpenPackDir"] = new[] { "打开角色文件夹", "Open characters folder", "キャラフォルダを開く" },
        ["Menu.ClickThrough"] = new[] { "鼠标穿透", "Click-through", "クリック透過" },
        ["Menu.Hotkeys"] = new[] { "⌨ 快捷键说明", "⌨ Hotkeys", "⌨ ショートカット" },
        ["Menu.Exit"] = new[] { "退出", "Exit", "終了" },
        ["Menu.Random"] = new[] { "随机换人物", "Random character", "ランダムに交代" },

        // 提示
        ["Msg.CharacterLoadFail"] = new[] { "角色图片无法读取", "Failed to load character image", "キャラ画像を読み込めません" },
        ["Msg.ClickThroughOff"] = new[] { "已解除鼠标穿透", "Click-through released", "クリック透過を解除しました" },
        ["Msg.PackImported"] = new[] { "角色包已导入：{0}", "Character pack imported: {0}", "キャラパックを取り込みました：{0}" },
        ["Msg.PackInvalid"] = new[] { "所选文件夹中没有可用图片（png/gif/jpg）", "No usable images (png/gif/jpg) in the selected folder", "選択フォルダに使える画像（png/gif/jpg）がありません" },
        ["Hotkeys.Title"] = new[] { "防锁死快捷键", "Anti-lock hotkeys", "ロック防止ショートカット" },
        ["Hotkeys.Info"] = new[]
        {
            "Ctrl+Alt+L：紧急解除鼠标穿透\nCtrl+Alt+Shift+F12：备用强制解锁\nCtrl+Alt+U：显示 / 隐藏挂件\nCtrl+Alt+S：打开设置\nCtrl+Alt+R：立即刷新\nCtrl+Alt+Q：退出\n\n若系统异常，Ctrl+Shift+Esc 可打开任务管理器结束 CodexWidget。",
            "Ctrl+Alt+L: emergency click-through release\nCtrl+Alt+Shift+F12: backup force unlock\nCtrl+Alt+U: show / hide widget\nCtrl+Alt+S: open settings\nCtrl+Alt+R: refresh now\nCtrl+Alt+Q: exit\n\nIf stuck, press Ctrl+Shift+Esc to open Task Manager and end CodexWidget.",
            "Ctrl+Alt+L：クリック透過を緊急解除\nCtrl+Alt+Shift+F12：予備の強制解除\nCtrl+Alt+U：表示 / 非表示\nCtrl+Alt+S：設定を開く\nCtrl+Alt+R：今すぐ更新\nCtrl+Alt+Q：終了\n\n異常時は Ctrl+Shift+Esc でタスクマネージャーから CodexWidget を終了してください。"
        },

        // 设置窗口
        ["Set.Title"] = new[] { "桌面额度挂件设置", "Widget Settings", "ウィジェット設定" },
        ["Set.SecGeneral"] = new[] { "常规", "GENERAL", "一般" },
        ["Set.SecLook"] = new[] { "外观", "APPEARANCE", "外観" },
        ["Set.SecBehavior"] = new[] { "行为", "BEHAVIOR", "動作" },
        ["Set.Language"] = new[] { "语言 / Language / 言語", "语言 / Language / 言語", "语言 / Language / 言語" },
        ["Set.Provider"] = new[] { "平台", "Provider", "プラットフォーム" },
        ["Set.Browser"] = new[] { "登录用浏览器", "Login browser", "ログイン用ブラウザ" },
        ["Set.Character"] = new[] { "角色", "Character", "キャラクター" },
        ["Set.ImportImage"] = new[] { "导入 PNG / GIF…", "Import PNG / GIF…", "PNG / GIF を取り込む…" },
        ["Set.ImportPack"] = new[] { "导入角色包…", "Import pack…", "パックを取り込む…" },
        ["Set.OpenPackDir"] = new[] { "打开角色文件夹", "Open folder", "フォルダを開く" },
        ["Set.Scale"] = new[] { "大小", "Size", "サイズ" },
        ["Set.Opacity"] = new[] { "透明度", "Opacity", "不透明度" },
        ["Set.Animate"] = new[] { "启用动态效果（呼吸、浮动；导入 GIF 时播放 GIF）", "Enable animation (breathing / floating; GIFs play)", "アニメ効果（呼吸・浮遊、GIF は再生）" },
        ["Set.Speed"] = new[] { "动画速度", "Animation speed", "アニメ速度" },
        ["Set.Topmost"] = new[] { "永远置顶", "Always on top", "常に最前面" },
        ["Set.ClickThrough"] = new[] { "鼠标穿透（Ctrl+Alt+L 可紧急解除）", "Click-through (Ctrl+Alt+L to release)", "クリック透過（Ctrl+Alt+L で解除）" },
        ["Set.Refresh"] = new[] { "自动刷新（分钟）", "Auto refresh (min)", "自動更新（分）" },
        ["Set.TestMode"] = new[] { "🧪 测试模式…", "🧪 Test mode…", "🧪 テストモード…" },
        ["Test.Title"] = new[] { "状态测试模式", "State test mode", "状態テストモード" },
        ["Test.Note"] = new[]
        {
            "拖动滑块预览角色各状态（换角色后也可用来检查效果）；关闭窗口即恢复真实数据。",
            "Drag sliders to preview mascot states (handy after switching characters); closing restores real data.",
            "スライダーで各状態をプレビュー（キャラ変更後の確認にも）。閉じると実データに戻ります。"
        },
        ["Test.Five"] = new[] { "5小时余量", "5-hour left", "5時間残量" },
        ["Test.Week"] = new[] { "周余量", "Weekly left", "週間残量" },
        ["Test.Cards"] = new[] { "重置卡数", "Reset cards", "リセットカード" },
        ["Test.Close"] = new[] { "关闭并恢复真实数据", "Close & restore real data", "閉じて実データに戻す" },
        ["Test.Active"] = new[] { "🧪 测试模式中（假数据）", "🧪 Test mode (fake data)", "🧪 テストモード中（ダミー）" },
        ["Set.Cancel"] = new[] { "取消", "Cancel", "キャンセル" },
        ["Set.Save"] = new[] { "保存", "Save", "保存" },

        // 登录窗口
        ["Login.WindowTitle"] = new[] { "AI 平台登录与用量同步", "AI Login & Usage Sync", "AI ログインと使用量同期" },
        ["Login.Title"] = new[] { "{0} 登录与同步", "{0} Login & Sync", "{0} ログインと同期" },
        ["Login.HintCodex"] = new[]
        {
            "本机装有 Codex CLI 并已登录时会自动同步（含重置卡），无需 Edge。否则请用下方按钮登录 ChatGPT 并打开 Usage 页面。",
            "If Codex CLI is signed in on this PC, sync (incl. reset cards) is automatic — no Edge needed. Otherwise use the buttons below.",
            "この PC で Codex CLI にログイン済みなら自動同期（リセットカード含む）。未ログインなら下のボタンで ChatGPT にログインしてください。"
        },
        ["Login.HintGemini"] = new[]
        {
            "登录 Gemini 后，在左下角打开：设置 → Usage limits（用量限制），保持该页打开再同步。",
            "After signing in to Gemini, open Settings → Usage limits (bottom-left), keep that page open, then sync.",
            "Gemini にログイン後、左下の設定 → Usage limits を開いたまま同期してください。"
        },
        ["Login.HintClaude"] = new[]
        {
            "登录 Claude 后进入 Settings → Usage，保持用量页打开再同步。",
            "After signing in to Claude, open Settings → Usage, keep it open, then sync.",
            "Claude にログイン後、Settings → Usage を開いたまま同期してください。"
        },
        ["Login.ModeTitle"] = new[] { "真正登录模式", "Real login mode", "実ログインモード" },
        ["Login.ModeBody"] = new[]
        {
            "点击下面按钮后，会打开一个独立的 Microsoft Edge 窗口。请在那个窗口完成网页登录并进入用量页面。登录状态只保存在本机专用浏览器目录，不与插件共享密码。",
            "The button opens a separate Microsoft Edge window. Sign in there and open the usage page. Login state stays in a local profile; passwords are never shared with this widget.",
            "ボタンを押すと別の Microsoft Edge ウィンドウが開きます。そこでログインして使用量ページを開いてください。ログイン状態はローカル専用プロファイルにのみ保存されます。"
        },
        ["Login.NotConnected"] = new[] { "尚未连接官方页面", "Not connected to the official page", "公式ページ未接続" },
        ["Login.EdgeOpened"] = new[] { "Edge 已打开：请完成登录并进入用量页", "Edge opened: sign in and open the usage page", "Edge を開きました：ログインして使用量ページへ" },
        ["Login.BtnOpen"] = new[] { "1. 打开 Edge 登录/用量页", "1. Open Edge login / usage page", "1. Edge でログイン / 用量ページ" },
        ["Login.BtnSync"] = new[] { "2. 登录完成，立即同步", "2. Signed in — sync now", "2. ログイン完了、今すぐ同期" },
        ["Login.BtnHide"] = new[] { "隐藏", "Hide", "隠す" },
        ["Login.NotLoggedIn"] = new[] { "尚未登录，或当前不是用量页面", "Not signed in, or not on the usage page", "未ログイン、または使用量ページではありません" },
        ["Login.SyncOk"] = new[] { "同步成功：{0}", "Synced: {0}", "同期成功：{0}" },
        ["Login.SyncFail"] = new[] { "尚未连接：{0}", "Not connected: {0}", "未接続：{0}" },
        ["Login.NoEdge"] = new[] { "找不到 Microsoft Edge。请先安装或修复 Edge。", "Microsoft Edge not found. Please install or repair Edge.", "Microsoft Edge が見つかりません。" },
        ["Login.NoEdgeTitle"] = new[] { "无法登录", "Cannot log in", "ログイン不可" },
    };
}
