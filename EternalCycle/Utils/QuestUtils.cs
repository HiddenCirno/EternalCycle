using HarmonyLib;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Server;
using SPTarkov.Server.Core.Models.Spt.Templates;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Json;
using SPTarkov.Server.Core.Utils.Logger;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Path = System.IO.Path;

namespace VulcanCore;


public class QuestUtils
{
    public static Quest GetQuest(string questid, DatabaseService databaseService)
    {
        return databaseService.GetQuests().FirstOrDefault(x => x.Value.Id == (MongoId)questid).Value;
    }
    public static void InitQuestData(Dictionary<string, CustomQuest> questData, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        foreach (var customquest in questData)
        {
            InitQuest(customquest.Value, databaseService, cloner, logger);
        }
    }
    public static void InitQuestData(string folderpath, DatabaseService databaseService, ModHelper modHelper, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var customquest = modHelper.GetJsonDataFromFile<CustomQuest>(folderpath, fileName);
                InitQuest(customquest, databaseService, cloner, logger);
            }
        }
    }
    public static void InitQuest(CustomQuest customQuest, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        var questid = customQuest.QuestId;
        //TODO
        Quest questPattern = cloner.Clone(GetQuest("5967733e86f774602332fc84", databaseService));
        questPattern.Conditions.AvailableForStart.Clear();
        questPattern.Conditions.AvailableForFinish.Clear();
        questPattern.Conditions.Fail.Clear();
        questPattern.Rewards = new Dictionary<string, List<Reward>>
        {
            ["Started"] = new List<Reward>(),
            ["Success"] = new List<Reward>(),
            ["Fail"] = new List<Reward>(),
        };
        questPattern.Type = (QuestTypeEnum)customQuest.QuestType;
        questPattern.AcceptPlayerMessage = $"{questid} acceptPlayerMessage";
        questPattern.ChangeQuestMessageText = $"{questid} changeQuestMessageText";
        questPattern.CompletePlayerMessage = $"{questid} completePlayerMessage";
        questPattern.Description = $"{questid} description";
        questPattern.FailMessageText = $"{questid} failMessageText";
        questPattern.Name = $"{questid} name";
        questPattern.Note = $"{questid} note";
        questPattern.StartedMessageText = $"{questid} startedMessageText";
        questPattern.SuccessMessageText = $"{questid} successMessageText";
        questPattern.Id = questid;
        questPattern.Image = customQuest.QuestImagePath;
        questPattern.TraderId = customQuest.QuestTraderId;
        questPattern.TemplateId = questid;
        questPattern.Location = customQuest.Location;
        questPattern.Restartable = customQuest.IsRestartableQuest;
        InitQuestConditions(questPattern.Conditions.AvailableForFinish, customQuest.QuestConditions.QuestFinishData, databaseService, cloner, logger);
        InitQuestConditions(questPattern.Conditions.Fail, customQuest.QuestConditions.QuestFailedData, databaseService, cloner, logger);
        databaseService.GetQuests().TryAdd(questid, questPattern);
        //为了完成原版兼容, 奖励定义有任务ID, 必须在任务初始化后添加
        //应该可以重载
        InitQuestRewards(customQuest.QuestRewards, databaseService, cloner, logger);

    }
    public static void InitQuestConditions(List<QuestCondition> conditions, List<CustomQuestData> customquestdata, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        var zhCNLang = databaseService.GetLocales().Global["ch"];
        foreach (CustomQuestData data in customquestdata)
        {
            switch (data)
            {
                case FindItemData finditemdata:
                    {
                        InitFindItemDataConditions(conditions, finditemdata, databaseService, cloner);
                    }
                    break;
                case FindItemGroupData finditemgroupdata:
                    {
                        InitFindItemGroupDataConditions(conditions, finditemgroupdata, databaseService, cloner);
                    }
                    break;
                case HandoverItemData handitemdata:
                    {
                        InitHandoverItemDataConditions(conditions, handitemdata, databaseService, cloner);
                    }
                    break;
                case HandoverItemGroupData handitemgroupdata:
                    {
                        InitHandoverItemGroupDataConditions(conditions, handitemgroupdata, databaseService, cloner);
                    }
                    break;
                case KillTargetData killtargetdata:
                    {
                        InitKillTargetDataConditions(conditions, killtargetdata, databaseService, cloner);
                    }
                    break;
                case ReachLevelData reachleveldata:
                    {
                        InitReachLevelDataConditions(conditions, reachleveldata, databaseService, cloner);
                    }
                    break;
                case ReachPrestigeLevelData reachprestigeleveldata:
                    {
                        InitReachPrestigeLevelDataConditions(conditions, reachprestigeleveldata, databaseService, cloner);
                    }
                    break;
                case VisitPlaceData visitplacedata:
                    {
                        InitVisitPlaceDataConditions(conditions, visitplacedata, databaseService, cloner);
                    }
                    break;
                case PlaceItemData placeitemdata:
                    {
                        InitPlaceItemDataConditions(conditions, placeitemdata, databaseService, cloner);
                    }
                    break;
                case ExitLocationData exitlocationdata:
                    {
                        InitExitLocationDataConditions(conditions, exitlocationdata, databaseService, cloner);
                    }
                    break;
                case ReachTraderStandingData reachtraderstandingdata:
                    {
                        InitReachTraderStandingDataConditions(conditions, reachtraderstandingdata, databaseService, cloner);
                    }
                    break;
                case ReachTraderTrustLevelData reachtradertrustleveldata:
                    {
                        InitReachTraderTrustLevelDataConditions(conditions, reachtradertrustleveldata, databaseService, cloner);
                    }
                    break;
                case ReachSkillLevelData reachskillleveldata:
                    {
                        InitReachSkillLevelDataConditions(conditions, reachskillleveldata, databaseService, cloner);
                    }
                    break;
                case CompleteQuestData completequestdata:
                    {
                        InitCompleteQuestDataConditions(conditions, completequestdata, databaseService, cloner);
                    }
                    break;
                case CustomizationBlockData customizationblockdata:
                    {
                        InitCustomizationBlockDataConditions(conditions, customizationblockdata, databaseService, cloner);
                    }
                    break;
                default:
                    {
                        VulcanLog.Warn($"发现未处理的任务属性({data.Id})! ", logger);
                    }
                    break;
            }
            if (data.Locale != null)
            {
                zhCNLang.AddTransformer(lang =>
                {
                    lang[$"{data.Id}"] = data.Locale;
                    return lang;
                });
            }
        }
    }
    public static void InitFindItemDataConditions(List<QuestCondition> conditions, FindItemData findItemData, DatabaseService databaseService, ICloner cloner)
    {
        var zhCNLang = databaseService.GetLocales().Global["ch"];
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForFinish)
            .FirstOrDefault(c => c.ConditionType == "FindItem");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = findItemData.Id;
            copycondition.OnlyFoundInRaid = findItemData.FindInRaid;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.Target = new ListOrT<string>(new List<string>(), null);
            copycondition.Target.List.Add(findItemData.ItemId);
            copycondition.Value = (double)findItemData.Count;
            conditions.Add(copycondition);
            if (findItemData.AutoLocale != null && findItemData.AutoLocale == true)
            {
                zhCNLang.AddTransformer(lang =>
                {
                    lang[$"{findItemData.Id}"] = $"在战局中找到{lang[$"{findItemData.ItemId} Name"]}";
                    return lang;
                });
            }
        }
    }
    public static void InitFindItemGroupDataConditions(List<QuestCondition> conditions, FindItemGroupData findItemData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForFinish)
            .FirstOrDefault(c => c.ConditionType == "HandoverItem");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = findItemData.Id;
            copycondition.OnlyFoundInRaid = findItemData.FindInRaid;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.Target = new ListOrT<string>(new List<string>(), null);
            foreach (string target in findItemData.Items)
            {
                copycondition.Target.List.Add(VulcanUtil.ConvertHashID(target));
            }
            copycondition.Value = (double)findItemData.Count;
            conditions.Add(copycondition);
        }
    }
    public static void InitHandoverItemDataConditions(List<QuestCondition> conditions, HandoverItemData handItemData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForFinish)
            .FirstOrDefault(c => c.ConditionType == "HandoverItem");
        if (condition != null)
        {
            var zhCNLang = databaseService.GetLocales().Global["ch"];
            var copycondition = cloner.Clone(condition);
            copycondition.Id = handItemData.Id;
            copycondition.OnlyFoundInRaid = handItemData.FindInRaid;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.Target = new ListOrT<string>(new List<string>(), null);
            copycondition.Target.List.Add(handItemData.ItemId);
            copycondition.Value = (double)handItemData.Count;
            conditions.Add(copycondition);
            if (handItemData.AutoLocale != null && handItemData.AutoLocale == true)
            {
                zhCNLang.AddTransformer(lang =>
                {
                    lang[$"{handItemData.Id}"] = $"上交在战局中找到的{lang[$"{handItemData.ItemId} Name"]}";
                    return lang;
                });
            }
        }
    }
    public static void InitHandoverItemGroupDataConditions(List<QuestCondition> conditions, HandoverItemGroupData handItemData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForFinish)
            .FirstOrDefault(c => c.ConditionType == "HandoverItem");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = handItemData.Id;
            copycondition.OnlyFoundInRaid = handItemData.FindInRaid;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.Target = new ListOrT<string>(new List<string>(), null);
            foreach (string target in handItemData.Items)
            {
                copycondition.Target.List.Add(VulcanUtil.ConvertHashID(target));
            }
            copycondition.Value = (double)handItemData.Count;
            conditions.Add(copycondition);
        }
    }
    public static void InitKillTargetDataConditions(List<QuestCondition> conditions, KillTargetData killTargetData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForFinish)
            .FirstOrDefault(c => c.ConditionType == "CounterCreator" && c.Type == "Elimination");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = killTargetData.Id;
            copycondition.Counter.Id = VulcanUtil.ConvertHashID($"{killTargetData.Id}_Counter");
            copycondition.Counter.Conditions.Clear();
            copycondition.OneSessionOnly = killTargetData.CompleteInOneRaid;
            copycondition.Value = (double)killTargetData.Count;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            var killtargets = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)   // 所有 AvailableForFinish 条件
                .Where(c => c.ConditionType == "CounterCreator")         // 过滤 CounterCreator
                .SelectMany(c => c.Counter?.Conditions).FirstOrDefault(c => c.ConditionType == "Kills"); // 获取 Counter 的 Conditions
            var locationtargets = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)   // 所有 AvailableForFinish 条件
                .Where(c => c.ConditionType == "CounterCreator")         // 过滤 CounterCreator
                .SelectMany(c => c.Counter?.Conditions).FirstOrDefault(c => c.ConditionType == "Location"); // 获取 Counter 的 Conditions

            var equiptargets = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)   // 所有 AvailableForFinish 条件
                .Where(c => c.ConditionType == "CounterCreator")         // 过滤 CounterCreator
                .SelectMany(c => c.Counter?.Conditions).FirstOrDefault(c => c.ConditionType == "Equipment"); // 获取 Counter 的 Conditions
            var zonetargets = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)   // 所有 AvailableForFinish 条件
                .Where(c => c.ConditionType == "CounterCreator")         // 过滤 CounterCreator
                .SelectMany(c => c.Counter?.Conditions).FirstOrDefault(c => c.ConditionType == "InZone"); // 获取 Counter 的 Conditions

            //需要新增装备需求
            //这玩意定义好弱智
            //草了, 还需要weaponmod解析
            if (killtargets != null)
            {
                var copytargets = cloner.Clone(killtargets);
                copytargets.BodyPart = BitMapUtils.GetBodyPartCode(killTargetData.BodyPart);
                copytargets.Daytime = new DaytimeCounter
                {
                    From = killTargetData.DayTime[0],
                    To = killTargetData.DayTime[1]
                };
                copytargets.Distance = new CounterConditionDistance
                {
                    CompareMethod = EnumUtils.GetCompareType(killTargetData.DistanceType),
                    Value = (double)killTargetData.Distance
                };
                copytargets.Id = VulcanUtil.ConvertHashID($"{killTargetData.Id}_KillsCounter");
                if (killTargetData.EnemyEquipmentList.Count > 0)
                {
                    List<List<string>> list = copytargets.EnemyEquipmentInclusive?.ToList() ?? new List<List<string>>();
                    foreach (List<string> itemarray in killTargetData.EnemyEquipmentList)
                    {
                        var addedarray = new List<string>();
                        foreach (var item in itemarray)
                        {
                            addedarray.Add(VulcanUtil.ConvertHashID(item));
                        }
                        list.Add(addedarray); // 添加新元素
                        copytargets.EnemyEquipmentInclusive = list;
                    }
                }
                if (killTargetData.WeaponList.Count > 0)
                {
                    foreach (var weapon in killTargetData.WeaponList)
                    {
                        copytargets.Weapon.Clear();
                        copytargets.Weapon.Add(VulcanUtil.ConvertHashID(weapon));
                    }
                }
                if (killTargetData.ModList.Count > 0)
                {
                    copytargets.WeaponModsInclusive = new List<List<string>>();
                    var count = killTargetData.ModList.Count;
                    for (var i = 0; i < count; i++)
                    {
                        var list = killTargetData.ModList[i];
                        copytargets.WeaponModsInclusive.AddItem(list);
                    }
                }
                copytargets.SavageRole = killTargetData.BotRole;
                copytargets.Target = new ListOrT<string>(null, killTargetData.BotType);
                copycondition.Counter.Conditions.Add(copytargets);
            }
            if (locationtargets != null && killTargetData.Location > 0)
            {
                var copytargets = cloner.Clone(locationtargets);
                copytargets.Id = VulcanUtil.ConvertHashID($"{killTargetData.Id}_LocationCounter");
                var locations = BitMapUtils.GetLocationCode(killTargetData.Location);
                copytargets.Target = new ListOrT<string>(new List<string>(), null);
                foreach (string location in locations)
                {
                    copytargets.Target.List.Add(location);
                }
                copycondition.Counter.Conditions.Add(copytargets);
            }
            //完事
            if (equiptargets != null && killTargetData.EquipmentList.Count > 0)
            {
                var count = killTargetData.EquipmentList.Count;
                for (var i = 0; i < count; i++)
                {
                    var copytargets = cloner.Clone(equiptargets);
                    copytargets.Id = VulcanUtil.ConvertHashID($"{killTargetData.Id}_EquipmentCounter_{count}");
                    copytargets.EquipmentExclusive.Clear();
                    copytargets.EquipmentInclusive = new List<List<string>>();
                    var list = killTargetData.EquipmentList[i];
                    foreach (var item in list)
                    {
                        copytargets.EquipmentInclusive.AddItem(new List<string>
                        {
                            item
                        });
                    }
                    copycondition.Counter.Conditions.Add(copytargets);
                }
            }
            //区域击杀
            if (zonetargets != null && killTargetData.ZoneList.Count > 0)
            {
                var copytargets = cloner.Clone(zonetargets);
                copytargets.Id = VulcanUtil.ConvertHashID($"{killTargetData.Id}_ZoneCounter");
                copytargets.Zones.Clear();
                foreach (var zone in killTargetData.ZoneList)
                {
                    copytargets.Zones.Add(zone);
                }
                copycondition.Counter.Conditions.Add(copytargets);
            }
            conditions.Add(copycondition);
        }
    }
    public static void InitReachLevelDataConditions(List<QuestCondition> conditions, ReachLevelData reachLevelData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForStart)
            .FirstOrDefault(c => c.ConditionType == "Level");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = reachLevelData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.CompareMethod = ">=";
            copycondition.Value = (double)reachLevelData.Count;
            conditions.Add(copycondition);
        }
    }
    public static void InitReachPrestigeLevelDataConditions(List<QuestCondition> conditions, ReachPrestigeLevelData reachPrestigeLevelData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForStart)
            .FirstOrDefault(c => c.ConditionType == "Level");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = reachPrestigeLevelData.Id;
            copycondition.ConditionType = "PrestigeLevel";
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.CompareMethod = EnumUtils.GetCompareType(reachPrestigeLevelData.CompareType);
            copycondition.Value = (double)reachPrestigeLevelData.Level;
            conditions.Add(copycondition);
        }
    }
    public static void InitVisitPlaceDataConditions(List<QuestCondition> conditions, VisitPlaceData visitPlaceData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForFinish)
            .FirstOrDefault(c => c.ConditionType == "CounterCreator" && c.Type == "Completion");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = visitPlaceData.Id;
            copycondition.Counter.Id = VulcanUtil.ConvertHashID($"{visitPlaceData.Id}_Counter");
            copycondition.Counter.Conditions.Clear();
            copycondition.OneSessionOnly = visitPlaceData.CompleteInOneRaid;
            copycondition.Value = (double)1;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            var visittargets = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)   // 所有 AvailableForFinish 条件
                .Where(c => c.ConditionType == "CounterCreator")         // 过滤 CounterCreator
                .SelectMany(c => c.Counter?.Conditions).FirstOrDefault(c => c.ConditionType == "VisitPlace"); // 获取 Counter 的 Conditions
            if (visittargets != null)
            {
                var copytargets = cloner.Clone(visittargets);
                copytargets.Id = VulcanUtil.ConvertHashID($"{visitPlaceData.Id}_VisitCounter");
                copytargets.Target = copytargets.Target = new ListOrT<string>(null, visitPlaceData.ZoneId);
                copycondition.Counter.Conditions.Add(copytargets);
            }
            conditions.Add(copycondition);
        }
    }
    public static void InitPlaceItemDataConditions(List<QuestCondition> conditions, PlaceItemData placeItemData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForFinish)
            .FirstOrDefault(c => c.ConditionType == "LeaveItemAtLocation");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = placeItemData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.Target = new ListOrT<string>(new List<string>(), null);
            copycondition.Target.List.Add(placeItemData.ItemId);
            copycondition.Value = (double)placeItemData.Count;
            copycondition.PlantTime = (double)placeItemData.Time;
            copycondition.ZoneId = placeItemData.ZoneId;
            conditions.Add(copycondition);
        }
    }
    public static void InitExitLocationDataConditions(List<QuestCondition> conditions, ExitLocationData exitLocationData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForFinish)
            .FirstOrDefault(c => c.ConditionType == "CounterCreator" && c.Type == "Completion");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = exitLocationData.Id;
            copycondition.Counter.Id = VulcanUtil.ConvertHashID($"{exitLocationData.Id}_Counter");
            copycondition.Counter.Conditions.Clear();
            copycondition.OneSessionOnly = exitLocationData.CompleteInOneRaid;
            copycondition.Value = (double)exitLocationData.Count;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            var locationtargets = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)   // 所有 AvailableForFinish 条件
                .Where(c => c.ConditionType == "CounterCreator")         // 过滤 CounterCreator
                .SelectMany(c => c.Counter?.Conditions).FirstOrDefault(c => c.ConditionType == "Location"); // 获取 Counter 的 Conditions
            var exitstatustargets = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)   // 所有 AvailableForFinish 条件
                .Where(c => c.ConditionType == "CounterCreator")         // 过滤 CounterCreator
                .SelectMany(c => c.Counter?.Conditions).FirstOrDefault(c => c.ConditionType == "ExitStatus"); // 获取 Counter 的 Conditions
            if (locationtargets != null)
            {
                var copytargets = cloner.Clone(locationtargets);
                copytargets.Id = VulcanUtil.ConvertHashID($"{exitLocationData.Id}_LocationCounter");
                var locations = BitMapUtils.GetLocationCode(exitLocationData.Locations);
                copytargets.Target = new ListOrT<string>(new List<string>(), null);
                foreach (string location in locations)
                {
                    copytargets.Target.List.Add(location);
                }
                copycondition.Counter.Conditions.Add(copytargets);
            }
            if (exitstatustargets != null)
            {
                var copytargets = cloner.Clone(exitstatustargets);
                copytargets.Id = VulcanUtil.ConvertHashID($"{exitLocationData.Id}_ExitStatusCounter");
                var statuslist = BitMapUtils.GetExitStatusCode(exitLocationData.ExitStatus);
                copytargets.Status.Clear();
                foreach (string status in statuslist)
                {
                    copytargets.Status.Add(status);
                }
                copycondition.Counter.Conditions.Add(copytargets);
            }
            if (exitLocationData.ChooseExitPoint == true)
            {
                var exitpointtargets = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)   // 所有 AvailableForFinish 条件
                .Where(c => c.ConditionType == "CounterCreator")         // 过滤 CounterCreator
                .SelectMany(c => c.Counter?.Conditions).FirstOrDefault(c => c.ConditionType == "ExitName"); // 获取 Counter 的 Conditions
                if (exitpointtargets != null)
                {
                    var copytargets = cloner.Clone(exitpointtargets);
                    copytargets.Id = VulcanUtil.ConvertHashID($"{exitLocationData.Id}_ExitPointCounter");
                    copytargets.ExitName = exitLocationData.ExitPoint;
                    copycondition.Counter.Conditions.Add(copytargets);
                }
            }
            conditions.Add(copycondition);
        }
    }
    public static void InitReachTraderStandingDataConditions(List<QuestCondition> conditions, ReachTraderStandingData reachTraderStandingData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForStart)
            .FirstOrDefault(c => c.ConditionType == "Level");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = reachTraderStandingData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.CompareMethod = ">=";
            copycondition.ConditionType = "TraderStanding";
            copycondition.Target = new ListOrT<string>(null, reachTraderStandingData.TraderId);
            copycondition.Value = reachTraderStandingData.TrustStanding;
            conditions.Add(copycondition);
        }
    }
    public static void InitReachTraderTrustLevelDataConditions(List<QuestCondition> conditions, ReachTraderTrustLevelData reachTraderTrustLevelData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForFinish)
            .FirstOrDefault(c => c.ConditionType == "TraderLoyalty");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = reachTraderTrustLevelData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.CompareMethod = ">=";
            copycondition.Target = new ListOrT<string>(null, reachTraderTrustLevelData.TraderId);
            copycondition.Value = (double)reachTraderTrustLevelData.TrustLevel;
            conditions.Add(copycondition);
        }
    }
    public static void InitReachSkillLevelDataConditions(List<QuestCondition> conditions, ReachSkillLevelData reachTraderTrustLevelData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForFinish)
            .FirstOrDefault(c => c.ConditionType == "Skill");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = reachTraderTrustLevelData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.CompareMethod = ">=";
            copycondition.Target = new ListOrT<string>(null, reachTraderTrustLevelData.SkillType);
            copycondition.Value = (double)reachTraderTrustLevelData.Level;
            conditions.Add(copycondition);
        }
    }
    public static void InitCompleteQuestDataConditions(List<QuestCondition> conditions, CompleteQuestData completeQuestData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetQuests()
            .SelectMany(q => q.Value.Conditions.AvailableForStart)
            .FirstOrDefault(c => c.ConditionType == "Quest");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = completeQuestData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.Target = new ListOrT<string>(null, completeQuestData.QuestId);
            copycondition.Status = BitMapUtils.GetQuestStatusCode(completeQuestData.QuestStatus);
            conditions.Add(copycondition);
        }
    }
    public static void InitCustomizationBlockDataConditions(List<QuestCondition> conditions, CustomizationBlockData customizationBlockData, DatabaseService databaseService, ICloner cloner)
    {
        var condition = databaseService.GetHideout()
            .Customisation
            .Globals
            .SelectMany(q => q.Conditions)
            .FirstOrDefault(c => c.ConditionType == "Block");
        if (condition != null)
        {
            var copycondition = cloner.Clone(condition);
            copycondition.Id = customizationBlockData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            conditions.Add(copycondition);
        }
    }
    public static void InitQuestRewards(string folderpath, DatabaseService databaseService, ModHelper modHelper, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var rewards = modHelper.GetJsonDataFromFile<List<CustomQuestRewardData>>(folderpath, fileName);
                InitQuestRewards(rewards, databaseService, cloner, logger);
            }
        }
    }
    public static void InitQuestRewards(List<CustomQuestRewardData> rewards, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        foreach (CustomQuestRewardData reward in rewards)
        {
            switch (reward)
            {
                case CustomItemRewardData itemreward:
                    {
                        InitItemRewards(itemreward, databaseService, cloner);
                    }
                    break;
                case CustomAssortUnlockRewardData assortunlockreward:
                    {
                        InitAssortUnlockRewards(assortunlockreward, databaseService, cloner, logger);
                    }
                    break;
                case CustomRecipeUnlockRewardData recipeunlockreward:
                    {
                        InitRecipeUnlockRewards(recipeunlockreward, databaseService, cloner);
                    }
                    break;
                case CustomExperienceRewardData experiencereward:
                    {
                        InitExperienceRewards(experiencereward, databaseService, cloner);
                    }
                    break;
                case CustomTraderStandingRewardData traderstandingreward:
                    {
                        InitTraderStandingRewards(traderstandingreward, databaseService, cloner);
                    }
                    break;
                case CustomCustomizationRewardData customizationreward:
                    {
                        InitCustomizationRewards(customizationreward, databaseService, cloner);
                    }
                    break;
                case CustomAchievementRewardData achievementreward:
                    {
                        InitAchievementRewards(achievementreward, databaseService, cloner);
                    }
                    break;
                case CustomTraderUnlockRewardData traderunlockreward:
                    {
                        InitTraderUnlockRewards(traderunlockreward, databaseService, cloner);
                    }
                    break;
                case CustomSkillExperienceRewardData skillexperiencereward:
                    {
                        InitSkillExperienceRewards(skillexperiencereward, databaseService, cloner);
                    }
                    break;
                case CustomPocketRewardData pocketreward:
                    {
                        InitPocketRewards(pocketreward, databaseService, cloner);
                    }
                    break;
                default:
                    {

                    }
                    break;
            }
        }
    }
    public static void InitItemRewards(CustomItemRewardData itemRewardData, DatabaseService databaseService, ICloner cloner)
    {
        var queststage = EnumUtils.GetQuestStageType(itemRewardData.QuestStage);
        var rewardtarget = databaseService.GetQuests()
            .SelectMany(q => q.Value.Rewards[queststage])
            .FirstOrDefault(r => r.Type == RewardType.Item);
        if (!itemRewardData.IsAchievement)
        {
            var target = GetQuest(itemRewardData.QuestId, databaseService).Rewards;
            if (target.Count > 0)
            {
                if (rewardtarget != null)
                {

                    var copyreward = GetItemReward(rewardtarget, target[queststage], itemRewardData, cloner);
                    target[queststage].Add(copyreward);
                }
            }
        }
        else
        {
            var target = AchievementUtils.GetAchievement(itemRewardData.QuestId, databaseService).Rewards.ToList();
            if (rewardtarget != null)
            {
                var copyreward = GetItemReward(rewardtarget, target, itemRewardData, cloner);
                target.Add(copyreward);
            }
            AchievementUtils.GetAchievement(itemRewardData.QuestId, databaseService).Rewards = target;
        }
    }
    public static void InitRecipeUnlockRewards(CustomRecipeUnlockRewardData recipeUnlockRewardData, DatabaseService databaseService, ICloner cloner)
    {
        //wip
        var queststage = EnumUtils.GetQuestStageType(recipeUnlockRewardData.QuestStage);
        var stringstage = queststage.ToString().ToLower();
        var questid = recipeUnlockRewardData.QuestId;
        var rewardid = recipeUnlockRewardData.Id;
        var rewardtarget = databaseService.GetQuests()
            .SelectMany(q => q.Value.Rewards[queststage])
            .FirstOrDefault(r => r.Type == RewardType.ProductionScheme);
        var target = GetQuest(questid, databaseService).Rewards;
        if (target.Count > 0)
        {
            if (rewardtarget != null)
            {
                var copyreward = InitCopiedReward(rewardtarget, target[queststage], recipeUnlockRewardData, cloner);
                var itemid = recipeUnlockRewardData.RecipeData.Output;
                copyreward.Items.Clear();
                copyreward.Items.Add(new Item
                {
                    Id = VulcanUtil.ConvertHashID(recipeUnlockRewardData.Id),
                    Template = itemid,
                    Upd = new Upd
                    {
                        StackObjectsCount = 1,
                        SpawnedInSession = true,
                    }
                });
                copyreward.Target = copyreward.Items[0].Id;
                copyreward.TraderId = (int)recipeUnlockRewardData.RecipeData.AreaType;
                copyreward.LoyaltyLevel = (int)recipeUnlockRewardData.RecipeData.AreaLevel;
                target[queststage].Add(copyreward);
                RecipeUtils.InitRecipe(recipeUnlockRewardData.RecipeData, databaseService, cloner);
            }
        }
    }
    public static void InitAssortUnlockRewards(CustomAssortUnlockRewardData assortUnlockRewardData, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        var queststage = EnumUtils.GetQuestStageType(assortUnlockRewardData.QuestStage);
        var stringstage = queststage.ToString().ToLower();
        var questid = assortUnlockRewardData.QuestId;
        var rewardid = assortUnlockRewardData.Id;
        var rewardtarget = databaseService.GetQuests()
            .SelectMany(q => q.Value.Rewards[queststage])
            .FirstOrDefault(r => r.Type == RewardType.AssortmentUnlock);
        var target = GetQuest(questid, databaseService).Rewards;
        if (target.Count > 0)
        {
            if (rewardtarget != null)
            {
                var copyreward = InitCopiedReward(rewardtarget, target[queststage], assortUnlockRewardData, cloner);
                var assortitems = ItemUtils.ConvertItemListData(assortUnlockRewardData.AssortData.Item, cloner);
                var items = ItemUtils.RegenerateItemListData(assortitems, (string)rewardid, cloner);
                var traderid = assortUnlockRewardData.AssortData.Trader;
                copyreward.Items.Clear();
                foreach (Item item in items)
                {
                    copyreward.Items.Add(item);
                }
                copyreward.Target = copyreward.Items[0].Id;
                copyreward.TraderId = traderid;
                copyreward.LoyaltyLevel = assortUnlockRewardData.AssortData.TrustLevel;
                target[queststage].Add(copyreward);
                AssortUtils.InitAssort((CustomAssortData)assortUnlockRewardData.AssortData, databaseService, cloner, logger);
                TraderUtils.GetTrader(traderid, databaseService).QuestAssort[stringstage].Add(assortitems[0].Id, questid);
            }
        }
    }
    public static void InitExperienceRewards(CustomExperienceRewardData experienceRewardData, DatabaseService databaseService, ICloner cloner)
    {
        var queststage = EnumUtils.GetQuestStageType(experienceRewardData.QuestStage);
        var rewardtarget = databaseService.GetQuests()
            .SelectMany(q => q.Value.Rewards[queststage])
            .FirstOrDefault(r => r.Type == RewardType.Experience);
        var target = GetQuest(experienceRewardData.QuestId, databaseService).Rewards;
        if (target.Count > 0)
        {
            if (rewardtarget != null)
            {
                var copyreward = InitCopiedReward(rewardtarget, target[queststage], experienceRewardData, cloner);
                copyreward.Value = (double)experienceRewardData.Count; //死了妈的东西你就这么喜欢用double是吗
                target[queststage].Add(copyreward);
            }
        }
    }
    public static void InitTraderStandingRewards(CustomTraderStandingRewardData traderStandingRewardData, DatabaseService databaseService, ICloner cloner)
    {
        var queststage = EnumUtils.GetQuestStageType(traderStandingRewardData.QuestStage);
        var rewardtarget = databaseService.GetQuests()
            .SelectMany(q => q.Value.Rewards[queststage])
            .FirstOrDefault(r => r.Type == RewardType.TraderStanding);
        var target = GetQuest(traderStandingRewardData.QuestId, databaseService).Rewards;
        if (target.Count > 0)
        {
            if (rewardtarget != null)
            {
                var copyreward = InitCopiedReward(rewardtarget, target[queststage], traderStandingRewardData, cloner);
                copyreward.Value = traderStandingRewardData.Count;
                copyreward.Target = (string)traderStandingRewardData.TraderId;
                target[queststage].Add(copyreward);
            }
        }
    }
    public static void InitCustomizationRewards(CustomCustomizationRewardData customiazationRewardData, DatabaseService databaseService, ICloner cloner)
    {
        var queststage = EnumUtils.GetQuestStageType(customiazationRewardData.QuestStage);
        var achievements = databaseService.GetAchievements();
        var rewardtarget = databaseService.GetQuests()
            .SelectMany(q => q.Value.Rewards[queststage])
            .FirstOrDefault(r => r.Type == RewardType.CustomizationDirect);
        if (!customiazationRewardData.IsAchievement)
        {
            var target = GetQuest(customiazationRewardData.QuestId, databaseService).Rewards;
            if (target.Count > 0)
            {
                if (rewardtarget != null)
                {
                    var copyreward = GetCustomizationReward(rewardtarget, target[queststage], customiazationRewardData, cloner);
                    target[queststage].Add(copyreward);
                }
            }
        }
        else
        {
            var target = AchievementUtils.GetAchievement(customiazationRewardData.QuestId, databaseService).Rewards.ToList();
            if (rewardtarget != null)
            {
                var copyreward = GetCustomizationReward(rewardtarget, target, customiazationRewardData, cloner);
                target.Add(copyreward);
            }
            AchievementUtils.GetAchievement(customiazationRewardData.QuestId, databaseService).Rewards = target;
        }
    }
    public static void InitAchievementRewards(CustomAchievementRewardData achievementRewardData, DatabaseService databaseService, ICloner cloner)
    {
        var queststage = EnumUtils.GetQuestStageType(achievementRewardData.QuestStage);
        var rewardtarget = databaseService.GetQuests()
            .SelectMany(q => q.Value.Rewards[queststage])
            .FirstOrDefault(r => r.Type == RewardType.Achievement);
        var target = GetQuest(achievementRewardData.QuestId, databaseService).Rewards;
        if (target.Count > 0)
        {
            if (rewardtarget != null)
            {
                var copyreward = InitCopiedReward(rewardtarget, target[queststage], achievementRewardData, cloner);
                copyreward.Target = (string)achievementRewardData.TargetId;
                target[queststage].Add(copyreward);
            }
        }
    }
    public static void InitTraderUnlockRewards(CustomTraderUnlockRewardData traderUnlockRewardData, DatabaseService databaseService, ICloner cloner)
    {
        var queststage = EnumUtils.GetQuestStageType(traderUnlockRewardData.QuestStage);
        var rewardtarget = databaseService.GetQuests()
            .SelectMany(q => q.Value.Rewards[queststage])
            .FirstOrDefault(r => r.Type == RewardType.TraderUnlock);
        var target = GetQuest(traderUnlockRewardData.QuestId, databaseService).Rewards;
        if (target.Count > 0)
        {
            if (rewardtarget != null)
            {
                var copyreward = InitCopiedReward(rewardtarget, target[queststage], traderUnlockRewardData, cloner);
                copyreward.Target = (string)traderUnlockRewardData.TraderId;
                target[queststage].Add(copyreward);
            }
        }
    }
    public static void InitSkillExperienceRewards(CustomSkillExperienceRewardData skillExperienceRewardData, DatabaseService databaseService, ICloner cloner)
    {
        var queststage = EnumUtils.GetQuestStageType(skillExperienceRewardData.QuestStage);
        var rewardtarget = databaseService.GetQuests()
            .SelectMany(q => q.Value.Rewards[queststage])
            .FirstOrDefault(r => r.Type == RewardType.Skill);
        var target = GetQuest(skillExperienceRewardData.QuestId, databaseService).Rewards;
        if (target.Count > 0)
        {
            if (rewardtarget != null)
            {
                var copyreward = InitCopiedReward(rewardtarget, target[queststage], skillExperienceRewardData, cloner);
                copyreward.Target = (string)skillExperienceRewardData.SkillType;
                copyreward.Value = (double)(skillExperienceRewardData.Count * 100);
                target[queststage].Add(copyreward);
            }
        }
    }
    public static void InitPocketRewards(CustomPocketRewardData customPocketRewardData, DatabaseService databaseService, ICloner cloner)
    {
        var queststage = EnumUtils.GetQuestStageType(customPocketRewardData.QuestStage);
        var rewardtarget = databaseService.GetQuests()
            .SelectMany(q => q.Value.Rewards[queststage])
            .FirstOrDefault(r => r.Type == RewardType.Pockets);
        var target = GetQuest(customPocketRewardData.QuestId, databaseService).Rewards;
        if (target.Count > 0)
        {
            if (rewardtarget != null)
            {
                var copyreward = InitCopiedReward(rewardtarget, target[queststage], customPocketRewardData, cloner);
                copyreward.Target = (string)customPocketRewardData.TargetId;
                target[queststage].Add(copyreward);
            }
        }
    }
    public static void InitQuestLogicTreeData(Dictionary<string, QuestLogicTree> questLogicTree, DatabaseService databaseService, ICloner cloner)
    {
        foreach (var data in questLogicTree)
        {
            InitQuestLogicTree(data.Value, databaseService, cloner);
        }
    }
    public static void InitQuestLogicTreeData(string folderpath, DatabaseService databaseService, ModHelper modHelper, ICloner cloner)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var logictree = modHelper.GetJsonDataFromFile<QuestLogicTree>(folderpath, fileName);
                InitQuestLogicTree(logictree, databaseService, cloner);
            }
        }
    }
    public static void InitQuestLogicTree(QuestLogicTree questLogicTree, DatabaseService databaseService, ICloner cloner)
    {
        var questTarget = GetQuest((string)questLogicTree.Id, databaseService);
        foreach (var quest in questLogicTree.PreQuestData)
        {
            var questid = VulcanUtil.ConvertHashID(quest.Key);
            InitCompleteQuestDataConditions(questTarget.Conditions.AvailableForStart, new CompleteQuestData
            {
                Id = VulcanUtil.ConvertHashID($"{questLogicTree.Id}_PreQuest_{quest.Key}"),
                QuestId = questid,
                QuestStatus = quest.Value
            },
            databaseService, cloner);
        }
        foreach (var trader in questLogicTree.PreTraderStandingData)
        {
            var traderid = VulcanUtil.ConvertHashID(trader.Key);
            InitReachTraderStandingDataConditions(questTarget.Conditions.AvailableForStart, new ReachTraderStandingData
            {
                Id = VulcanUtil.ConvertHashID($"{questLogicTree.Id}_PreTraderStanding_{trader.Key}"),
                TraderId = traderid,
                TrustStanding = trader.Value
            },
            databaseService, cloner);
        }
        foreach (var trader in questLogicTree.PreTraderTrustLevelData)
        {
            var traderid = VulcanUtil.ConvertHashID(trader.Key);
            InitReachTraderTrustLevelDataConditions(questTarget.Conditions.AvailableForStart, new ReachTraderTrustLevelData
            {
                Id = VulcanUtil.ConvertHashID($"{questLogicTree.Id}_PreTraderTrustLevel_{trader.Key}"),
                TraderId = traderid,
                TrustLevel = trader.Value
            },
            databaseService, cloner);
        }
        if (questLogicTree.PrePlayerLevel > 0)
        {
            InitReachLevelDataConditions(questTarget.Conditions.AvailableForStart, new ReachLevelData
            {
                Id = VulcanUtil.ConvertHashID($"{questLogicTree.Id}_PrePlayerLevel"),
                Count = questLogicTree.PrePlayerLevel
            },
            databaseService, cloner);
        }
        if (questLogicTree?.PrePlayerPrestigeLevel > 0)
        {
            InitReachPrestigeLevelDataConditions(questTarget.Conditions.AvailableForStart, new ReachPrestigeLevelData
            {
                Id = VulcanUtil.ConvertHashID($"{questLogicTree.Id}_PrePlayerPrestigeLevel"),
                CompareType = questLogicTree.PrestigeCompareType ?? 3,
                Level = (int)questLogicTree.PrePlayerPrestigeLevel
            },
            databaseService, cloner);
        }
    }
    public static Reward InitCopiedReward(Reward reward, List<Reward> target, CustomQuestRewardData rewardData, ICloner cloner)
    {
        var copyreward = cloner.Clone(reward);
        copyreward.Id = rewardData.Id;
        copyreward.Index = target.Count;
        if (copyreward.AvailableInGameEditions != null)
        {
            copyreward.AvailableInGameEditions?.Clear();
        }
        else
        {
            copyreward.AvailableInGameEditions = new HashSet<string>();
        }
        if (rewardData.AvailableGameEdition != null)
        {
            var gameversion = BitMapUtils.GetGameVersionCode((int)rewardData.AvailableGameEdition);
            foreach (var v in gameversion)
            {
                copyreward.AvailableInGameEditions.Add(v);
                //Console.WriteLine(v);
            }
        }
        return copyreward;
    }
    public static Reward GetItemReward(Reward rewardtarget, List<Reward> target, CustomItemRewardData itemRewardData, ICloner cloner)
    {
        var copyreward = InitCopiedReward(rewardtarget, target, itemRewardData, cloner);
        var items = ItemUtils.ConvertItemListData(itemRewardData.Items, cloner);
        copyreward.FindInRaid = itemRewardData.FindInRaid;
        copyreward.Unknown = itemRewardData.IsUnknownReward;
        copyreward.IsHidden = itemRewardData.IsHiddenReward;
        copyreward.Items.Clear();
        foreach (Item item in items)
        {
            copyreward.Items.Add(item);
        }
        copyreward.Target = copyreward.Items[0].Id;
        copyreward.Value = (double)itemRewardData.Count;
        return copyreward;
    }
    public static Reward GetCustomizationReward(Reward rewardtarget, List<Reward> target, CustomCustomizationRewardData customizationRewardData, ICloner cloner)
    {
        var copyreward = InitCopiedReward(rewardtarget, target, customizationRewardData, cloner);
        copyreward.Target = (string)customizationRewardData.TargetId;
        return copyreward;
    }
}