using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
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
using System.IO;
using System.Reflection;
using System.Text.Json;
using Path = System.IO.Path;
namespace VulcanCore;
public class AssortUtils
{
    public static void InitAssortData(List<CustomAssortData> assortData, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        foreach (CustomAssortData assort in assortData)
        {
            switch (assort)
            {
                case CustomNormalAssortData customAssortData:
                    {
                        InitAssort(assort, databaseService, cloner, logger);
                    }
                    break;
                case CustomLockedAssortData customLockedAssortData:
                    {
                        var assortUnlockRewardData = new CustomAssortUnlockRewardData
                        {
                            Id = (MongoId)VulcanUtil.ConvertHashID($"{customLockedAssortData.Id}_Locked"),
                            QuestId = (MongoId)VulcanUtil.ConvertHashID(customLockedAssortData.QuestId),
                            QuestStage = customLockedAssortData.QuestStage,
                            IsUnknownReward = customLockedAssortData.IsUnknownReward,
                            AssortData = customLockedAssortData,
                        };
                        QuestUtils.InitAssortUnlockRewards(assortUnlockRewardData, databaseService, cloner, logger);
                    }
                    break;
            }
        }
    }
    public static void InitAssortData(string folderpath, DatabaseService databaseService, ModHelper modHelper, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var assort = modHelper.GetJsonDataFromFile<List<CustomAssortData>>(folderpath, fileName);
                InitAssortData(assort, databaseService, cloner, logger);
            }
        }
    }

    public static void InitAssort(CustomAssortData assortData, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        var assort = assortData;
        var assortid = VulcanUtil.ConvertHashID(assort.Id);
        var traderassort = TraderUtils.GetTrader((string)assort.Trader, databaseService).Assort;
        var items = ItemUtils.ConvertItemListData(assort.Item, cloner);
        var mainitem = items[0];
        var mainitemid = mainitem.Template;
        if (ItemUtils.GetItemRagfairTag(mainitemid, databaseService) == ERagfairTagsType.µ¯Ò©°ü)
        {
            ItemUtils.AddAmmoToAmmoBoxInList(mainitem.Id, mainitemid, items, databaseService);
        }
        foreach (Item item in items)
        {
            traderassort.Items.Add(item);
        }
        var barterlist = new List<List<BarterScheme>> {
            new List<BarterScheme>()
        };
        foreach (var barter in assort.Barter)
        {
            barterlist[0].Add(
            new BarterScheme
            {
                Template = VulcanUtil.ConvertHashID(barter.Key),
                Count = barter.Value
            });
        }
        foreach (var barter in assort.DogTag)
        {
            barterlist[0].Add(
            new BarterScheme
            {
                Template = VulcanUtil.ConvertHashID(barter.Key),
                Count = (double)barter.Value.Count,
                Level = barter.Value.Level,
                Side = (DogtagExchangeSide)barter.Value.Side
            });
        }
        traderassort.BarterScheme.Add((MongoId)assortid, barterlist);
        traderassort.LoyalLevelItems.Add((MongoId)assortid, assort.TrustLevel);
    }
    public static void AddAssortToTrader(MongoId itemid, MongoId traderid, int price, DatabaseService databaseService)
    {
        var trader = databaseService.GetTrader(traderid);
        if (trader != null)
        {
            var id = new MongoId();
            trader.Assort.Items.Add(new Item
            {
                Id = id,
                Template = itemid,
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd
                {
                    UnlimitedCount = true,
                    StackObjectsCount = 99999999
                }
            });
            trader.Assort.BarterScheme.TryAdd(id, new List<List<BarterScheme>>
            {
                new List<BarterScheme>
                {
                    new BarterScheme
                    {
                        Count = price,
                        Template = Money.ROUBLES
                    }
                }
            });
            trader.Assort.LoyalLevelItems.TryAdd(id, 1);
        }
    }
    public static void AddAssortToTrader(MongoId itemid, MongoId traderid, int price, MongoId money, DatabaseService databaseService)
    {
        var trader = databaseService.GetTrader(traderid);
        if (trader != null)
        {
            var id = new MongoId();
            trader.Assort.Items.Add(new Item
            {
                Id = id,
                Template = itemid,
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd
                {
                    UnlimitedCount = true,
                    StackObjectsCount = 99999999
                }
            });
            trader.Assort.BarterScheme.TryAdd(id, new List<List<BarterScheme>>
            {
                new List<BarterScheme>
                {
                    new BarterScheme
                    {
                        Count = price,
                        Template = money
                    }
                }
            });
            trader.Assort.LoyalLevelItems.TryAdd(id, 1);
        }
    }
    public static void AddAssortToTrader(MongoId itemid, MongoId traderid, int price, int level, DatabaseService databaseService)
    {
        var trader = databaseService.GetTrader(traderid);
        if (trader != null)
        {
            var id = new MongoId();
            trader.Assort.Items.Add(new Item
            {
                Id = id,
                Template = itemid,
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd
                {
                    UnlimitedCount = true,
                    StackObjectsCount = 99999999
                }
            });
            trader.Assort.BarterScheme.TryAdd(id, new List<List<BarterScheme>>
            {
                new List<BarterScheme>
                {
                    new BarterScheme
                    {
                        Count = price,
                        Template = Money.ROUBLES
                    }
                }
            });
            trader.Assort.LoyalLevelItems.TryAdd(id, level);
        }
    }
    public static void AddAssortToTrader(MongoId itemid, MongoId traderid, int price, int level, MongoId money, DatabaseService databaseService)
    {
        var trader = databaseService.GetTrader(traderid);
        if (trader != null)
        {
            var id = new MongoId();
            trader.Assort.Items.Add(new Item
            {
                Id = id,
                Template = itemid,
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = new Upd
                {
                    UnlimitedCount = true,
                    StackObjectsCount = 99999999
                }
            });
            trader.Assort.BarterScheme.TryAdd(id, new List<List<BarterScheme>>
            {
                new List<BarterScheme>
                {
                    new BarterScheme
                    {
                        Count = price,
                        Template = money
                    }
                }
            });
            trader.Assort.LoyalLevelItems.TryAdd(id, level);
        }
    }
    public static void AddAssortToTrader(List<CustomItem> item, MongoId traderid, int price, int level, DatabaseService databaseService, ICloner cloner)
    {
        var trader = databaseService.GetTrader(traderid);
        var itemlist = ItemUtils.ConvertItemListData(item, cloner);
        if (trader != null)
        {
            var id = itemlist[0].Id;
            var mainitem = new Item
            {
                Id = id,
                Template = itemlist[0].Template,
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = itemlist[0].Upd
            };
            if(mainitem.Upd == null)
            {
                mainitem.Upd = new Upd();
            }
            mainitem.Upd.UnlimitedCount = true;
            mainitem.Upd.StackObjectsCount = 99999999;
            trader.Assort.Items.Add(mainitem);
            for (var i = 1; i < itemlist.Count; i++)
            {
                trader.Assort.Items.Add(new Item
                {
                    Id = itemlist[i].Id,
                    Template = itemlist[i].Template,
                    ParentId = itemlist[i].ParentId,
                    SlotId = itemlist[i].SlotId,
                    Upd = itemlist[i].Upd,
                });
            }
            trader.Assort.BarterScheme.TryAdd(id, new List<List<BarterScheme>>
            {
                new List<BarterScheme>
                {
                    new BarterScheme
                    {
                        Count = price,
                        Template = Money.ROUBLES
                    }
                }
            });
            trader.Assort.LoyalLevelItems.TryAdd(id, level);
        }
    }
    public static void AddAssortToTrader(List<CustomItem> item, MongoId traderid, int price, int level, MongoId money, DatabaseService databaseService, ICloner cloner)
    {
        var trader = databaseService.GetTrader(traderid);
        var itemlist = ItemUtils.ConvertItemListData(item, cloner);
        if (trader != null)
        {
            var id = itemlist[0].Id;
            var mainitem = new Item
            {
                Id = id,
                Template = itemlist[0].Template,
                ParentId = "hideout",
                SlotId = "hideout",
                Upd = itemlist[0].Upd
            };
            if (mainitem.Upd == null)
            {
                mainitem.Upd = new Upd();
            }
            mainitem.Upd.UnlimitedCount = true;
            mainitem.Upd.StackObjectsCount = 99999999;
            trader.Assort.Items.Add(mainitem);
            for (var i = 1; i < itemlist.Count; i++)
            {
                trader.Assort.Items.Add(new Item
                {
                    Id = itemlist[i].Id,
                    Template = itemlist[i].Template,
                    ParentId = itemlist[i].ParentId,
                    SlotId = itemlist[i].SlotId,
                    Upd = itemlist[i].Upd,
                });
            }
            trader.Assort.BarterScheme.TryAdd(id, new List<List<BarterScheme>>
            {
                new List<BarterScheme>
                {
                    new BarterScheme
                    {
                        Count = price,
                        Template = money
                    }
                }
            });
            trader.Assort.LoyalLevelItems.TryAdd(id, level);
        }
    }
}










