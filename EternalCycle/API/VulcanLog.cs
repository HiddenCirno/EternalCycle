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
using Microsoft.Extensions.Logging;

namespace VulcanCore;


public class VulcanLog
{
    public static void Access(string str, ISptLogger<VulcanCore> logger)
    {
        logger.LogWithColor($"[火神之心]: {str}", LogTextColor.Green, LogBackgroundColor.Default);
    }
    public static void Warn(string str, ISptLogger<VulcanCore> logger)
    {
        logger.LogWithColor($"[火神之心]: {str}", LogTextColor.Yellow, LogBackgroundColor.Default);
    }
    public static void Debug(string str, ISptLogger<VulcanCore> logger)
    {
        logger.LogWithColor($"[火神之心]: {str}", LogTextColor.Gray, LogBackgroundColor.Default);
    }
    public static void Error(string str, ISptLogger<VulcanCore> logger)
    {
        logger.LogWithColor($"[火神之心]: {str}", LogTextColor.Red, LogBackgroundColor.Default);
    }
    public static void Log(string str, ISptLogger<VulcanCore> logger)
    {
        logger.LogWithColor($"[火神之心]: {str}", LogTextColor.Cyan, LogBackgroundColor.Default);
    }


}






