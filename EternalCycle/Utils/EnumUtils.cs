

namespace EternalCycleServer;


public class EnumUtils
{
    public static string GetQuestStageType(int type)
    {
        switch ((EQuestStageType)type)
        {
            case EQuestStageType.Start:
                {
                    return "Started";
                }
            case EQuestStageType.Finish:
                {
                    return "Success";
                }
            case EQuestStageType.Failed:
                {
                    return "Fail";
                }
            default:
                {
                    return "Success";
                }
        }
    }
    public static string GetCompareType(int type)
    {
        switch ((ECompareType)type)
        {
            case ECompareType.Equal:
                {
                    return "==";
                }
            case ECompareType.NotEqual:
                {
                    return "!=";
                }
            case ECompareType.Greater:
                {
                    return ">";
                }
            case ECompareType.GreaterOrEqual:
                {
                    return ">=";
                }
            case ECompareType.Less:
                {
                    return "<";
                }
            case ECompareType.LessOrEqual:
                {
                    return "<=";
                }
            default:
                {
                    return ">="; // Ä¬ÈÏ·µ»Ø >=
                }
        }
    }

}










