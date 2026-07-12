using System.Text.Json;
using System.IO;

namespace CodexWidget;

public sealed class WidgetSettings
{
    public string Provider { get; set; } = "Codex";
    public string Character { get; set; } = "Umaru";
    public string? CustomCharacterPath { get; set; }
    public string Language { get; set; } = "zh";
    public string Browser { get; set; } = "Chrome";
    public double Scale { get; set; } = 0.9;
    public double Opacity { get; set; } = 0.96;
    public bool Animate { get; set; } = true;
    public double AnimationSpeed { get; set; } = 1.0;
    public bool AlwaysOnTop { get; set; } = true;
    public bool ClickThrough { get; set; }
    public int RefreshMinutes { get; set; } = 5;

    public WidgetSettings Clone() => (WidgetSettings)MemberwiseClone();

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexUmaruWidget", "settings.json");

    public static WidgetSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<WidgetSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { return new(); }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
