using System.Windows;

namespace CodexWidget;

/// <summary>
/// 状态测试模式：用假余量实时驱动看板娘，方便创作者预览角色各状态；
/// 窗口关闭时恢复真实数据。
/// </summary>
public partial class TestWindow : Window
{
    private readonly Action<double, double, int> apply;
    private readonly Action end;
    private bool ready;

    public TestWindow(Action<double, double, int> applyTest, Action endTest)
    {
        InitializeComponent();
        apply = applyTest;
        end = endTest;
        Title = Loc.T("Test.Title");
        TitleText.Text = Loc.T("Test.Title");
        NoteText.Text = Loc.T("Test.Note");
        FiveLabel.Text = Loc.T("Test.Five");
        WeekLabel.Text = Loc.T("Test.Week");
        CardsLabel.Text = Loc.T("Test.Cards");
        CloseButton.Content = Loc.T("Test.Close");
        Closed += (_, _) => end();
        ready = true;
        Push();
    }

    private void Push()
    {
        if (!ready) return;
        FiveValue.Text = $"{FiveSlider.Value:0}%";
        WeekValue.Text = $"{WeekSlider.Value:0}%";
        CardsValue.Text = $"{CardsSlider.Value:0}";
        apply(FiveSlider.Value, WeekSlider.Value, (int)CardsSlider.Value);
    }

    private void Changed(object sender, RoutedPropertyChangedEventArgs<double> e) => Push();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
