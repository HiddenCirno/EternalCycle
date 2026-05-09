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
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
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
using VulcanCore;

namespace VulcanCore
{
    public class OpenRandomLootContainerPatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(InventoryController).GetMethod("OpenRandomLootContainer", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }
        [PatchPrefix]
        public static bool Prefix(InventoryController __instance, PmcData pmcData, OpenRandomLootContainerRequestData request, MongoId sessionId, ItemEventRouterResponse output)
        {
            var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
            var logger = ServiceLocator.ServiceProvider.GetService<ISptLogger<VulcanCore>>();
            var configServer = ServiceLocator.ServiceProvider.GetService<ConfigServer>();
            var itemHelper = ServiceLocator.ServiceProvider.GetService<ItemHelper>();
            var inventoryHelper = ServiceLocator.ServiceProvider.GetService<InventoryHelper>();
            var profileHelper = ServiceLocator.ServiceProvider.GetService<ProfileHelper>();
            var traderHelper = ServiceLocator.ServiceProvider.GetService<TraderHelper>();
            var modHelper = ServiceLocator.ServiceProvider.GetService<ModHelper>();
            var jsonUtil = ServiceLocator.ServiceProvider.GetService<JsonUtil>();
            var lootGenerator = ServiceLocator.ServiceProvider.GetService<LootGenerator>();
            var cloner = ServiceLocator.ServiceProvider.GetService<ICloner>();
            Random random = new Random();

            // Container player opened in their inventory
            var openedItem = pmcData.Inventory.Items.FirstOrDefault(item => item.Id == request.Item);
            var containerDetailsDb = itemHelper.GetItem(openedItem.Template);
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
                        for(var i = 0; i < boxdata.Count; i++)
                        {
                            var result = ItemUtils.GetAdvancedBoxData(sessionId, drawpoolname, drawpool, jsonUtil, itemHelper, databaseService, modHelper, logger, cloner);
                            if (result.Count > 0)
                            {
                                rewards.Add(result);
                            }
                        }
                    }
                }
            }
            else if (isstaticbox)
            {
                ItemUtils.StaticBoxData.TryGetValue(itemid, out var boxdata);
                if (boxdata != null)
                {
                    VulcanLog.Debug("进入静态箱子流程", logger);
                    var giftdata = boxdata.GiftData;
                    if (boxdata.ForcedFindInRaid) foundInRaid = true;
                    foreach (var data in giftdata)
                    {
                        VulcanLog.Debug("正在检查数据....", logger);
                        var hashkey = VulcanUtil.ConvertHashID(DateTime.Now.ToString());
                        var reward = ItemUtils.GetGiftItemByType(data, hashkey, databaseService, logger, cloner);
                        if (reward.Count > 0)
                        {
                            VulcanLog.Debug("数据返回成功", logger);
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
                                    profileHelper.AddSkillPointsToPlayer(pmcData, skillData.Skill, (double)skillData.Count, false);
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
                                    profileHelper.AddExperienceToPmc(sessionId, experienceData.Count);
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
                                    traderHelper.AddStandingToTrader(sessionId, traderStandingData.TraderId, traderStandingData.Count);
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
                    var containerSettings = inventoryHelper.GetInventoryConfig().SealedAirdropContainer;
                    rewards.AddRange(lootGenerator.GetSealedWeaponCaseLoot(containerSettings));

                    if (containerSettings.FoundInRaid)
                    {
                        foundInRaid = containerSettings.FoundInRaid;
                    }
                }
                else
                {
                    var rewardContainerDetails = inventoryHelper.GetRandomLootContainerRewardDetails(openedItem.Template);
                    if (rewardContainerDetails?.RewardCount == null)
                    {
                        logger.Error($"Unable to add loot to container: {openedItem.Template}, no rewards found");
                    }
                    else
                    {
                        rewards.AddRange(lootGenerator.GetRandomLootContainerLoot(rewardContainerDetails));

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
                inventoryHelper.AddItemsToStash(sessionId, addItemsRequest, pmcData, output);
                if (output.Warnings?.Count > 0)
                {
                    return false;
                }
            }

            // Find and delete opened container item from player inventory
            inventoryHelper.RemoveItemByCount(pmcData, request.Item, 1, sessionId, output);

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