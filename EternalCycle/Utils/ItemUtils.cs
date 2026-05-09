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

namespace VulcanCore;


public class ItemUtils
{
    public static HashSet<CustomFixData> FixList = new HashSet<CustomFixData>();
    public static Dictionary<MongoId, StaticGiftBoxData> StaticBoxData = new Dictionary<MongoId, StaticGiftBoxData>();
    public static Dictionary<MongoId, List<GiftData>> SpecialBoxData = new Dictionary<MongoId, List<GiftData>>();
    public static Dictionary<MongoId, AdvancedGiftBoxData> AdvancedBoxData = new Dictionary<MongoId, AdvancedGiftBoxData>();
    public static Dictionary<string, DrawPoolClass> DrawPoolData = new Dictionary<string, DrawPoolClass>();
    public static bool firstlogin = false;
    public static string modPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    public static TemplateItem GetItem(string itemid, DatabaseService databaseService)
    {
        return databaseService.GetItems().FirstOrDefault(x => x.Value.Id == (MongoId)itemid).Value;
    }
    public static MongoId? GetItemRagfairTag(string itemid, DatabaseService databaseService)
    {
        var handbook = databaseService.GetHandbook();
        var item = handbook.Items.FirstOrDefault(x => x.Id == (MongoId)itemid);
        if (item != null)
        {
            return item.ParentId;
        }
        return null;
    }
    public static int GetItemMinPrice(string itemid, DatabaseService databaseService)
    {
        var item = GetItem(itemid, databaseService);
        var itemsid = (MongoId)itemid;
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
    public static void InitItem(Dictionary<string, CustomItemTemplate> items, string creator, string modname, ISptLogger<VulcanCore> logger, DatabaseService databaseService, ICloner cloner, ConfigServer configServer)
    {
        foreach (var item in items)
        {
            CreateAndAddItem(item.Value, item.Value.TargetId, creator, modname, logger, databaseService, cloner, configServer);
        }
    }
    public static void InitItem(string folderPath, string creator, string modname, ISptLogger<VulcanCore> logger, DatabaseService databaseService, JsonUtil jsonUtil, ICloner cloner, ConfigServer configServer)
    {
        List<string> files = Directory.GetFiles(folderPath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileContent = File.ReadAllText(file);
                string processedJson = VulcanUtil.RemoveJsonComments(fileContent);
                var item = VulcanUtil.ConvertItemData<CustomItemTemplate>(processedJson, jsonUtil);
                ItemUtils.CreateAndAddItem(item, item.TargetId, creator, modname, logger, databaseService, cloner, configServer);
            }
        }
    }
    public static void CreateAndAddItem(CustomItemTemplate template, string targetid, string creator, string modname, ISptLogger<VulcanCore> logger, DatabaseService databaseService, ICloner cloner, ConfigServer configServer)
    {
        //TemplateItem itemClone = VulcanUtil.DeepCopyJson(GetItem(targetid, databaseService));
        //物品模板复制
        TemplateItem itemClone = cloner.Clone(GetItem(targetid, databaseService));
        //原版属性覆盖
        VulcanUtil.CopyNonNullProperties(template.Props, itemClone.Properties);
        var itemid = VulcanUtil.ConvertHashID(template.Id);
        template.Id = itemid;
        //参数覆盖
        SetItemBaseData(template, itemClone);
        //CustomItemService.CreateItemFromClone();
        var _inventoryConfig = configServer.GetConfig<InventoryConfig>();
        //自定义货币处理
        if (template.CustomProps.IsMoney)
        {
            _inventoryConfig.CustomMoneyTpls.Add((MongoId)itemid);
        }
        //跳蚤市场价格处理
        //已经并入方法
        //if (template.CustomProps?.RagfairPrice != null)
        //{
        //    databaseService.GetPrices()[itemid] = (double)template.CustomProps.RagfairPrice;
        //}
        //Buff物品处理
        AddBuffItemData(template, configServer, databaseService);
        //黑名单处理
        if (template.CustomProps?.BlackListType != null)
        {
            AddBlackList(template, configServer);
        }
        //携带限制
        if (template.CustomProps.InRaidCountLimit != null)
        {
            SetInRaidLimitCount(template, databaseService);
        }
        //自定义狗牌生成
        if (template.CustomProps.ApplyAsPMCDogTag == true)
        {
            SetCustomPMCDogTag(template, configServer);
        }
        //手册数据
        AddPriceData(template, databaseService);
        //武器相关
        AddWeaponItemData(template, databaseService);
        //任务物品
        AddQuestItemGeneaate(template, databaseService, logger, cloner);
        //容器物品
        SetContainerSize(itemClone, template, databaseService);
        //Fix
        AddItemFixData(template, databaseService);
        //抽奖箱(原版/固定/技能, 高级抽卡需要技术验证
        //Finally
        SetGiftBoxData(template, databaseService, configServer, logger, cloner);
        //test
        LootUtils.AddStaticLoot(template, databaseService, logger);
        LootUtils.AddLooseLoot(template, databaseService, logger);
        //本地化数据
        var Locales = BuildItemLocales(template.CustomProps, creator, modname);
        LocaleUtils.AddItemToLocales(Locales, itemid, databaseService);
        //尝试添加物品
        databaseService.GetItems().TryAdd(itemid, itemClone);
        //Kappa
        if (template.CustomProps.AddToKappa == true)
        {
            AddItemToKappa(template, databaseService, cloner);
        }
        VulcanLog.Debug($"物品添加成功: {template.CustomProps.Name}", logger);
    }
    public static void AddItemToKappa(CustomItemTemplate item, DatabaseService databaseService, ICloner cloner)
    {
        var kappa = QuestUtils.GetQuest(QuestTpl.COLLECTOR, databaseService);
        var twitchcase = GetItem(ItemTpl.CONTAINER_STREAMER_ITEM_CASE, databaseService);
        var conditions = kappa.Conditions.AvailableForFinish;
        var itemid = VulcanUtil.ConvertHashID(item.Id);
        QuestUtils.InitHandoverItemDataConditions(conditions, new HandoverItemData
        {
            Id = VulcanUtil.ConvertHashID($"Kappa_{item.Id}"),
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
    public static Dictionary<string, LocaleDetails> BuildItemLocales(CustomProps props, string creator, string modname)
    {
        var locales = new Dictionary<string, LocaleDetails>();
        var modstring = $"<color=#FFFFFF><b>\n由{creator}创建\n添加者: {modname}\n物品API：火神之心\n物品ID：#ItemId</b></color>";
        var chdescription = $"{props.Description}{modstring}";
        // 默认中文
        locales["ch"] = new LocaleDetails
        {
            Name = props.Name,
            ShortName = props.ShortName,
            Description = chdescription
        };

        // 英文（优先英文字段）
        locales["en"] = new LocaleDetails
        {
            Name = string.IsNullOrEmpty(props.EName) ? props.Name : props.EName,
            ShortName = string.IsNullOrEmpty(props.EShortName) ? props.ShortName : props.EShortName,
            Description = string.IsNullOrEmpty(props.EDescription) ? chdescription : props.EDescription
        };

        // 日语（默认中文）
        locales["jp"] = new LocaleDetails
        {
            Name = string.IsNullOrEmpty(props.JName) ? props.Name : props.JName,
            ShortName = string.IsNullOrEmpty(props.JShortName) ? props.ShortName : props.JShortName,
            Description = string.IsNullOrEmpty(props.JDescription) ? chdescription : props.JDescription
        };

        return locales;
    }
    public static void AddBlackList(CustomItemTemplate template, ConfigServer configServer)
    {
        List<string> blacklist = BitMapUtils.GetBlackListCode(template.CustomProps.BlackListType);
        foreach (string black in blacklist)
        {
            string itemid = template.Id;
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
    public static void AddBuffItemData(CustomItemTemplate template, ConfigServer configserver, DatabaseService databaseService)
    {
        Globals globals = databaseService.GetGlobals();
        if (template.CustomProps is BuffItemProps itemProps)
        {
            globals.Configuration.Health.Effects.Stimulator.Buffs[template.Props.StimulatorBuffs] = itemProps.BuffValue;
        }
    }
    public static void AddItemFixData(CustomItemTemplate template, DatabaseService databaseService)
    {
        if (template.CustomProps is CustomFixedItemProps itemProps)
        {
            var customFixData = new CustomFixData
            {
                FixType = itemProps.FixType,
                ItemId = VulcanUtil.ConvertHashID(template.Id),
                TargetId = itemProps.CustomFixID != null ? (MongoId)itemProps.CustomFixID : template.TargetId
            };
            FixList.Add(customFixData);
        }
    }
    public static void AddPriceData(CustomItemTemplate template, DatabaseService databaseService)
    {
        string itemid = template.Id;
        string targetid = template.TargetId;
        var handbook = databaseService.GetHandbook().Items;
        var price = databaseService.GetPrices();
        var itemragfairprice = template.CustomProps.RagfairPrice;
        var targethandbook = handbook.FirstOrDefault(x => x.Id == targetid);
        var logger = ServiceLocator.ServiceProvider.GetService<ISptLogger<VulcanCore>>();

        // 价格计算逻辑：根据 CopyPrice 或者默认价格决定价格
        var priceToSet = (template.CustomProps.CopyPrice == true && targethandbook != null)
                            ? targethandbook.Price
                            : (double)template.CustomProps.DefaultPrice;
        // 如果需要复制价格，或没有找到目标手册项，设置价格
        handbook.Add(new HandbookItem
        {
            Id = itemid,
            ParentId = VulcanUtil.ConvertHashID(template.CustomProps.RagfairType),
            Price = (double)priceToSet
        });
        if (template.CustomProps.CopyPrice == true && price.ContainsKey(targetid))
        {
            price.TryAdd(itemid, price[targetid]);
        }
        else if (itemragfairprice != null)
        {
            price.TryAdd(itemid, (double)itemragfairprice);
            price.TryGetValue(itemid, out var test);
            //VulcanLog.Debug($"{test}", logger);
        }
        else
        {
            price.TryAdd(itemid, (double)template.CustomProps.DefaultPrice);
        }
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
    public static void SetContainerSize(TemplateItem itemTemplate, CustomItemTemplate template, DatabaseService databaseService)
    {
        if (template.CustomProps is CustomSizeContainerProps itemProps)
        {
            var grid = itemTemplate.Properties.Grids.ToList();
            grid[0].Properties.CellsH = itemProps.ContainerCellsH;
            grid[0].Properties.CellsV = itemProps.ContainerCellsV;
            itemTemplate.Properties.Grids = grid;
        }
    }
    public static void AddWeaponItemData(CustomItemTemplate template, DatabaseService databaseService)
    {
        if (template.CustomProps is WeaponItemProps itemProps)
        {
            if (itemProps?.FixMastering == true)
            {
                FixWeaponMastering(template, databaseService);
            }
            if (itemProps?.AddMastering == true)
            {
                AddWeaponMastering(template, databaseService);
            }
        }
    }
    public static void FixWeaponMastering(CustomItemTemplate template, DatabaseService databaseService)
    {
        Globals globals = databaseService.GetGlobals();
        foreach (Mastering mastering in globals.Configuration.Mastering)
        {
            WeaponItemProps itemProps = (WeaponItemProps)template.CustomProps;
            if (itemProps?.CustomMasteringTarget != null)
            {
                if (mastering.Templates.Contains(itemProps.CustomMasteringTarget))
                {
                    List<MongoId> list = mastering.Templates?.ToList() ?? new List<MongoId>();
                    list.Add((MongoId)template.Id); // 添加新元素
                    mastering.Templates = list;
                }
            }
            else
            {
                if (mastering.Templates.Contains(template.TargetId))
                {
                    List<MongoId> list = mastering.Templates?.ToList() ?? new List<MongoId>();
                    list.Add((MongoId)template.Id); // 添加新元素
                    mastering.Templates = list;
                }
            }

        }
    }
    public static void AddWeaponMastering(CustomItemTemplate template, DatabaseService databaseService)
    {
        Globals globals = databaseService.GetGlobals();
        WeaponItemProps itemProps = (WeaponItemProps)template.CustomProps;
        globals.Configuration.Mastering = VulcanUtil.AddToArray<Mastering>(globals.Configuration.Mastering, itemProps.Mastering);
    }
    public static void AddQuestItemGeneaate(CustomItemTemplate template, DatabaseService databaseService, ISptLogger<VulcanCore> logger, ICloner cloner)
    {
        if (template.CustomProps is QuestItemProps questItemProps)
        {
            //VulcanLog.Debug("111", logger);
            var spawnpoint = questItemProps.SpawnPointData;
            var looseloot = databaseService.GetLocation(spawnpoint.Location)?.LooseLoot;
            if (looseloot != null)
            {
                looseloot.AddTransformer(delegate (LooseLoot loostLoot)
                {
                    VulcanLog.Debug(loostLoot.SpawnpointsForced.Count().ToString(), logger);
                    spawnpoint.Template.Root = VulcanUtil.ConvertHashID(spawnpoint.Template.Root);
                    var list = loostLoot.SpawnpointsForced.ToList();
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
                            Items = new List<SptLootItem>()
                        }
                    };
                    var spawnpointitemlist = newspawnpoint.Template.Items.ToList();
                    foreach (var item in spawnpoint.Template.Items)
                    {
                        spawnpointitemlist.Add(new SptLootItem
                        {
                            Id = item.Id,
                            Template = item.Template
                        });
                        VulcanLog.Debug(spawnpoint.Template.Root, logger);
                        VulcanLog.Debug(item.Id, logger);
                    }
                    newspawnpoint.Template.Items = spawnpointitemlist;
                    list.Add(newspawnpoint);
                    loostLoot.SpawnpointsForced = list;
                    VulcanLog.Debug(loostLoot.SpawnpointsForced.Count().ToString(), logger);
                    return loostLoot;
                });
            }
        }
    }
    public static List<Item> ConvertItemListData(List<CustomItem> itemlist, ICloner cloner)
    {
        var list = new List<Item>();
        foreach (CustomItem item in itemlist)
        {
            var copyitem = cloner.Clone<Item>(item);
            if (copyitem.ParentId != null && copyitem.ParentId != "hideout")
            {
                copyitem.ParentId = VulcanUtil.ConvertHashID(copyitem.ParentId);
            }
            list.Add((Item)copyitem);
        }
        return list;
    }
    public static List<Item> RegenerateItemListData(List<Item> itemlist, string addinfo, ICloner cloner)
    {
        var list = new List<Item>();
        foreach (Item item in itemlist)
        {
            var copyitem = cloner.Clone<Item>(item);
            copyitem.Id = VulcanUtil.ConvertHashID($"{copyitem.Id}_{addinfo}");
            if (copyitem.ParentId != null && copyitem.ParentId != "hideout")
            {
                copyitem.ParentId = VulcanUtil.ConvertHashID($"{copyitem.ParentId}_{addinfo}");
            }
            list.Add((Item)copyitem);
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
                filter.Add(VulcanUtil.ConvertHashID(str));
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
                filter.Add(VulcanUtil.ConvertHashID(str));
            }
        }
    }
    public static void SetGiftBoxData(CustomItemTemplate template, DatabaseService databaseService, ConfigServer configServer, ISptLogger<VulcanCore> logger, ICloner cloner)
    {
        var inventoryConfig = configServer.GetConfig<InventoryConfig>();
        var itemid = VulcanUtil.ConvertHashID(template.Id);
        if (template.CustomProps is GiftBoxProps itemProps)
        {
            if (itemProps.IsGiftBox != null && itemProps.IsGiftBox == true)
            {
                var boxdata = itemProps.BoxData;
                var randomloot = inventoryConfig.RandomLootContainers;
                var rewardpool = new Dictionary<MongoId, double>();
                foreach (var kvp in boxdata.Rewards)
                {
                    rewardpool.TryAdd(VulcanUtil.ConvertHashID(kvp.Key), kvp.Value);
                }
                randomloot.TryAdd(itemid, new RewardDetails
                {
                    RewardCount = boxdata.Count,
                    FoundInRaid = true,
                    RewardTplPool = rewardpool
                });
            }
            if (itemProps.IsStaticBox != null && itemProps.IsStaticBox == true)
            {
                var boxdata = itemProps.StaticBoxData;
                StaticBoxData.TryAdd(itemid, boxdata);
            }
            if (itemProps.IsSpecialBox != null && itemProps.IsSpecialBox == true)
            {
                var boxdata = itemProps.SpecialBoxData;
                SpecialBoxData.TryAdd(itemid, boxdata.GiftData);
            }
            //adv还没写
            //写了
            if (itemProps.IsAdvGiftBox != null && itemProps.IsAdvGiftBox == true)
            {
                var boxdata = itemProps.AdvancedBoxData;
                AdvancedBoxData.TryAdd(itemid, itemProps.AdvancedBoxData);
            }
        }
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
                            Id = VulcanUtil.ConvertHashID($"{parent}_ammo_{i}"),
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
                        Id = VulcanUtil.ConvertHashID($"{parent}_ammo_inside"),
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
                        Id = VulcanUtil.ConvertHashID($"{parent}_ammo_end"),
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
        VulcanLog.Debug("开始统计抽卡结果", logger);
        VulcanLog.Debug($"当前卡池: {drawpoolname}", logger);
        VulcanLog.Debug("开始进行抽卡计算", logger);
        VulcanLog.Debug($"当前金色数据: 累加概率: {srdata.AddChance}, 抽取次数: {srdata.Count}, 保底叠加概率: {srdata.UpAddChance}", logger);
        VulcanLog.Debug($"当前紫色数据: 累加概率: {rdata.AddChance}, 抽取次数: {rdata.Count}, 保底叠加概率: {rdata.UpAddChance}", logger);
        VulcanLog.Debug($"当前金色概率: {randomchance}/{srrealchance + srdata.AddChance}", logger);
        if ((randomchance <= (srrealchance + srdata.AddChance)) || (srdata.Count == (sr.ChanceGrowCount + 1 + Math.Floor(((1 - sr.Chance) / sr.ChanceGrowPerCount)))))
        {
            VulcanLog.Warn("你抽到了金色传说! ", logger);
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
                VulcanLog.Access("小保底没歪", logger);
                srdata.UpAddChance = 0;
                result = GetGiftItemByType(VulcanUtil.DrawFromList<GiftData>(srpool.ChanceUp), VulcanUtil.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
                var tpl = result.First().Template;
                cachererord.ItemId = tpl;
                cachererord.ItemName = itemHelper.GetItemName(tpl);
                cachererord.IsUpReward = true;
                srdata.Record.Add(cachererord);
            }
            else
            {
                VulcanLog.Error("哎呀, 小保底歪了", logger);
                srdata.UpAddChance += sr.UpAddChance;
                result = GetGiftItemByType(VulcanUtil.DrawFromList<GiftData>(srpool.Normal), VulcanUtil.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
                var tpl = result.First().Template;
                cachererord.ItemId = tpl;
                cachererord.ItemName = itemHelper.GetItemName(tpl);
                srdata.Record.Add(cachererord);

            }
        }
        else if (randomchance <= (r.Chance) || (rdata.Count == Math.Floor((r.ChanceGrowCount + 1 + ((1 - r.Chance) / r.ChanceGrowPerCount)))))
        {
            VulcanLog.Warn("你抽到了紫色史诗 ", logger);
            rdata.AddChance = 0;
            rdata.Count = 0;
            if (upchance <= (r.UpChance + rdata.UpAddChance))
            {
                VulcanLog.Access("保底没歪", logger);
                rdata.UpAddChance = 0;
                result = GetGiftItemByType(VulcanUtil.DrawFromList<GiftData>(rpool.ChanceUp), VulcanUtil.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
            }
            else
            {
                VulcanLog.Error("哎呀, 保底歪了", logger);
                rdata.UpAddChance += r.UpAddChance;
                result = GetGiftItemByType(VulcanUtil.DrawFromList<GiftData>(rpool.Normal), VulcanUtil.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
            }
        }
        else
        {
            VulcanLog.Debug("很遗憾, 你抽到了一坨垃圾:( ", logger);
            VulcanLog.Debug("无需灰心, 霉运乃人生常事, 少侠请重新来过", logger);
            if (upchance < normal.UpChance)
            {
                result = GetGiftItemByType(VulcanUtil.DrawFromList<GiftData>(normalpool.ChanceUp), VulcanUtil.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
            }
            else
            {
                result = GetGiftItemByType(VulcanUtil.DrawFromList<GiftData>(normalpool.Normal), VulcanUtil.ConvertHashID($"{DateTime.Now.ToString()}_{srdata.Count}"), databaseService, logger, cloner);
            }
        }
        var dwarrecordstring = jsonUtil.Serialize(drawrecord, true);
        VulcanLog.Access("抽卡统计结束", logger);
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
    public static void SetInRaidLimitCount(CustomItemTemplate template, DatabaseService databaseService)
    {
        var globals = databaseService.GetGlobals();
        var limits = globals.Configuration.RestrictionsInRaid.ToList();
        limits.Add(new RestrictionsInRaid
        {
            TemplateId = VulcanUtil.ConvertHashID(template.Id),
            MaxInLobby = (double)template.CustomProps.InLobbyCountLimit,
            MaxInRaid = (double)template.CustomProps.InRaidCountLimit
        });
        globals.Configuration.RestrictionsInRaid = limits.ToArray();
    }
    public static void SetCustomPMCDogTag(CustomItemTemplate template, ConfigServer configServer)
    {
        var pmcconfig = configServer.GetConfig<PmcConfig>();
        var customprops = template.CustomProps;
        if (customprops.ApplyToBEAR == true)
        {
            SetCustomDotTagGenerate(template, PlayerSide.Bear, configServer);
        }
        if (customprops.ApplyToUSEC == true)
        {
            SetCustomDotTagGenerate(template, PlayerSide.Usec, configServer);
        }
    }
    public static void SetCustomDotTagGenerate(CustomItemTemplate template, PlayerSide side, ConfigServer configServer)
    {

        var pmcconfig = configServer.GetConfig<PmcConfig>();
        var customprops = template.CustomProps;
        var sidestring = side == PlayerSide.Bear ? "bear" : "usec";
        var itemid = VulcanUtil.ConvertHashID(template.Id);
        if (customprops.ApplyToStandard == true)
        {
            pmcconfig.DogtagSettings[sidestring]["default"].Add(itemid, 1);
        }
        if (customprops.ApplyToEOD == true)
        {
            pmcconfig.DogtagSettings[sidestring]["edge_of_darkness"].Add(itemid, 1);
        }
        if (customprops.ApplyToUnheard == true)
        {
            pmcconfig.DogtagSettings[sidestring]["unheard_edition"].Add(itemid, 1);
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

