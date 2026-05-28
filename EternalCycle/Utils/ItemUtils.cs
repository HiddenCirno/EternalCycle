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
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Templates;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Json;
using SPTarkov.Server.Core.Utils.Logger;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

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
        public static HashSet<CustomFixData> FixList = new HashSet<CustomFixData>();
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
        //这部分得大改
        public static int GetItemMinPrice(string itemid, DatabaseService databaseService)
        {
            var item = GetItem(itemid, databaseService);
            var itemsid = itemid;
            var priceTable = databaseService.GetPrices();
            var handbook = databaseService.GetHandbook().Items;
            //var ragfairPrice = offers.Min;
            var tablePrice = (int)priceTable.FirstOrDefault(kv => kv.Key == itemsid).Value;
            if (tablePrice > 0)
            {
                return tablePrice;
            }
            else
            {
                var handbookdata = handbook.FirstOrDefault(i => i.Id == itemsid);
                if (handbookdata != null && handbookdata.Price > 0)
                {
                    return (int)(handbookdata.Price * 0.6);
                }
                else return 1;
            }
        }
        public static int GetItemPrice(string itemid, DatabaseService databaseService)
        {
            var item = GetItem(itemid, databaseService);
            var itemsid = (MongoId)itemid;
            var priceTable = databaseService.GetPrices();
            var handbook = databaseService.GetHandbook().Items;
            //var ragfairPrice = offers.Min;
            var tablePrice = (int)priceTable.FirstOrDefault(kv => kv.Key == itemsid).Value;

            //var logger = ServiceLocator.ServiceProvider.GetService<ISptLogger<VulcanCore>>();
            //if (itemid == VulcanUtil.ConvertHashID("补佳乐")) VulcanLog.Debug($"{tablePrice}", logger);
            if (tablePrice > 0)
            {
                return tablePrice;
            }
            else
            {
                var handbookdata = handbook.FirstOrDefault(i => i.Id == itemsid);
                if (handbookdata != null && handbookdata.Price > 0)
                {
                    return (int)(handbookdata.Price);
                }
                else return 1;
            }
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
        public static void AddAirDropBlackList(string itemid, ConfigServer configserver)
        {
            AirdropConfig lootConfig = configserver.GetConfig<AirdropConfig>();
            foreach (AirdropLoot loot in lootConfig.Loot.Values)
            {
                loot.ItemBlacklist.Add(itemid);
            }
        }
        public static void AddPMCLootBlackList(string itemid, ConfigServer configserver)
        {
            PmcConfig lootConfig = configserver.GetConfig<PmcConfig>();
            lootConfig.VestLoot.Blacklist.Add(itemid);
            lootConfig.PocketLoot.Blacklist.Add(itemid);
            lootConfig.BackpackLoot.Blacklist.Add(itemid);
        }
        public static void AddScavCaseLootBlackList(string itemid, ConfigServer configserver)
        {
            ScavCaseConfig lootConfig = configserver.GetConfig<ScavCaseConfig>();
            lootConfig.RewardItemBlacklist.Add(itemid);
        }
        public static void AddFenceBlackList(string itemid, ConfigServer configserver)
        {
            TraderConfig lootConfig = configserver.GetConfig<TraderConfig>();
            lootConfig.Fence.Blacklist.Add(itemid);
        }
        public static void AddCircleBlackList(string itemid, ConfigServer configserver)
        {
            HideoutConfig lootConfig = configserver.GetConfig<HideoutConfig>();
            lootConfig.CultistCircle.RewardItemBlacklist.Add(itemid);
        }
        public static void AddDailyRewardBlackList(string itemid, ConfigServer configserver)
        {
            QuestConfig questConfig = configserver.GetConfig<QuestConfig>();
            questConfig.RepeatableQuests.ForEach(type => type.RewardBlacklist.Add(itemid));
        }
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
            if (template.CustomProps is BuffItemProps itemProps && template.Props.StimulatorBuffs!=null)
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
            if (template.CustomProps is CustomFixedItemProps itemProps)
            {
                var itemid = template.Id.ConvertHashID();
                var customFixData = new CustomFixData
                {
                    FixType = itemProps.FixType,
                    ItemId = itemid,
                    TargetId = itemProps.CustomFixID != null ? (MongoId)itemProps.CustomFixID : template.TargetId
                };
                if(FixList.FirstOrDefault(x=>x.ItemId == itemid)==null) FixList.Add(customFixData);
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
                    looseloot.AddTransformer(loostLoot=>
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
        public static void FixItemCompatible(CustomFixData customFixData, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            var items = databaseService.GetItems();
            var quests = databaseService.GetQuests();
            var globals = databaseService.GetGlobals();
            var handbooks = databaseService.GetHandbook().Items;
            var prices = databaseService.GetPrices();
            foreach (var item in items.Values)
            {
                if (customFixData != null)
                {
                    if (customFixData.FixType != null)
                    {
                        if (customFixData.FixType.Contains("Mags"))
                        {
                            if (item.Properties != null && item.Properties.Cartridges != null)
                            {
                                foreach (var cartridge in item.Properties.Cartridges)
                                {
                                    var filters = cartridge.Properties.Filters;
                                    if (filters.First().Filter.Contains(customFixData.TargetId))
                                    {
                                        filters.First().Filter.Add(customFixData.ItemId);
                                    }
                                }
                            }
                        }
                        if (customFixData.FixType.Contains("Chamber"))
                        {
                            if (item.Properties != null && item.Properties.Chambers != null)
                            {
                                foreach (var chamber in item.Properties.Chambers)
                                {
                                    var filters = chamber.Properties.Filters;
                                    if (filters.First().Filter.Contains(customFixData.TargetId))
                                    {
                                        filters.First().Filter.Add(customFixData.ItemId);
                                    }
                                }
                            }
                        }
                        if (customFixData.FixType.Contains("Mods"))
                        {
                            if (item.Properties != null && item.Properties.Slots != null)
                            {
                                foreach (var slot in item.Properties.Slots)
                                {
                                    var filters = slot.Properties.Filters;
                                    if (filters.First().Filter.Contains(customFixData.TargetId))
                                    {
                                        filters.First().Filter.Add(customFixData.ItemId);
                                    }
                                }
                            }
                        }
                        if (customFixData.FixType.Contains("ModsBlackList"))
                        {
                            if (item.Properties != null && item.Properties.ConflictingItems != null)
                            {
                                var list = item.Properties.ConflictingItems;
                                if (list.Contains(customFixData.TargetId))
                                {
                                    list.Add(customFixData.ItemId);
                                }
                            }
                        }
                        if (customFixData.FixType.Contains("Container"))
                        {
                            if (item.Properties != null && item.Properties.Grids != null)
                            {
                                foreach (var grid in item.Properties.Grids)
                                {
                                    var filters = grid.Properties.Filters;
                                    if (filters != null)
                                    {
                                        if (filters.FirstOrDefault() != null && filters.FirstOrDefault().Filter.Contains(customFixData.TargetId))
                                        {
                                            filters.FirstOrDefault().Filter.Add(customFixData.ItemId);
                                        }
                                    }
                                }
                            }
                        }
                        if (customFixData.FixType.Contains("ContainerBlackList"))
                        {
                            if (item.Properties != null && item.Properties.Grids != null)
                            {
                                foreach (var grid in item.Properties.Grids)
                                {
                                    var filters = grid.Properties.Filters;
                                    if (filters != null)
                                    {
                                        if (filters.FirstOrDefault() != null && filters.FirstOrDefault().ExcludedFilter.Contains(customFixData.TargetId))
                                        {
                                            filters.FirstOrDefault().ExcludedFilter.Add(customFixData.ItemId);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            foreach (var quest in quests.Values)
            {
                var finishes = quest.Conditions.AvailableForFinish;
                if (customFixData != null)
                {
                    if (customFixData.FixType != null && finishes.Count > 0)
                    {
                        if (
                            customFixData.FixType.Contains("QuestEquip") ||
                            customFixData.FixType.Contains("QuestEquipBlackList") ||
                            customFixData.FixType.Contains("QuestWeapon") ||
                            customFixData.FixType.Contains("QuestWeaponGroup")
                            )
                        {
                            foreach (var finish in finishes.Where(f => f.Type == "Elimination"))
                            {
                                var counters = finish.Counter?.Conditions;
                                if (counters == null) continue; // 如果没有 Conditions 跳过
                                                                // 遍历所有的 condition
                                foreach (var condition in counters)
                                {
                                    if (condition.ConditionType != "Equipment" || condition.ConditionType != "Kills") continue;
                                    // 处理 EquipmentInclusive
                                    var inclusive = condition.EquipmentInclusive;
                                    if (inclusive != null && customFixData.FixType.Contains("QuestEquip"))
                                    {
                                        // 只在需要时执行，避免重复遍历
                                        if (inclusive.Any(equipment => equipment.Contains(customFixData.TargetId))) continue;
                                        var list = inclusive.ToList();
                                        list.Add(new List<string> { customFixData.TargetId });
                                        condition.EquipmentInclusive = list;
                                    }
                                    // 处理 EquipmentExclusive
                                    var exclusive = condition.EquipmentExclusive;
                                    if (exclusive != null && customFixData.FixType.Contains("QuestEquipBlackList"))
                                    {
                                        // 只在需要时执行，避免重复遍历
                                        if (exclusive.Any(equipment => equipment.Contains(customFixData.TargetId))) continue;
                                        exclusive.Add(new List<string> { customFixData.TargetId });
                                    }
                                    var weapon = condition.Weapon;
                                    if (weapon != null)
                                    {
                                        if (customFixData.FixType.Contains("QuestWeapon"))
                                        {
                                            if (weapon.Contains(customFixData.TargetId))
                                            {
                                                weapon.Add(customFixData.ItemId);
                                            }
                                        }
                                        else if (customFixData.FixType.Contains("QuestWeaponGroup"))
                                        {
                                            if (weapon.Contains(customFixData.TargetId) && weapon.Count > 1)
                                            {
                                                weapon.Add(customFixData.ItemId);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        if (
                            customFixData.FixType.Contains("HandoverItem") ||
                            customFixData.FixType.Contains("HandoverItemGroup")
                            )
                        {
                            foreach (var finish in finishes.Where(f => f.ConditionType == "HandoverItem"))
                            {
                                if (finish.Target == null) continue;
                                if (finish.Target.IsList && finish.Target.List.Contains(customFixData.TargetId))
                                {
                                    if (customFixData.FixType.Contains("HandoverItem"))
                                    {
                                        finish.Target.List.Add(customFixData.ItemId);
                                    }
                                    else if (customFixData.FixType.Contains("HandoverItemGroup") && finish.Target.List.Count > 1)
                                    {
                                        finish.Target.List.Add(customFixData.ItemId);
                                    }
                                }
                            }
                        }
                        if (
                            customFixData.FixType.Contains("FindItem") ||
                            customFixData.FixType.Contains("FindItemGroup")
                            )
                        {
                            foreach (var finish in finishes.Where(f => f.ConditionType == "FindItem"))
                            {
                                if (finish.Target == null) continue;
                                if (finish.Target.IsList && finish.Target.List.Contains(customFixData.TargetId))
                                {
                                    if (customFixData.FixType.Contains("FindItem"))
                                    {
                                        finish.Target.List.Add(customFixData.ItemId);
                                    }
                                    else if (customFixData.FixType.Contains("FindItemGroup") && finish.Target.List.Count > 1)
                                    {
                                        finish.Target.List.Add(customFixData.ItemId);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            if (customFixData != null && customFixData.FixType != null)
            {
                if (customFixData.FixType.Contains("InRaidCountLimit"))
                {
                    var limits = globals.Configuration.RestrictionsInRaid.ToList();
                    var target = limits.FirstOrDefault(x => x.TemplateId == customFixData.TargetId);
                    if (target != null)
                    {
                        limits.Add(new RestrictionsInRaid
                        {
                            TemplateId = customFixData.ItemId,
                            MaxInLobby = target.MaxInLobby,
                            MaxInRaid = target.MaxInRaid
                        });
                    }
                    globals.Configuration.RestrictionsInRaid = limits.ToArray();
                }
            }
        }
        public static void FixItemCompatibleInit(HashSet<CustomFixData> fixData, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            foreach (var item in fixData)
            {
                FixItemCompatible(item, databaseService, logger, cloner);
            }
        }
        public static void AddItemToListByRagfairTag(MongoId ragfairtag, List<MongoId> filter, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner, int itemsize = 100)
        {
            var handbook = databaseService.GetHandbook().Items;
            var items = databaseService.GetItems();
            var list = handbook.Where(x => x.ParentId == ragfairtag);
            foreach (var item in list)
            {
                var templateid = item.Id;
                items.TryGetValue(templateid, out var template);
                if (template != null)
                {
                    if (template.Properties != null)
                    {
                        if (template.Properties.Width * template.Properties.Height <= itemsize && !filter.Contains(templateid))
                        {
                            filter.Add(templateid);
                        }
                    }
                }
            }
        }
        public static void AddItemToListByRagfairTag(MongoId ragfairtag, List<string> filter, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner, int itemsize = 100)
        {
            var handbook = databaseService.GetHandbook().Items;
            var items = databaseService.GetItems();
            var list = handbook.Where(x => x.ParentId == ragfairtag);
            foreach (var item in list)
            {
                var templateid = item.Id;
                items.TryGetValue(templateid, out var template);
                if (template != null)
                {
                    if (template.Properties != null)
                    {
                        if (template.Properties.Width * template.Properties.Height <= itemsize && !filter.Contains(templateid))
                        {
                            filter.Add(templateid);
                        }
                    }
                }
            }
        }
        public static void AddItemToListByRagfairTag(MongoId ragfairtag, HashSet<MongoId> filter, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner, int itemsize = 100)
        {
            var handbook = databaseService.GetHandbook().Items;
            var items = databaseService.GetItems();
            var list = handbook.Where(x => x.ParentId == ragfairtag);
            foreach (var item in list)
            {
                var templateid = item.Id;
                items.TryGetValue(templateid, out var template);
                if (template != null)
                {
                    if (template.Properties != null)
                    {
                        if (template.Properties.Width * template.Properties.Height <= itemsize && !filter.Contains(templateid))
                        {
                            filter.Add(templateid);
                        }
                    }
                }
            }
        }
        public static void InitFilePackage(MongoId itemid, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            databaseService.GetItems().TryGetValue(itemid, out var targetfilter);
            if (targetfilter != null)
            {
                var filter = targetfilter.Properties.Grids.First().Properties.Filters.First().Filter;
                filter.Clear();
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.其他, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.地图, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.货币, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.情报物品, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.机械钥匙, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.电子钥匙, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.特殊物品, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.特殊装备, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.次元博物, filter, databaseService, logger, cloner, 4);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.贵重物品, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.医疗用品, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.工具, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.建筑材料, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.日常用品, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.易燃物品, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.电子产品, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.能源物品, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.子弹, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.食物, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.饮品, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.创伤处理, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.急救包, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.注射器, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.药品, filter, databaseService, logger, cloner, 1);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.容器, filter, databaseService, logger, cloner, 1);
            }
        }
        public static bool HavePreset(MongoId itemid, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            var preset = databaseService.GetGlobals().ItemPresets;
            var target = preset.Values.FirstOrDefault(x => x.Encyclopedia == itemid);
            return target != null;
        }
        public static List<Item>? GetPreset(MongoId itemid, string key, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            var preset = databaseService.GetGlobals().ItemPresets;
            var target = preset.Values.FirstOrDefault(x => x.Encyclopedia == itemid);
            if (target == null) return null;
            var itemlist = target.Items;
            var newitemlist = RegenerateItemListData(itemlist, key, cloner);
            return newitemlist;
        }
        public static int GetPresetPrice(MongoId itemid, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
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
                int price = 0;
                var preset = GetPreset(itemid, "getpreset", databaseService, logger, cloner);
                if (preset.Count > 0)
                {
                    foreach (Item items in preset)
                    {
                        price += GetItemPrice(items.Template, databaseService);
                    }
                    return price;
                }
                else
                {
                    return minprice;
                }
            }
        }
        public static int GetPresetPrice(List<Item> item, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            int price = 0;
            if (item.Count > 0)
            {
                foreach (Item items in item)
                {
                    price += GetItemPrice(items.Template, databaseService);
                }
                return price;
            }
            return 0;
        }
        public static void InitEquipmentChest(MongoId itemid, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            databaseService.GetItems().TryGetValue(itemid, out var targetfilter);
            if (targetfilter != null)
            {
                var filter = targetfilter.Properties.Grids.First().Properties.Filters.First().Filter;
                filter.Clear();
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.头部装备, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.战术胸挂, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.眼部装备, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.耳机, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.背包, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.装备组件, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.防弹衣, filter, databaseService, logger, cloner);
                ItemUtils.AddItemToListByRagfairTag(ERagfairTagsType.面部装备, filter, databaseService, logger, cloner);
            }
        }
        public static void AddExcludeFilter(MongoId itemid, List<string> list, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            databaseService.GetItems().TryGetValue(itemid, out var targetfilter);
            if (targetfilter != null)
            {
                var filter = targetfilter.Properties.Grids.First().Properties.Filters.First().ExcludedFilter;
                foreach (var str in list)
                {
                    filter.Add(Utils.ConvertHashID(str));
                }
            }
        }
        public static void SetExcludeFilter(MongoId itemid, List<string> list, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            databaseService.GetItems().TryGetValue(itemid, out var targetfilter);
            if (targetfilter != null)
            {
                var filter = targetfilter.Properties.Grids.First().Properties.Filters.First().ExcludedFilter;
                filter.Clear();
                foreach (var str in list)
                {
                    filter.Add(Utils.ConvertHashID(str));
                }
            }
        }

        /// <summary>
        /// 为自定义物品配置礼盒数据
        /// 这部分是不是应该放进另一个前置里? 开箱算法是一个破坏性Patch
        /// 还是算了, 数据处理放在这, 数据读取另存
        /// </summary>
        /// <param name="template"></param>
        /// <param name="configServer"></param>
        /// 
        /// 
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
        public static List<Item> GetGiftItemByType(GiftData giftData, string hash, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            var result = new List<Item>();
            switch (giftData)
            {
                case GiftDataCustomPreset customPreset:
                    {
                        var itemlist = RegenerateItemListData(ConvertItemListData(customPreset.Item, cloner), hash, cloner);
                        return itemlist;
                    }
                case GiftDataVanillaPreset vanillaPreset:
                    {
                        var itemlist = RegenerateItemListData(GetPreset(vanillaPreset.Item, hash, databaseService, logger, cloner), hash, cloner);
                        return itemlist;
                    }
                case GiftDataItemData item:
                    {
                        var itemid = item.ItemId;
                        var mainitemid = new MongoId();
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
                        return RegenerateItemListData(itemlist, hash, cloner);
                    }
                case GiftDataContainerData container:
                    {
                        var itemlist = RegenerateItemListData(ConvertItemListData(container.Item, cloner), hash, cloner);
                        return itemlist;
                    }
                default:
                    {
                        return result;
                    }
            }
        }
        public static void AddAmmoToAmmoBoxInList(MongoId mainitemid, MongoId itemid, List<Item> itemlist, DatabaseService databaseService)
        {
            var ammopack = GetItem(itemid, databaseService);
            if (ammopack != null)
            {
                var parent = mainitemid;
                var maxstackcount = (double)ammopack.Properties.StackSlots.First().MaxCount;
                var ammo = ammopack.Properties.StackSlots.First().Properties.Filters.First().Filter.First();
                var ammoitem = GetItem(ammo, databaseService);
                if (ammoitem != null)
                {
                    var ammostackcount = (double)ammoitem.Properties.StackMaxSize;
                    var extrasize = maxstackcount > ammostackcount;
                    var lastcount = extrasize ? Math.Floor(maxstackcount % ammostackcount) : 0;
                    var stackcount = extrasize ? (int)Math.Floor(maxstackcount / ammostackcount) : 0;
                    var location = 0;
                    if (stackcount > 0)
                    {
                        for (var i = 0; i < stackcount; i++)
                        {
                            itemlist.Add(new Item
                            {
                                Id = Utils.ConvertHashID($"{parent}_ammo_{i}"),
                                Template = ammo,
                                ParentId = parent,
                                SlotId = "cartridges",
                                Location = i,
                                Upd = new Upd
                                {
                                    StackObjectsCount = Math.Floor(ammostackcount)
                                }
                            });
                            location = i;
                        }
                    }
                    else
                    {
                        itemlist.Add(new Item
                        {
                            Id = Utils.ConvertHashID($"{parent}_ammo_inside"),
                            Template = ammo,
                            ParentId = parent,
                            SlotId = "cartridges",
                            Location = 0,
                            Upd = new Upd
                            {
                                StackObjectsCount = Math.Floor(maxstackcount)
                            }
                        });
                    }
                    if (lastcount != 0)
                    {
                        itemlist.Add(new Item
                        {
                            Id = Utils.ConvertHashID($"{parent}_ammo_end"),
                            Template = ammo,
                            ParentId = parent,
                            SlotId = "cartridges",
                            Location = location + 1,
                            Upd = new Upd
                            {
                                StackObjectsCount = lastcount
                            }
                        });
                    }
                }
            }
        }
        public static void InitDrawPool(Dictionary<string, DrawPoolClass> drawPool)
        {
            foreach (var pool in drawPool)
            {
                DrawPoolData.TryAdd(pool.Value.Name, pool.Value);
            }
        }
        public static void InitDrawPool(string folderpath)
        {
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
        public static List<Item> GetAdvancedBoxData(MongoId sessionId, string drawpoolname, DrawPoolClass drawpool, JsonUtil jsonUtil, ItemHelper itemHelper, DatabaseService databaseService, ModHelper modHelper, ISptLogger<VulcanCore> logger, ICloner cloner)
        {
            //var modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var recordfile = System.IO.Path.Combine(modPath, "drawrecord.json");
            var recordContent = File.ReadAllText(recordfile);
            var result = new List<Item>();
            var drawrecord = jsonUtil.Deserialize<Dictionary<MongoId, Dictionary<string, DrawRecord>>>(recordContent);
            var localeService = ServiceLocator.ServiceProvider.GetService<LocaleService>();
            //var drawrecord = modHelper.GetJsonDataFromFile<Dictionary<MongoId, Dictionary<string, DrawRecord>>>(modPath, "drawrecord.json");
            Random random = new Random();
            if (!drawrecord.TryGetValue(sessionId, out var pmcrecord))
            {
                pmcrecord = new Dictionary<string, DrawRecord>();
                drawrecord[sessionId] = pmcrecord;  // 将新创建的 pmcrecord 存回 drawrecord
            }
            if (!pmcrecord.TryGetValue(drawpoolname, out var pooldata))
            {
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
                };  // 可以创建一个新的 DrawRecord
                pmcrecord[drawpoolname] = pooldata;  // 如果没有找到，则添加新的记录
            }
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
            var randomchance = Math.Floor(random.NextDouble() * 1000) / 1000;
            var srrealchance = Math.Floor((1 / (sr.ChanceGrowCount + 1 + ((1 - sr.Chance) / sr.ChanceGrowPerCount))) * 1000) / 1000;
            var upchance = Math.Floor(random.NextDouble() * 1000) / 1000;
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
            if ((randomchance <= (srrealchance + srdata.AddChance)) || (srdata.Count == (sr.ChanceGrowCount + 1 + Math.Floor(((1 - sr.Chance) / sr.ChanceGrowPerCount)))))
            {
                //VulcanLog.Warn("你抽到了金色传说! ", logger);
                var cachererord = new SuperRareCardRecord
                {
                    ItemId = "",
                    ItemName = "",
                    Count = srdata.Count,
                    IsUpReward = false
                };
                srdata.AddChance = 0;
                srdata.Count = 0;
                rdata.AddChance = 0;
                rdata.Count = 0;
                if (upchance <= (sr.UpChance + srdata.UpAddChance))
                {
                    //VulcanLog.Access("小保底没歪", logger);
                    srdata.UpAddChance = 0;
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(srpool.ChanceUp), Utils.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
                    var tpl = result.First().Template;
                    cachererord.ItemId = tpl;
                    cachererord.ItemName = itemHelper.GetItemName(tpl);
                    cachererord.IsUpReward = true;
                    srdata.Record.Add(cachererord);
                }
                else
                {
                    //VulcanLog.Error("哎呀, 小保底歪了", logger);
                    srdata.UpAddChance += sr.UpAddChance;
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(srpool.Normal), Utils.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
                    var tpl = result.First().Template;
                    cachererord.ItemId = tpl;
                    cachererord.ItemName = itemHelper.GetItemName(tpl);
                    srdata.Record.Add(cachererord);

                }
            }
            else if (randomchance <= (r.Chance) || (rdata.Count == Math.Floor((r.ChanceGrowCount + 1 + ((1 - r.Chance) / r.ChanceGrowPerCount)))))
            {
                //VulcanLog.Warn("你抽到了紫色史诗 ", logger);
                rdata.AddChance = 0;
                rdata.Count = 0;
                if (upchance <= (r.UpChance + rdata.UpAddChance))
                {
                    //VulcanLog.Access("保底没歪", logger);
                    rdata.UpAddChance = 0;
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(rpool.ChanceUp), Utils.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
                }
                else
                {
                    //VulcanLog.Error("哎呀, 保底歪了", logger);
                    rdata.UpAddChance += r.UpAddChance;
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(rpool.Normal), Utils.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
                }
            }
            else
            {
                //VulcanLog.Debug("很遗憾, 你抽到了一坨垃圾:( ", logger);
                //VulcanLog.Debug("无需灰心, 霉运乃人生常事, 少侠请重新来过", logger);
                if (upchance < normal.UpChance)
                {
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(normalpool.ChanceUp), Utils.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
                }
                else
                {
                    result = GetGiftItemByType(Utils.DrawFromList<GiftData>(normalpool.Normal), Utils.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
                }
            }
            var dwarrecordstring = jsonUtil.Serialize(drawrecord, true);
            //VulcanLog.Access("抽卡统计结束", logger);
            File.WriteAllText(recordfile, dwarrecordstring);
            //VulcanLog.Debug(dwarrecordstring, logger);
            //VulcanLog.Warn("警告! 无法获取卡池信息", logger);
            return result;
        }
        public static void AddModsToInventory(BotBaseInventory inventory, MongoId itemid, MongoId targetid, string slotid, ISptLogger<VulcanCore> logger)
        {
            var items = inventory.Items.FirstOrDefault(x => x.Template == targetid);
            if (items == null)
            {
                return;
            }
            else
            {
                var parentid = items.Id;
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
            }
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
            if (template.CustomProps!=null && template.CustomProps.ApplyAsPMCDogTag == true)
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
        
        public static List<string> GetItemListByRagfairTag(MongoId ragfairTag, DatabaseService databaseService)
        {
            var result = new List<string>();
            var handbooks = databaseService.GetHandbook().Items;
            handbooks
            .Where(x => x.ParentId == ragfairTag)
            .ToList()?
            .ForEach(x => result.Add(x.Id));
            return result;
        }
    }
}
