using HarmonyLib;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils.Json;
using static EternalCycleServer.ContextManager;
using Path = System.IO.Path;

namespace EternalCycleServer
{
    /// <summary>
    /// 对任务进行操作处理的工具类
    /// </summary>
    public static class QuestUtils
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
        /// <param name="context">上下文实例</param>
        /// <returns></returns>
        public static Quest? GetQuest(string questid, LoadModContext context)
        {
            if (context.DB.GetQuests().TryGetValue(questid, out var quest))
            {
                return quest;
            }
            return null;
        }

        /// <summary>
        /// 从序列化对象加载任务
        /// </summary>
        /// <param name="questData">任务数据</param>
        /// <param name="context">上下文实例</param>
        public static void InitQuestData(Dictionary<string, CustomQuest> questData, string respath, LoadModContext context)
        {
            foreach (var customquest in questData)
            {
                InitQuest(customquest.Value, respath, context);
            }
        }

        /// <summary>
        /// 从指定目录加载任务
        /// </summary>
        /// <param name="folderpath">文件夹路径</param>
        /// <param name="context">上下文实例</param>
        public static void InitQuestData(string folderpath, string respath, LoadModContext context)
        {
            List<string> files = Directory.GetFiles(folderpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var customquest = context.ModHelper.GetJsonDataFromFile<CustomQuest>(folderpath, fileName);
                    InitQuest(customquest, respath, context);
                }
            }
        }

        /// <summary>
        /// 从自定义结构序列化完整任务数据
        /// </summary>
        /// <param name="customQuest">自定义任务数据</param>
        /// <param name="context">上下文实例</param>
        public static void InitQuest(CustomQuest customQuest, string respath, LoadModContext context)
        {
            var questid = customQuest.QuestId;
            //短缺
            var pattern = GetQuest(QuestTpl.SHORTAGE, context);
            if (pattern == null) return; //怎么可能呢?
            Quest? questPattern = context.Cloner.Clone(pattern);
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
            //InitQuestConditions(questPattern.Conditions.AvailableForFinish, customQuest.QuestConditions.QuestFinishData, context);
            //InitQuestConditions(questPattern.Conditions.Fail, customQuest.QuestConditions.QuestFailedData, context);
            //临时
            context.DB.GetQuests().TryAdd(questid, questPattern);
            var imageRouter = ServiceLocator.ServiceProvider.GetService<ImageRouter>();
            ImageUtils.RegisterQuestRoute(questPattern.Image, Path.Combine(respath, "res/questimage/"), imageRouter);
            //为了完成原版兼容, 奖励定义有任务ID, 必须在任务初始化后添加
            //应该可以重载
            EventManager.DataLoadEvent.LoadQuestDataEvent += (eventContext) =>
            {
                try
                {
                    InitQuestConditions(questPattern.Conditions.AvailableForFinish, customQuest.QuestConditions.QuestFinishData, eventContext);
                    InitQuestConditions(questPattern.Conditions.Fail, customQuest.QuestConditions.QuestFailedData, eventContext);
                    //InitQuestRewards(customQuest.QuestRewards, eventContext);
                }
                catch (Exception ex)
                {
                    EventManager.EventLogger.Error($"注入任务数据层时发生异常：{questid}", ex);
                }
            };
            EventManager.DataLoadEvent.LoadQuestRewardEvent += (eventContext) =>
            {
                try
                {
                    InitQuestRewards(customQuest.QuestRewards, eventContext);
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
        /// <param name="context">上下文实例</param>
        public static void InitQuestConditions(List<QuestCondition> conditions, List<CustomQuestData> customquestdata, LoadModContext context)
        {
            var zhCNLang = context.DB.GetLocales().Global["ch"];
            foreach (CustomQuestData data in customquestdata)
            {
                switch (data)
                {
                    case FindItemData finditemdata:
                        {
                            InitFindItemDataConditions(conditions, finditemdata, context);
                        }
                        break;
                    case FindItemGroupData finditemgroupdata:
                        {
                            InitFindItemGroupDataConditions(conditions, finditemgroupdata, context);
                        }
                        break;
                    case HandoverItemData handitemdata:
                        {
                            InitHandoverItemDataConditions(conditions, handitemdata, context);
                        }
                        break;
                    case HandoverItemGroupData handitemgroupdata:
                        {
                            InitHandoverItemGroupDataConditions(conditions, handitemgroupdata, context);
                        }
                        break;
                    case KillTargetData killtargetdata:
                        {
                            InitKillTargetDataConditions(conditions, killtargetdata, context);
                        }
                        break;
                    case ReachLevelData reachleveldata:
                        {
                            InitReachLevelDataConditions(conditions, reachleveldata, context);
                        }
                        break;
                    case ReachPrestigeLevelData reachprestigeleveldata:
                        {
                            InitReachPrestigeLevelDataConditions(conditions, reachprestigeleveldata, context);
                        }
                        break;
                    case VisitPlaceData visitplacedata:
                        {
                            InitVisitPlaceDataConditions(conditions, visitplacedata, context);
                        }
                        break;
                    case PlaceItemData placeitemdata:
                        {
                            InitPlaceItemDataConditions(conditions, placeitemdata, context);
                        }
                        break;
                    case PlaceItemGroupData placeitemgroupdata:
                        {
                            InitPlaceItemGroupDataConditions(conditions, placeitemgroupdata, context);
                        }
                        break;
                    case ExitLocationData exitlocationdata:
                        {
                            InitExitLocationDataConditions(conditions, exitlocationdata, context);
                        }
                        break;
                    case ReachTraderStandingData reachtraderstandingdata:
                        {
                            InitReachTraderStandingDataConditions(conditions, reachtraderstandingdata, context);
                        }
                        break;
                    case ReachTraderTrustLevelData reachtradertrustleveldata:
                        {
                            InitReachTraderTrustLevelDataConditions(conditions, reachtradertrustleveldata, context);
                        }
                        break;
                    case ReachSkillLevelData reachskillleveldata:
                        {
                            InitReachSkillLevelDataConditions(conditions, reachskillleveldata, context);
                        }
                        break;
                    case CompleteQuestData completequestdata:
                        {
                            InitCompleteQuestDataConditions(conditions, completequestdata, context);
                        }
                        break;
                    case CustomizationBlockData customizationblockdata:
                        {
                            InitCustomizationBlockDataConditions(conditions, customizationblockdata, context);
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
                        InitQuestData(path, respath, context);
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
                        InitQuestData(questData, respath, context);

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
        /// <param name="context">上下文实例</param>
        /// <returns>返回一个任务条件模板</returns>
        public static QuestCondition GetConditionTemplate(EQuestConditionsTypeCache cacheType, string conditionTypeStr, LoadModContext context)
        {
            if (cacheConditions.TryGetValue(cacheType, out var condition) && condition != null)
            {
                return condition;
            }
            var foundCondition = context.DB.GetQuests()
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
        /// <param name="context">上下文实例</param>
        /// <returns>返回一个任务子条件模板</returns>
        public static QuestConditionCounterCondition GetCounterConditionTemplate(EQuestCountersCacheType cacheType, string conditionTypeStr, LoadModContext context)
        {
            if (cacheCounters.TryGetValue(cacheType, out var condition) && condition != null)
            {
                return condition;
            }
            var foundCondition = context.DB.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)
                .Where(c => c.ConditionType == "CounterCreator")
                .SelectMany(c => c.Counter.Conditions)
                .FirstOrDefault(c => c.ConditionType == conditionTypeStr);
            cacheCounters[cacheType] = foundCondition;
            return foundCondition;
        }

        /// <summary>
        /// 拓展方法, 定义任务基础数据, ID, 可选, 可见性, 内置本地化
        /// </summary>
        /// <param name="conditon"></param>
        /// <param name="questData"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static QuestCondition InitQuestConditionBase(this QuestCondition conditon, CustomQuestData questData, LoadModContext context)
        {
            var zhCNLang = context.DB.GetLocales().Global["ch"];
            conditon.Id = questData.Id;
            conditon?.VisibilityConditions?.Clear();
            if (questData.ParentVisible != null && questData.ParentVisible.Count>0)
            {
                //你为什么是个数组??
                //这玩意儿难道还支持拓展??
                //我chovy, 真支持
                //rnm
                foreach(var visible in questData.ParentVisible)
                {
                    var visibleid = visible.ConvertHashID();
                    conditon.VisibilityConditions = new List<VisibilityCondition>()
                    {
                        new VisibilityCondition()
                        {
                            ConditionType = "CompleteCondition",
                            Id = $"{questData.Id}_{visibleid}_VisibleConditions".ConvertHashID(),
                            Target = visibleid
                        }
                    };
                }
            }
            if (questData.ParentConditionsId != null)
            {
                conditon.ParentId = questData.ParentConditionsId;
            }
            if (questData.Locale != null)
            {
                zhCNLang.AddTransformer(lang =>
                {
                    lang[questData.Id] = questData.Locale;
                    return lang;
                });
            }
            return conditon;
        }

        /// <summary>
        /// 处理寻找物品任务条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="findItemData">自定义任务数据</param>
        /// <param name="context">上下文实例</param>
        public static void InitFindItemDataConditions(List<QuestCondition> conditions, FindItemData findItemData, LoadModContext context)
        {
            var zhCNLang = context.DB.GetLocales().Global["ch"];
            //缓存引用, 这里不可能空, 绿就绿吧, 无所谓了
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.FindItem, "FindItem", context);
            if (condition == null) return;
            //复制引用
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(findItemData, context);
            copycondition.OnlyFoundInRaid = findItemData.FindInRaid;
            copycondition.Index = conditions.Count;
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
        /// <param name="context">上下文实例</param>
        public static void InitFindItemGroupDataConditions(List<QuestCondition> conditions, FindItemGroupData findItemData, LoadModContext context)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.FindItem, "FindItem", context);
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(findItemData, context);
            copycondition.OnlyFoundInRaid = findItemData.FindInRaid;
            copycondition.Index = conditions.Count;
            copycondition.Target.List.GenerateFromTag(findItemData.Items, findItemData.UseTag, context);
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
        /// <param name="context">上下文实例</param>
        public static void InitHandoverItemDataConditions(List<QuestCondition> conditions, HandoverItemData handItemData, LoadModContext context)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.HandoverItem, "HandoverItem", context);
            if (condition == null) return;
            var zhCNLang = context.DB.GetLocales().Global["ch"];
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(handItemData, context);
            copycondition.OnlyFoundInRaid = handItemData.FindInRaid;
            copycondition.Index = conditions.Count;
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
        /// <param name="context">上下文实例</param>
        public static void InitHandoverItemGroupDataConditions(List<QuestCondition> conditions, HandoverItemGroupData handItemData, LoadModContext context)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.HandoverItem, "HandoverItem", context);
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(handItemData, context);
            copycondition.OnlyFoundInRaid = handItemData.FindInRaid;
            copycondition.Index = conditions.Count;
            copycondition.Target.List.GenerateFromTag(handItemData.Items, handItemData.UseTag, context);
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
        /// <param name="context">上下文实例</param>
        public static void InitKillTargetDataConditions(List<QuestCondition> conditions, KillTargetData killTargetData, LoadModContext context)
        {
            //多了一层所以不适用方法
            cacheConditions.TryGetValue(EQuestConditionsTypeCache.Elimination, out var condition);
            if (condition == null)
            {
                condition = context.DB.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)
                .FirstOrDefault(c => c.ConditionType == "CounterCreator" && c.Type == "Elimination");
                cacheConditions[EQuestConditionsTypeCache.Elimination] = condition;
            }
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(killTargetData, context);
            copycondition.Counter.Id = $"{killTargetData.Id}_Counter".ConvertHashID();
            copycondition.Counter.Conditions.Clear();
            copycondition.OneSessionOnly = killTargetData.CompleteInOneRaid;
            copycondition.Value = (double)killTargetData.Count;
            copycondition.Index = conditions.Count;
            var killtargets = GetCounterConditionTemplate(EQuestCountersCacheType.Kills, "Kills", context);
            var locationtargets = GetCounterConditionTemplate(EQuestCountersCacheType.Location, "Location", context);
            var equiptargets = GetCounterConditionTemplate(EQuestCountersCacheType.Equipment, "Equipment", context);
            var zonetargets = GetCounterConditionTemplate(EQuestCountersCacheType.InZone, "InZone", context);

            //需要新增装备需求
            //这玩意定义好弱智
            //草了, 还需要weaponmod解析
            if (killtargets != null)
            {
                var copytargets = context.Cloner.Clone(killtargets);
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
                copytargets.Weapon = new List<string>().GenerateFromTag(killTargetData.WeaponList, killTargetData.UseTag, context).ToHashSet();
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
                var copytargets = context.Cloner.Clone(locationtargets);
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
                    var copytargets = context.Cloner.Clone(equiptargets);
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
                var copytargets = context.Cloner.Clone(zonetargets);
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
        /// <param name="context">上下文实例</param>
        public static void InitReachLevelDataConditions(List<QuestCondition> conditions, ReachLevelData reachLevelData, LoadModContext context)
        {
            var condition = context.DB.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForStart)
                .FirstOrDefault(c => c.ConditionType == "Level");
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(reachLevelData, context);
            copycondition.Index = conditions.Count;
            copycondition.CompareMethod = ">=";
            if (reachLevelData.ParentVisible != null)
            {
                copycondition.ParentId = reachLevelData.ParentConditionsId;
            }
            copycondition.Value = (double)reachLevelData.Count;
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理达到转生等级条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="reachPrestigeLevelData">自定义任务数据</param>
        /// <param name="context">上下文实例</param>
        public static void InitReachPrestigeLevelDataConditions(List<QuestCondition> conditions, ReachPrestigeLevelData reachPrestigeLevelData, LoadModContext context)
        {
            var condition = context.DB.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForStart)
                .FirstOrDefault(c => c.ConditionType == "Level");
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(reachPrestigeLevelData, context);
            copycondition.ConditionType = "PrestigeLevel";
            copycondition.Index = conditions.Count;
            copycondition.CompareMethod = EnumUtils.GetCompareType(reachPrestigeLevelData.CompareType);
            copycondition.Value = (double)reachPrestigeLevelData.Level;
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理访问地点条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="visitPlaceData">自定义任务数据</param>
        /// <param name="context">上下文实例</param>
        public static void InitVisitPlaceDataConditions(List<QuestCondition> conditions, VisitPlaceData visitPlaceData, LoadModContext context)
        {
            cacheConditions.TryGetValue(EQuestConditionsTypeCache.Completion, out var condition);
            if (condition == null)
            {
                condition = context.DB.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)
                .FirstOrDefault(c => c.ConditionType == "CounterCreator" && c.Type == "Completion");
                cacheConditions[EQuestConditionsTypeCache.Completion] = condition;
            }
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(visitPlaceData, context);
            copycondition.Counter.Id = $"{visitPlaceData.Id}_Counter".ConvertHashID();
            copycondition.Counter.Conditions.Clear();
            copycondition.OneSessionOnly = visitPlaceData.CompleteInOneRaid;
            copycondition.Value = (double)1;
            copycondition.Index = conditions.Count;
            var visittargets = GetCounterConditionTemplate(EQuestCountersCacheType.VisitPlace, "VisitPlace", context);
            if (visittargets == null) return;
            var copytargets = context.Cloner.Clone(visittargets);
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
        /// <param name="context">上下文实例</param>
        public static void InitPlaceItemDataConditions(List<QuestCondition> conditions, PlaceItemData placeItemData, LoadModContext context)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.LeaveItemAtLocation, "LeaveItemAtLocation", context);
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(placeItemData, context);
            copycondition.Index = conditions.Count;
            copycondition.Target = new ListOrT<string>(new List<string>(), null);
            copycondition.Target.List.Add(placeItemData.ItemId);
            copycondition.Value = (double)placeItemData.Count;
            copycondition.PlantTime = (double)placeItemData.Time;
            copycondition.ZoneId = placeItemData.ZoneId;
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理在指定地点安放物品组条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="placeItemGroupData">自定义任务数据</param>
        /// <param name="context">上下文实例</param>
        public static void InitPlaceItemGroupDataConditions(List<QuestCondition> conditions, PlaceItemGroupData placeItemGroupData, LoadModContext context)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.LeaveItemAtLocation, "LeaveItemAtLocation", context);
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(placeItemGroupData, context);
            copycondition.Index = conditions.Count;
            copycondition.Target = new ListOrT<string>(new List<string>(), null);
            copycondition.Target.List.GenerateFromTag(placeItemGroupData.Items, placeItemGroupData.UseTag, context);
            copycondition.Value = (double)placeItemGroupData.Count;
            copycondition.PlantTime = (double)placeItemGroupData.Time;
            copycondition.ZoneId = placeItemGroupData.ZoneId;
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理从指定地图撤离的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="exitLocationData">自定义任务数据</param>
        /// <param name="context">上下文实例</param>
        public static void InitExitLocationDataConditions(List<QuestCondition> conditions, ExitLocationData exitLocationData, LoadModContext context)
        {
            cacheConditions.TryGetValue(EQuestConditionsTypeCache.Completion, out var condition);
            if (condition == null)
            {
                condition = context.DB.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForFinish)
                .FirstOrDefault(c => c.ConditionType == "CounterCreator" && c.Type == "Completion");
                cacheConditions[EQuestConditionsTypeCache.Completion] = condition;
            }
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(exitLocationData, context);
            copycondition.Counter.Id = $"{exitLocationData.Id}_Counter".ConvertHashID();
            copycondition.Counter.Conditions.Clear();
            copycondition.OneSessionOnly = exitLocationData.CompleteInOneRaid;
            copycondition.Value = (double)exitLocationData.Count;
            copycondition.Index = conditions.Count;
            var locationtargets = GetCounterConditionTemplate(EQuestCountersCacheType.Location, "Location", context);
            var exitstatustargets = GetCounterConditionTemplate(EQuestCountersCacheType.ExitStatus, "ExitStatus", context);
            if (locationtargets != null)
            {
                var copytargets = context.Cloner.Clone(locationtargets);
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
                var copytargets = context.Cloner.Clone(exitstatustargets);
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

                var exitpointtargets = GetCounterConditionTemplate(EQuestCountersCacheType.ExitName, "ExitName", context);
                if (exitpointtargets != null)
                {
                    var copytargets = context.Cloner.Clone(exitpointtargets);
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
        /// <param name="context">上下文实例</param>
        public static void InitReachTraderStandingDataConditions(List<QuestCondition> conditions, ReachTraderStandingData reachTraderStandingData, LoadModContext context)
        {
            var condition = context.DB.GetQuests()
                .SelectMany(q => q.Value.Conditions.AvailableForStart)
                .FirstOrDefault(c => c.ConditionType == "Level");
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(reachTraderStandingData, context);
            copycondition.Index = conditions.Count;
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
        /// <param name="context">上下文实例</param>
        public static void InitReachTraderTrustLevelDataConditions(List<QuestCondition> conditions, ReachTraderTrustLevelData reachTraderTrustLevelData, LoadModContext context)
        {

            var condition = GetConditionTemplate(EQuestConditionsTypeCache.TraderLoyalty, "TraderLoyalty", context);
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(reachTraderTrustLevelData, context);
            copycondition.Index = conditions.Count;
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
        /// <param name="context">上下文实例</param>
        public static void InitReachSkillLevelDataConditions(List<QuestCondition> conditions, ReachSkillLevelData reachSkillLevelData, LoadModContext context)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.Skill, "Skill", context);
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(reachSkillLevelData, context);
            copycondition.Index = conditions.Count;
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
        /// <param name="context">上下文实例</param>
        public static void InitCompleteQuestDataConditions(List<QuestCondition> conditions, CompleteQuestData completeQuestData, LoadModContext context)
        {
            var condition = GetConditionTemplate(EQuestConditionsTypeCache.Quest, "Quest", context);
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(completeQuestData, context);
            copycondition.Index = conditions.Count;
            copycondition.Target = new ListOrT<string>(null, completeQuestData.QuestId);
            copycondition.Status = BitMapUtils.GetQuestStatusCode(completeQuestData.QuestStatus);
            copycondition.AvailableAfter = completeQuestData?.AvailableAfterTime ?? 0;
            copycondition.Dispersion = completeQuestData?.AvailableAfterTimeRandomExtra ?? 0;
            conditions.Add(copycondition);
        }

        /// <summary>
        /// 处理装饰封锁条件的工具方法
        /// </summary>
        /// <param name="conditions">目标列表</param>
        /// <param name="customizationBlockData">自定义任务数据</param>
        /// <param name="context">上下文实例</param>
        public static void InitCustomizationBlockDataConditions(List<QuestCondition> conditions, CustomizationBlockData customizationBlockData, LoadModContext context)
        {
            cacheConditions.TryGetValue(EQuestConditionsTypeCache.Block, out var condition);
            if (condition == null)
            {
                condition = context.DB.GetHideout()
                 .Customisation
                 .Globals
                 .SelectMany(q => q.Conditions)
                 .FirstOrDefault(c => c.ConditionType == "Block");
                cacheConditions[EQuestConditionsTypeCache.Block] = condition;
            }
            if (condition == null) return;
            var copycondition = context.Cloner.Clone(condition).InitQuestConditionBase(customizationBlockData, context);
            copycondition.Index = conditions.Count;
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
                        InitQuestRewards(path, context);
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
                        InitQuestRewards(rewardsData, context);

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
        /// <param name="context">上下文实例</param>
        public static void InitQuestRewards(string folderpath, LoadModContext context)
        {
            if (Directory.Exists(folderpath))
            {
                List<string> files = Directory.GetFiles(folderpath).ToList();
                if (files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileName(file);
                        var rewards = context.ModHelper.GetJsonDataFromFile<List<CustomQuestRewardData>>(folderpath, fileName);

                        if (rewards != null)
                        {
                            InitQuestRewards(rewards, context);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 加载任务奖励
        /// </summary>
        /// <param name="rewards">奖励List结构</param>
        /// <param name="context">上下文实例</param>
        public static void InitQuestRewards(List<CustomQuestRewardData> rewards, LoadModContext context)
        {
            foreach (CustomQuestRewardData reward in rewards)
            {
                switch (reward)
                {
                    case CustomItemRewardData itemreward:
                        {
                            InitItemRewards(itemreward, context);
                        }
                        break;
                    case CustomAssortUnlockRewardData assortunlockreward:
                        {
                            InitAssortUnlockRewards(assortunlockreward, context);
                        }
                        break;
                    case CustomRecipeUnlockRewardData recipeunlockreward:
                        {
                            InitRecipeUnlockRewards(recipeunlockreward, context);
                        }
                        break;
                    case CustomExperienceRewardData experiencereward:
                        {
                            InitExperienceRewards(experiencereward, context);
                        }
                        break;
                    case CustomTraderStandingRewardData traderstandingreward:
                        {
                            InitTraderStandingRewards(traderstandingreward, context);
                        }
                        break;
                    case CustomCustomizationRewardData customizationreward:
                        {
                            InitCustomizationRewards(customizationreward, context);
                        }
                        break;
                    case CustomAchievementRewardData achievementreward:
                        {
                            InitAchievementRewards(achievementreward, context);
                        }
                        break;
                    case CustomTraderUnlockRewardData traderunlockreward:
                        {
                            InitTraderUnlockRewards(traderunlockreward, context);
                        }
                        break;
                    case CustomSkillExperienceRewardData skillexperiencereward:
                        {
                            InitSkillExperienceRewards(skillexperiencereward, context);
                        }
                        break;
                    case CustomPocketRewardData pocketreward:
                        {
                            InitPocketRewards(pocketreward, context);
                        }
                        break;
                    default:
                        {

                        }
                        break;
                }
            }
        }

        public static void InitItemRewards(CustomItemRewardData itemRewardData, LoadModContext context)
        {
            var queststage = EnumUtils.GetQuestStageType(itemRewardData.QuestStage);
            var rewardtarget = context.DB.GetQuests()
                .SelectMany(q => q.Value.Rewards[queststage])
                .FirstOrDefault(r => r.Type == RewardType.Item);
            if (!itemRewardData.IsAchievement)
            {
                var target = GetQuest(itemRewardData.QuestId, context).Rewards;
                if (target.Count > 0)
                {
                    if (rewardtarget != null)
                    {

                        var copyreward = GetItemReward(rewardtarget, target[queststage], itemRewardData, context);
                        target[queststage].Add(copyreward);
                    }
                }
            }
            else
            {
                var target = AchievementUtils.GetAchievement(itemRewardData.QuestId, context.DB).Rewards.ToList();
                if (rewardtarget != null)
                {
                    var copyreward = GetItemReward(rewardtarget, target, itemRewardData, context);
                    target.Add(copyreward);
                }
                AchievementUtils.GetAchievement(itemRewardData.QuestId, context.DB).Rewards = target;
            }
        }

        public static void InitRecipeUnlockRewards(CustomRecipeUnlockRewardData recipeUnlockRewardData, LoadModContext context)
        {
            //wip
            var queststage = EnumUtils.GetQuestStageType(recipeUnlockRewardData.QuestStage);
            var stringstage = queststage.ToString().ToLower();
            var questid = recipeUnlockRewardData.QuestId;
            var rewardid = recipeUnlockRewardData.Id;
            var rewardtarget = context.DB.GetQuests()
                .SelectMany(q => q.Value.Rewards[queststage])
                .FirstOrDefault(r => r.Type == RewardType.ProductionScheme);
            var target = GetQuest(questid, context).Rewards;
            if (target.Count > 0)
            {
                if (rewardtarget != null)
                {
                    var copyreward = InitCopiedReward(rewardtarget, target[queststage], recipeUnlockRewardData, context);
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
                    EventManager.DataLoadEvent.LoadLockedRecipeEvent += (eventContext) =>
                    {
                        RecipeUtils.InitRecipe(recipeUnlockRewardData.RecipeData, eventContext);
                    };
                }
            }
        }

        public static void InitAssortUnlockRewards(CustomAssortUnlockRewardData assortUnlockRewardData, LoadModContext context)
        {
            var queststage = EnumUtils.GetQuestStageType(assortUnlockRewardData.QuestStage);
            var stringstage = queststage.ToString().ToLower();
            var questid = assortUnlockRewardData.QuestId;
            var rewardid = assortUnlockRewardData.Id;
            var rewardtarget = context.DB.GetQuests()
                .SelectMany(q => q.Value.Rewards[queststage])
                .FirstOrDefault(r => r.Type == RewardType.AssortmentUnlock);
            var target = GetQuest(questid, context).Rewards;
            if (target.Count > 0)
            {
                if (rewardtarget != null)
                {
                    var copyreward = InitCopiedReward(rewardtarget, target[queststage], assortUnlockRewardData, context);
                    var assortitems = ItemUtils.ConvertItemListData(assortUnlockRewardData.AssortData.Item, context);
                    var items = ItemUtils.RegenerateItemListData(assortitems, (string)rewardid, context);
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
                    EventManager.DataLoadEvent.LoadLockedTraderAssortEvent += (eventContext) =>
                    {
                        AssortUtils.InitAssort(assortUnlockRewardData.AssortData, eventContext);
                    };
                    
                    TraderUtils.GetTrader(traderid, context.DB).QuestAssort[stringstage].Add(assortitems[0].Id, questid);
                }
            }
        }

        public static void InitExperienceRewards(CustomExperienceRewardData experienceRewardData, LoadModContext context)
        {
            var queststage = EnumUtils.GetQuestStageType(experienceRewardData.QuestStage);
            var rewardtarget = context.DB.GetQuests()
                .SelectMany(q => q.Value.Rewards[queststage])
                .FirstOrDefault(r => r.Type == RewardType.Experience);
            var target = GetQuest(experienceRewardData.QuestId, context).Rewards;
            if (target.Count > 0)
            {
                if (rewardtarget != null)
                {
                    var copyreward = InitCopiedReward(rewardtarget, target[queststage], experienceRewardData, context);
                    copyreward.Value = (double)experienceRewardData.Count; //死了妈的东西你就这么喜欢用double是吗
                    target[queststage].Add(copyreward);
                }
            }
        }

        public static void InitTraderStandingRewards(CustomTraderStandingRewardData traderStandingRewardData, LoadModContext context)
        {
            var queststage = EnumUtils.GetQuestStageType(traderStandingRewardData.QuestStage);
            var rewardtarget = context.DB.GetQuests()
                .SelectMany(q => q.Value.Rewards[queststage])
                .FirstOrDefault(r => r.Type == RewardType.TraderStanding);
            var target = GetQuest(traderStandingRewardData.QuestId, context).Rewards;
            if (target.Count > 0)
            {
                if (rewardtarget != null)
                {
                    var copyreward = InitCopiedReward(rewardtarget, target[queststage], traderStandingRewardData, context);
                    copyreward.Value = traderStandingRewardData.Count;
                    copyreward.Target = (string)traderStandingRewardData.TraderId;
                    target[queststage].Add(copyreward);
                }
            }
        }

        public static void InitCustomizationRewards(CustomCustomizationRewardData customiazationRewardData, LoadModContext context)
        {
            var queststage = EnumUtils.GetQuestStageType(customiazationRewardData.QuestStage);
            var achievements = context.DB.GetAchievements();
            var rewardtarget = context.DB.GetQuests()
                .SelectMany(q => q.Value.Rewards[queststage])
                .FirstOrDefault(r => r.Type == RewardType.CustomizationDirect);
            if (!customiazationRewardData.IsAchievement)
            {
                var target = GetQuest(customiazationRewardData.QuestId, context).Rewards;
                if (target.Count > 0)
                {
                    if (rewardtarget != null)
                    {
                        var copyreward = GetCustomizationReward(rewardtarget, target[queststage], customiazationRewardData, context);
                        target[queststage].Add(copyreward);
                    }
                }
            }
            else
            {
                var target = AchievementUtils.GetAchievement(customiazationRewardData.QuestId, context.DB).Rewards.ToList();
                if (rewardtarget != null)
                {
                    var copyreward = GetCustomizationReward(rewardtarget, target, customiazationRewardData, context);
                    target.Add(copyreward);
                }
                AchievementUtils.GetAchievement(customiazationRewardData.QuestId, context.DB).Rewards = target;
            }
        }

        public static void InitAchievementRewards(CustomAchievementRewardData achievementRewardData, LoadModContext context)
        {
            var queststage = EnumUtils.GetQuestStageType(achievementRewardData.QuestStage);
            var rewardtarget = context.DB.GetQuests()
                .SelectMany(q => q.Value.Rewards[queststage])
                .FirstOrDefault(r => r.Type == RewardType.Achievement);
            var target = GetQuest(achievementRewardData.QuestId, context).Rewards;
            if (target.Count > 0)
            {
                if (rewardtarget != null)
                {
                    var copyreward = InitCopiedReward(rewardtarget, target[queststage], achievementRewardData, context);
                    copyreward.Target = (string)achievementRewardData.TargetId;
                    target[queststage].Add(copyreward);
                }
            }
        }

        public static void InitTraderUnlockRewards(CustomTraderUnlockRewardData traderUnlockRewardData, LoadModContext context)
        {
            var queststage = EnumUtils.GetQuestStageType(traderUnlockRewardData.QuestStage);
            var rewardtarget = context.DB.GetQuests()
                .SelectMany(q => q.Value.Rewards[queststage])
                .FirstOrDefault(r => r.Type == RewardType.TraderUnlock);
            var target = GetQuest(traderUnlockRewardData.QuestId, context).Rewards;
            if (target.Count > 0)
            {
                if (rewardtarget != null)
                {
                    var copyreward = InitCopiedReward(rewardtarget, target[queststage], traderUnlockRewardData, context);
                    copyreward.Target = (string)traderUnlockRewardData.TraderId;
                    target[queststage].Add(copyreward);
                }
            }
        }

        public static void InitSkillExperienceRewards(CustomSkillExperienceRewardData skillExperienceRewardData, LoadModContext context)
        {
            var queststage = EnumUtils.GetQuestStageType(skillExperienceRewardData.QuestStage);
            var rewardtarget = context.DB.GetQuests()
                .SelectMany(q => q.Value.Rewards[queststage])
                .FirstOrDefault(r => r.Type == RewardType.Skill);
            var target = GetQuest(skillExperienceRewardData.QuestId, context).Rewards;
            if (target.Count > 0)
            {
                if (rewardtarget != null)
                {
                    var copyreward = InitCopiedReward(rewardtarget, target[queststage], skillExperienceRewardData, context);
                    copyreward.Target = (string)skillExperienceRewardData.SkillType;
                    copyreward.Value = (double)(skillExperienceRewardData.Count * 100);
                    target[queststage].Add(copyreward);
                }
            }
        }

        public static void InitPocketRewards(CustomPocketRewardData customPocketRewardData, LoadModContext context)
        {
            var queststage = EnumUtils.GetQuestStageType(customPocketRewardData.QuestStage);
            var rewardtarget = context.DB.GetQuests()
                .SelectMany(q => q.Value.Rewards[queststage])
                .FirstOrDefault(r => r.Type == RewardType.Pockets);
            var target = GetQuest(customPocketRewardData.QuestId, context).Rewards;
            if (target.Count > 0)
            {
                if (rewardtarget != null)
                {
                    var copyreward = InitCopiedReward(rewardtarget, target[queststage], customPocketRewardData, context);
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
                        InitQuestLogicTreeData(path, context);
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
                        InitQuestLogicTreeData(logicTreeData, context);

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
        public static void InitQuestLogicTreeData(string folderpath, LoadModContext context)
        {
            if (Directory.Exists(folderpath))
            {
                List<string> files = Directory.GetFiles(folderpath).ToList();
                if (files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileName(file);
                        var logictree = context.ModHelper.GetJsonDataFromFile<QuestLogicTree>(folderpath, fileName);

                        if (logictree != null)
                        {
                            InitQuestLogicTree(logictree, context);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Init重载 2：处理实际的反序列化数据（字典形式处理）
        /// </summary>
        public static void InitQuestLogicTreeData(Dictionary<string, QuestLogicTree> questLogicTree, LoadModContext context)
        {
            if (questLogicTree == null || questLogicTree.Count == 0) return;

            foreach (var data in questLogicTree)
            {
                if (data.Value != null)
                {
                    InitQuestLogicTree(data.Value, context);
                }
            }
        }

        public static void InitQuestLogicTree(QuestLogicTree questLogicTree, LoadModContext context)
        {
            var questTarget = GetQuest((string)questLogicTree.Id, context);
            foreach (var quest in questLogicTree.PreQuestData)
            {
                var questid = Utils.ConvertHashID(quest.Key);
                InitCompleteQuestDataConditions(questTarget.Conditions.AvailableForStart, new CompleteQuestData
                {
                    Id = Utils.ConvertHashID($"{questLogicTree.Id}_PreQuest_{quest.Key}"),
                    QuestId = questid,
                    QuestStatus = quest.Value.PreQuestState,
                    AvailableAfterTime = quest.Value.AvailableAfterTime,
                    AvailableAfterTimeRandomExtra = quest.Value.AvailableAfterTimeRandomExtra
                },
                context);
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
                context);
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
                context);
            }
            if (questLogicTree.PrePlayerLevel > 0)
            {
                InitReachLevelDataConditions(questTarget.Conditions.AvailableForStart, new ReachLevelData
                {
                    Id = Utils.ConvertHashID($"{questLogicTree.Id}_PrePlayerLevel"),
                    Count = questLogicTree.PrePlayerLevel
                },
                context);
            }
            if (questLogicTree?.PrePlayerPrestigeLevel > 0)
            {
                InitReachPrestigeLevelDataConditions(questTarget.Conditions.AvailableForStart, new ReachPrestigeLevelData
                {
                    Id = Utils.ConvertHashID($"{questLogicTree.Id}_PrePlayerPrestigeLevel"),
                    CompareType = questLogicTree.PrestigeCompareType ?? 3,
                    Level = (int)questLogicTree.PrePlayerPrestigeLevel
                },
                context);
            }
        }

        public static Reward InitCopiedReward(Reward reward, List<Reward> target, CustomQuestRewardData rewardData, LoadModContext context)
        {
            var copyreward = context.Cloner.Clone(reward);
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

        public static Reward GetItemReward(Reward rewardtarget, List<Reward> target, CustomItemRewardData itemRewardData, LoadModContext context)
        {
            var copyreward = InitCopiedReward(rewardtarget, target, itemRewardData, context);
            var items = ItemUtils.ConvertItemListData(itemRewardData.Items, context);
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

        public static Reward GetCustomizationReward(Reward rewardtarget, List<Reward> target, CustomCustomizationRewardData customizationRewardData, LoadModContext context)
        {
            var copyreward = InitCopiedReward(rewardtarget, target, customizationRewardData, context);
            copyreward.Target = (string)customizationRewardData.TargetId;
            return copyreward;
        }

        public static List<string> GenerateFromTag(this List<string> list, List<string> itemlist, ItemTag tag, LoadModContext context)
        {
            var listset = list?.ToHashSet() ?? new HashSet<string>();
            var cacheset = new ItemTag();
            if (tag != null && tag.Count > 0) 
            { 
                cacheset.UnionWith(ItemTagUtils.GetTagList(tag));
            }
            if (itemlist != null && itemlist.Count > 0)
            {
                foreach (var item in itemlist)
                {
                    cacheset.Add(item.ConvertHashID());
                }
            }
            foreach (var item in cacheset)
            {
                try
                {
                    listset.Add((MongoId)item);
                }
                catch (Exception ex)
                {
                    context.Logger.Warn($"发现到无效的物品 ID: '{item}'。已跳过该物品。请检查你的任务或标签配置文件！");
                }
            }
            if (list != null)
            { 
                list.AddRange(listset);
            }
            return list;
        }
    }
}