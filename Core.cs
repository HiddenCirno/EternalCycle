using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.DI.Routing;
using SPTarkov.Server.Core.Extensions;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Request;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Services.Modding.Custom;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using static EternalCycleServer.ContextManager;
namespace EternalCycleServer
{
    public record ModMetadata : IModMetadata
    {
        public string ModGuid { get; init; } = "projectspark.hiddenhiragi.eternalcycleserver";
        public  string Name { get; init; } = "永恒时序";
        public  string Author { get; init; } = "HiddenHiragi";
        public  List<string>? Contributors { get; init; }
        public  SemanticVersioning.Version Version { get; init; } = new("1.3.1");
        public  SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
        public  List<string>? Incompatibilities { get; init; }
        public  Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
        public  string? Url { get; init; } = "https://github.com/sp-tarkov/server-mod-examples";
        public  bool? IsBundleMod { get; init; } = true;
        public  string? License { get; init; } = "MIT";
        public bool HasPrepatcher {  get; init; } = false;
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
                    //火神之心兼容
                    new AddBundlePatch().Enable();
                    //我去你妈的傻逼白皮, 会写代码吗
                    new FuckMongoIdPatch().Enable();
                    new FuckMongoIdPatch2().Enable();
                    new FuckMongoIdPatch3().Enable();
                    new FuckMongoIdPatch4().Enable();
                    new FuckParentIdPatch().Enable();
                }
                catch (Exception ex)
                {
                }
            }
        }
    }

    public class ModDiRegistration : IOnDIConstruct
    {
        public static async Task OnDIConstructAsync(IServiceCollection serviceCollection, CancellationToken cancellationToken)
        {
            serviceCollection.AddSingleton<ConfigServer>();
            await Task.CompletedTask;
        }
    }

    [Injectable(TypePriority = OnLoadOrder.Preload + 1)]
    public class PatchEnabler(IEnumerable<IRuntimePatch> patches) : IOnLoad
    {
        public Task OnLoadAsync(CancellationToken cancellationToken)
        {
            foreach (var patch in patches)
                patch.Enable();
            return Task.CompletedTask;
        }
    }

    // We want to load after PreSptModLoader is complete, so we set our type priority to that, plus 1.
    [Injectable(TypePriority = OnLoadOrder.Preload + 1)]
    public class EternalCycle(
        CustomItemService customItemService,
        ModHelper modHelper,
        ItemHelper itemHelper,
        JsonUtil jsonUtil,
        ICloner cloner,
        ConfigServer configServer,
        ImageRouter imageRouter,
        PresetHelper presetHelper,
        RagfairOfferService ragfairOfferService,
        RagfairController ragfairController,
        TemplateTable templateTable,
        LocaleTable localeTable,
        GlobalTable globalTable,
        TradersTable tradersTable,
        HideoutTable hideoutTable,
        LocationTable locationTable,
        BotTable botTable,
        HandbookHelper handbookHelper
        ) // We inject a logger for use inside our class, it must have the class inside the diamond <> brackets
        : IOnLoad // Implement the IOnLoad interface so that this mod can do something on server load
    {
        public string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        public async Task OnLoadAsync(CancellationToken cancellationToken)
        {
            //var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(pathToMod, "db/base.json");
            //VulcanUtil.DoAsyncWork(logger);
            // VulcanLog.Access("test", logger);

            DatabaseService databaseService = new DatabaseService(templateTable, localeTable, globalTable, tradersTable, hideoutTable, locationTable, botTable);
            var context = new LoadModContext
            {
                DB = databaseService,
                JsonUtil = jsonUtil,
                ConfigServer = configServer,
                ModHelper = modHelper,
                Logger = Utils.commonLogger,
                ImageRouter = imageRouter,
                ItemHelper = itemHelper,
                PresetHelper = presetHelper,
                Cloner = cloner
            };
            //火神之心兼容层
            ImageUtils.RegisterFolderImageRoute("/files/icon/", System.IO.Path.Combine(modPath, "res/"), imageRouter);
            var dim = ERagfairTagsType.次元博物;
            var special = ERagfairTagsType.特殊物品;
            var dev = ERagfairTagsType.调试物品;
            var quest = ERagfairTagsType.任务物品;
            var categories = databaseService.GetHandbook().Categories;
            if (!categories.Any(x => x.Id == dim))
            {
                databaseService.GetHandbook().Categories.Add(new HandbookCategory
                {
                    Id = dim,
                    ParentId = "5b47574386f77428ca22b33e",
                    Icon = "/files/icon/nuclear_star.png",
                    Color = "",
                    Order = "100"
                });
            }
            if (!categories.Any(x => x.Id == special))
            {

                databaseService.GetHandbook().Categories.Add(new HandbookCategory
                {
                    Id = special,
                    ParentId = null,
                    Icon = "/files/icon/barrier.png",
                    Color = "",
                    Order = "15"
                });
            }
            if (!categories.Any(x => x.Id == dev))
            {
                databaseService.GetHandbook().Categories.Add(new HandbookCategory
                {
                    Id = dev,
                    ParentId = null,
                    Icon = "/files/icon/commandblock.png",
                    Color = "",
                    Order = "16"
                });
            }
            if (!categories.Any(x => x.Id == quest))
            {
                databaseService.GetHandbook().Categories.Add(new HandbookCategory
                {
                    Id = quest,
                    ParentId = null,
                    Icon = "/files/icon/quest.png",
                    Color = "",
                    Order = "17"
                });
            }
            databaseService.GetLocales().Global["ch"].AddTransformer(delegate (GlobalLocaleDictionary lang)
            {
                lang[dim] = "次元博物";
                lang[special] = "特殊物品";
                lang[dev] = "技术物品";
                lang[quest] = "任务物品";
                return lang;
            });

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
                //new ReplaceFleaBasePricesPatch().Enable();
            }
            //new OpenRandomLootContainerPatch().Enable();

            //new StartupLogPatch().Enable();
            //new RemoveExpiredItemsFromMessagePatch().Enable();
            //new RagfairLoadPatch().Enable();
            //new ProfileHelperPatch().Enable();
            //new PresetHelperPatch().Enable();   
            //new BotGeneratorPatch.BotGeneratorPatch_GenerateBot().Enable();
            Utils.commonLogger.Success("正在同步……");
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
            void testmethod4(BotBase bot, BotType botJsonTemplate, BotGenerationDetails botGenerationDetails, LoadModContext prlc)
            {
                prlc.Logger.Error("Test");
            }

            EventManager.OnBeforeRagfairLoadedEvent += testmethod;

            string pubkey = "-----BEGIN PUBLIC KEY-----MIICIjANBgkqhkiG9w0BAQEFAAOCAg8AMIICCgKCAgEApwO5ENxGmgxJLCld9mdzziVmeOvmBeno9vMxJDZ1hZqszSwmJnGx/QZDBefd5swguXvRBVjYcrM5CQ7ZDmr0JsBlOpFizrLKdM91l10rxnPkWGVYU1no6usVagoTlyZx8NyERSrOLsM05s49MbOSwdc5v4X5NPbU3ZSfAK7EOTEJUsikMLZL4ZpVWiYqiIZdix61Sq5W2Dj1mXHHAkNTfAAgjIWN4iil/Y9VGfG4j8A/XSOkHS29kp4KT+BuF+gz8/hf9w6jFmQ4lBFOZeBi1ewp8c/yWsMnMPntFeHeEmhryD8O1h8WPEaFWZ3e85aYElclvYkUY2WMDIstV8neT+OXfcmBqg7Nz3kNA9uMj64k/cYft5WjZGEHb+qK0ED/ofzAJ9Bd4EoV1rJIeZKU0bvoCy2nXJMcCJOqPBQUwHCdqaDHsSqFm1T1c7GUXa2sVXIUQWgDeUXval2DQ19j3TC3YeKAJUUZ5PWnULVusR1prpVhsdiAVPHVD5roKPSA7ywk0UZc7FJMlRPdFoCYMduUmbrdeRu2R2z+UARrQKrsBzDxzueXXJ8rKer+9FN6GT2VxLTNcgo4MZM2FVDctha4n+lij/ZEWRKorQ43CQQn1iuE1CQhlgRg7teo0xDUz5OEANlFIQYo2FubAsrLUqzbmYWOHz/IKFsUuS+Tp9MCAwEAAQ==-----END PUBLIC KEY-----";

            EventManager.DataLoadEvent.LoadItemEvent += (context) =>
            {
                try
                {
                    var item = Utils.ConvertItemData(FileDecodeUtils.DecodeToRawJson(modPath, "永恒之环.ecf", "永恒之环.sig", "eternalcycle.sig", pubkey, "201633e196f836f185ef4c1ded38ea5181064a08d946099df4b4d4362d370cb8", "da91b793b230778064740ea9a953cbce"), context.JsonUtil);
                    ItemUtils.InitItem(item, "<color=#5BCEFA>永恒<color=#F5A9B8>时序</color></color>", "<color=#5BCEFA>永恒<color=#F5A9B8>时序</color></color>", context);
                }
                catch (Exception ex)
                {
                }
            };
            EventManager.DataLoadEvent.FixItemCompatibleEvent += (context) =>
            {
                try
                {
                    var 永恒之环 = "永恒之环".ConvertHashID();
                    var items = context.DB.GetItems();
                    items["55d7217a4bdc2d86028b456d"]
                        .Properties.Slots
                        .First(x => x.Name == "ArmBand")
                        .Properties.Filters
                        .First()
                        .Filter
                        .Add(永恒之环);
                    foreach (var item in items.Values)
                    {
                        if (item.Id == 永恒之环) continue;
                        foreach (var filter in item.Properties?.Grids?
                             .SelectMany(x => x.Properties.Filters ?? Enumerable.Empty<GridFilter>())
                             ?? Enumerable.Empty<GridFilter>())
                        {
                            filter?.ExcludedFilter?.Add(永恒之环);
                        }
                    }
                }
                catch (Exception ex)
                {
                }
            };

            return;
        }

        public record ProfileStashSyncRequestData : BaseInteractionRequestData
        {
            [JsonPropertyName("stashData")]
            public Item[] StashData { get; set; }
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
        //操死傻逼白皮的血妈，他妈逼你妈小时候生你是不是也得先喊一声本宫要生了实例化了才能继续啊
        //死妈东西
        [Injectable]
        public class ProfileStashSyncService
        {
            private readonly InventoryHelper _inventoryHelper;
            private readonly HttpResponseUtil _httpResponseUtil;
            private readonly ServerLocalisationService _serverLocalisationService;
            private readonly ICloner _cloner;
            private readonly ItemHelper _itemHelper;
            public ProfileStashSyncService(
                InventoryHelper inventoryHelper,
                HttpResponseUtil httpResponseUtil,
                ServerLocalisationService serverLocalisationService,
                ICloner cloner,
                ItemHelper itemHelper)
            {
                _inventoryHelper = inventoryHelper;
                _httpResponseUtil = httpResponseUtil;
                _serverLocalisationService = serverLocalisationService;
                _cloner = cloner;
                _itemHelper = itemHelper;
            }
            public async ValueTask<ItemEventRouterResponse> HandleSyncStashExtendAsync(
                string url,
                PmcData pmcData,
                ProfileStashSyncRequestData body,
                MongoId sessionID,
                ItemEventRouterResponse output,
                CancellationToken cancellationToken)
            {
                if (body.StashData == null || body.StashData.Length == 0)
                    return output;
                var request = new ProfileStashDataContext
                {
                    StashDataContext = new[] { body.StashData.ToList() },
                    StrictJsonFormat = false,
                    Callback = null,
                    DiscardOverflowItem = false
                };
                SyncProfileStashExtend(sessionID, request, pmcData, output);
                return output;
            }
            private void SyncProfileStashExtend(
                MongoId sessionId,
                ProfileStashDataContext request,
                PmcData pmcData,
                ItemEventRouterResponse output)
            {
                if (!_inventoryHelper.CanPlaceItemsInInventory(sessionId, request.StashDataContext))
                {
                    _httpResponseUtil.AppendErrorToOutput(
                        output,
                        _serverLocalisationService.GetText("inventory-no_stash_space"),
                        BackendErrorCodes.NotEnoughSpace);
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
                        return;
                }
            }
            private void SyncStashExtend(
                MongoId sessionId,
                ProfileStashData request,
                PmcData pmcData,
                ItemEventRouterResponse output)
            {
                var itemSnapshot = _cloner.Clone(request.StashData);
                var allMethods = AccessTools.GetDeclaredMethods(typeof(InventoryHelper));
                var stashFS2D = (int[,])AccessTools.Method(typeof(InventoryHelper), "GetStashSlotMap")
                    .Invoke(_inventoryHelper, new object[] { pmcData });
                if (stashFS2D == null)
                    return;
                var sortingTableFS2D = AccessTools.Method(typeof(InventoryHelper), "GetSortingTableSlotMap")
                    .Invoke(_inventoryHelper, new object[] { pmcData });
                var syncMethod = allMethods.FirstOrDefault(m =>
                    m.Name.Contains("Inventory") &&
                    m.GetParameters().Length == 6 &&
                    m.GetParameters()[0].ParameterType == typeof(int[,]) &&
                    m.GetParameters()[1].ParameterType == typeof(int[,]));
                syncMethod?.Invoke(_inventoryHelper, new object[]
                {
            stashFS2D,
            sortingTableFS2D,
            itemSnapshot,
            pmcData.Inventory,
            !request.DiscardOverflowItem.GetValueOrDefault(true),
            output
                });
                if (output.Warnings?.Count > 0)
                    return;
                ResetItemState(itemSnapshot);
                AccessTools.Method(typeof(InventoryHelper), "RemoveTraderRagfairRelatedUpdProperties")
                    .Invoke(_inventoryHelper, new object[] { itemSnapshot[0].Upd });
                try
                {
                    request.Callback?.Invoke((int)(itemSnapshot[0].Upd.StackObjectsCount ?? 0));
                }
                catch (Exception ex)
                {
                    _httpResponseUtil.AppendErrorToOutput(output, ex.Message);
                    return;
                }
                output.ProfileChanges[sessionId].Items.NewItems.AddRange(itemSnapshot);
                pmcData.Inventory.Items.AddRange(itemSnapshot);
            }
            private void ResetItemState(IEnumerable<Item> itemList)
            {
                foreach (var item in itemList)
                {
                    item.AddUpd();
                    item.Upd.SpawnedInSession = _itemHelper.IsOfBaseclass(item.Template, BaseClasses.AMMO)
                        ? null
                        : item.Upd.SpawnedInSession ?? false;
                }
            }
        }

        //Weird, sometimes item from gift box will missing and sometimes will duplicate, profile broken risk, tried to fix it.
        [Injectable(TypePriority = OnLoadOrder.Routers + 1)]
        public class ProfileStashSyncExtendEventRouter : ItemEventRouter
        {
            public ProfileStashSyncExtendEventRouter(ProfileStashSyncService syncService)
                : base(new ItemRouteAction[]
                {
            new ItemRouteAction<ProfileStashSyncRequestData>(
                "SyncStashExtend",
                async (url, pmcData, body, sessionID, output, cancellationToken) =>
                    await syncService.HandleSyncStashExtendAsync(url, pmcData, body, sessionID, output, cancellationToken)
            )
                })
            { }
        }

        [Injectable]
        // 1. 使用主构造函数，将所有需要的服务（包括之前用 ServiceLocator 获取的）全部在此声明
        public class EternalCycleAwakeRouter(
         JsonUtil jsonUtil,
         HttpResponseUtil httpResponseUtil,
         RagfairController ragfairController,
         ItemHelper itemHelper,
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
        "/eternalcycle/callprofilebackup",
        (_, _, sessionId, _, _) => // 这里需要用到 sessionId
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
            (_, info, sessionId, _, _) =>
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
            (_, info, sessionId, _, _) =>
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
            (_, info, sessionId, _, _) =>
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
            (_, info, sessionId, _, _) =>
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
            (_, _, _, _, _) =>
            {

                var jsonResponse = jsonUtil.Serialize(new VoiceResourceRequest{ VoicePath = ResourceUtils.VoicePath});
                return ValueTask.FromResult<object>(jsonResponse);
            }
        ),

        new RouteAction(
            "/eternalcycle/loadquestzone",
            (_, _, _, _, _) =>
            {

                var zones = QuestZoneUtils.GetZones(cloner);
                return ValueTask.FromResult<object>(jsonUtil.Serialize(zones));
            }
        )
     ]);
    }

}
