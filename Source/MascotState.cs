namespace CodexWidget;

/// <summary>由 5 小时余量决定的表情。</summary>
public enum MascotExpression { Happy, Nervous, Crying, Asleep }

/// <summary>由每周余量决定的姿势。</summary>
public enum MascotPose { Stand, Sit, Crushed }

public static class MascotState
{
    // 小时限制：>50% 开心；20~50% 紧张；0~20% 哭泣；0% 睡着
    public static MascotExpression ExpressionFor(double? remaining) => remaining switch
    {
        null => MascotExpression.Happy,
        <= 0 => MascotExpression.Asleep,
        <= 20 => MascotExpression.Crying,
        <= 50 => MascotExpression.Nervous,
        _ => MascotExpression.Happy
    };

    // 周限制：>30% 站立；0~30% 坐着；0% 被看板压垮
    public static MascotPose PoseFor(double? remaining) => remaining switch
    {
        null => MascotPose.Stand,
        <= 0 => MascotPose.Crushed,
        <= 30 => MascotPose.Sit,
        _ => MascotPose.Stand
    };

    public static string Emoji(MascotExpression e) => e switch
    {
        MascotExpression.Nervous => "😰",
        MascotExpression.Crying => "😭",
        MascotExpression.Asleep => "😴",
        _ => "😊"
    };

    public static string FileName(MascotExpression e) => e switch
    {
        MascotExpression.Nervous => "nervous",
        MascotExpression.Crying => "crying",
        MascotExpression.Asleep => "sleeping",
        _ => "happy"
    };

    public static string FileName(MascotPose p) => p switch
    {
        MascotPose.Sit => "sit",
        MascotPose.Crushed => "crushed",
        _ => "stand"
    };
}
