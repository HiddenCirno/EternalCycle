using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System.Text.Json;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services.Mod;
using System.Reflection;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Logger;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Utils.Json;
using Microsoft.Extensions.Logging;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Spt.Config;

namespace VulcanCore;


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










