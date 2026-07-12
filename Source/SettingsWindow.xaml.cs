using System.Windows;
using System.Windows.Controls;

namespace CodexWidget;

public partial class SettingsWindow : Window
{
    public WidgetSettings Value { get; private set; }

    private readonly Action<WidgetSettings>? preview;
    private readonly Action<double, double, int>? testApply;
    private readonly Action? testEnd;
    private TestWindow? testWindow;
    private bool ready;

    public SettingsWindow(WidgetSettings current, Action<WidgetSettings>? livePreview = null,
        Action<double, double, int>? applyTest = null, Action? endTest = null)
    {
        InitializeComponent();
        Value = current;
        preview = livePreview;
        testApply = applyTest;
        testEnd = endTest;
        Closed += (_, _) => testWindow?.Close();

        RefreshCharacterItems(Value.Character);
        SelectByTag(LanguageBox, Value.Language);
        SelectByTag(ProviderBox, Value.Provider);
        SelectByTag(BrowserBox, Value.Browser);
        ScaleSlider.Value = Value.Scale;
        OpacitySlider.Value = Value.Opacity;
        SpeedSlider.Value = Value.AnimationSpeed;
        AnimateCheck.IsChecked = Value.Animate;
        TopmostCheck.IsChecked = Value.AlwaysOnTop;
        ClickThroughCheck.IsChecked = Value.ClickThrough;
        RefreshBox.Text = Value.RefreshMinutes.ToString();

        ApplyLanguage();
        ready = true;
    }

    /// <summary>角色下拉：内置（中文名）+ 已装角色包 + 自定义图片。</summary>
    private void RefreshCharacterItems(string selectedKey)
    {
        CharacterBox.Items.Clear();
        CharacterBox.Items.Add(new ComboBoxItem { Content = "多啦A梦", Tag = "Doraemon" });
        CharacterBox.Items.Add(new ComboBoxItem { Content = "小埋", Tag = "Umaru" });
        foreach (var pack in CharacterPack.LoadAll())
            CharacterBox.Items.Add(new ComboBoxItem { Content = pack.DisplayName, Tag = pack.Key });
        CharacterBox.Items.Add(new ComboBoxItem { Content = Loc.T("Menu.CustomImage"), Tag = "Custom" });
        SelectByTag(CharacterBox, selectedKey);
    }

    private static void SelectByTag(System.Windows.Controls.ComboBox box, string value)
    {
        foreach (ComboBoxItem item in box.Items)
            if (string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            { box.SelectedItem = item; return; }
        if (box.SelectedIndex < 0 && box.Items.Count > 0) box.SelectedIndex = 0;
    }

    private static string TagOf(System.Windows.Controls.ComboBox box) =>
        (box.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

    private void ApplyLanguage()
    {
        Loc.Lang = TagOf(LanguageBox) is { Length: > 0 } lang ? lang : Value.Language;
        Title = Loc.T("Set.Title");
        TitleText.Text = Loc.T("Set.Title");
        LanguageLabel.Text = Loc.T("Set.Language");
        ProviderLabel.Text = Loc.T("Set.Provider");
        BrowserLabel.Text = Loc.T("Set.Browser");
        CharacterLabel.Text = Loc.T("Set.Character");
        ImportImageButton.Content = Loc.T("Set.ImportImage");
        ImportPackButton.Content = Loc.T("Set.ImportPack");
        OpenPackDirButton.Content = Loc.T("Set.OpenPackDir");
        ScaleLabelTitle.Text = Loc.T("Set.Scale");
        OpacityLabelTitle.Text = Loc.T("Set.Opacity");
        AnimateCheck.Content = Loc.T("Set.Animate");
        SpeedLabel.Text = Loc.T("Set.Speed");
        TopmostCheck.Content = Loc.T("Set.Topmost");
        ClickThroughCheck.Content = Loc.T("Set.ClickThrough");
        RefreshLabel.Text = Loc.T("Set.Refresh");
        TestModeButton.Content = Loc.T("Set.TestMode");
        CancelButton.Content = Loc.T("Set.Cancel");
        SaveButton.Content = Loc.T("Set.Save");
        var custom = CharacterBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string?)i.Tag == "Custom");
        if (custom != null) custom.Content = Loc.T("Menu.CustomImage");
    }

    /// <summary>把控件当前值写回 Value，并实时预览到挂件。</summary>
    private void PushPreview()
    {
        if (!ready) return;
        Value.Language = TagOf(LanguageBox);
        Value.Provider = TagOf(ProviderBox);
        Value.Browser = TagOf(BrowserBox);
        Value.Character = TagOf(CharacterBox);
        Value.Scale = ScaleSlider.Value;
        Value.Opacity = OpacitySlider.Value;
        Value.AnimationSpeed = SpeedSlider.Value;
        Value.Animate = AnimateCheck.IsChecked == true;
        Value.AlwaysOnTop = TopmostCheck.IsChecked == true;
        Value.ClickThrough = ClickThroughCheck.IsChecked == true;
        Value.RefreshMinutes = int.TryParse(RefreshBox.Text, out var minutes) ? Math.Clamp(minutes, 1, 120) : 5;
        ApplyLanguage();
        preview?.Invoke(Value.Clone());
    }

    private void Control_Changed(object sender, RoutedEventArgs e) => PushPreview();
    private void Control_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => PushPreview();
    private void Control_TextChanged(object sender, TextChangedEventArgs e) => PushPreview();

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleLabel != null) ScaleLabel.Text = $"{e.NewValue:P0}";
        PushPreview();
    }

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OpacityLabel != null) OpacityLabel.Text = $"{e.NewValue:P0}";
        PushPreview();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "图片或动图|*.png;*.jpg;*.jpeg;*.gif|所有文件|*.*" };
        if (dialog.ShowDialog(this) == true)
        {
            Value.CustomCharacterPath = dialog.FileName;
            SelectByTag(CharacterBox, "Custom");
            PushPreview();
        }
    }

    private void ImportPack_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog();
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
        var pack = CharacterPack.Import(dialog.SelectedPath);
        if (pack == null)
        {
            System.Windows.MessageBox.Show(Loc.T("Msg.PackInvalid"), Loc.T("Set.Title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        RefreshCharacterItems(pack.Key);
        PushPreview();
    }

    private void OpenPackDir_Click(object sender, RoutedEventArgs e) => MainWindow.OpenCharacterFolder();

    private void TestMode_Click(object sender, RoutedEventArgs e)
    {
        if (testApply == null || testEnd == null) return;
        if (testWindow is { IsLoaded: true }) { testWindow.Activate(); return; }
        testWindow = new TestWindow(testApply, testEnd) { Owner = this };
        testWindow.Show();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        PushPreview();
        DialogResult = true;
    }
}
