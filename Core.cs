using HarmonyLib;
using Microsoft.AspNetCore.Razor.TagHelpers;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Loaders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Json.Converters;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using static EternalCycleServer.AddBundlePatch;
using static EternalCycleServer.ContextManager;
namespace EternalCycleServer;
public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "projectspark.hiddenhiragi.eternalcycle";
    public override string Name { get; init; } = "永恒时序";
    public override string Author { get; init; } = "HiddenHiragi";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.0.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new(">=4.0.13");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; } = "https://github.com/sp-tarkov/server-mod-examples";
    public override bool? IsBundleMod { get; init; } = false;
    public override string? License { get; init; } = "MIT";
}
public static class Init
{
    private static bool _initialized;
    private static readonly object InitLock = new();

    [ModuleInitializer]
    public static void Initialize()
    {
        lock (InitLock)
        {
            if (_initialized) return;
            //这个开关检测有必要吗?
            //不知道, 那就留着吧
            _initialized = true;
            try
            {
                //最前列hookAddBundle方法移除重复警告
                new AddBundlePatch().Enable();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
[Injectable(TypePriority = OnLoadOrder.PreSptModLoader + 1)]
public class CorePreSptLoad(
    ISptLogger<EternalCycle> logger,
    DatabaseService databaseService,
    CustomItemService customItemService,
    ModHelper modHelper,
    JsonUtil jsonutil,
    ICloner cloner,
    ConfigServer configServer,
    ImageRouter imageRouter
    ) // We inject a logger for use inside our class, it must have the class inside the diamond <> brackets
    : IOnLoad // Implement the IOnLoad interface so that this mod can do something on server load
{
    public Task OnLoad()
    {
        //new AddBundlePatch().Enable();
        //new SafeRagfairPricePatch().Enable();
        //var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(pathToMod, "db/base.json");
        //VulcanUtil.DoAsyncWork(logger);
        //VulcanLog.Access("test", logger);
        //LootUtils.GenerateStaticLootMap(databaseService, logger);
        //ItemUtils.GetItem("5e42c81886f7742a01529f57", databaseService).Properties.MaximumNumberOfUsage = 0; //完全可以
        //databaseService.GetTraders().Values[IEnumerable<Trader>.]
        return Task.CompletedTask;
    }
}
// We want to load after PreSptModLoader is complete, so we set our type priority to that, plus 1.
[Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1)]
public class EternalCycle(
    ISptLogger<EternalCycle> logger,
    DatabaseService databaseService,
    CustomItemService customItemService,
    ModHelper modHelper,
    ItemHelper itemHelper,
    JsonUtil jsonUtil,
    ICloner cloner,
    ConfigServer configServer,
    ImageRouter imageRouter,
    RagfairOfferService ragfairOfferService,
    RagfairController ragfairController,
    HandbookHelper handbookHelper
    ) // We inject a logger for use inside our class, it must have the class inside the diamond <> brackets
    : IOnLoad // Implement the IOnLoad interface so that this mod can do something on server load
{
    public string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
    public Task OnLoad()
    {
        BaseInteractionRequestDataConverter.RegisterModDataHandler("SyncStashExtend", (jsonText) =>
        {
            return jsonUtil.Deserialize<ProfileStashSyncExtendRequestData>(jsonText);
        });

        //var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(pathToMod, "db/base.json");
        //VulcanUtil.DoAsyncWork(logger);
        // VulcanLog.Access("test", logger);
        ImageUtils.RegisterFolderImageRoute("/files/icon/", System.IO.Path.Combine(modPath, "res/"), imageRouter);
        var dim = ERagfairTagsType.次元博物;
        var special = ERagfairTagsType.特殊物品;
        var dev = ERagfairTagsType.调试物品;
        var quest = ERagfairTagsType.任务物品;
        databaseService.GetHandbook().Categories.Add(new HandbookCategory
        {
            Id = dim,
            ParentId = "5b47574386f77428ca22b33e",
            Icon = "/files/icon/nuclear_star.png",
            Color = "",
            Order = "100"
        });
        databaseService.GetHandbook().Categories.Add(new HandbookCategory
        {
            Id = special,
            ParentId = null,
            Icon = "/files/icon/barrier.png",
            Color = "",
            Order = "15"
        });
        databaseService.GetHandbook().Categories.Add(new HandbookCategory
        {
            Id = dev,
            ParentId = null,
            Icon = "/files/icon/commandblock.png",
            Color = "",
            Order = "16"
        });
        databaseService.GetHandbook().Categories.Add(new HandbookCategory
        {
            Id = quest,
            ParentId = null,
            Icon = "/files/icon/quest.png",
            Color = "",
            Order = "17"
        });
        databaseService.GetLocales().Global["ch"].AddTransformer(delegate (Dictionary<string, string> lang)
        {
            lang[dim] = "次元博物";
            lang[special] = "特殊物品";
            lang[dev] = "技术物品";
            lang[quest] = "任务物品";
            return lang;
        });
        var context = new ContextManager.LoadModContext
        {
            DB = databaseService,
            JsonUtil = jsonUtil,
            ConfigServer = configServer,
            ModHelper = modHelper,
            Logger = Utils.commonLogger,
            ImageRouter = imageRouter,
            ItemHelper = itemHelper,
            Cloner = cloner
        };
        var items = databaseService.GetItems();
        foreach (var item in items)
        {
            var handbooks = databaseService.GetHandbook().Items;
            var handbook = handbooks.FirstOrDefault(x => x.Id == item.Value.Id);
            if (item.Value.Type != "Node" && item.Value.Properties != null)
            {
                if (item.Value.Properties.Width >= 10)
                {
                    item.Value.Properties.Width = 2;
                }
                if (item.Value.Properties.Height >= 10)
                {
                    item.Value.Properties.Height = 2;
                }
                if ((bool)item.Value.Properties.QuestItem)
                {
                    if (handbook != null)
                    {
                        handbook.ParentId = quest;
                        ItemUtils.AddBlackList(item.Value.Id, 31, context);
                    }
                    else
                    {
                        handbooks.Add(new HandbookItem
                        {
                            Id = item.Value.Id,
                            ParentId = quest,
                            Price = 20000
                        });
                        ItemUtils.AddBlackList(item.Value.Id, 31, context);
                    }
                }
                else if (handbook == null)
                {
                    item.Value.Properties.CanSellOnRagfair = false;
                    handbooks.Add(new HandbookItem
                    {
                        Id = item.Value.Id,
                        ParentId = dev,
                        Price = 20000
                    });
                    ItemUtils.AddBlackList(item.Value.Id, 64, context);
                }
            }
        }
        //LootUtils.GenerateStaticLootMap(databaseService, logger);
        //ItemUtils.GetItem("5e42c81886f7742a01529f57", databaseService).Properties.MaximumNumberOfUsage = 0; //完全可以
        //databaseService.GetTraders().Values[IEnumerable<Trader>.]
        var config = ConfigManager.GetConfig();
        if (config.UseOldRagfairPrice)
        {
            new ReplaceFleaBasePricesPatch().Enable();
        }
        new OpenRandomLootContainerPatch().Enable();

        //new StartupLogPatch().Enable();
        //new RemoveExpiredItemsFromMessagePatch().Enable();
        new RagfairLoadPatch().Enable();
        void testmethod(LoadModContext prlc)
        {
            var item = prlc.DB.GetItems();
            prlc.Logger.Warn(item.FirstOrDefault().Value.Id.ToString());
            prlc.Logger.Info("Mod加载完成后市场初始化前");
        }
        void testmethod2(LoadModContext prlc)
        {
            prlc.Logger.Error("市场初始化后游戏启动前");
        }
        void testmethod3(LoadModContext prlc)
        {
            prlc.Logger.Error("Mod加载完成后");
        }
        EventManager.OnBeforeRagfairLoadedEvent += testmethod;
        EventManager.OnAfterRagfairLoadedEvent += testmethod2;
        EventManager.OnAfterModLoadedEvent += testmethod3;
        ItemUtils.RegisterItem(System.IO.Path.Combine(modPath, "items/"), "<color=#8FFF00>永恒时序-调试物品加载</color>", "<color=#FFFF80>永恒时序</color>");
        ItemUtils.RegisterItem(System.IO.Path.Combine(modPath, "gunfight.json"), "<color=#8FFF00>永恒时序-物品加载器</color>", "<color=#FFFF80>枪械武术</color>");
        QuestUtils.RegisterQuest(System.IO.Path.Combine(modPath, "init.json"), System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        TraderUtils.RegisterTrader(System.IO.Path.Combine(modPath, "base.json"), System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "<color=#8FFF00>永恒时序-调试商人加载</color>", "<color=#FFFF80>永恒时序</color>");
        AchievementUtils.RegisterAchievement(System.IO.Path.Combine(modPath, "achievement.json"), System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        AssortUtils.RegisterAssort(System.IO.Path.Combine(modPath, "assort_mod.json"));
        QuestUtils.RegisterQuestRewards(System.IO.Path.Combine(modPath, "rewards_vanilla.json"));
        QuestUtils.RegisterQuestLogicTree(System.IO.Path.Combine(modPath, "logic.json"));
        RecipeUtils.RegisterRecipe(System.IO.Path.Combine(modPath, "recipe.json"));
        RecipeUtils.RegisterScavCaseRecipe(System.IO.Path.Combine(modPath, "scavcase.json"));
        PresetUtils.RegisterPreset(System.IO.Path.Combine(modPath, "preset.json"));
        CustomizationUtils.RegisterCustomization(modPath, "custom.json", "deco/");
        SuitUtils.RegisterSuit(System.IO.Path.Combine(modPath, "suits.json"));
        LocaleUtils.RegisterQuestLocale(System.IO.Path.Combine(modPath, "quest/"), "<color=#8FFF00>永恒时序-调试任务加载</color>", "<color=#FFFF80>永恒时序</color>");
        ItemUtils.InitDrawPool(modHelper.GetJsonDataFromFile<Dictionary<string, DrawPoolClass>>(modPath, "newdrawpool.json"));
        ResourceUtils.RegisterRigLayoutResource(modPath, "clientres/");
        ResourceUtils.RegisterSlotIconResource(modPath, "sloticon/");
        CustomizationUtils.RegisterHideoutCustomization(System.IO.Path.Combine(modPath, "hideoutcustom.json"));
        //ItemUtils.InitItem(System.IO.Path.Combine(modPath, "items/"), "<color=#8FFF00>永恒时序-物品加载器</color>", "<color=#FFFF80>永恒时序</color>", databaseService, jsonutil, configServer, cloner);
        return Task.CompletedTask;
    }

    public record ProfileStashSyncExtendRequestData : BaseInteractionRequestData
    {
        [JsonPropertyName("stashData")]
        public Item[] StashData { get; set; }
    }

    //Weird, sometimes item from gift box will missing and sometimes will duplicate, profile broken risk, tried to fix it.
    [Injectable]
    public class ProfileStashSyncExtendEventRouter : ItemEventRouterDefinition
    {
        protected override List<HandledRoute> GetHandledRoutes()
        {
            return new List<HandledRoute>
            {
                new HandledRoute("SyncStashExtend", false)
            };
        }

        protected override ValueTask<ItemEventRouterResponse> HandleItemEventInternal(
            string url,
            PmcData pmcData,
            BaseInteractionRequestData body,
            MongoId sessionID,
            ItemEventRouterResponse output)
        {
            if (url == "SyncStashExtend" && body is ProfileStashSyncExtendRequestData requestBody)
            {
                if (requestBody.StashData != null && requestBody.StashData.Length > 0)
                {
                    var request = new ProfileStashDataContext
                    {
                        StashDataContext = [requestBody.StashData.ToList()],
                        StrictJsonFormat = false,
                        Callback = null,
                        DiscardOverflowItem = false
                    };

                    //Forced sync missing item after open a gift
                    SyncProfileStashExtend(
                        sessionID,
                        request,
                        pmcData,
                        output);
                }
                else
                {
                    //Console.WriteLine("数据为空！");
                }
            }

            return new ValueTask<ItemEventRouterResponse>(output);
        }

        public void SyncProfileStashExtend(MongoId sessionId, ProfileStashDataContext request, PmcData pmcData, ItemEventRouterResponse output)
        {
            var inventoryHelper = ServiceLocator.ServiceProvider.GetService<InventoryHelper>();
            var httpResponseUtil = ServiceLocator.ServiceProvider.GetService<HttpResponseUtil>();
            var serverLocalisationService = ServiceLocator.ServiceProvider.GetService<ServerLocalisationService>();

            if (!inventoryHelper.CanPlaceItemsInInventory(sessionId, request.StashDataContext))
            {
                httpResponseUtil.AppendErrorToOutput(
                    output,
                    serverLocalisationService.GetText("inventory-no_stash_space"),
                    BackendErrorCodes.NotEnoughSpace
                );

                return;
            }

            var checkItemRequest = new ProfileStashData
            {
                StrictJsonFormat = request.StrictJsonFormat,
                DiscardOverflowItem = request.DiscardOverflowItem,
                Callback = request.Callback,
            };
            foreach (var stashData in request.StashDataContext)
            {
                checkItemRequest.StashData = stashData;

                SyncStashExtend(sessionId, checkItemRequest, pmcData, output);
                if (output.Warnings?.Count > 0)
                {
                    return;
                }
            }
        }

        public void SyncStashExtend(MongoId sessionId, ProfileStashData request, PmcData pmcData, ItemEventRouterResponse output)
        {
            var inventoryHelper = ServiceLocator.ServiceProvider.GetService<InventoryHelper>();
            var httpResponseUtil = ServiceLocator.ServiceProvider.GetService<HttpResponseUtil>();
            var cloner = ServiceLocator.ServiceProvider.GetService<ICloner>();
            var itemSnapshot = cloner.Clone(request.StashData);

            //Reflection search
            var allMethods = AccessTools.GetDeclaredMethods(typeof(InventoryHelper));

            //Generate 2D Grid Map
            var stashFS2D = (int[,])AccessTools.Method(typeof(InventoryHelper), "GetStashSlotMap")
                        .Invoke(inventoryHelper, new object[] { pmcData });
            if (stashFS2D is null)
            {

                return;
            }

            //Generate 2D Grid Map
            var sortingTableFS2D = AccessTools.Method(typeof(InventoryHelper), "GetSortingTableSlotMap")
                        .Invoke(inventoryHelper, new object[] { pmcData });

            //Sync missing item
            allMethods.FirstOrDefault(m =>
                m.Name.Contains("Inventory") &&
                m.GetParameters().Length == 6 && 
                m.GetParameters()[0].ParameterType == typeof(int[,]) &&
                m.GetParameters()[1].ParameterType == typeof(int[,]))
                ?.Invoke(inventoryHelper, new object[] { 
                        stashFS2D,
                        sortingTableFS2D,
                        itemSnapshot,
                        pmcData.Inventory,
                        !request.DiscardOverflowItem.GetValueOrDefault(true),
                        output });

            if (output.Warnings?.Count > 0)

            {
                return;
            }
            
            //Sync item fir from client
            ResetItemState(itemSnapshot);

            AccessTools.Method(typeof(InventoryHelper), "RemoveTraderRagfairRelatedUpdProperties")
                        .Invoke(inventoryHelper, new object[] { itemSnapshot[0].Upd });

            try
            {
                request.Callback?.Invoke((int)(itemSnapshot[0].Upd.StackObjectsCount ?? 0));
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                httpResponseUtil.AppendErrorToOutput(output, message);

                return;
            }

            //Sync data into profile and callback
            output.ProfileChanges[sessionId].Items.NewItems.AddRange(itemSnapshot);
            pmcData.Inventory.Items.AddRange(itemSnapshot);
        }
        
        protected virtual void ResetItemState(IEnumerable<Item> ItenList)
        {
            var itemHelper = ServiceLocator.ServiceProvider.GetService<ItemHelper>();
            foreach (Item item in ItenList)
            {
                item.AddUpd();
                item.Upd.SpawnedInSession = (itemHelper.IsOfBaseclass(item.Template, BaseClasses.AMMO) ? null : item.Upd.SpawnedInSession ?? false);
            }
        }
    }

    public record ProfileStashData
    {
        public virtual List<Item>? StashData { get; set; }
        public virtual bool? StrictJsonFormat { get; set; }
        public virtual Action<int>? Callback { get; set; }
        public virtual bool? DiscardOverflowItem { get; set; }
    }

    public record ProfileStashDataContext
    {
        public virtual IEnumerable<List<Item>>? StashDataContext { get; set; }
        public virtual bool? StrictJsonFormat { get; set; }
        public virtual Action<int>? Callback { get; set; }
        public virtual bool? DiscardOverflowItem { get; set; }
    }

    [Injectable]
    // 1. 使用主构造函数，将所有需要的服务（包括之前用 ServiceLocator 获取的）全部在此声明
    public class VulcanCoreAwakeRouter(
     JsonUtil jsonUtil,
     HttpResponseUtil httpResponseUtil,
     DatabaseService databaseService,
     RagfairController ragfairController,
     RagfairOfferService ragfairOfferService,
     ItemHelper itemHelper,
     ISptLogger<EternalCycle> logger,
     ICloner cloner,
     EternalCycle vulcanCore,
     LocaleService localeService,   // <- 从内部提取到这里的注入
     ProfileHelper profileHelper    // <- 从内部提取到这里的注入
    ) : StaticRouter(jsonUtil, [

        /* 这俩删了, 备份后面单独写
    // 2. 直接在基类构造时传入路由数组，使用 Lambda 表达式内联逻辑
    new RouteAction(
        "/VulcanCoreClient/InitFix",
        (_, _, _, _) => // 如果不需要用到 url, info, sessionId，用下划线丢弃
        {
            if (!ItemUtils.firstlogin)
            {
                // VulcanLog.Warn("正在修复物品数据....", logger);
                // ItemUtils.FixItemCompatibleInit(ItemUtils.FixDict, databaseService, cloner);
                // VulcanLog.Debug($"{LocaleUtils.GetItemName(VulcanUtil.ConvertHashID("为了全人类海报"), localeService)}", logger);
                // VulcanLog.Access("物品数据修复完成", logger);
                ItemUtils.firstlogin = true;
            }

            return ValueTask.FromResult<object>("Response successful.");
        }
    ),

    new RouteAction(
        "/VulcanCoreClient/ClientStartCall",
        (_, _, _, _) =>
        {
            //Console.WriteLine("游戏启动");
            return ValueTask.FromResult<object>("Response successful.");
        }
    ),
        */
    new RouteAction(
        "/VulcanCoreClient/CallBackup",
        (_, _, sessionId, _) => // 这里需要用到 sessionId
        {
            // 直接使用构造函数注入的 vulcanCore 和 profileHelper
            var backupPath = System.IO.Path.Combine(vulcanCore.modPath, "Backup");
            var currectProfile = profileHelper.GetFullProfile(sessionId);
            var profileToSave = jsonUtil.Serialize(currectProfile, true);

            var pmcName = currectProfile.CharacterData.PmcData.Info.Nickname;
            var currectPmcName = Utils.GetValidFolderName(pmcName);

            var timePath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            var time = DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒");
            var currcetBackupPath = System.IO.Path.Combine(backupPath, timePath, currectPmcName);

            Directory.CreateDirectory(currcetBackupPath);
            var filePath = System.IO.Path.Combine(currcetBackupPath, $"{sessionId}.json");
            File.WriteAllText(filePath, profileToSave);

            var backupLog = $"当前存档已成功备份! 玩家名: {pmcName} 备份时间: {time} 保存路径: {filePath}";
            var backupMessage = $"{pmcName}的存档已成功备份到{filePath}";
            
            // VulcanLog.Access(backupLog, logger);
            
            return ValueTask.FromResult<object>(backupMessage);
        }
    ),

        new RouteAction<SyncResourceRequest>(
            "/eternalcycle/loadriglayout",
            (_, info, sessionId, _) =>
            {
                var clientReq = info ?? new SyncResourceRequest();
                var response = new SyncResourceResponse();

                foreach (var kvp in ResourceUtils.BundleHashes)
                {
                    var relativePath = kvp.Key;
                    var serverHash = kvp.Value;

                    response.ValidFiles.Add(relativePath);

                    if (!clientReq.ClientHashes.TryGetValue(relativePath, out var clientHash) || clientHash != serverHash)
                    {
                        if (ResourceUtils.BundleBase64Data.TryGetValue(relativePath, out var base64Data))
                        {
                            response.FilesToUpdate.Add(relativePath, base64Data);
                        }
                    }
                }

                var jsonResponse = jsonUtil.Serialize(response);
                return ValueTask.FromResult(jsonResponse);
            }
        ),

        new RouteAction<SyncResourceRequest>(
            "/eternalcycle/loadsloticon",
            (_, info, sessionId, _) =>
            {
                var clientReq = info ?? new SyncResourceRequest();
                var response = new SyncResourceResponse();

                foreach (var kvp in ResourceUtils.SlotIconHashes)
                {
                    var relativePath = kvp.Key;
                    var serverHash = kvp.Value;

                    response.ValidFiles.Add(relativePath);

                    if (!clientReq.ClientHashes.TryGetValue(relativePath, out var clientHash) || clientHash != serverHash)
                    {
                        if (ResourceUtils.SlotIconBase64Data.TryGetValue(relativePath, out var base64Data))
                        {
                            response.FilesToUpdate.Add(relativePath, base64Data);
                        }
                    }
                }

                var jsonResponse = jsonUtil.Serialize(response);
                return ValueTask.FromResult(jsonResponse);
            }
        ),

        new RouteAction<SyncResourceRequest>(
            "/eternalcycle/loaddecoicon",
            (_, info, sessionId, _) =>
            {
                var clientReq = info ?? new SyncResourceRequest();
                var response = new SyncResourceResponse();

                foreach (var kvp in ResourceUtils.DecoIconHashes)
                {
                    var relativePath = kvp.Key;
                    var serverHash = kvp.Value;

                    response.ValidFiles.Add(relativePath);

                    if (!clientReq.ClientHashes.TryGetValue(relativePath, out var clientHash) || clientHash != serverHash)
                    {
                        if (ResourceUtils.DecoIconBase64Data.TryGetValue(relativePath, out var base64Data))
                        {
                            response.FilesToUpdate.Add(relativePath, base64Data);
                        }
                    }
                }

                var jsonResponse = jsonUtil.Serialize(response);
                return ValueTask.FromResult(jsonResponse);
            }
        ),

        new RouteAction<SyncResourceRequest>(
            "/eternalcycle/loadtarget",
            (_, info, sessionId, _) =>
            {
                var clientReq = info ?? new SyncResourceRequest();
                var response = new SyncResourceResponse();

                foreach (var kvp in ResourceUtils.TargetHashes)
                {
                    var relativePath = kvp.Key;
                    var serverHash = kvp.Value;

                    response.ValidFiles.Add(relativePath);

                    if (!clientReq.ClientHashes.TryGetValue(relativePath, out var clientHash) || clientHash != serverHash)
                    {
                        if (ResourceUtils.TargetBase64Data.TryGetValue(relativePath, out var base64Data))
                        {
                            response.FilesToUpdate.Add(relativePath, base64Data);
                        }
                    }
                }

                var jsonResponse = jsonUtil.Serialize(response);
                return ValueTask.FromResult(jsonResponse);
            }
        ),

        new RouteAction(
            "/eternalcycle/loadvoice",
            (_, _, _, _) =>
            {

                var jsonResponse = jsonUtil.Serialize(new VoiceResourceRequest{ VoicePath = ResourceUtils.VoicePath});
                return ValueTask.FromResult<object>(jsonResponse);
            }
        )
 ]);
}


