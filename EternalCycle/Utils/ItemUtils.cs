using HarmonyLib.Tools;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging;
using SPTarkov.Common.Extensions;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System.Reflection;

namespace EternalCycle
{
    /// <summary>
    /// 对物品进行操作处理的工具类
    /// </summary>
    public static class ItemUtils
    {
        /// <summary>
        /// 用于物品兼容性修复的哈希表
        /// </summary>
        public static Dictionary<MongoId, List<CustomFixData>> FixDict = new Dictionary<MongoId, List<CustomFixData>>();

        /// <summary>
        /// 固定可打开包裹数据
        /// </summary>
        public static Dictionary<MongoId, StaticGiftBoxData> StaticBoxData = new Dictionary<MongoId, StaticGiftBoxData>();

        /// <summary>
        /// 特殊可打开包裹数据(技能, 好感度, etc)
        /// </summary>
        public static Dictionary<MongoId, List<GiftData>> SpecialBoxData = new Dictionary<MongoId, List<GiftData>>();

        /// <summary>
        /// 高级可打开包裹数据(米池抽卡)
        /// </summary>
        public static Dictionary<MongoId, AdvancedGiftBoxData> AdvancedBoxData = new Dictionary<MongoId, AdvancedGiftBoxData>();

        /// <summary>
        /// 卡池数据
        /// </summary>
        public static Dictionary<string, DrawPoolClass> DrawPoolData = new Dictionary<string, DrawPoolClass>();
        public static bool firstlogin = false;

        /// <summary>
        /// 当前Mod目录, 这东西是不是也应该挪到CommonUtils里去?
        /// 好像只在卡池读写用了, 那就不挪了....吧
        /// </summary>
        public static string modPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        /// <summary>
        /// 从数据库返回某个物品的引用
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="databaseService">数据库服务实例</param>
        /// <returns></returns>
        public static TemplateItem? GetItem(string itemid, DatabaseService databaseService)
        {
            if (databaseService.GetItems().TryGetValue(itemid, out var item))
            {
                return item;
            }
            return null;
        }

        /// <summary>
        /// 从数据库返回指定物品的手册分类
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="databaseService">数据库服务实例</param>
        /// <returns></returns>
        public static MongoId? GetItemRagfairTag(string itemid, DatabaseService databaseService)
        {
            var targetId = itemid;
            var handbook = databaseService.GetHandbook();
            var item = handbook.Items.FirstOrDefault(x => x.Id == targetId);
            return item?.ParentId;
        }

        /// <summary>
        /// 从字典对象加载Mod物品
        /// </summary>
        /// <param name="items">字典对象</param>
        /// <param name="creator">创建者字段</param>
        /// <param name="modname">Mod名字段</param>
        /// <param name="databaseService">数据库服务实例</param>
        /// <param name="configServer">配置服务实例</param>
        /// <param name="cloner">克隆器接口实例</param>
        public static void InitItem(Dictionary<string, CustomItemTemplate> items, string creator, string modname, DatabaseService databaseService, ConfigServer configServer, ICloner cloner)
        {
            foreach (var item in items)
            {
                CreateAndAddItem(item.Value, item.Value.TargetId, creator, modname, databaseService, configServer, cloner);
            }
        }

        /// <summary>
        /// 从指定文件加载Mod物品
        /// </summary>
        /// <param name="folderPath"></param>
        /// <param name="creator">创建者字段</param>
        /// <param name="modname">Mod名字段</param>
        /// <param name="databaseService">数据库服务实例</param>
        /// <param name="jsonUtil">json序列化器实例</param>
        /// <param name="configServer">配置服务实例</param>
        /// <param name="cloner">克隆器接口实例</param>
        public static void InitItem(string folderPath, string creator, string modname, DatabaseService databaseService, JsonUtil jsonUtil, ConfigServer configServer, ICloner cloner)
        {
            List<string> files = Directory.GetFiles(folderPath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileContent = File.ReadAllText(file);
                    //string processedJson = Utils.RemoveJsonComments(fileContent);
                    var item = Utils.ConvertItemData<CustomItemTemplate>(fileContent, jsonUtil);
                    CreateAndAddItem(item, item.TargetId, creator, modname, databaseService, configServer, cloner);
                }
            }
        }

        /// <summary>
        /// 创建并添加一个物品
        /// </summary>
        /// <param name="template">需要加载的物品对象</param>
        /// <param name="targetid">复制的物品目标ID</param>
        /// <param name="creator">创建者</param>
        /// <param name="modname">Mod名字</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="configServer">配置实例</param>
        /// <param name="cloner">克隆器实例</param>
        public static void CreateAndAddItem(CustomItemTemplate template, string targetid, string creator, string modname, DatabaseService databaseService, ConfigServer configServer, ICloner cloner)
        {
            //需要添加一个验证器, 实现覆盖和加载双模
            //已经有了
            //转换真实ID
            var itemid = template.Id.ConvertHashID();
            template.Id = itemid;
            //检查字典
            TemplateItem itemClone;
            var itemOriginal = GetItem(itemid, databaseService);
            if (itemOriginal != null)
            {
                itemClone = itemOriginal;
            }
            else
            {
                itemClone = cloner.Clone(GetItem(targetid, databaseService));
            }
            //参数覆盖
            Utils.CopyNonNullProperties(template.Props, itemClone.Properties);
            //参数覆盖
            SetItemBaseData(template, itemClone);
            //总之上面这两条是肯定要做的
            //问题是下面咋改....没思路啊, 唉
            //要给我自己的类型增加拓展方法吗?
            //那还得给原版也加上
            //很烦
            //主要是这些玩意不是需要实例就是需要实例....
            //唉
            //我讨厌DI
            var _inventoryConfig = configServer.GetConfig<InventoryConfig>();
            //自定义货币处理
            if (template.CustomProps.IsMoney && !_inventoryConfig.CustomMoneyTpls.Contains(itemid))
            {
                _inventoryConfig.CustomMoneyTpls.Add(itemid);
            }
            //改吧, 改吧, 来都来了
            //Buff物品处理
            template
                .AddBuffItemData(configServer, databaseService)
                .AddBlackList(configServer)
                .SetInRaidLimitCount(databaseService)
                .SetCustomPMCDogTag(configServer)
                .AddPriceData(databaseService)
                .AddWeaponItemData(databaseService)
                .AddQuestItemGenerate(databaseService)
                .SetContainerSize(itemClone, databaseService)
                .SetGiftBoxData(configServer)
                .AddStaticLoot(databaseService)
                .AddLooseLoot(databaseService)
                .AddItemFixData();

            //本地化数据
            LocaleUtils.AddItemToLocales(LocaleUtils.BuildItemLocales(template.CustomProps, creator, modname), itemid, databaseService);
            //尝试添加物品
            //在非空情况下itemClone直接就是来自物品表的引用, 因此无需覆盖更新
            if (itemOriginal == null) databaseService.GetItems().TryAdd(itemid, itemClone);
            //Kappa
            if (template.CustomProps.AddToKappa == true)
            {
                AddItemToKappa(template, databaseService, cloner);
            }
            Utils.commonLogger.Debug($"物品添加成功: {template.CustomProps.Name}");
        }

        /// <summary>
        /// 将自定义物品注册到加载事件
        /// </summary>
        /// <param name="path">指定的存放单一物品文件的路径或完整的物品文件路径</param>
        /// <param name="creator">创建者</param>
        /// <param name="modname">Mod名</param>
        public static void RegisterItem(string path, string creator, string modname)
        {
            //文件夹
            if (Directory.Exists(path))
            {
                EventManager.DataLoadEvent.LoadItemEvent += (context) =>
                {
                    InitItem(path, creator, modname, context.DB, context.JsonUtil, context.ConfigServer, context.Cloner);
                };
            }
            //单文件
            else if (File.Exists(path))
            {
                EventManager.DataLoadEvent.LoadItemEvent += (context) =>
                {
                    try
                    {
                        //var item = context.JsonUtil.Deserialize<Dictionary<string, CustomItemTemplate>>(File.ReadAllText(path));
                        //var item = context.ModHelper.GetJsonDataFromFile<Dictionary<string, CustomItemTemplate>>("", path);
                        var item = Utils.ConvertItemData("", path, context.JsonUtil);
                        InitItem(item, creator, modname, context.DB, context.ConfigServer, context.Cloner);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册物品时发生错误：指定的文件 {path} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册物品时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        //这个也得大改....
        //所有加载器计划变更为事件统一点
        //物品-任务-商人-预设-任务逻辑-任务奖励-报价单-配方
        //大概就是这样, Kappa涉及到任务数据所以放在物品后
        //这玩意调用了QuestUtils....先放着吧
        public static void AddItemToKappa(CustomItemTemplate item, DatabaseService databaseService, ICloner cloner)
        {
            var kappa = QuestUtils.GetQuest(QuestTpl.COLLECTOR, databaseService);
            var twitchcase = GetItem(ItemTpl.CONTAINER_STREAMER_ITEM_CASE, databaseService);
            var conditions = kappa.Conditions.AvailableForFinish;
            var itemid = Utils.ConvertHashID(item.Id);
            QuestUtils.InitHandoverItemDataConditions(conditions, new HandoverItemData
            {
                Id = Utils.ConvertHashID($"Kappa_{item.Id}"),
                FindInRaid = true,
                ItemId = itemid,
                Count = 1,
                AutoLocale = true
            },
            databaseService, cloner);
            var twitchcasecontainer = twitchcase.Properties.Grids.First().Properties.Filters.First().Filter;
            if (!twitchcasecontainer.Contains(itemid))
            {
                twitchcasecontainer.Add(itemid);
            }
        }

        /// <summary>
        /// 处理自定义物品的黑名单数据
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="configServer">配置实例</param>
        /// <returns>自定义物品对象</returns>
        public static CustomItemTemplate AddBlackList(this CustomItemTemplate template, ConfigServer configServer)
        {
            if (template.CustomProps?.BlackListType != null)
            {
                string itemid = template.Id;
                AddBlackList(itemid, template.CustomProps.BlackListType, configServer);
            }
            return template;
        }

        /// <summary>
        /// 为指定ID的物品处理黑名单数据
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="blacklistType">黑名单位图</param>
        /// <param name="configServer">配置实例</param>
        public static void AddBlackList(string itemid, int blacklistType, ConfigServer configServer)
        {
            List<string> blacklist = BitMapUtils.GetBlackListCode(blacklistType);
            foreach (string black in blacklist)
            {
                switch (black)
                {
                    case "AirDrop":
                        {
                            AddAirDropBlackList(itemid, configServer);
                        }
                        break;
                    case "PMCLoot":
                        {
                            AddPMCLootBlackList(itemid, configServer);
                        }
                        break;
                    case "ScavCaseLoot":
                        {
                            AddScavCaseLootBlackList(itemid, configServer);
                        }
                        break;
                    case "Fence":
                        {
                            AddFenceBlackList(itemid, configServer);
                        }
                        break;
                    case "Circle":
                        {
                            AddCircleBlackList(itemid, configServer);
                        }
                        break;
                    case "DailyReward":
                        {
                            AddDailyRewardBlackList(itemid, configServer);
                        }
                        break;
                    case "Global":
                        {
                            AddGlobalBlackList(itemid, configServer);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 处理黑名单的工具方法
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="configserver">配置实例</param>
        public static void AddAirDropBlackList(string itemid, ConfigServer configserver)
        {
            AirdropConfig lootConfig = configserver.GetConfig<AirdropConfig>();
            foreach (AirdropLoot loot in lootConfig.Loot.Values)
            {
                //你TM为什么是List呢?!
                if (!loot.ItemBlacklist.Contains(itemid)) loot.ItemBlacklist.Add(itemid);
            }
        }

        /// <summary>
        /// 处理黑名单的工具方法
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="configserver">配置实例</param>
        public static void AddPMCLootBlackList(string itemid, ConfigServer configserver)
        {
            PmcConfig lootConfig = configserver.GetConfig<PmcConfig>();
            //HashSet, 因此可以直接Add, 无需查重
            lootConfig.VestLoot.Blacklist.Add(itemid);
            lootConfig.PocketLoot.Blacklist.Add(itemid);
            lootConfig.BackpackLoot.Blacklist.Add(itemid);
        }

        /// <summary>
        /// 处理黑名单的工具方法
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="configserver">配置实例</param>
        public static void AddScavCaseLootBlackList(string itemid, ConfigServer configserver)
        {
            ScavCaseConfig lootConfig = configserver.GetConfig<ScavCaseConfig>();
            lootConfig.RewardItemBlacklist.Add(itemid);
        }

        /// <summary>
        /// 处理黑名单的工具方法
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="configserver">配置实例</param>
        public static void AddFenceBlackList(string itemid, ConfigServer configserver)
        {
            TraderConfig lootConfig = configserver.GetConfig<TraderConfig>();
            lootConfig.Fence.Blacklist.Add(itemid);
        }

        /// <summary>
        /// 处理黑名单的工具方法
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="configserver">配置实例</param>
        public static void AddCircleBlackList(string itemid, ConfigServer configserver)
        {
            HideoutConfig lootConfig = configserver.GetConfig<HideoutConfig>();
            lootConfig.CultistCircle.RewardItemBlacklist.Add(itemid);
        }

        /// <summary>
        /// 处理黑名单的工具方法
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="configserver">配置实例</param>
        public static void AddDailyRewardBlackList(string itemid, ConfigServer configserver)
        {
            QuestConfig questConfig = configserver.GetConfig<QuestConfig>();
            questConfig.RepeatableQuests.ForEach(type => type.RewardBlacklist.Add(itemid));
        }

        /// <summary>
        /// 处理黑名单的工具方法
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="configserver">配置实例</param>
        public static void AddGlobalBlackList(string itemid, ConfigServer configserver)
        {
            ItemConfig itemConfig = configserver.GetConfig<ItemConfig>();
            itemConfig.RewardItemBlacklist.Add(itemid);
        }

        /// <summary>
        /// 为自定义物品修复Buff数据
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="configserver">配置实例</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>自定义物品对象</returns>
        public static CustomItemTemplate AddBuffItemData(this CustomItemTemplate template, ConfigServer configserver, DatabaseService databaseService)
        {
            Globals globals = databaseService.GetGlobals();
            if (template.CustomProps is BuffItemProps itemProps && template.Props.StimulatorBuffs != null)
            {
                globals.Configuration.Health.Effects.Stimulator.Buffs[template.Props.StimulatorBuffs] = itemProps.BuffValue;
            }
            return template;
        }

        /// <summary>
        /// 为物品初始化兼容修复数据
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <returns>自定义物品对象</returns>
        public static CustomItemTemplate AddItemFixData(this CustomItemTemplate template)
        {
            if (template.CustomProps is CustomFixedItemProps itemProps && itemProps.FixType != null)
            {
                //已调整为新的字典逻辑
                var itemid = template.Id.ConvertHashID();
                MongoId targetid = itemProps.CustomFixID != null ? (MongoId)itemProps.CustomFixID : template.TargetId;
                var customFixData = new CustomFixData
                {
                    FixType = itemProps.FixType,
                    ItemId = itemid
                };
                FixDict.TryGetValue(targetid, out var list);
                if (list == null) FixDict.Add(targetid, new List<CustomFixData>() { customFixData });
                else
                {
                    list.Add(customFixData);
                }
                //if(FixDict.FirstOrDefault(x=>x.ItemId == itemid)==null) FixDict.Add(customFixData);
            }
            return template;
        }

        /// <summary>
        /// 为自定义物品增加手册标签和价格
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>自定义物品对象</returns>
        public static CustomItemTemplate AddPriceData(this CustomItemTemplate template, DatabaseService databaseService)
        {
            if (template.CustomProps == null) return template;
            var props = template.CustomProps;
            string itemid = template.Id.ConvertHashID();
            string targetid = template.TargetId;
            //处理手册
            var handbookList = databaseService.GetHandbook().Items;
            var targetHandbook = handbookList.FirstOrDefault(x => x.Id == targetid);
            var myHandbook = handbookList.FirstOrDefault(x => x.Id == itemid);
            //查价格
            var handbookPrice = (template.CustomProps.CopyPrice == true && targetHandbook != null)
                ? targetHandbook?.Price ?? 0
                : (double)template.CustomProps.DefaultPrice;
            //回退手册Id
            string ragfairTag = string.IsNullOrEmpty(props.RagfairType)
                ? (myHandbook?.ParentId ?? ERagfairTagsType.其他)
                : props.RagfairType.ConvertHashID();

            if (myHandbook == null)
            {
                //新增
                handbookList.Add(new HandbookItem
                {
                    Id = itemid,
                    ParentId = ragfairTag,
                    Price = handbookPrice
                });
            }
            else
            {
                //覆盖
                if (!string.IsNullOrEmpty(ragfairTag)) myHandbook.ParentId = ragfairTag;
                myHandbook.Price = handbookPrice;
            }
            //处理价格表
            var pricesDict = databaseService.GetPrices();
            double finalRagfairPrice;
            //再次判断逻辑
            if (props.CopyPrice == true && pricesDict.TryGetValue(targetid, out var targetPrice))
            {
                finalRagfairPrice = targetPrice;
            }
            else if (props.RagfairPrice != null)
            {
                finalRagfairPrice = (double)props.RagfairPrice;
            }
            else
            {
                finalRagfairPrice = (double)template.CustomProps.DefaultPrice;
            }
            //覆盖
            pricesDict[itemid] = finalRagfairPrice;
            return template;
        }

        /// <summary>
        /// 复制物品基本数据
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="item">原版物品对象</param>
        public static void SetItemBaseData(CustomItemTemplate template, TemplateItem item)
        {
            item.Id = template.Id;
            item.Parent = template.ParentId != null ? template.ParentId : item.Parent;
            if (item.Prototype != null)
            {
                item.Prototype = template.Prototype != null ? template.Prototype : item.Prototype;
            }
            item.Type = template.Type != null ? template.Type : item.Type;
        }

        /// <summary>
        /// 为自定义物品调整主容器大小
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="itemTemplate">物品引用实例</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>自定义物品对象</returns>
        public static CustomItemTemplate SetContainerSize(this CustomItemTemplate template, TemplateItem itemTemplate, DatabaseService databaseService)
        {
            if (template.CustomProps is CustomSizeContainerProps itemProps)
            {
                var grid = itemTemplate.Properties.Grids.FirstOrDefault();
                grid.Properties.CellsH = itemProps.ContainerCellsH;
                grid.Properties.CellsV = itemProps.ContainerCellsV;
            }
            return template;
        }

        /// <summary>
        /// 为自定义物品设置武器数据(专精)
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>自定义物品对象</returns>
        public static CustomItemTemplate AddWeaponItemData(this CustomItemTemplate template, DatabaseService databaseService)
        {
            if (template.CustomProps is WeaponItemProps itemProps)
            {
                if (itemProps?.FixMastering == true) FixWeaponMastering(template, itemProps, databaseService);
                if (itemProps?.AddMastering == true) AddWeaponMastering(template, itemProps, databaseService);
            }
            return template;
        }

        /// <summary>
        /// 为自定义物品修复专精数据
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="itemProps">多态序列化后的武器物品数据</param>
        /// <param name="databaseService">数据库实例</param>
        public static void FixWeaponMastering(CustomItemTemplate template, WeaponItemProps itemProps, DatabaseService databaseService)
        {
            Globals globals = databaseService.GetGlobals();
            var itemId = template.Id.ConvertHashID();
            //确定修复目标
            string targetToFind = itemProps.CustomMasteringTarget ?? template.TargetId;

            foreach (Mastering mastering in globals.Configuration.Mastering)
            {
                if (mastering.Templates.Contains(targetToFind))
                {
                    if (!mastering.Templates.Contains(itemId))
                    {
                        List<MongoId> list = mastering.Templates?.ToList() ?? new List<MongoId>();
                        list.Add(itemId);
                        mastering.Templates = list;
                    }
                }
            }
        }

        /// <summary>
        /// 为自定义物品新增专精
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="itemProps">多态序列化后的武器物品数据</param>
        /// <param name="databaseService">数据库实例</param>
        public static void AddWeaponMastering(CustomItemTemplate template, WeaponItemProps itemProps, DatabaseService databaseService)
        {
            if (itemProps.Mastering == null) return;

            Globals globals = databaseService.GetGlobals();
            int existingIndex = Array.FindIndex(globals.Configuration.Mastering, m => m.Name == itemProps.Mastering.Name);
            if (existingIndex >= 0)
            {
                //覆盖
                globals.Configuration.Mastering[existingIndex] = itemProps.Mastering;
            }
            else
            {
                //新增
                globals.Configuration.Mastering = Utils.AddToArray(globals.Configuration.Mastering, itemProps.Mastering);
            }
        }

        /// <summary>
        /// 为自定义物品添加任务物品刷新
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>自定义物品对象</returns>
        public static CustomItemTemplate AddQuestItemGenerate(this CustomItemTemplate template, DatabaseService databaseService)
        {
            if (template.CustomProps is QuestItemProps questItemProps)
            {
                //提取数据, 定位地图
                var spawnpoint = questItemProps.SpawnPointData;
                var looseloot = databaseService.GetLocation(spawnpoint.Location)?.LooseLoot;
                if (looseloot != null)
                {
                    //对战利品执行懒加载
                    looseloot.AddTransformer(loostLoot =>
                    {
                        //获取物品根节点
                        spawnpoint.Template.Root = spawnpoint.Template.Root.ConvertHashID();
                        var list = loostLoot.SpawnpointsForced.ToList();
                        //定义刷新点, 物品留空做预处理
                        var newspawnpoint = new Spawnpoint
                        {
                            LocationId = spawnpoint.LocationId,
                            Probability = spawnpoint.Probability,
                            Template = new SpawnpointTemplate
                            {
                                Id = spawnpoint.Template.Id,
                                IsAlwaysSpawn = spawnpoint.Template.IsAlwaysSpawn,
                                IsGroupPosition = spawnpoint.Template.IsGroupPosition,
                                GroupPositions = spawnpoint.Template.GroupPositions,
                                Position = spawnpoint.Template.Position,
                                Rotation = spawnpoint.Template.Rotation,
                                Root = spawnpoint.Template.Root,
                                Items = null
                            }
                        };
                        //处理物品表
                        var spawnpointitemlist = new List<SptLootItem>();
                        foreach (var item in spawnpoint.Template.Items)
                        {
                            spawnpointitemlist.Add(new SptLootItem
                            {
                                Id = item.Id,
                                Template = item.Template
                            });
                        }
                        newspawnpoint.Template.Items = spawnpointitemlist;
                        //处理战利品表
                        list.Add(newspawnpoint);
                        loostLoot.SpawnpointsForced = list;
                        return loostLoot;
                    });
                }
            }
            return template;
        }

        /// <summary>
        /// 将自定义物品树转换为原版物品树
        /// </summary>
        /// <param name="itemlist">自定义物品树实例</param>
        /// <param name="cloner">克隆器实例</param>
        /// <returns>原版物品树实例</returns>
        public static List<Item> ConvertItemListData(this List<CustomItem> itemlist, ICloner cloner)
        {
            //重写了一下底层, ParentId在底层自动转换了, 这里可以直接原生搞定12
            return itemlist.ConvertAll(item => (Item)item);
        }

        /// <summary>
        /// 清洗物品树, 将其转换为独立实例
        /// </summary>
        /// <param name="itemlist">传入的物品树实例</param>
        /// <param name="addinfo">加盐信息</param>
        /// <param name="cloner">克隆器实例</param>
        /// <returns>全新的物品树实例</returns>
        public static List<Item> RegenerateItemListData(this List<Item> itemlist, string addinfo, ICloner cloner)
        {
            var list = new List<Item>();
            foreach (Item item in itemlist)
            {
                var copyitem = cloner.Clone(item);
                copyitem.Id = ($"{copyitem.Id}_{addinfo}").ConvertHashID();
                if (copyitem.ParentId != null && copyitem.ParentId != "hideout")
                {
                    //怪了, 根节点为什么会洗掉啊? 我咋写的代码....
                    //既然没问题那就留着吧
                    copyitem.ParentId = ($"{copyitem.ParentId}_{addinfo}").ConvertHashID();
                }
                list.Add(copyitem);
            }
            return list;
        }

        /// <summary>
        /// 为物品修复兼容性
        /// </summary>
        /// <param name="fixDictionary">待修复列表</param>
        /// <param name="databaseService">数据库实例</param>
        public static void FixItemCompatible(Dictionary<MongoId, List<CustomFixData>> fixDictionary, DatabaseService databaseService)
        {
            var items = databaseService.GetItems().Values;
            var quests = databaseService.GetQuests().Values;
            var globals = databaseService.GetGlobals();
            //施工中
            //不对, 反了, 这里应该foreach-item在外面
            //吗?
            //damn, 反了
            //不对, 没反
            //哎呦不对, 反了我草
            //....
            //我日你的, 不对, 这里逻辑错了!!
            //item和quest必须都是m*n才能保证遍历!!
            //那我TM改结构图什么....
            //改都改了, 就不改回去了, 算了算了
            //唉, 闹的....
            foreach (var item in items)
            {
                foreach (var data in fixDictionary)
                {
                    foreach (var customFixData in data.Value)
                    {
                        if (customFixData == null || customFixData.FixType == null) continue;
                        foreach (var fixType in customFixData.FixType)
                        {
                            var type = fixType.ToLower();
                            switch (type)
                            {
                                case "mags":
                                case "chamber":
                                case "mods":
                                case "modsblacklist":
                                case "removemodsblacklist":
                                case "container":
                                case "containerblacklist":
                                case "removecontainerblacklist":
                                    {
                                        FixItems(data.Key, customFixData.ItemId, type, item);
                                    }
                                    break;
                                case "inraidcountlimit":
                                    {
                                        FixInRaidLimit(data.Key, customFixData.ItemId, type, globals);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
            foreach (var quest in quests)
            {
                foreach (var data in fixDictionary)
                {
                    foreach (var customFixData in data.Value)
                    {
                        if (customFixData == null || customFixData.FixType == null) continue;
                        foreach (var fixType in customFixData.FixType)
                        {
                            var type = fixType.ToLower();
                            switch (type)
                            {
                                case "questequip":
                                case "questequipblacklist":
                                case "questweapon":
                                case "questweapongroup":
                                case "handoveritem":
                                case "handoveritemgroup":
                                case "finditem":
                                case "finditemgroup":
                                    {
                                        FixQuests(data.Key, customFixData.ItemId, type, quest);
                                    }
                                    break;
                            }
                        }
                    }
                }

            }
            //施工完毕
        }

        /// <summary>
        /// 处理物品在物品中的兼容数据修复的工具方法
        /// </summary>
        /// <param name="targetId">目标ID</param>
        /// <param name="itemId">物品ID</param>
        /// <param name="fixType">修复类型</param>
        /// <param name="item">物品对象</param>
        public static void FixItems(MongoId targetId, MongoId itemId, string fixType, TemplateItem item)
        {
            switch (fixType)
            {
                case "mags":
                    {
                        if (item.Properties == null || item.Properties.Cartridges == null) break;
                        foreach (var cartridge in item.Properties.Cartridges)
                        {
                            if (cartridge.Properties == null) continue;
                            FixItemsFilter(cartridge.Properties, targetId, itemId);
                        }
                    }
                    break;
                case "chamber":
                    {
                        if (item.Properties == null || item.Properties.Chambers == null) break;
                        foreach (var chambers in item.Properties.Chambers)
                        {
                            if (chambers.Properties == null) continue;
                            FixItemsFilter(chambers.Properties, targetId, itemId);
                        }
                    }
                    break;
                case "mods":
                    {
                        if (item.Properties == null || item.Properties.Slots == null) break;
                        foreach (var slots in item.Properties.Slots)
                        {
                            if (slots.Properties == null) continue;
                            FixItemsFilter(slots.Properties, targetId, itemId);
                        }
                    }
                    break;
                case "modblacklist":
                    {
                        if (item.Properties == null || item.Properties.ConflictingItems == null) break;
                        var list = item.Properties.ConflictingItems;
                        if (list.Contains(targetId) && !list.Contains(itemId))
                        {
                            list.Add(itemId);
                        }
                    }
                    break;
                case "removemodblacklist":
                    {
                        if (item.Properties == null || item.Properties.ConflictingItems == null) break;
                        var list = item.Properties.ConflictingItems;
                        if (list.Contains(itemId))
                        {
                            list.Remove(itemId);
                        }
                    }
                    break;
                case "container":
                    {
                        if (item.Properties == null || item.Properties.Grids == null) break;
                        foreach (var grid in item.Properties.Grids)
                        {
                            //你的定义怎么不一样??
                            //哦, 有Exclude
                            var filter = grid?.Properties?.Filters?.FirstOrDefault()?.Filter;
                            if (filter != null && filter.Contains(targetId) && !filter.Contains(itemId))
                            {
                                filter.Add(itemId);
                            }
                        }
                    }
                    break;
                case "containerblacklist":
                    {
                        if (item.Properties == null || item.Properties.Grids == null) break;
                        foreach (var grid in item.Properties.Grids)
                        {
                            //你的定义怎么不一样??
                            //哦, 有Exclude
                            var filter = grid?.Properties?.Filters?.FirstOrDefault()?.ExcludedFilter;
                            if (filter != null && filter.Contains(targetId) && !filter.Contains(itemId))
                            {
                                filter.Add(itemId);
                            }
                        }
                    }
                    break;
                //这个好像有点危险
                //还是做了吧, 大不了不写文档里
                case "removecontainerblacklist":
                    {
                        if (item.Properties == null || item.Properties.Grids == null) break;
                        foreach (var grid in item.Properties.Grids)
                        {
                            var filter = grid?.Properties?.Filters?.FirstOrDefault()?.ExcludedFilter;
                            if (filter != null && filter.Contains(itemId))
                            {
                                filter.Remove(itemId);
                            }
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 处理物品兼容性的工具方法
        /// </summary>
        /// <param name="slot">目标容器</param>
        /// <param name="targetId">目标ID</param>
        /// <param name="itemId">物品ID</param>
        public static void FixItemsFilter(SlotProperties slot, MongoId targetId, MongoId itemId)
        {
            var filter = slot?.Filters?.FirstOrDefault()?.Filter;
            if (filter != null && filter.Contains(targetId) && !filter.Contains(itemId))
            {
                filter.Add(itemId);
            }
        }

        /// <summary>
        /// 处理物品在任务中的兼容数据修复的工具方法
        /// </summary>
        /// <param name="targetId">目标ID</param>
        /// <param name="itemId">物品ID</param>
        /// <param name="fixType">修复类型</param>
        /// <param name="quest">任务对象</param>
        public static void FixQuests(MongoId targetId, MongoId itemId, string fixType, Quest quest)
        {
            var finish = quest.Conditions.AvailableForFinish;
            if (finish == null) return;
            var kill = finish.Where(f => f.Type == "Elimination");
            var handover = finish.Where(f => f.ConditionType == "HandoverItem");
            var find = finish.Where(f => f.ConditionType == "FindItem");
            var failed = quest.Conditions.Fail;
            foreach (var conditions in kill)
            {
                var counter = conditions?.Counter?.Conditions;
                if (counter == null) continue;
                foreach (var condition in counter)
                {
                    if (condition.ConditionType != "Equipment" && condition.ConditionType != "Kills") continue;
                    if (condition.EquipmentInclusive != null && fixType == "questequip")
                    {
                        //出门整点吃的先
                        //imback
                        if (condition.EquipmentInclusive.Any(x => x.Count > 0 && x.First() == targetId))
                        {
                            if (condition.EquipmentInclusive.Any(x => x.Count > 0 && x.First() == itemId)) continue;
                            condition.EquipmentInclusive = Utils.AddToArray(condition.EquipmentInclusive.ToArray(), new List<string>() { itemId });
                        }
                    }
                    //hyw, 你为什么和白名单不一样啊??
                    if (condition.EquipmentExclusive != null)
                    {
                        if (fixType == "questequipblacklist")
                        {
                            if (condition.EquipmentExclusive.Any(x => x.Count > 0 && x.First() == targetId) && !condition.EquipmentExclusive.Any(x => x.Count > 0 && x.First() == itemId))
                            {
                                condition.EquipmentExclusive.Add(new List<string>() { itemId });
                            }
                        }
                        if (fixType == "removequestequipblacklist")
                        {
                            var list = condition.EquipmentExclusive;
                            for (int i = list.Count - 1; i >= 0; i--)
                            {
                                var slotList = list[i];
                                if (slotList.Count > 0 && slotList.First() == itemId)
                                {
                                    list.RemoveAt(i);
                                }
                            }
                        }
                    }
                    if (condition.Weapon != null)
                    {
                        var weapon = condition.Weapon;
                        if (fixType == "questweapon")
                        {
                            //还是HashSet省心
                            if (weapon.Contains(targetId) && !weapon.Contains(itemId))
                            {
                                weapon.Add(itemId);
                            }
                        }
                        if (fixType == "questweapongroup")
                        {
                            //还是HashSet省心
                            if (weapon.Contains(targetId) && !weapon.Contains(itemId) && weapon.Count > 1)
                            {
                                weapon.Add(itemId);
                            }
                        }
                    }
                }
            }
            foreach (var condition in handover)
            {
                //处理SPT特有的ListOrT, 感觉这个定义可以反向应用到战利品的应用目标上
                if (condition.Target == null || !condition.Target.IsList || condition.Target.List == null) continue;
                var list = condition.Target.List;
                if (!list.Contains(targetId) || list.Contains(itemId)) continue;
                if (fixType == "handoveritem")
                {
                    condition.Target.List.Add(itemId);
                }
                else if (fixType == "handoveritemgroup" && condition.Target.List.Count > 1)
                {
                    condition.Target.List.Add(itemId);
                }
            }
            foreach (var condition in find)
            {
                if (condition.Target == null || !condition.Target.IsList || condition.Target.List == null) continue;
                var list = condition.Target.List;
                if (!list.Contains(targetId) || list.Contains(itemId)) continue;
                if (fixType == "finditem")
                {
                    condition.Target.List.Add(itemId);
                }
                else if (fixType == "finditemgroup" && condition.Target.List.Count > 1)
                {
                    condition.Target.List.Add(itemId);
                }
            }
        }

        /// <summary>
        /// 处理物品携带数量的兼容数据修复的工具方法
        /// </summary>
        /// <param name="targetId">目标ID</param>
        /// <param name="itemId">物品ID</param>
        /// <param name="fixType">修复类型</param>
        /// <param name="globals">配置器实例</param>
        public static void FixInRaidLimit(MongoId targetId, MongoId itemId, string fixType, Globals globals)
        {
            var limits = globals.Configuration.RestrictionsInRaid;
            var target = limits.FirstOrDefault(x => x.TemplateId == targetId);
            var self = limits.FirstOrDefault(x => x.TemplateId == itemId);
            if (target == null || self != null) return;
            globals.Configuration.RestrictionsInRaid = Utils.AddToArray(globals.Configuration.RestrictionsInRaid, new RestrictionsInRaid
            {
                TemplateId = itemId,
                MaxInLobby = target.MaxInLobby,
                MaxInRaid = target.MaxInRaid
            });
        }

        /// <summary>
        /// 注册物品修复事件, 内部调用, 勿动, 勿用
        /// </summary>
        public static void RegisterFixItem()
        {
            EventManager.DataLoadEvent.FixItemCompatibleEvent += (context) =>
            {
                try
                {
                    //古法Debug
                    //File.WriteAllText(System.IO.Path.Combine(ConfigManager.modPath, "exportfixdata.json"), context.JsonUtil.Serialize(FixDict, true));
                    FixItemCompatible(FixDict, context.DB);
                }
                catch (Exception ex)
                {
                    EventManager.EventLogger.Error($"注册物品修复数据时发生错误：", ex);
                }
            };
        }

        /// <summary>
        /// 根据跳蚤市场标签处理列表
        /// </summary>
        /// <typeparam name="T">类型, 支持HashSet/List.MongoId/string</typeparam>
        /// <param name="ragfairtag">输入的标签</param>
        /// <param name="filter">输入的容器</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="itemsize">物品大小, 不填则默认为100(10x10以内)</param>
        public static void AddItemToListByRagfairTag<T>(MongoId ragfairtag, ICollection<T> filter, DatabaseService databaseService, int itemsize = 100)
        {
            // 1. 顶级防空
            if (filter == null) return;

            var handbook = databaseService.GetHandbook().Items;
            var items = databaseService.GetItems();
            if (handbook == null || items == null) return;

            // 2. 筛选出当前跳蚤分类下的所有Handbook物品项
            var list = handbook.Where(x => x.ParentId == ragfairtag);

            foreach (var item in list)
            {
                var templateid = item.Id;
                var template = GetItem(templateid, databaseService);
                if (template == null || template.Properties == null) continue;

                // 3. 核心格子大小过滤判定
                if (template.Properties.Width * template.Properties.Height <= itemsize)
                {
                    T valueToAdd = (T)(object)templateid;

                    if (!filter.Contains(valueToAdd))
                    {
                        filter.Add(valueToAdd);
                    }
                }
            }
        }

        /* 这个方法后面会放到火神重工里, 纯单例
        public static void InitFilePackage(MongoId itemid, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            databaseService.GetItems().TryGetValue(itemid, out var targetfilter);
            if (targetfilter != null)
            {
                var filter = targetfilter.Properties.Grids.First().Properties.Filters.First().Filter;
                filter.Clear();
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.其他, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.地图, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.货币, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.情报物品, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.机械钥匙, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.电子钥匙, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.特殊物品, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.特殊装备, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.次元博物, filter, databaseService, 4);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.贵重物品, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.医疗用品, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.工具, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.建筑材料, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.日常用品, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.易燃物品, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.电子产品, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.能源物品, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.子弹, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.食物, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.饮品, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.创伤处理, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.急救包, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.注射器, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.药品, filter, databaseService, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.容器, filter, databaseService, 1);
            }
        }
        */

        /// <summary>
        /// 判断物品是否存在预设
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>返回一个布尔值</returns>
        public static bool HavePreset(MongoId itemid, DatabaseService databaseService)
        {
            var preset = databaseService.GetGlobals().ItemPresets;
            var target = preset.Values.FirstOrDefault(x => x.Encyclopedia == itemid);
            return target != null;
        }

        /// <summary>
        /// 从指定的物品ID获取预设对象
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        /// 
        /// <returns></returns>
        public static List<Item> GetPreset(MongoId itemid, DatabaseService databaseService, ICloner cloner)
        {
            var preset = databaseService.GetGlobals().ItemPresets;
            var target = preset.Values.FirstOrDefault(x => x.Encyclopedia == itemid);
            if (target == null) return new List<Item>();
            var itemlist = target.Items.RegenerateItemListData(($"{DateTime.Now}_{itemid}_{Guid.NewGuid()}").ConvertHashID(), cloner);
            return itemlist;
        }

        /// <summary>
        /// 获取物品最低价格, 顺位为price-handbook*0.6
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>物品价格</returns>
        public static int GetItemMinPrice(string itemid, DatabaseService databaseService)
        {
            var tablePrice = GetItemPrice(itemid, databaseService);
            if (tablePrice > 0)
            {
                return tablePrice;
            }
            else
            {
                var handbook = databaseService.GetHandbook().Items;
                var handbookdata = handbook.FirstOrDefault(i => i.Id == itemid);
                if (handbookdata != null && handbookdata.Price > 0)
                {
                    return (int)(handbookdata.Price * 0.6);
                }
                else return 1;
            }
        }

        /// <summary>
        /// 获取物品价格, 顺位为price-handbook
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>物品价格</returns>
        public static int GetItemCommonPrice(string itemid, DatabaseService databaseService)
        {
            var tablePrice = GetItemPrice(itemid, databaseService);
            if (tablePrice > 0)
            {
                return tablePrice;
            }
            else
            {
                var handbook = databaseService.GetHandbook().Items;
                var handbookdata = handbook.FirstOrDefault(i => i.Id == itemid);
                if (handbookdata != null && handbookdata.Price > 0)
                {
                    return (int)(handbookdata.Price);
                }
                else return 1;
            }
        }

        /// <summary>
        /// 获取物品在price表里的价格
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>物品价格</returns>
        public static int GetItemPrice(string itemid, DatabaseService databaseService)
        {
            var priceTable = databaseService.GetPrices();
            priceTable.TryGetValue(itemid, out double value);
            var tablePrice = (int)value;
            if (tablePrice > 0)
            {
                return tablePrice;
            }
            return 0;
        }

        /// <summary>
        /// 从物品ID获取基础预设的价格
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        /// <returns>预设的价格</returns>
        public static int GetPresetPrice(MongoId itemid, DatabaseService databaseService, ICloner cloner)
        {
            var item = GetItem(itemid, databaseService);
            var ragfairs = item?.Properties?.CanSellOnRagfair ?? false;
            var minprice = GetItemPrice(itemid, databaseService);
            if (ragfairs)
            {
                return minprice;
            }
            else
            {
                var preset = GetPreset(itemid, databaseService, cloner);
                if (preset == null) return minprice;
                return GetPresetPrice(preset, databaseService);
            }
        }

        /// <summary>
        /// 从物品表获取预设价格
        /// </summary>
        /// <param name="item">物品表</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>返回价格</returns>
        public static int GetPresetPrice(List<Item> item, DatabaseService databaseService)
        {
            int price = 0;
            if (item.Count > 0)
            {
                foreach (Item items in item)
                {
                    price += GetItemCommonPrice(items.Template, databaseService);
                }
                return price;
            }
            return 1;
        }

        /* 单例方法, 后面单独用
        public static void InitEquipmentChest(MongoId itemid, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            databaseService.GetItems().TryGetValue(itemid, out var targetfilter);
            if (targetfilter != null)
            {
                var filter = targetfilter.Properties.Grids.First().Properties.Filters.First().Filter;
                filter.Clear();
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.头部装备, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.战术胸挂, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.眼部装备, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.耳机, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.背包, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.装备组件, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.防弹衣, filter, databaseService);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.面部装备, filter, databaseService);
            }
        }
        */

        /// <summary>
        /// 为容器添加黑名单
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="list">黑名单</param>
        /// <param name="databaseService">数据库实例</param>
        public static void AddExcludeFilter(MongoId itemid, List<string> list, DatabaseService databaseService)
        {
            var target = GetItem(itemid, databaseService);
            if (target != null)
            {
                //hashset
                var filter = target?.Properties?.Grids?.First()?.Properties?.Filters?.First()?.ExcludedFilter;
                if (filter == null) return;
                foreach (var str in list)
                {
                    filter.Add(str.ConvertHashID());
                }
            }
        }

        /// <summary>
        /// 覆盖容器黑名单
        /// </summary>
        /// <param name="itemid">物品ID</param>
        /// <param name="list">黑名单</param>
        /// <param name="databaseService">数据库实例</param>
        public static void SetExcludeFilter(MongoId itemid, List<string> list, DatabaseService databaseService)
        {
            var target = GetItem(itemid, databaseService);
            if (target != null)
            {
                var filter = target?.Properties?.Grids?.First()?.Properties?.Filters?.First()?.ExcludedFilter;
                if (filter == null) return;
                target.Properties.Grids.First().Properties.Filters.First().ExcludedFilter = list.Select(x => (MongoId)x.ConvertHashID()).ToHashSet();
            }
        }

        /// <summary>
        /// 为自定义物品配置礼盒数据
        /// 这部分是不是应该放进另一个前置里? 开箱算法是一个破坏性Patch
        /// 还是算了, 数据处理放在这, 数据读取另存
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="configServer">配置器实例</param>
        public static CustomItemTemplate SetGiftBoxData(this CustomItemTemplate template, ConfigServer configServer)
        {
            var inventoryConfig = configServer.GetConfig<InventoryConfig>();
            var itemid = template.Id.ConvertHashID();
            if (template.CustomProps is GiftBoxProps itemProps)
            {
                //原版随机盒子
                if (itemProps.IsGiftBox == true)
                {
                    var boxdata = itemProps.BoxData;
                    var randomloot = inventoryConfig.RandomLootContainers;
                    var rewardpool = new Dictionary<MongoId, double>();
                    //生成卡池数据
                    foreach (var kvp in boxdata.Rewards)
                    {
                        rewardpool.TryAdd(kvp.Key.ConvertHashID(), kvp.Value);
                    }
                    //强制覆盖卡池
                    randomloot[itemid] = new RewardDetails
                    {
                        RewardCount = boxdata.Count,
                        FoundInRaid = true,
                        RewardTplPool = rewardpool
                    };
                }
                //固定容器, Mod数据, 要提供覆盖吗?
                //还是提供了吧
                if (itemProps.IsStaticBox == true)
                {
                    var boxdata = itemProps.StaticBoxData;
                    StaticBoxData[itemid] = boxdata;
                }
                if (itemProps.IsSpecialBox == true)
                {
                    var boxdata = itemProps.SpecialBoxData;
                    SpecialBoxData[itemid] = boxdata.GiftData;
                }
                //adv还没写
                //写了
                if (itemProps.IsAdvGiftBox == true)
                {
                    var boxdata = itemProps.AdvancedBoxData;
                    AdvancedBoxData[itemid] = boxdata;
                }
            }
            return template;
        }

        /// <summary>
        /// 获取礼盒数据的类型
        /// </summary>
        /// <param name="giftData">自定义数据</param>
        /// <param name="hash">加盐信息</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="cloner">克隆器实例</param>
        /// <returns>返回一个独立的物品表</returns>
        public static List<Item> GetGiftItemByType(GiftData giftData, string hash, DatabaseService databaseService, ICloner cloner)
        {
            var result = new List<Item>();
            switch (giftData)
            {
                case GiftCustomPresetData customPreset:
                    {
                        return customPreset.Item.ConvertItemListData(cloner).RegenerateItemListData(hash, cloner);
                    }
                case GiftVanillaPresetData vanillaPreset:
                    {
                        var preset = GetPreset(vanillaPreset.Item, databaseService, cloner);
                        return preset.Count > 0 ? preset.RegenerateItemListData(hash, cloner) : result;
                    }
                case GiftItemData item:
                    {
                        var itemid = item.ItemId;
                        var mainitemid = ($"{hash}_{itemid}_{DateTime.Now}_{Guid.NewGuid()}").ConvertHashID();
                        var itemlist = new List<Item>();
                        var isAmmoBox = GetItemRagfairTag(itemid, databaseService) == ERagfairTagsType.弹药包;
                        itemlist.Add(new Item
                        {
                            Id = mainitemid,
                            Template = itemid,
                            Upd = new Upd
                            {
                                StackObjectsCount = item.Count
                            }
                        });
                        if (isAmmoBox)
                        {
                            AddAmmoToAmmoBoxInList(mainitemid, itemid, itemlist, databaseService);
                        }
                        return itemlist.RegenerateItemListData(hash, cloner);
                    }
                case GiftContainerData container:
                    {
                        return container.Item.ConvertItemListData(cloner).RegenerateItemListData(hash, cloner);
                    }
                default:
                    {
                        return result;
                    }
            }
        }

        /// <summary>
        /// 工具方法, 为弹药盒在物品树里添加弹药子对象
        /// </summary>
        /// <param name="mainid">父物品ID</param>
        /// <param name="itemid">物品ID</param>
        /// <param name="itemlist">物品表</param>
        /// <param name="databaseService">数据库实例</param>
        public static void AddAmmoToAmmoBoxInList(MongoId mainid, MongoId itemid, List<Item> itemlist, DatabaseService databaseService)
        {
            //一大坨东西, 总之是为了获取弹药和弹盒数据
            var ammopack = GetItem(itemid, databaseService);
            if (ammopack == null) return;
            double maxstackcount = (ammopack?.Properties?.StackSlots?.First()?.MaxCount ?? 0);
            MongoId ammo = (ammopack?.Properties?.StackSlots?.First()?.Properties?.Filters?.First()?.Filter?.First() ?? ($"{Guid.NewGuid()}_{DateTime.Now}").ConvertHashID());
            var ammoitem = GetItem(ammo, databaseService);
            if (ammoitem == null) return;
            double ammostackcount = (ammoitem?.Properties?.StackMaxSize ?? 0);
            //计算部分
            //内置序号
            var location = 0;
            //子对象数量大于0 (???我写的什么逻辑, 我怎么看不懂了)
            //不对啊, 这里直接减法循环不就完了吗, 取余和向下取整求商完全是多余的....
            double cachecount = maxstackcount;
            //倒序循环递减
            while (cachecount > ammostackcount)
            {
                itemlist.Add(new Item
                {
                    Id = ($"{mainid}_ammo_{location}").ConvertHashID(),
                    Template = ammo,
                    ParentId = mainid,
                    SlotId = "cartridges",
                    Location = location,
                    Upd = new Upd { StackObjectsCount = ammostackcount }
                });
                cachecount -= ammostackcount;
                location++;
            }
            //余数/弹盒小于堆叠数
            if (cachecount > 0)
            {
                itemlist.Add(new Item
                {
                    Id = ($"{mainid}_ammo_{location}").ConvertHashID(),
                    Template = ammo,
                    ParentId = mainid,
                    SlotId = "cartridges",
                    Location = location,
                    Upd = new Upd { StackObjectsCount = cachecount }
                });
            }
        }

        /// <summary>
        /// 加载卡池数据
        /// </summary>
        /// <param name="drawPool">卡池数据对象</param>
        public static void InitDrawPool(Dictionary<string, DrawPoolClass> drawPool)
        {
            foreach (var pool in drawPool)
            {
                DrawPoolData.TryAdd(pool.Value.Name, pool.Value);
            }
        }

        /// <summary>
        /// 从指定目录读取单文件卡池加载
        /// </summary>
        /// <param name="folderpath">指定路径</param>
        public static void InitDrawPool(string folderpath)
        {
            //这里的静态引用后面还得改成调包
            var modHelper = ServiceLocator.ServiceProvider.GetService<ModHelper>();
            List<string> files = Directory.GetFiles(folderpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    //需要修改为原生支持
                    //Item就没有办法了
                    //Item根本没通过Json走, 只能走自定义处理了
                    //明天继续, 今天摸了
                    string fileName = System.IO.Path.GetFileName(file);
                    var pool = modHelper.GetJsonDataFromFile<DrawPoolClass>(folderpath, fileName);
                    DrawPoolData.TryAdd(pool.Name, pool);
                }
            }
        }
        //差个卡池注册

        /// <summary>
        /// 抽卡方法
        /// </summary>
        /// <param name="sessionId">pmc存档ID</param>
        /// <param name="drawpoolname">卡池名称</param>
        /// <param name="drawpool">卡池数据</param>
        /// <param name="drawrecord">抽卡记录</param>
        /// <param name="jsonUtil">json工具实例</param>
        /// <param name="itemHelper">物品帮助实例</param>
        /// <param name="databaseService">数据库实例</param>
        /// <param name="modHelper">mod帮助实例</param>
        /// <param name="cloner">克隆器实例</param>
        /// <returns>返回一个物品表</returns>
        /// 
        public static List<Item> GetAdvancedBoxData(MongoId sessionId, string drawpoolname, DrawPoolClass drawpool, Dictionary<MongoId, Dictionary<string, DrawRecord>> drawrecord, JsonUtil jsonUtil, ItemHelper itemHelper, DatabaseService databaseService, ModHelper modHelper, ICloner cloner)
        {
            //输出结果
            var result = new List<Item>();
            //随机种子, 这里是不是应该存一个全局实例?
            //这个, 不需要了
            //Random random = new Random();
            //检查是否有抽卡记录
            if (!drawrecord.TryGetValue(sessionId, out var pmcrecord))
            {
                //没有就创建一个新的
                pmcrecord = new Dictionary<string, DrawRecord>();
                drawrecord[sessionId] = pmcrecord;
            }
            //检查是否有当前卡池记录
            if (!pmcrecord.TryGetValue(drawpoolname, out var pooldata))
            {
                //没有就创建一个新的
                pooldata = new DrawRecord
                {
                    SuperRare = new SuperRareRecord
                    {
                        AddChance = 0,
                        Count = 0,
                        UpAddChance = 0,
                        Record = new List<SuperRareCardRecord>()
                    },
                    Rare = new RareRecord
                    {
                        AddChance = 0,
                        Count = 0,
                        UpAddChance = 0
                    }
                };
                pmcrecord[drawpoolname] = pooldata;
            }
            //卡池基础数据
            var basedata = drawpool.BaseReward;
            var itempool = drawpool.ItemPool;
            var sr = basedata.SuperRare;
            var srpool = itempool.SuperRare;
            var r = basedata.Rare;
            var rpool = itempool.Rare;
            var normal = basedata.Normal;
            var normalpool = itempool.Normal;
            var srdata = pooldata.SuperRare;
            var rdata = pooldata.Rare;
            //概率计算
            var randomchance = Math.Floor(Random.Shared.NextDouble() * 1000) / 1000;
            var srrealchance = Math.Floor((1 / (sr.ChanceGrowCount + 1 + ((1 - sr.Chance) / sr.ChanceGrowPerCount))) * 1000) / 1000;
            var upchance = Math.Floor(Random.Shared.NextDouble() * 1000) / 1000;
            //保底计算
            if (sr.HaveBaseReward)
            {
                //保底计算
                srdata.Count++;
                if (srdata.Count > sr.ChanceGrowCount)
                {
                    srdata.AddChance += sr.ChanceGrowPerCount;
                }
            }
            if (r.HaveBaseReward)
            {
                //保底计算
                rdata.Count++;
                if (rdata.Count > r.ChanceGrowCount)
                {
                    rdata.AddChance += r.ChanceGrowPerCount;
                }
            }
            //VulcanLog.Debug("开始统计抽卡结果", logger);
            //VulcanLog.Debug($"当前卡池: {drawpoolname}", logger);
            //VulcanLog.Debug("开始进行抽卡计算", logger);
            //VulcanLog.Debug($"当前金色数据: 累加概率: {srdata.AddChance}, 抽取次数: {srdata.Count}, 保底叠加概率: {srdata.UpAddChance}", logger);
            //VulcanLog.Debug($"当前紫色数据: 累加概率: {rdata.AddChance}, 抽取次数: {rdata.Count}, 保底叠加概率: {rdata.UpAddChance}", logger);
            //VulcanLog.Debug($"当前金色概率: {randomchance}/{srrealchance + srdata.AddChance}", logger);
            //抽到五星
            if ((randomchance <= (srrealchance + srdata.AddChance)) || (srdata.Count == (sr.ChanceGrowCount + 1 + Math.Floor(((1 - sr.Chance) / sr.ChanceGrowPerCount)))))
            {
                //VulcanLog.Warn("你抽到了金色传说! ", logger);
                //记录抽卡结果
                var cachererord = new SuperRareCardRecord
                {
                    ItemId = "",
                    ItemName = "",
                    Count = srdata.Count,
                    IsUpReward = false
                };
                //清空抽卡历史
                srdata.AddChance = 0;
                srdata.Count = 0;
                rdata.AddChance = 0;
                rdata.Count = 0;
                //小保底命中
                if (upchance <= (sr.UpChance + srdata.UpAddChance))
                {
                    //VulcanLog.Access("小保底没歪", logger);
                    srdata.UpAddChance = 0;
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(srpool.ChanceUp), $"{DateTime.Now}_{srdata.Count}_{Guid.NewGuid()}".ConvertHashID(), databaseService, cloner);
                    var tpl = result.First().Template;
                    cachererord.ItemId = tpl;
                    cachererord.ItemName = itemHelper.GetItemName(tpl);
                    cachererord.IsUpReward = true;
                    srdata.Record.Add(cachererord);
                }
                //小保底歪了
                else
                {
                    //VulcanLog.Error("哎呀, 小保底歪了", logger);
                    srdata.UpAddChance += sr.UpAddChance;
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(srpool.Normal), $"{DateTime.Now}_{srdata.Count}_{Guid.NewGuid()}".ConvertHashID(), databaseService, cloner);
                    var tpl = result.First().Template;
                    cachererord.ItemId = tpl;
                    cachererord.ItemName = itemHelper.GetItemName(tpl);
                    srdata.Record.Add(cachererord);

                }
                //itemHelper后面会换掉
            }
            //四星保底
            else if (randomchance <= (r.Chance) || (rdata.Count == Math.Floor((r.ChanceGrowCount + 1 + ((1 - r.Chance) / r.ChanceGrowPerCount)))))
            {
                //VulcanLog.Warn("你抽到了紫色史诗 ", logger);
                rdata.AddChance = 0;
                rdata.Count = 0;
                if (upchance <= (r.UpChance + rdata.UpAddChance))
                {
                    //VulcanLog.Access("保底没歪", logger);
                    rdata.UpAddChance = 0;
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(rpool.ChanceUp), $"{DateTime.Now.ToString()}_{rdata.Count}_{Guid.NewGuid()}".ConvertHashID(), databaseService, cloner);
                }
                else
                {
                    //VulcanLog.Error("哎呀, 保底歪了", logger);
                    rdata.UpAddChance += r.UpAddChance;
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(rpool.Normal), ($"{DateTime.Now}_{rdata.Count}_{Guid.NewGuid()}").ConvertHashID(), databaseService, cloner);
                }
            }
            //三星小垃圾
            else
            {
                //VulcanLog.Debug("很遗憾, 你抽到了一坨垃圾:( ", logger);
                //VulcanLog.Debug("无需灰心, 霉运乃人生常事, 少侠请重新来过", logger);
                if (upchance < normal.UpChance)
                {
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(normalpool.ChanceUp), $"{DateTime.Now}_{srdata.Count}_{Guid.NewGuid()}".ConvertHashID(), databaseService, cloner);
                }
                else
                {
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(normalpool.Normal), $"{DateTime.Now}_{srdata.Count}_{Guid.NewGuid()}".ConvertHashID(), databaseService, cloner);
                }
            }
            //VulcanLog.Debug(dwarrecordstring, logger);
            //VulcanLog.Warn("警告! 无法获取卡池信息", logger);
            return result;
        }

        /// <summary>
        /// 向物品栏添加装备
        /// </summary>
        /// <param name="inventory"></param>
        /// <param name="itemid"></param>
        /// <param name="targetid"></param>
        /// <param name="slotid"></param>
        /// 
        //不对啊, 这个方法有用吗?
        public static void AddModsToInventory(BotBaseInventory inventory, MongoId itemid, MongoId targetid, string slotid)
        {
            //查找目标
            if (inventory == null || inventory.Items == null) return;
            var items = inventory.Items.FirstOrDefault(x => x.Template == targetid);
            if (items == null) return;
            var parentid = items.Id;
            //生成装备节点
            var newitems = new Item
            {
                Id = new MongoId(),
                Template = itemid,
                ParentId = parentid,
                SlotId = slotid,
                Upd = new Upd
                {
                    StackObjectsCount = 1,
                    SpawnedInSession = true
                }
            };
            inventory.Items.Add(newitems);
            //logger.LogWithColor("尝试生成箭头", LogTextColor.Magenta);
        }

        /// <summary>
        /// 设置战局内携带数量限制
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>自定义物品对象</returns>
        public static CustomItemTemplate SetInRaidLimitCount(this CustomItemTemplate template, DatabaseService databaseService)
        {
            if (template.CustomProps?.InRaidCountLimit == null)
            {
                return template;
            }
            var globals = databaseService.GetGlobals();
            var limits = globals.Configuration.RestrictionsInRaid;
            var targetId = template.Id.ConvertHashID();
            //新建对象
            var newLimit = new RestrictionsInRaid
            {
                TemplateId = targetId,
                MaxInLobby = (template.CustomProps.InLobbyCountLimit ?? -1),
                MaxInRaid = (double)template.CustomProps.InRaidCountLimit
            };
            //检查是否已经存在
            int existingIndex = Array.FindIndex(limits, x => x.TemplateId == targetId);
            if (existingIndex >= 0)
            {
                limits[existingIndex] = newLimit;
            }
            else
            {
                globals.Configuration.RestrictionsInRaid = Utils.AddToArray(limits, newLimit);
            }
            return template;
        }

        /// <summary>
        /// 设置狗牌刷新数据
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="configServer">配置实例</param>
        /// <returns>自定义物品对象</returns>
        public static CustomItemTemplate SetCustomPMCDogTag(this CustomItemTemplate template, ConfigServer configServer)
        {
            if (template.CustomProps != null && template.CustomProps.ApplyAsPMCDogTag == true)
            {
                var customprops = template.CustomProps;
                if (customprops.ApplyToBEAR == true)
                {
                    SetCustomDogTagGenerate(template, PlayerSide.Bear, configServer);
                }
                if (customprops.ApplyToUSEC == true)
                {
                    SetCustomDogTagGenerate(template, PlayerSide.Usec, configServer);
                }
            }
            return template;
        }

        /// <summary>
        /// 为自定义物品设置狗牌刷新的工具方法
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="side">阵营</param>
        /// <param name="configServer">配置实例</param>
        public static void SetCustomDogTagGenerate(CustomItemTemplate template, PlayerSide side, ConfigServer configServer)
        {
            var pmcconfig = configServer.GetConfig<PmcConfig>();
            var customprops = template.CustomProps;
            var sidestring = side == PlayerSide.Bear ? "bear" : "usec";
            var itemid = template.Id.ConvertHashID();
            var standard = pmcconfig.DogtagSettings[sidestring]["default"];
            var edgeofdarkness = pmcconfig.DogtagSettings[sidestring]["edge_of_darkness"];
            var unheard = pmcconfig.DogtagSettings[sidestring]["unheard_edition"];
            if (customprops.ApplyToStandard == true && !standard.ContainsKey(itemid))
            {
                standard.Add(itemid, 1);
            }
            if (customprops.ApplyToEOD == true && !edgeofdarkness.ContainsKey(itemid))
            {
                edgeofdarkness.Add(itemid, 1);
            }
            if (customprops.ApplyToUnheard == true && !unheard.ContainsKey(itemid))
            {
                unheard.Add(itemid, 1);
            }
        }

        /// <summary>
        /// 返回基于手册分类的物品列表
        /// </summary>
        /// <param name="ragfairTag">分类标签</param>
        /// <param name="databaseService">数据库实例</param>
        /// <returns>符合标签的物品ID表</returns>
        public static List<MongoId> GetItemListByRagfairTag(MongoId ragfairTag, DatabaseService databaseService)
        {
            return databaseService.GetHandbook().Items.Where(x => x.ParentId == ragfairTag).Select(x => x.Id).ToList();
        }
    }
}
