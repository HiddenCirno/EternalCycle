using HarmonyLib;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Loaders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Ragfair;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
namespace VulcanCore;
public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.hiddenhiragi.vulcancore";
    public override string Name { get; init; } = "火神之心-重生";
    public override string Author { get; init; } = "HiddenHiragi";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("1.0.1");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.0");
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
            _initialized = true;

            try
            {
                //var harmony = new Harmony("com.hiddenhiragi.vulcancore");
                //harmony.PatchAll(typeof(Init).Assembly);
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
    ISptLogger<VulcanCore> logger,
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
public class VulcanCore(
    ISptLogger<VulcanCore> logger,
    DatabaseService databaseService,
    CustomItemService customItemService,
    ModHelper modHelper,
    JsonUtil jsonutil,
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
                        ItemUtils.AddBlackList(item.Value.Id, 31, configServer);
                    }
                    else
                    {
                        handbooks.Add(new HandbookItem
                        {
                            Id = item.Value.Id,
                            ParentId = quest,
                            Price = 20000
                        });
                        ItemUtils.AddBlackList(item.Value.Id, 31, configServer);
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
                    ItemUtils.AddBlackList(item.Value.Id, 64, configServer);
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
        //new RemoveExpiredItemsFromMessagePatch().Enable();
        new RagfairLoadPatch().Enable();
        ItemUtils.InitItem(System.IO.Path.Combine(modPath, "items/"), "<color=#8FFF00>火神之心-物品加载器</color>", "<color=#FFFF80>火神之心</color>", logger, databaseService, jsonutil, cloner, configServer);
        return Task.CompletedTask;
    }

    [Injectable]
    public class VulcanCoreAwakeRouter : StaticRouter
    {
        private static HttpResponseUtil _httpResponseUtil;
        private static DatabaseService _databaseService;
        private static RagfairController _ragfairController;
        private static JsonUtil _jsonUtil;
        private static RagfairOfferService _ragfairOfferService;
        private static ItemHelper _itemHelper;
        private static ISptLogger<VulcanCore> _logger;
        private static ICloner _cloner;
        private static VulcanCore _vulcanCore;

        public VulcanCoreAwakeRouter(
            JsonUtil jsonUtil,
            HttpResponseUtil httpResponseUtil,
            DatabaseService databaseService,
            RagfairController ragfairController,
            RagfairOfferService ragfairOfferService,
            ItemHelper itemHelper,
            ISptLogger<VulcanCore> logger,
            ICloner cloner,
            VulcanCore vulcanCore)
            : base(jsonUtil, GetCustomRoutes())
        {
            _httpResponseUtil = httpResponseUtil;
            _databaseService = databaseService;
            _ragfairController = ragfairController;
            _ragfairOfferService = ragfairOfferService;
            _itemHelper = itemHelper;
            _logger = logger;
            _cloner = cloner;
            _jsonUtil = jsonUtil;
            _vulcanCore = vulcanCore;
        }

        private static List<RouteAction> GetCustomRoutes()
        {
            return new List<RouteAction>
        {
            new RouteAction(
                "/VulcanCoreClient/InitFix",
                async (url, info, sessionId, output) =>
                    await HandleRoute(
                        url,
                        sessionId,
                        _jsonUtil,
                        _databaseService,
                        _ragfairController,
                        _ragfairOfferService,
                        _itemHelper,
                        _logger,
                        _cloner,
                        _vulcanCore
                    )
            ),
            new RouteAction(
                "/VulcanCoreClient/ClientStartCall",
                async (url, info, sessionId, output) =>
                    await HandleClientStart(
                        url,
                        sessionId,
                        _jsonUtil,
                        _databaseService,
                        _ragfairController,
                        _ragfairOfferService,
                        _itemHelper,
                        _logger,
                        _cloner,
                        _vulcanCore
                    )
            ),
            new RouteAction(
                "/VulcanCoreClient/CallBackup",
                async (url, info, sessionId, output) =>
                    await HandleBackupCall(
                        url,
                        sessionId,
                        _jsonUtil,
                        _databaseService,
                        _ragfairController,
                        _ragfairOfferService,
                        _itemHelper,
                        _logger,
                        _cloner,
                        _vulcanCore
                    )
            )
        };
        }
        private static ValueTask<string> HandleRoute(
            string url,
            MongoId sessionId,
            JsonUtil jsonUtil,
            DatabaseService databaseService,
            RagfairController ragfairController,
            RagfairOfferService ragfairOfferService,
            ItemHelper itemHelper,
            ISptLogger<VulcanCore> logger,
            ICloner cloner,
            VulcanCore vulcanCore
            )
        {
            var localeService = ServiceLocator.ServiceProvider.GetService<LocaleService>();
            if (!ItemUtils.firstlogin)
            {
                VulcanLog.Warn("正在修复物品数据....", logger);
                // 构建返回的价格字典
                ItemUtils.FixItemCompatibleInit(ItemUtils.FixList, databaseService, logger, cloner);
                //VulcanLog.Debug($"{LocaleUtils.GetItemName(VulcanUtil.ConvertHashID("为了全人类海报"), localeService)}", logger);
                VulcanLog.Access("物品数据修复完成", logger);
                ItemUtils.firstlogin = true;
            }
            // 使用 HttpResponseUtil 返回标准格式 JSON
            //string jsonResponse = _httpResponseUtil.GetBody(priceMap);
            //绕过SPT提供的方法直接传递原始数据
            return new ValueTask<string>("Response successful.");
        }
        private static ValueTask<string> HandleClientStart(
            string url,
            MongoId sessionId,
            JsonUtil jsonUtil,
            DatabaseService databaseService,
            RagfairController ragfairController,
            RagfairOfferService ragfairOfferService,
            ItemHelper itemHelper,
            ISptLogger<VulcanCore> logger,
            ICloner cloner,
            VulcanCore vulcanCore
            )
        {
            var localeService = ServiceLocator.ServiceProvider.GetService<LocaleService>();
            VulcanLog.Warn("游戏启动", logger);
            // 使用 HttpResponseUtil 返回标准格式 JSON
            //string jsonResponse = _httpResponseUtil.GetBody(priceMap);
            //绕过SPT提供的方法直接传递原始数据
            return new ValueTask<string>("Response successful.");
        }
        private static ValueTask<string> HandleBackupCall(
            string url,
            MongoId sessionId,
            JsonUtil jsonUtil,
            DatabaseService databaseService,
            RagfairController ragfairController,
            RagfairOfferService ragfairOfferService,
            ItemHelper itemHelper,
            ISptLogger<VulcanCore> logger,
            ICloner cloner,
            VulcanCore vulcanCore
            )
        {
            var localeService = ServiceLocator.ServiceProvider.GetService<LocaleService>();
            var profileHelper = ServiceLocator.ServiceProvider.GetService<ProfileHelper>();
            var backupPath = System.IO.Path.Combine(vulcanCore.modPath, "Backup");
            var currectProfile = profileHelper.GetFullProfile(sessionId);
            var profileToSave = jsonUtil.Serialize(currectProfile, true);
            var pmcName = currectProfile.CharacterData.PmcData.Info.Nickname;
            var currectPmcName = VulcanUtil.GetValidFolderName(pmcName);
            var timePath = DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss");
            var time = DateTime.Now.ToString("yyyy年MM月dd日HH时mm分ss秒");
            var currcetBackupPath = System.IO.Path.Combine(backupPath, timePath, currectPmcName);
            Directory.CreateDirectory(currcetBackupPath);
            var filePath = System.IO.Path.Combine(currcetBackupPath, $"{sessionId}.json");
            File.WriteAllText(filePath, profileToSave);
            var backupLog = $"当前存档已成功备份! 玩家名: {pmcName} 备份时间: {time} 保存路径: {filePath}";
            var backupMessage = $"{pmcName}的存档已成功备份到{filePath}";
            VulcanLog.Access(backupLog, logger);
            // 使用 HttpResponseUtil 返回标准格式 JSON
            //string jsonResponse = _httpResponseUtil.GetBody(priceMap);
            //绕过SPT提供的方法直接传递原始数据
            return new ValueTask<string>(backupMessage);
        }

    }
}

