using HarmonyLib;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Json;
using System.Reflection;
using Path = System.IO.Path;

namespace EternalCycle
{
    /// <summary>
    /// 对任务进行操作处理的工具类
    /// </summary>
    public class QuestUtils
    {
        //缓存任务条件字典
        public static Dictionary<EQuestConditionsTypeCache, QuestCondition> cacheConditions = new Dictionary<EQuestConditionsTypeCache, QuestCondition>();
        public static Dictionary<EQuestCountersCacheType, QuestConditionCounterCondition> cacheCounters = new Dictionary<EQuestCountersCacheType, QuestConditionCounterCondition>();

        //为任务条件重定义的枚举类, 作为字典索引
        public enum EQuestConditionsTypeCache
        {
            FindItem,
            HandoverItem,
            LeaveItemAtLocation,
            Level,
            TraderLoyalty,
            Skill,
            Quest,
            Elimination,
            Completion,
            Block
        }

        public enum EQuestCountersCacheType
        {
            Location,
            ExitStatus,
            ExitName,
            Equipment,
            VisitPlace,
            Kills,
            InZone
        }

        /// <summary>
        /// 从数据库返回一个任务的引用
        /// </summary>
        /// <param name="questid">任务ID</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns></returns>
        public static Quest? GetQuest(string questid, DatabaseService databaseService)
        {
            if (databaseService.GetQuests().TryGetValue(questid, out var quest))
            {
                return quest;
            }
            return null;
        }

            /// <summary>
            /// 从序列化对象加载任务
            /// </summary>
            /// <param name="questData">任务数据</param>
            /// <param name="databaseService">数据库实例</param>
            /// <param name="cloner">克隆器实例</param>
            public static void InitQuestData(Dictionary<string, CustomQuest> questData, string respath, DatabaseService databaseService, ICloner cloner)
            {
                foreach (var customquest in questData)
                {
                    InitQuest(customquest.Value, respath, databaseService, cloner);
                }
            }

            /// <summary>
            /// 从指定目录加载任务
            /// </summary>
            /// <param name="folderpath">文件夹路径</param>
            /// <param name="databaseService">数据库实例</param>
            /// <param name="modHelper">mod帮助</param>
            /// <param name="cloner">克隆器实例</param>
            public static void InitQuestData(string folderpath, string respath, DatabaseService databaseService, ModHelper modHelper, ICloner cloner)
            {
                List<string> files = Directory.GetFiles(folderpath).ToList();
                if (files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileName(file);
                        var customquest = modHelper.GetJsonDataFromFile<CustomQuest>(folderpath, fileName);
                        InitQuest(customquest, respath, databaseService, cloner);
                    }
                }
            }

            /// <summary>
            /// 从自定义结构序列化完整任务数据
            /// </summary>
            /// <param name="customQuest">自定义任务数据</param>
            /// <param name="databaseService">数据库实例</param>
            /// <param name="cloner">克隆器实例</param>
            public static void InitQuest(CustomQuest customQuest, string respath, DatabaseService databaseService, ICloner cloner)
            {
                var questid = customQuest.QuestId;
                //短缺
                var pattern = GetQuest(QuestTpl.SHORTAGE, databaseService);
                if (pattern == null) return; //怎么可能呢?
                Quest? questPattern = cloner.Clone(pattern);
                if (questPattern == null) return; //神经病....
                //哎我尼玛的不改了, 防空防空十防九空, 我防你妈, 报错拉倒
                //清空任务数据
                questPattern.Conditions.AvailableForStart.Clear();
                questPattern.Conditions.AvailableForFinish.Clear();
                questPattern.Conditions.Fail.Clear();
                //清空并重建任务奖励
                questPattern.Rewards = new Dictionary<string, List<Reward>>
                {
                    ["Started"] = new List<Reward>(),
                    ["Success"] = new List<Reward>(),
                    ["Fail"] = new List<Reward>(),
                };
                //覆盖任务基础数据
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
                //InitQuestConditions(questPattern.Conditions.AvailableForFinish, customQuest.QuestConditions.QuestFinishData, databaseService, cloner);
                //InitQuestConditions(questPattern.Conditions.Fail, customQuest.QuestConditions.QuestFailedData, databaseService, cloner);
                //临时
                databaseService.GetQuests().TryAdd(questid, questPattern);
                var imageRouter = ServiceLocator.ServiceProvider.GetService<ImageRouter>();
                ImageUtils.RegisterQuestRoute(questPattern.Image, Path.Combine(respath, "res/questimage/"), imageRouter);
                //为了完成原版兼容, 奖励定义有任务ID, 必须在任务初始化后添加
                //应该可以重载
                EventManager.DataLoadEvent.LoadQuestDataEvent += (context) =>
                {
                    try
                    {
                        InitQuestConditions(questPattern.Conditions.AvailableForFinish, customQuest.QuestConditions.QuestFinishData, context.DB, context.Cloner);
                        InitQuestConditions(questPattern.Conditions.Fail, customQuest.QuestConditions.QuestFailedData, context.DB, context.Cloner);
                        //InitQuestRewards(customQuest.QuestRewards, context.DB, context.Cloner);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注入任务数据层时发生异常：{questid}", ex);
                    }
                };
                EventManager.DataLoadEvent.LoadQuestRewardEvent += (context) =>
                {
                    try
                    {
                        InitQuestRewards(customQuest.QuestRewards, context.DB, context.Cloner);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注入任务数据层时发生异常：{questid}", ex);
                    }
                };
            }

            /// <summary>
            /// 加载任务数据
            /// </summary>
            /// <param name="conditions">目标列表</param>
            /// <param name="customquestdata">自定义任务对象列表</param>
            /// <param name="databaseService">数据库实例</param>
            /// <param name="cloner">克隆器实例</param>
            public static void InitQuestConditions(List<QuestCondition> conditions, List<CustomQuestData> customquestdata, DatabaseService databaseService, ICloner cloner)
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
                                //VulcanLog.Warn($"发现未处理的任务属性({data.Id})! ", logger);
                            }
                            break;
                    }
                    //自动本地化
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

            /// <summary>
            /// 将自定义任务注册到加载事件
            /// </summary>
            /// <param name="path">指定的存放任务文件的路径或完整的任务文件路径</param>
            /// <param name="creator">创建者</param>
            /// <param name="modname">Mod名</param>
            public static void RegisterQuest(string path, string respath)
            {
                // 文件夹加载模式
                if (Directory.Exists(path))
                {
                    EventManager.DataLoadEvent.LoadQuestEvent += (context) =>
                    {
                        try
                        {
                            // 对应调用已有的文件夹重载方法
                            InitQuestData(path, respath, context.DB, context.ModHelper, context.Cloner);
                            //EventManager.EventLogger.Info($"[{modname}] {creator} 的任务模块(文件夹)注册成功");
                        }
                        catch (Exception ex)
                        {
                            EventManager.EventLogger.Error($"注册任务时发生错误：指定的文件夹 {path} 存在问题", ex);
                        }
                    };
                }
                // 单文件加载模式
                else if (File.Exists(path))
                {
                    EventManager.DataLoadEvent.LoadQuestEvent += (context) =>
                    {
                        try
                        {
                            // 反序列化为字典字典，对应已有的 Dictionary 重载方法
                            var questData = context.JsonUtil.Deserialize<Dictionary<string, CustomQuest>>(File.ReadAllText(path));
                            InitQuestData(questData, respath, context.DB, context.Cloner);

                            //EventManager.EventLogger.Info($"[{modname}] {creator} 的任务模块(单文件)注册成功");
                        }
                        catch (Exception ex)
                        {
                            EventManager.EventLogger.Error($"注册任务时发生错误：指定的文件 {path} 存在问题", ex);
                        }
                    };
                }
                else
                {
                    EventManager.EventLogger.Warn($"注册任务时发生异常：找不到指定的文件或文件夹 {path}");
                }
            }

        /// <summary>
        /// 获取任务条件的工具方法
        /// </summary>
        /// <param name="cacheType">条件类型枚举</param>
        /// <param name="conditionTypeStr">条件类型</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>返回一个任务条件模板</returns>
        public static QuestCondition GetConditionTemplate(EQuestConditionsTypeCache cacheType, string conditionTypeStr, DatabaseService databaseService)
        {
            if (cacheConditions.TryGetValue(cacheType, out var condition) && condition != null)
            {
                return condition;
            }
            var foundCondition = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)
                .FirstOrDefault(c => c.ConditionType == conditionTypeStr);
            cacheConditions[cacheType] = foundCondition;
            return foundCondition;
        }

        /// <summary>
        /// 获取任务子条件的工具方法
        /// </summary>
        /// <param name="cacheType">子条件类型枚举</param>
        /// <param name="conditionTypeStr">子条件类型</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>返回一个任务子条件模板</returns>
        public static QuestConditionCounterCondition GetCounterConditionTemplate(EQuestCountersCacheType cacheType, string conditionTypeStr, DatabaseService databaseService)
        {
            if (cacheCounters.TryGetValue(cacheType, out var condition) && condition != null)
            {
                return condition;
            }
            var foundCondition = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)
                .Where(c => c.ConditionType == "CounterCreator")
                .SelectMany(c => c.Counter.Conditions)
                .FirstOrDefault(c => c.ConditionType == conditionTypeStr);
            cacheCounters[cacheType] = foundCondition;
            return foundCondition;
        }

        /// <summary>
        /// 处理寻找物品任务条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="findItemData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitFindItemDataConditions(List<QuestCondition> conditions, FindItemData findItemData, DatabaseService databaseService, ICloner cloner)
        {
            var zhCNLang = databaseService.GetLocales().Global["ch"];
            //缓存引用, 这里不可能空, 绿就绿吧, 无所谓了
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.FindItem, "FindItem", databaseService);
            if (condition == null) return;
            //复制引用
            var copycondition = cloner.Clone(condition);
            //设置基础数据
            copycondition.Id = findItemData.Id;
            copycondition.OnlyFoundInRaid = findItemData.FindInRaid;
            copycondition.Index = conditions.Count;
            //移除可见性定义
            copycondition.VisibilityConditions.Clear();
            //这里肯定是List, 直接操作
            copycondition.Target.List.Clear();
            copycondition.Target.List.Add(findItemData.ItemId);
            //尼基塔小时候从外面捡到3.1415926瓶矿泉水
            copycondition.Value = (double)findItemData.Count;
            //加入
            conditions.Add(copycondition);
            //自动本地化
            if (findItemData.AutoLocale != null && findItemData.AutoLocale == true)
            {
                zhCNLang.AddTransformer(lang =>
                {
                    lang[$"{findItemData.Id}"] = $"在战局中找到{lang[$"{findItemData.ItemId} Name"]}";
                    return lang;
                });
            }
        }

        /// <summary>
        /// 处理寻找物品组任务条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="findItemData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitFindItemGroupDataConditions(List<QuestCondition> conditions, FindItemGroupData findItemData, DatabaseService databaseService, ICloner cloner)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.FindItem, "FindItem", databaseService);
            if (condition == null) return;
            var copycondition = cloner.Clone(condition);
            copycondition.Id = findItemData.Id;
            copycondition.OnlyFoundInRaid = findItemData.FindInRaid;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.Target.List.Clear();
            foreach (string target in findItemData.Items)
            {
                copycondition.Target.List.Add(target.ConvertHashID());
            }
            copycondition.Value = (double)findItemData.Count;
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理上交物品任务条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="handItemData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitHandoverItemDataConditions(List<QuestCondition> conditions, HandoverItemData handItemData, DatabaseService databaseService, ICloner cloner)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.HandoverItem, "HandoverItem", databaseService);
            if (condition == null) return;
            var zhCNLang = databaseService.GetLocales().Global["ch"];
            var copycondition = cloner.Clone(condition);
            copycondition.Id = handItemData.Id;
            copycondition.OnlyFoundInRaid = handItemData.FindInRaid;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.Target.List.Clear();
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

        /// <summary>
        /// 处理上交物品组任务条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="handItemData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitHandoverItemGroupDataConditions(List<QuestCondition> conditions, HandoverItemGroupData handItemData, DatabaseService databaseService, ICloner cloner)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.HandoverItem, "HandoverItem", databaseService);
            if (condition == null) return;
            var copycondition = cloner.Clone(condition);
            copycondition.Id = handItemData.Id;
            copycondition.OnlyFoundInRaid = handItemData.FindInRaid;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.Target.List.Clear();
            foreach (string target in handItemData.Items)
            {
                copycondition.Target.List.Add(target.ConvertHashID());
            }
            copycondition.Value = (double)handItemData.Count;
            conditions.Add(copycondition);
        }


        /// <summary>
        /// 处理击杀任务条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="killTargetData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitKillTargetDataConditions(List<QuestCondition> conditions, KillTargetData killTargetData, DatabaseService databaseService, ICloner cloner)
        {
            //多了一层所以不适用方法
            cacheConditions.TryGetValue(EQuestConditionsTypeCache.Elimination, out var condition);
            if (condition == null)
            {
                condition = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)
                .FirstOrDefault(c => c.ConditionType == "CounterCreator" && c.Type == "Elimination");
                cacheConditions[EQuestConditionsTypeCache.Elimination] = condition;
            }
            if (condition == null) return;
            var copycondition = cloner.Clone(condition);
            copycondition.Id = killTargetData.Id;
            copycondition.Counter.Id = $"{killTargetData.Id}_Counter".ConvertHashID();
            copycondition.Counter.Conditions.Clear();
            copycondition.OneSessionOnly = killTargetData.CompleteInOneRaid;
            copycondition.Value = (double)killTargetData.Count;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            var killtargets = GetCounterConditionTemplate(EQuestCountersCacheType.Kills, "Kills", databaseService);
            var locationtargets = GetCounterConditionTemplate(EQuestCountersCacheType.Location, "Location", databaseService);
            var equiptargets = GetCounterConditionTemplate(EQuestCountersCacheType.Equipment, "Equipment", databaseService);
            var zonetargets = GetCounterConditionTemplate(EQuestCountersCacheType.InZone, "InZone", databaseService);

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
                copytargets.Id = $"{killTargetData.Id}_KillsCounter".ConvertHashID();
                if (killTargetData.EnemyEquipmentList.Count > 0)
                {
                    //万恶的IEnumerable
                    //这里为啥不直接用新元素覆盖嘞?
                    //不对, 我在干啥啊
                    copytargets.EnemyEquipmentInclusive = new List<List<string>>();
                    foreach (List<string> itemarray in killTargetData.EnemyEquipmentList)
                    {
                        var addedarray = new List<string>();
                        foreach (var item in itemarray)
                        {
                            addedarray.Add(item.ConvertHashID());
                        }
                        copytargets.EnemyEquipmentInclusive.AddItem(addedarray); // 添加新元素
                    }
                }
                if (killTargetData.WeaponList.Count > 0)
                {
                    copytargets.Weapon.Clear();
                    foreach (var weapon in killTargetData.WeaponList)
                    {
                        copytargets.Weapon.Add(weapon.ConvertHashID());
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
                copytargets.Id = $"{killTargetData.Id}_LocationCounter".ConvertHashID();
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
                    copytargets.Id = $"{killTargetData.Id}_EquipmentCounter_{i}".ConvertHashID();
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
                copytargets.Id = $"{killTargetData.Id}_ZoneCounter".ConvertHashID();
                copytargets.Zones.Clear();
                foreach (var zone in killTargetData.ZoneList)
                {
                    copytargets.Zones.Add(zone);
                }
                copycondition.Counter.Conditions.Add(copytargets);
            }
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理达到等级条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="reachLevelData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitReachLevelDataConditions(List<QuestCondition> conditions, ReachLevelData reachLevelData, DatabaseService databaseService, ICloner cloner)
        {
            var condition = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForStart)
                .FirstOrDefault(c => c.ConditionType == "Level");
            if (condition == null) return;
            var copycondition = cloner.Clone(condition);
            copycondition.Id = reachLevelData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.CompareMethod = ">=";
            copycondition.Value = (double)reachLevelData.Count;
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理达到转生等级条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="reachPrestigeLevelData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitReachPrestigeLevelDataConditions(List<QuestCondition> conditions, ReachPrestigeLevelData reachPrestigeLevelData, DatabaseService databaseService, ICloner cloner)
        {
            var condition = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForStart)
                .FirstOrDefault(c => c.ConditionType == "Level");
            if (condition == null) return;
            var copycondition = cloner.Clone(condition);
            copycondition.Id = reachPrestigeLevelData.Id;
            copycondition.ConditionType = "PrestigeLevel";
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.CompareMethod = EnumUtils.GetCompareType(reachPrestigeLevelData.CompareType);
            copycondition.Value = (double)reachPrestigeLevelData.Level;
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理访问地点条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="visitPlaceData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitVisitPlaceDataConditions(List<QuestCondition> conditions, VisitPlaceData visitPlaceData, DatabaseService databaseService, ICloner cloner)
        {
            cacheConditions.TryGetValue(EQuestConditionsTypeCache.Completion, out var condition);
            if (condition == null)
            {
                condition = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)
                .FirstOrDefault(c => c.ConditionType == "CounterCreator" && c.Type == "Completion");
                cacheConditions[EQuestConditionsTypeCache.Completion] = condition;
            }
            if (condition == null) return;
            var copycondition = cloner.Clone(condition);
            copycondition.Id = visitPlaceData.Id;
            copycondition.Counter.Id = $"{visitPlaceData.Id}_Counter".ConvertHashID();
            copycondition.Counter.Conditions.Clear();
            copycondition.OneSessionOnly = visitPlaceData.CompleteInOneRaid;
            copycondition.Value = (double)1;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            var visittargets = GetCounterConditionTemplate(EQuestCountersCacheType.VisitPlace, "VisitPlace", databaseService);
            if (visittargets == null) return;
            var copytargets = cloner.Clone(visittargets);
            copytargets.Id = $"{visitPlaceData.Id}_VisitCounter".ConvertHashID();
            copytargets.Target = new ListOrT<string>(null, visitPlaceData.ZoneId);
            copycondition.Counter.Conditions.Add(copytargets);
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理在指定地点安放物品条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="placeItemData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitPlaceItemDataConditions(List<QuestCondition> conditions, PlaceItemData placeItemData, DatabaseService databaseService, ICloner cloner)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.LeaveItemAtLocation, "LeaveItemAtLocation", databaseService);
            if (condition == null) return;
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

        /// <summary>
        /// 处理从指定地图撤离的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="exitLocationData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitExitLocationDataConditions(List<QuestCondition> conditions, ExitLocationData exitLocationData, DatabaseService databaseService, ICloner cloner)
        {
            cacheConditions.TryGetValue(EQuestConditionsTypeCache.Completion, out var condition);
            if (condition == null)
            {
                condition = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)
                .FirstOrDefault(c => c.ConditionType == "CounterCreator" && c.Type == "Completion");
                cacheConditions[EQuestConditionsTypeCache.Completion] = condition;
            }
            if (condition == null) return;
            var copycondition = cloner.Clone(condition);
            copycondition.Id = exitLocationData.Id;
            copycondition.Counter.Id = $"{exitLocationData.Id}_Counter".ConvertHashID();
            copycondition.Counter.Conditions.Clear();
            copycondition.OneSessionOnly = exitLocationData.CompleteInOneRaid;
            copycondition.Value = (double)exitLocationData.Count;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            var locationtargets = GetCounterConditionTemplate(EQuestCountersCacheType.Location, "Location", databaseService);
            var exitstatustargets = GetCounterConditionTemplate(EQuestCountersCacheType.ExitStatus, "ExitStatus", databaseService);
            if (locationtargets != null)
            {
                var copytargets = cloner.Clone(locationtargets);
                copytargets.Id = $"{exitLocationData.Id}_LocationCounter".ConvertHashID();
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
                copytargets.Id = $"{exitLocationData.Id}_ExitStatusCounter".ConvertHashID();
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

                var exitpointtargets = GetCounterConditionTemplate(EQuestCountersCacheType.ExitName, "ExitName", databaseService);
                if (exitpointtargets != null)
                {
                    var copytargets = cloner.Clone(exitpointtargets);
                    copytargets.Id = $"{exitLocationData.Id}_ExitPointCounter".ConvertHashID();
                    copytargets.ExitName = exitLocationData.ExitPoint;
                    copycondition.Counter.Conditions.Add(copytargets);
                }
            }
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理到达指定商人信任度的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="reachTraderStandingData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitReachTraderStandingDataConditions(List<QuestCondition> conditions, ReachTraderStandingData reachTraderStandingData, DatabaseService databaseService, ICloner cloner)
        {
            var condition = databaseService.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForStart)
                .FirstOrDefault(c => c.ConditionType == "Level");
            if (condition == null) return;
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

        /// <summary>
        /// 处理到达指定商人信任等级的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="reachTraderTrustLevelData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitReachTraderTrustLevelDataConditions(List<QuestCondition> conditions, ReachTraderTrustLevelData reachTraderTrustLevelData, DatabaseService databaseService, ICloner cloner)
        {

            var condition = GetConditionTemplate(EQuestConditionsTypeCache.TraderLoyalty, "TraderLoyalty", databaseService);
            if (condition == null) return;
            var copycondition = cloner.Clone(condition);
            copycondition.Id = reachTraderTrustLevelData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.CompareMethod = ">=";
            copycondition.Target = new ListOrT<string>(null, reachTraderTrustLevelData.TraderId);
            copycondition.Value = (double)reachTraderTrustLevelData.TrustLevel;
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理到达指定技能等级的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="reachSkillLevelData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitReachSkillLevelDataConditions(List<QuestCondition> conditions, ReachSkillLevelData reachSkillLevelData, DatabaseService databaseService, ICloner cloner)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.Skill, "Skill", databaseService);
            if (condition == null) return;
            var copycondition = cloner.Clone(condition);
            copycondition.Id = reachSkillLevelData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.CompareMethod = ">=";
            copycondition.Target = new ListOrT<string>(null, reachSkillLevelData.SkillType);
            copycondition.Value = (double)reachSkillLevelData.Level;
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理完成指定任务的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="completeQuestData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitCompleteQuestDataConditions(List<QuestCondition> conditions, CompleteQuestData completeQuestData, DatabaseService databaseService, ICloner cloner)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.Quest, "Quest", databaseService);
            if (condition == null) return;
            var copycondition = cloner.Clone(condition);
            copycondition.Id = completeQuestData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            copycondition.Target = new ListOrT<string>(null, completeQuestData.QuestId);
            copycondition.Status = BitMapUtils.GetQuestStatusCode(completeQuestData.QuestStatus);
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理装饰封锁条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="customizationBlockData">自定义任务数据</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitCustomizationBlockDataConditions(List<QuestCondition> conditions, CustomizationBlockData customizationBlockData, DatabaseService databaseService, ICloner cloner)
        {
            cacheConditions.TryGetValue(EQuestConditionsTypeCache.Block, out var condition);
            if (condition == null)
            {
                condition = databaseService.GetHideout()
                 .Customisation
                 .Globals
                 .SelectMany(q => q.Conditions)
                 .FirstOrDefault(c => c.ConditionType == "Block");
                cacheConditions[EQuestConditionsTypeCache.Block] = condition;
            }
            if (condition == null) return;
            var copycondition = cloner.Clone(condition);
            copycondition.Id = customizationBlockData.Id;
            copycondition.Index = conditions.Count;
            copycondition.VisibilityConditions.Clear();
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 将自定义任务奖励注册到加载事件
        /// </summary>
        /// <param name="path">指定路径</param>
        /// <param name="creator">创建者</param>
        /// <param name="modname">Mod名</param>
        public static void RegisterQuestRewards(string path)
        {
            // 文件夹加载模式
            if (Directory.Exists(path))
            {
                EventManager.DataLoadEvent.LoadQuestRewardEvent += (context) =>
                {
                    try
                    {
                        //对应调用已有的文件夹重载方法
                        InitQuestRewards(path, context.DB, context.ModHelper, context.Cloner);
                        //EventManager.EventLogger.Info($"[{modname}] {creator} 的任务奖励模块(文件夹)注册成功");
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册任务奖励时发生错误：指定的文件夹 {path} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(path))
            {
                EventManager.DataLoadEvent.LoadQuestRewardEvent += (context) =>
                {
                    try
                    {
                        // 反序列化为 List 集合，对应已有的 List 重载方法
                        var rewardsData = context.JsonUtil.Deserialize<List<CustomQuestRewardData>>(File.ReadAllText(path));
                        InitQuestRewards(rewardsData, context.DB, context.Cloner);

                        //EventManager.EventLogger.Info($"[{modname}] {creator} 的任务奖励模块(单文件)注册成功");
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册任务奖励时发生错误：指定的文件 {path} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册任务奖励时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        //傻逼哈基米
        /// <summary>
        /// 从文件夹加载奖励的重载, 感觉没必要
        /// </summary>
        /// <param name="folderpath">路径</param>
        /// <param name="databaseService">数据库</param>
        /// <param name="modHelper">modHelper实例啦啦啦</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitQuestRewards(string folderpath, DatabaseService databaseService, ModHelper modHelper, ICloner cloner)
        {
            if (Directory.Exists(folderpath))
            {
                List<string> files = Directory.GetFiles(folderpath).ToList();
                if (files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileName(file);
                        var rewards = modHelper.GetJsonDataFromFile<List<CustomQuestRewardData>>(folderpath, fileName);

                        if (rewards != null)
                        {
                            InitQuestRewards(rewards, databaseService, cloner);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 加载任务奖励
        /// </summary>
        /// <param name="rewards">奖励List结构</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void InitQuestRewards(List<CustomQuestRewardData> rewards, DatabaseService databaseService, ICloner cloner)
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
                            InitAssortUnlockRewards(assortunlockreward, databaseService, cloner);
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
                        Id = Utils.ConvertHashID(recipeUnlockRewardData.Id),
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
        
        public static void InitAssortUnlockRewards(CustomAssortUnlockRewardData assortUnlockRewardData, DatabaseService databaseService, ICloner cloner)
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
                    AssortUtils.InitAssort((CustomAssortData)assortUnlockRewardData.AssortData, databaseService, cloner);
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
                    copyreward.Target = customPocketRewardData.TargetId;
                    target[queststage].Add(copyreward);
                }
            }
        }

        /// <summary>
        /// 将自定义任务逻辑树注册到加载事件
        /// </summary>
        /// <param name="path">指定的存放任务逻辑文件的路径或完整的任务逻辑文件路径</param>
        /// <param name="creator">创建者</param>
        /// <param name="modname">Mod名</param>
        public static void RegisterQuestLogicTree(string path)
        {
            // 文件夹加载模式
            if (Directory.Exists(path))
            {
                EventManager.DataLoadEvent.LoadQuestLogicEvent += (context) =>
                {
                    try
                    {
                        // 对应调用已有的文件夹重载方法
                        InitQuestLogicTreeData(path, context.DB, context.ModHelper, context.Cloner);
                        //EventManager.EventLogger.Info($"[{modname}] {creator} 的任务逻辑模块(文件夹)注册成功");
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册任务逻辑时发生错误：指定的文件夹 {path} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(path))
            {
                EventManager.DataLoadEvent.LoadQuestLogicEvent += (context) =>
                {
                    try
                    {
                        // 反序列化为字典字典，对应已有的 Dictionary 重载方法
                        var logicTreeData = context.JsonUtil.Deserialize<Dictionary<string, QuestLogicTree>>(File.ReadAllText(path));
                        InitQuestLogicTreeData(logicTreeData, context.DB, context.Cloner);

                        //EventManager.EventLogger.Info($"[{modname}] {creator} 的任务逻辑模块(单文件)注册成功");
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册任务逻辑时发生错误：指定的文件 {path} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册任务逻辑时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        /// <summary>
        /// Init重载 1：处理文件夹路径，遍历文件并解析单体数据
        /// </summary>
        public static void InitQuestLogicTreeData(string folderpath, DatabaseService databaseService, ModHelper modHelper, ICloner cloner)
        {
            if (Directory.Exists(folderpath))
            {
                List<string> files = Directory.GetFiles(folderpath).ToList();
                if (files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileName(file);
                        var logictree = modHelper.GetJsonDataFromFile<QuestLogicTree>(folderpath, fileName);

                        if (logictree != null)
                        {
                            InitQuestLogicTree(logictree, databaseService, cloner);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Init重载 2：处理实际的反序列化数据（字典形式处理）
        /// </summary>
        public static void InitQuestLogicTreeData(Dictionary<string, QuestLogicTree> questLogicTree, DatabaseService databaseService, ICloner cloner)
        {
            if (questLogicTree == null || questLogicTree.Count == 0) return;

            foreach (var data in questLogicTree)
            {
                if (data.Value != null)
                {
                    InitQuestLogicTree(data.Value, databaseService, cloner);
                }
            }
        }

        public static void InitQuestLogicTree(QuestLogicTree questLogicTree, DatabaseService databaseService, ICloner cloner)
        {
            var questTarget = GetQuest((string)questLogicTree.Id, databaseService);
            foreach (var quest in questLogicTree.PreQuestData)
            {
                var questid = Utils.ConvertHashID(quest.Key);
                InitCompleteQuestDataConditions(questTarget.Conditions.AvailableForStart, new CompleteQuestData
                {
                    Id = Utils.ConvertHashID($"{questLogicTree.Id}_PreQuest_{quest.Key}"),
                    QuestId = questid,
                    QuestStatus = quest.Value
                },
                databaseService, cloner);
            }
            foreach (var trader in questLogicTree.PreTraderStandingData)
            {
                var traderid = Utils.ConvertHashID(trader.Key);
                InitReachTraderStandingDataConditions(questTarget.Conditions.AvailableForStart, new ReachTraderStandingData
                {
                    Id = Utils.ConvertHashID($"{questLogicTree.Id}_PreTraderStanding_{trader.Key}"),
                    TraderId = traderid,
                    TrustStanding = trader.Value
                },
                databaseService, cloner);
            }
            foreach (var trader in questLogicTree.PreTraderTrustLevelData)
            {
                var traderid = Utils.ConvertHashID(trader.Key);
                InitReachTraderTrustLevelDataConditions(questTarget.Conditions.AvailableForStart, new ReachTraderTrustLevelData
                {
                    Id = Utils.ConvertHashID($"{questLogicTree.Id}_PreTraderTrustLevel_{trader.Key}"),
                    TraderId = traderid,
                    TrustLevel = trader.Value
                },
                databaseService, cloner);
            }
            if (questLogicTree.PrePlayerLevel > 0)
            {
                InitReachLevelDataConditions(questTarget.Conditions.AvailableForStart, new ReachLevelData
                {
                    Id = Utils.ConvertHashID($"{questLogicTree.Id}_PrePlayerLevel"),
                    Count = questLogicTree.PrePlayerLevel
                },
                databaseService, cloner);
            }
            if (questLogicTree?.PrePlayerPrestigeLevel > 0)
            {
                InitReachPrestigeLevelDataConditions(questTarget.Conditions.AvailableForStart, new ReachPrestigeLevelData
                {
                    Id = Utils.ConvertHashID($"{questLogicTree.Id}_PrePlayerPrestigeLevel"),
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
}