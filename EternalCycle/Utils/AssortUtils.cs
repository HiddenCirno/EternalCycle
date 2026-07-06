using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils.Cloners;
using static EternalCycle.ContextManager;
using Path = System.IO.Path;

namespace EternalCycle
{
    /// <summary>
    /// 报价单部分
    /// </summary>
    public class AssortUtils
    {
        /// <summary>
        /// 将自定义报价单(Assort)注册到加载事件
        /// </summary>
        /// <param name="path">指定的存放报价单文件的路径或完整的报价单文件路径</param>
        public static void RegisterAssort(string path)
        {
            // 文件夹加载模式
            if (Directory.Exists(path))
            {
                // 注意：这里的事件名请根据你实际的 DataLoadEvent 进行调整（可能是 LoadAssortEvent 或挂载在 LoadTraderEvent 下）
                EventManager.DataLoadEvent.LoadTraderAssortEvent += (context) =>
                {
                    try
                    {
                        // 对应调用已有的文件夹重载方法
                        // 假设 context 提供了 Logger，如果没有，请使用 ServiceLocator.ServiceProvider.GetService<ISptLogger<EternalCycle>>()
                        InitAssortData(path, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册报价单时发生错误：指定的文件夹 {path} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(path))
            {
                EventManager.DataLoadEvent.LoadTraderAssortEvent += (context) =>
                {
                    try
                    {
                        // 反序列化为 List 集合，对应已有的 List 重载方法
                        var assortData = context.JsonUtil.Deserialize<List<CustomAssortData>>(File.ReadAllText(path));

                        if (assortData != null)
                        {
                            InitAssortData(assortData, context);
                        }
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册报价单时发生错误：指定的文件 {path} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册报价单时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        /// <summary>
        /// 加载报价单数据
        /// </summary>
        /// <param name="assortData"></param>
        /// <param name="context">上下文实例</param>
        public static void InitAssortData(List<CustomAssortData> assortData, LoadModContext context)
        {
            foreach (CustomAssortData assort in assortData)
            {
                switch (assort)
                {
                    case CustomNormalAssortData customAssortData:
                        {
                            InitAssort(assort, context);
                        }
                        break;
                    case CustomLockedAssortData customLockedAssortData:
                        {
                            var assortUnlockRewardData = new CustomAssortUnlockRewardData
                            {
                                Id = (MongoId)Utils.ConvertHashID($"{customLockedAssortData.Id}_Locked"),
                                QuestId = (MongoId)Utils.ConvertHashID(customLockedAssortData.QuestId),
                                QuestStage = customLockedAssortData.QuestStage,
                                IsUnknownReward = customLockedAssortData.IsUnknownReward,
                                AssortData = customLockedAssortData,
                            };
                            QuestUtils.InitAssortUnlockRewards(assortUnlockRewardData, context);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 从文件夹加载报价单数据
        /// </summary>
        /// <param name="folderpath"></param>
        /// <param name="context">上下文实例</param>
        public static void InitAssortData(string folderpath, LoadModContext context)
        {
            List<string> files = Directory.GetFiles(folderpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var assort = context.ModHelper.GetJsonDataFromFile<List<CustomAssortData>>(folderpath, fileName);
                    InitAssortData(assort, context);
                }
            }
        }

        public static void InitAssort(CustomAssortData assortData, LoadModContext context)
        {
            var assort = assortData;
            var assortid = Utils.ConvertHashID(assort.Id);
            var traderassort = TraderUtils.GetTrader((string)assort.Trader, context.DB).Assort;
            var items = ItemUtils.ConvertItemListData(assort.Item, context);
            var mainitem = items[0];
            var mainitemid = mainitem.Template;
            if (ItemUtils.GetItemRagfairTag(mainitemid, context) == ERagfairTagsType.弹药包)
            {
                ItemUtils.AddAmmoToAmmoBoxInList(mainitem.Id, mainitemid, items, context);
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
                    Template = Utils.ConvertHashID(barter.Key),
                    Count = barter.Value
                });
            }
            foreach (var barter in assort.DogTag)
            {
                barterlist[0].Add(
                new BarterScheme
                {
                    Template = Utils.ConvertHashID(barter.Key),
                    Count = (double)barter.Value.Count,
                    Level = barter.Value.Level,
                    Side = (DogtagExchangeSide)barter.Value.Side
                });
            }
            traderassort.BarterScheme.Add((MongoId)assortid, barterlist);
            traderassort.LoyalLevelItems.Add((MongoId)assortid, assort.TrustLevel);
        }

        public static void AddAssortToTrader(MongoId itemid, MongoId traderid, int price, LoadModContext context)
        {
            var trader = context.DB.GetTrader(traderid);
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

        public static void AddAssortToTrader(MongoId itemid, MongoId traderid, int price, MongoId money, LoadModContext context)
        {
            var trader = context.DB.GetTrader(traderid);
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

        public static void AddAssortToTrader(MongoId itemid, MongoId traderid, int price, int level, LoadModContext context)
        {
            var trader = context.DB.GetTrader(traderid);
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

        public static void AddAssortToTrader(MongoId itemid, MongoId traderid, int price, int level, MongoId money, LoadModContext context)
        {
            var trader = context.DB.GetTrader(traderid);
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

        public static void AddAssortToTrader(List<CustomItem> item, MongoId traderid, int price, int level, LoadModContext context)
        {
            var trader = context.DB.GetTrader(traderid);
            var itemlist = ItemUtils.ConvertItemListData(item, context);
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
                        Template = Money.ROUBLES
                    }
                }
            });
                trader.Assort.LoyalLevelItems.TryAdd(id, level);
            }
        }

        public static void AddAssortToTrader(List<CustomItem> item, MongoId traderid, int price, int level, MongoId money, LoadModContext context)
        {
            var trader = context.DB.GetTrader(traderid);
            var itemlist = ItemUtils.ConvertItemListData(item, context);
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
}