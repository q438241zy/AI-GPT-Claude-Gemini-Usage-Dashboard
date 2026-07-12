using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using CodexWidget;

namespace CodexWidgetCross;

public partial class SettingsWindow : Window
{
    public WidgetSettings Value { get; private set; }

    private readonly Action<WidgetSettings>? preview;
    private bool ready;

    public SettingsWindow(WidgetSettings current, Action<WidgetSettings>? livePreview = null)
    {
        InitializeComponent();
        Value = current;
        preview = livePreview;

        FillCombo(LanguageBox, new[] { ("中文", "zh"), ("English", "en"), ("日本語", "ja") }, Value.Language);
        FillCombo(ProviderBox, new[] { ("Codex", "Codex"), ("Gemini", "Gemini"), ("Claude", "Claude") }, Value.Provider);
        FillCombo(BrowserBox, new[] { ("Chrome", "Chrome"), ("Edge", "Edge") }, Value.Browser);
        RefreshCharacterItems(Value.Character);
        ScaleSlider.Value = Value.Scale;
        OpacitySlider.Value = Value.Opacity;
        RefreshBox.Text = Value.RefreshMinutes.ToString();

        ApplyLanguage();
        ready = true;
    }

    private static void FillCombo(ComboBox box, (string Text, string Tag)[] items, string selected)
    {
        box.Items.Clear();
        foreach (var (text, tag) in items)
            box.Items.Add(new ComboBoxItem { Content = text, Tag = tag });
        SelectByTag(box, selected);
    }

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

    private static void SelectByTag(ComboBox box, string value)
    {
        foreach (var item in box.Items.OfType<ComboBoxItem>())
            if (string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            { box.SelectedItem = item; return; }
        if (box.SelectedIndex < 0 && box.ItemCount > 0) box.SelectedIndex = 0;
    }

    private static string TagOf(ComboBox box) =>
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
        ImportPackButton.Content = Loc.T("Set.ImportPack");
        ScaleLabelTitle.Text = Loc.T("Set.Scale");
        OpacityLabelTitle.Text = Loc.T("Set.Opacity");
        RefreshLabel.Text = Loc.T("Set.Refresh");
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
        Value.RefreshMinutes = int.TryParse(RefreshBox.Text, out var minutes) ? Math.Clamp(minutes, 1, 120) : 5;
        ApplyLanguage();
        preview?.Invoke(Value.Clone());
    }

    private void Control_Changed(object? sender, SelectionChangedEventArgs e) => PushPreview();
    private void Control_TextChanged(object? sender, TextChangedEventArgs e) => PushPreview();

    private void Scale_Changed(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (ScaleLabel != null) ScaleLabel.Text = $"{e.NewValue:P0}";
        PushPreview();
    }

    private void Opacity_Changed(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (OpacityLabel != null) OpacityLabel.Text = $"{e.NewValue:P0}";
        PushPreview();
    }

    private async void ImportPack_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path == null) return;
        var pack = CharacterPack.Import(path);
        if (pack == null) return;
        RefreshCharacterItems(pack.Key);
        PushPreview();
    }

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        PushPreview();
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
