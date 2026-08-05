using EternalCycleServer;
using HarmonyLib;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Razor.TagHelpers;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System;
using System.Net;
using System.Reflection;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static EternalCycleServer.ContextManager;

namespace EternalCycleServer
{
    [Injectable]
    public class OpenRandomLootContainerPatch : AbstractPatch
    {

        private static TemplateTable _templateTable = default!;
        private static LocaleTable _localeTable = default!;
        private static GlobalTable _globalTable = default!;
        private static TradersTable _tradersTable = default!;
        private static HideoutTable _hideoutTable = default!;
        private static LocationTable _locationTable = default!;
        private static JsonUtil _jsonUtil = default!;
        private static ConfigServer _configServer = default!;
        private static ModHelper _modHelper = default!;
        private static ProfileHelper _profileHelper = default!;
        private static TraderHelper _traderHelper = default!;
        private static InventoryHelper _inventoryHelper = default!;
        private static LootGenerator _lootGenerator = default!;
        private static ItemHelper _itemHelper = default!;
        private static ICloner _cloner = default!;
        private static PresetHelper _presetHelper = default!;
        private static ImageRouter _imageRouter = default!;
        private static ECLogger _logger = default!;
        public OpenRandomLootContainerPatch(
        TemplateTable templateTable,
        LocaleTable localeTable,
        GlobalTable globalTable,
        TradersTable tradersTable,
        HideoutTable hideoutTable,
        LocationTable locationTable,
        JsonUtil jsonUtil,
        ConfigServer configServer,
        ModHelper modHelper,
        ProfileHelper profileHelper,
        TraderHelper traderHelper,
        InventoryHelper inventoryHelper,
        LootGenerator lootGenerator,
        ItemHelper itemHelper,
        ICloner cloner,
        PresetHelper presetHelper,
        ImageRouter imageRouter)
        {
            _templateTable = templateTable;
            _localeTable = localeTable;
            _globalTable = globalTable;
            _tradersTable = tradersTable;
            _jsonUtil = jsonUtil;
            _configServer = configServer;
            _modHelper = modHelper;
            _profileHelper = profileHelper;
            _traderHelper = traderHelper;
            _hideoutTable = hideoutTable;
            _locationTable = locationTable;
            _inventoryHelper = inventoryHelper;
            _lootGenerator = lootGenerator;
            _itemHelper = itemHelper;
            _cloner = cloner;
            _presetHelper = presetHelper;
            _imageRouter = imageRouter;
            _logger = new ECLogger("OpenRandomLootContainer", true);
        }
        protected override MethodBase GetTargetMethod()
        {
            return typeof(InventoryController).GetMethod("OpenRandomLootContainer", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }
        [PatchPrefix]
        public static bool Prefix(InventoryController __instance, PmcData pmcData, OpenRandomLootContainerRequestData request, MongoId sessionId, ItemEventRouterResponse output)
        {
            var context = new LoadModContext
            {
                DB = new DatabaseService(_templateTable, _localeTable, _globalTable, _tradersTable, _hideoutTable, _locationTable),
                JsonUtil = _jsonUtil,
                ConfigServer = _configServer,
                ModHelper = _modHelper,
                Logger = Utils.commonLogger,
                PresetHelper = _presetHelper,
                ImageRouter = _imageRouter,
                ItemHelper = _itemHelper,
                Cloner = _cloner
            };
            Random random = new Random();

            // Container player opened in their inventory
            var openedItem = pmcData.Inventory.Items.FirstOrDefault(item => item.Id == request.Item);
            var containerDetailsDb = context.ItemHelper.GetItem(openedItem.Template);
            var isSealedWeaponBox = containerDetailsDb.Value.Name.Contains("event_container_airdrop");

            var foundInRaid = openedItem.Upd?.SpawnedInSession;
            var rewards = new List<List<Item>>();
            var unlockedWeaponCrates = new HashSet<MongoId>
        {
            ItemTpl.RANDOMLOOTCONTAINER_ARENA_WEAPONCRATE_VIOLET_OPEN,
            ItemTpl.RANDOMLOOTCONTAINER_ARENA_WEAPONCRATE_BLUE_OPEN,
            ItemTpl.RANDOMLOOTCONTAINER_ARENA_WEAPONCRATE_GREEN_OPEN,
        };
            var itemid = containerDetailsDb.Value.Id;
            var isadvbox = ItemUtils.AdvancedBoxData.ContainsKey(itemid);//false; //placeholder
            var isstaticbox = ItemUtils.StaticBoxData.ContainsKey(itemid);
            var isspecialbox = ItemUtils.SpecialBoxData.ContainsKey(itemid);//false; //placeholder
            // Temp fix for unlocked weapon crate hideout craft
            //VulcanLog.Log($"{itemHelper.GetItemName(VulcanUtil.ConvertHashID("基建材料抽奖箱"))}", logger);
            if (isadvbox)
            {
                //可算到这了
                //所以卡池数据应该怎么办呢
                ItemUtils.AdvancedBoxData.TryGetValue(itemid, out var boxdata);
                if (boxdata != null)
                {
                    var drawpoolname = boxdata.PoolName;
                    if (boxdata.ForcedFindInRaid) foundInRaid = true;
                    ItemUtils.DrawPoolData.TryGetValue(drawpoolname, out var drawpool);
                    if (drawpool != null)
                    {
                        var modPath = context.ModHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
                        var recordfile = System.IO.Path.Combine(modPath, "drawrecord.json");
                        Dictionary<MongoId, Dictionary<string, DrawRecord>> currentRecordCache;
                        if (File.Exists(recordfile))
                        {
                            currentRecordCache = context.JsonUtil.Deserialize<Dictionary<MongoId, Dictionary<string, DrawRecord>>>(File.ReadAllText(recordfile))
                                                 ?? new Dictionary<MongoId, Dictionary<string, DrawRecord>>();
                        }
                        else
                        {
                            currentRecordCache = new Dictionary<MongoId, Dictionary<string, DrawRecord>>();
                        }
                        for (var i = 0; i < boxdata.Count; i++)
                        {
                            var result = ItemUtils.GetAdvancedBoxData(sessionId, drawpoolname, drawpool, currentRecordCache, context);
                            if (result.Count > 0)
                            {
                                rewards.Add(result);
                            }
                        }
                        File.WriteAllText(recordfile, context.JsonUtil.Serialize(currentRecordCache, true));
                    }
                }
            }
            else if (isstaticbox)
            {
                ItemUtils.StaticBoxData.TryGetValue(itemid, out var boxdata);
                if (boxdata != null)
                {
                    //VulcanLog.Debug("进入静态箱子流程", logger);
                    var giftdata = boxdata.GiftData;
                    if (boxdata.ForcedFindInRaid) foundInRaid = true;
                    foreach (var data in giftdata)
                    {
                        //VulcanLog.Debug("正在检查数据....", logger);
                        var hashkey = Utils.ConvertHashID(DateTime.Now.ToString());
                        var reward = ItemUtils.GetGiftItemByType(data, hashkey, context);
                        if (reward.Count > 0)
                        {
                            //VulcanLog.Debug("数据返回成功", logger);
                            rewards.Add(reward);
                        }
                    }
                }
            }
            else if (isspecialbox)
            {
                ItemUtils.SpecialBoxData.TryGetValue(itemid, out var boxdata);
                if (boxdata != null)
                {
                    foreach (var data in boxdata)
                    {
                        switch (data)
                        {
                            case GiftDataSkillData skillData:
                                {
                                    rewards.Add(new List<Item>
                                    {
                                        new Item
                                        {
                                            Id = new MongoId(),
                                            Template = skillData.ItemId,
                                            Upd = new Upd
                                            {
                                                StackObjectsCount = 1
                                            }
                                        }
                                    });
                                    _profileHelper.AddSkillPointsToPlayer(pmcData, skillData.Skill, (double)skillData.Count, false);
                                }
                                break;
                            case GiftDataExperienceData experienceData:
                                {
                                    rewards.Add(new List<Item>
                                    {
                                        new Item
                                        {
                                            Id = new MongoId(),
                                            Template = experienceData.ItemId,
                                            Upd = new Upd
                                            {
                                                StackObjectsCount = 1
                                            }
                                        }
                                    });
                                    _profileHelper.AddExperienceToPmc(sessionId, experienceData.Count);
                                }
                                break;
                            case GiftDataTraderStandingData traderStandingData:
                                {
                                    rewards.Add(new List<Item>
                                    {
                                        new Item
                                        {
                                            Id = new MongoId(),
                                            Template = traderStandingData.ItemId,
                                            Upd = new Upd
                                            {
                                                StackObjectsCount = 1
                                            }
                                        }
                                    });
                                    _traderHelper.AddStandingToTrader(sessionId, traderStandingData.TraderId, traderStandingData.Count);
                                }
                                break;
                        }
                    }
                }
            }
            else
            {
                if (isSealedWeaponBox || unlockedWeaponCrates.Contains(containerDetailsDb.Value.Id))
                {
                    var containerSettings = _inventoryHelper.GetInventoryConfig().SealedAirdropContainer;
                    rewards.AddRange(_lootGenerator.GetSealedWeaponCaseLoot(containerSettings));

                    if (containerSettings.FoundInRaid)
                    {
                        foundInRaid = containerSettings.FoundInRaid;
                    }
                }
                else
                {
                    var rewardContainerDetails = _inventoryHelper.GetRandomLootContainerRewardDetails(openedItem.Template);
                    if (rewardContainerDetails?.RewardCount == null)
                    {
                        _logger.Error($"Unable to add loot to container: {openedItem.Template}, no rewards found");
                    }
                    else
                    {
                        rewards.AddRange(_lootGenerator.GetRandomLootContainerLoot(rewardContainerDetails));

                        if (rewardContainerDetails.FoundInRaid)
                        {
                            foundInRaid = rewardContainerDetails.FoundInRaid;
                        }
                    }
                }
            }

            // Add items to player inventory
            if (rewards.Count > 0)
            {
                var addItemsRequest = new AddItemsDirectRequest
                {
                    ItemsWithModsToAdd = rewards,
                    FoundInRaid = foundInRaid,
                    Callback = null,
                    UseSortingTable = true,
                };
                _inventoryHelper.AddItemsToStash(sessionId, addItemsRequest, pmcData, output);
                if (output.Warnings?.Count > 0)
                {
                    return false;
                }
            }

            // Find and delete opened container item from player inventory
            _inventoryHelper.RemoveItemByCount(pmcData, request.Item, 1, sessionId, output);

            return false;
        }
    }

    public class DrawRecord
    {
        [JsonPropertyName("SuperRare")]
        public SuperRareRecord SuperRare { get; set; }
        [JsonPropertyName("Rare")]
        public RareRecord Rare { get; set; }
    }
    public class SuperRareRecord
    {
        [JsonPropertyName("AddChance")]
        public double AddChance { get; set; }
        [JsonPropertyName("Count")]
        public int Count { get; set; }
        [JsonPropertyName("UpAddChance")]
        public double UpAddChance { get; set; }
        [JsonPropertyName("Record")]
        public List<SuperRareCardRecord> Record { get; set; }

    }
    public class SuperRareCardRecord
    {
        [JsonPropertyName("ItemId")]
        public MongoId ItemId { get; set; }
        [JsonPropertyName("ItemName")]
        public string ItemName { get; set; }
        [JsonPropertyName("Count")]
        public int Count { get; set; }
        [JsonPropertyName("IsUpReward")]
        public bool IsUpReward { get; set; }
    }
    public class RareRecord
    {
        [JsonPropertyName("AddChance")]
        public double AddChance { get; set; }
        [JsonPropertyName("Count")]
        public int Count { get; set; }
        [JsonPropertyName("UpAddChance")]
        public double UpAddChance { get; set; }

    }
}