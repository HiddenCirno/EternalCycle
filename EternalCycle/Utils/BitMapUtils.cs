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
using SPTarkov.Server.Core.Models.Eft.Profile;
using SPTarkov.Server.Core.Models.Enums;

namespace VulcanCore;


public class BitMapUtils
{
    public static List<string> GetBlackListCode(int bitmask)
    {
        var result = new List<string>();

        foreach (EBlackListType type in Enum.GetValues(typeof(EBlackListType)))
        {
            if (type != EBlackListType.None && (bitmask & (int)type) != 0)
            {
                result.Add(type.ToString());
            }
        }

        return result;
    }
    public static List<string> GetGameVersionCode(int bitmask)
    {
        var result = new List<string>();

        foreach (EGameVersionType type in Enum.GetValues(typeof(EGameVersionType)))
        {
            if (type != EGameVersionType.none && (bitmask & (int)type) != 0)
            {
                result.Add(type.ToString());
            }
        }

        return result;
    }
    public static List<string> GetBodyPartCode(int bitmask)
    {
        var result = new List<string>();
        var bitmaskEnum = (EBodyPartType)bitmask;

        foreach (EBodyPartType type in Enum.GetValues(typeof(EBodyPartType)))
        {
            if (type != EBodyPartType.None && bitmaskEnum.HasFlag(type))
            {
                result.Add(type.ToString());
            }
        }

        return result;
    }
    public static List<string> GetLocationCode(int bitmask)
    {
        var result = new List<string>();

        // 按原始顺序映射枚举到字符串
        var map = new Dictionary<ELocationType, string>
    {
        { ELocationType.Custom, "bigmap" },
        { ELocationType.Woods, "Woods" },
        { ELocationType.Factory_Day, "factory4_day" },
        { ELocationType.Factory_Night, "factory4_night" },
        { ELocationType.Laboratory, "laboratory" },
        { ELocationType.Shoreline, "Shoreline" },
        { ELocationType.ReserveBase, "RezervBase" },
        { ELocationType.Interchange, "Interchange" },
        { ELocationType.Lighthouse, "Lighthouse" },
        { ELocationType.TarkovStreets, "TarkovStreets" },
        { ELocationType.GroundZero, "Sandbox" },
        { ELocationType.GroundZero_High, "Sandbox_high" },
        { ELocationType.Labyrinth, "Labyrinth" }
    };

        foreach (ELocationType type in Enum.GetValues(typeof(ELocationType)))
        {
            if (type != ELocationType.None && (bitmask & (int)type) != 0 && map.ContainsKey(type))
            {
                result.Add(map[type]);
            }
        }

        return result;
    }
    public static List<string> GetExitStatusCode(int bitmask)
    {
        var result = new List<string>();

        foreach (EExitStatusType type in Enum.GetValues(typeof(EExitStatusType)))
        {
            if (type != EExitStatusType.None && (bitmask & (int)type) != 0)
            {
                result.Add(type.ToString());
            }
        }

        return result;
    }
    public static HashSet<QuestStatusEnum> GetQuestStatusCode(int bitmask)
    {
        var result = new HashSet<QuestStatusEnum>();

        foreach (EQuestStatusType statusFlag in Enum.GetValues(typeof(EQuestStatusType)))
        {
            if (statusFlag == EQuestStatusType.None)
                continue;

            if (((int)statusFlag & bitmask) != 0)
            {
                // 将位图枚举映射回原来的 QuestStatusEnum
                if (Enum.TryParse<QuestStatusEnum>(statusFlag.ToString(), out var originalStatus))
                {
                    result.Add(originalStatus);
                }
            }
        }

        return result;
    }
}










