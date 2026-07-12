using System.IO;
using System.Text.Json;

namespace CodexWidget;

/// <summary>
/// 创作者角色包：%LOCALAPPDATA%\CodexUmaruWidget\Characters\ 下的一个文件夹。
/// 可选 character.json（{"name":"显示名","author":"作者"}），
/// 新版图片按 姿势/表情.png > 姿势/default.png 取用；同时兼容旧版平铺命名。
/// 姿势：stand / sit / crushed；表情：happy / nervous / crying / sleeping。
/// </summary>
public sealed class CharacterPack
{
    public string FolderName { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Dir { get; init; } = "";
    public string Key => "Pack:" + FolderName;

    private static readonly string[] Extensions = { ".png", ".gif", ".jpg", ".jpeg" };

    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CodexUmaruWidget", "Characters");

    public static List<CharacterPack> LoadAll()
    {
        var list = new List<CharacterPack>();
        try
        {
            if (!Directory.Exists(Root)) return list;
            foreach (var dir in Directory.GetDirectories(Root))
                if (Load(dir) is { } pack) list.Add(pack);
        }
        catch { }
        return list;
    }

    public static CharacterPack? Load(string dir)
    {
        if (!HasImage(dir)) return null;
        var name = Path.GetFileName(dir);
        var manifest = Path.Combine(dir, "character.json");
        if (File.Exists(manifest))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
                if (doc.RootElement.TryGetProperty("name", out var n) && !string.IsNullOrWhiteSpace(n.GetString()))
                    name = n.GetString()!;
            }
            catch { }
        }
        return new CharacterPack { FolderName = Path.GetFileName(dir), DisplayName = name, Dir = dir };
    }

    /// <summary>把创作者的文件夹复制进角色目录并返回角色包；无可用图片时返回 null。</summary>
    public static CharacterPack? Import(string sourceDir)
    {
        if (!HasImage(sourceDir)) return null;
        var target = Path.Combine(Root, Path.GetFileName(sourceDir.TrimEnd('\\', '/')));
        CopyDirectory(sourceDir, target);
        return Load(target);
    }

    public (string? path, bool poseFromImage, bool expressionFromImage) Resolve(MascotPose pose, MascotExpression expression)
    {
        var p = MascotState.FileName(pose);
        var x = MascotState.FileName(expression);
        if (Find(Path.Combine(p, x)) is { } nestedCombo) return (nestedCombo, true, true);
        if (Find(Path.Combine(p, "default")) is { } nestedPose) return (nestedPose, true, false);
        if (Find($"{p}-{x}") is { } combo) return (combo, true, true);
        if (Find(p) is { } poseOnly) return (poseOnly, true, false);
        if (Find(x) is { } exprOnly) return (exprOnly, false, true);
        return (Find("default") ?? FirstImage(), false, false);
    }

    private string? Find(string baseName) =>
        Extensions.Select(e => Path.Combine(Dir, baseName + e)).FirstOrDefault(File.Exists);

    private string? FirstImage() =>
        Directory.EnumerateFiles(Dir, "*", SearchOption.AllDirectories).FirstOrDefault(IsImage);

    private static bool HasImage(string dir) =>
        Directory.Exists(dir) && Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Any(IsImage);

    private static void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
        foreach (var child in Directory.GetDirectories(sourceDir))
            CopyDirectory(child, Path.Combine(targetDir, Path.GetFileName(child)));
    }

    private static bool IsImage(string file) =>
        Extensions.Contains(Path.GetExtension(file).ToLowerInvariant());
}
