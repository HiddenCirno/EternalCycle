using HarmonyLib;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http.HttpResults;
using MonoMod.Cil;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Callbacks;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Launcher;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Presets;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services.Locales;
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
using static EternalCycleServer.ContextManager;

namespace EternalCycleServer
{

    [Injectable]
    public class ProfileHelperPatch : AbstractPatch
    {
        private static TemplateTable _templateTable = default!;
        private static LocaleTable _localeTable = default!;
        private static GlobalTable _globalTable = default!;
        private static TradersTable _tradersTable = default!;
        private static HideoutTable _hideoutTable = default!;
        private static LocationTable _locationTable = default!;
        private static BotTable _botTable = default!;
        private static JsonUtil _jsonUtil = default!;
        private static ConfigServer _configServer = default!;
        private static ModHelper _modHelper = default!;
        private static ItemHelper _itemHelper = default!;
        private static LocaleService _localeService = default!;
        private static ICloner _cloner = default!;
        private static PresetHelper _presetHelper = default!;
        private static ImageRouter _imageRouter = default!;
        private static ECLogger _logger = default!;
        public ProfileHelperPatch(
        TemplateTable templateTable,
        LocaleTable localeTable,
        GlobalTable globalTable,
        TradersTable tradersTable,
        HideoutTable hideoutTable,
        LocationTable locationTable,
        BotTable botTable,
        JsonUtil jsonUtil,
        ConfigServer configServer,
        ModHelper modHelper,
        ItemHelper itemHelper,
        LocaleService localeService,
        ICloner cloner,
        PresetHelper presetHelper,
        ImageRouter imageRouter)
        {
            _templateTable = templateTable;
            _localeTable = localeTable;
            _globalTable = globalTable;
            _tradersTable = tradersTable;
            _hideoutTable = hideoutTable;
            _locationTable = locationTable;
            _botTable = botTable;
            _jsonUtil = jsonUtil;
            _configServer = configServer;
            _modHelper = modHelper;
            _itemHelper = itemHelper;
            _localeService = localeService;
            _cloner = cloner;
            _presetHelper = presetHelper;
            _imageRouter = imageRouter;
            _logger = new ECLogger("RagfairServer", true);
        }
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SaveCallbacks).GetMethod("OnLoadAsync", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        public static bool Prefix(SaveCallbacks __instance)
        {
            var context = new LoadModContext
            {
                DB = new DatabaseService(_templateTable, _localeTable, _globalTable, _tradersTable, _hideoutTable, _locationTable, _botTable),
                JsonUtil = _jsonUtil,
                ConfigServer = _configServer,
                ModHelper = _modHelper,
                Logger = Utils.commonLogger,
                PresetHelper = _presetHelper,
                ImageRouter = _imageRouter,
                ItemHelper = _itemHelper,
                Cloner = _cloner
            };

            EventManager.InitPreDataLoadEvent(context);

            EventManager.InitLoadItemEvent(context);
            EventManager.InitLoadTraderBaseEvent(context);
            EventManager.InitLoadQuestEvent(context);
            EventManager.InitLoadAchievementEvent(context);
            EventManager.InitLoadRecipeEvent(context);
            EventManager.InitLoadScavCaseRecipeEvent(context);
            EventManager.InitLoadCultistCircleRecipeEvent(context);
            EventManager.InitLoadGiftCodeEvent(context);
            EventManager.InitLoadAlterBotEvent(context);
            EventManager.InitLoadItemTagEvent(context);
            InitItemTag(context);
            EventManager.InitLoadDrawPoolEventEvent(context);
            EventManager.InitLoadTraderAssortEvent(context);
            EventManager.InitLoadQuestDataEvent(context);
            EventManager.InitLoadQuestRewardEvent(context);
            EventManager.InitLoadLockedTraderAssortEvent(context);
            EventManager.InitLoadLockedRecipeEvent(context);
            EventManager.InitLoadQuestLogicEvent(context);
            EventManager.InitLoadQuestLocaleEvent(context);
            EventManager.InitLoadPresetEvent(context);
            EventManager.InitLoadCustomizationEvent(context);
            EventManager.InitLoadSuitEvent(context);
            EventManager.InitLoadHideoutCustomizationEvent(context);
            EventManager.InitLoadQuestZoneEvent(context);
            EventManager.InitLoadDialogueEvent(context);
            EventManager.InitLoadResourceEvent(context);
            EventManager.InitLoadBannerEvent(context);
            EventManager.InitLoadLocaleEvent(context);

            EventManager.InitPostDataLoadEvent(context);

            //调试代码
            var items = context.DB.GetItems();
            foreach (var item in items)
            {
                if (item.Value == null || item.Value.Properties == null) continue;
                //item.Value.Properties.ExaminedByDefault = true;
            }
            ItemUtils.RegisterFixItem();
            EventManager.InitFixItemCompatibleEvent(context);
            EventManager.InitAfterModLoadedEvent(context);
            EventManager.InitPreRagfairLoadEvent(context);
            LocaleUtils.InitGiftBoxLocale(context.DB, _localeService);
            //试试游戏启动抓到的语言是不是MiniHUD的版本
            //是的话还得改过去(不会出问题吧)
            //看看迷宫的机关怎么回事
            return true;
        }
        
        public static void InitItemTag(LoadModContext context)
        {
            //内置tag
            var taglist = new ItemTagDictionary();

            // 1. 建立武器专用的“白名单映射字典”
            // 这里只放你明确需要生成的武器类型，彻底隔绝建筑材料、医疗用品等垃圾数据
            var targetWeapons = new Dictionary<string, string>
                {
                    { "突击卡宾枪", ERagfairTagsType.突击卡宾枪 },
                    { "突击步枪", ERagfairTagsType.突击步枪 },
                    { "精确射手步枪", ERagfairTagsType.精确射手步枪 },
                    { "手枪", ERagfairTagsType.手枪 },
                    { "霰弹枪", ERagfairTagsType.霰弹枪 },
                    { "冲锋枪", ERagfairTagsType.冲锋枪 },
                    { "栓动式步枪", ERagfairTagsType.栓动式步枪 },
                    { "机枪", ERagfairTagsType.机枪 },
                    { "榴弹发射器", ERagfairTagsType.榴弹发射器 },
                    { "特殊武器", ERagfairTagsType.特殊武器 },
                    { "近战武器", ERagfairTagsType.近战武器 },
                    { "投掷物", ERagfairTagsType.投掷物 },
                    { "其他", ERagfairTagsType.其他 },
                    { "医疗用品", ERagfairTagsType.医疗用品 },
                    { "工具", ERagfairTagsType.工具 },
                    { "建筑材料", ERagfairTagsType.建筑材料 },
                    { "日常用品", ERagfairTagsType.日常用品 },
                    { "易燃物品", ERagfairTagsType.易燃物品 },
                    { "电子产品", ERagfairTagsType.电子产品 },
                    { "能源物品", ERagfairTagsType.能源物品 },
                    { "贵重物品", ERagfairTagsType.贵重物品 },
                    { "耳机", ERagfairTagsType.耳机 },
                    { "背包", ERagfairTagsType.背包 },
                    { "防弹衣", ERagfairTagsType.防弹衣 },
                    { "战术胸挂", ERagfairTagsType.战术胸挂 },
                    { "子弹", ERagfairTagsType.子弹 },
                    { "弹药包", ERagfairTagsType.弹药包 },
                    { "食物", ERagfairTagsType.食物 },
                    { "饮品", ERagfairTagsType.饮品 },
                    { "创伤处理", ERagfairTagsType.创伤处理 },
                    { "急救包", ERagfairTagsType.急救包 },
                    { "注射器", ERagfairTagsType.注射器 },
                    { "药品", ERagfairTagsType.药品 },
                    { "机械钥匙", ERagfairTagsType.机械钥匙 },
                    { "电子钥匙", ERagfairTagsType.电子钥匙 },
                    { "情报物品", ERagfairTagsType.情报物品 },
                    { "特殊装备", ERagfairTagsType.特殊装备 }
                };

            // 2. 精准遍历白名单
            foreach (var kvp in targetWeapons)
            {
                string tagName = kvp.Key;
                string tagValue = kvp.Value;

                // 每次必须 new 一个新的对象，避免引用陷阱
                var newTagSet = new ItemTag();

                // 尝试获取该分类下的所有物品
                var items = ItemUtils.GetItemListByRagfairTag(tagValue, context);

                // 如果获取不到物品，或者集合为空，直接跳过当前分类
                if (items == null) continue;

                foreach (var item in items)
                {
                    newTagSet.Add(item);
                }

                // 3. 【终极防呆】只有当集合里确确实实装了东西，才允许塞进最终字典！
                if (newTagSet.Count > 0)
                {
                    taglist[tagName] = newTagSet;
                }
            }

            //狗牌
            var usecDogTags = new ItemTag();
            var bearDogTags = new ItemTag();
            try
            {
                var pmcConfig = context.ConfigServer.GetConfig<PmcConfig>();
                if (pmcConfig?.DogtagSettings != null)
                {
                    if (pmcConfig.DogtagSettings.TryGetValue("usec", out var usecEditions))
                        foreach (var edition in usecEditions.Values)
                            foreach (var id in edition.Keys)
                                usecDogTags.Add(id);
                    if (pmcConfig.DogtagSettings.TryGetValue("bear", out var bearEditions))
                        foreach (var edition in bearEditions.Values)
                            foreach (var id in edition.Keys)
                                bearDogTags.Add(id);
                }
            }
            catch (Exception ex)
            {
                Utils.commonLogger.Warn($"狗牌标签生成失败: {ex.Message}");
            }
            if (usecDogTags.Count > 0)
            {
                taglist["USEC狗牌"] = usecDogTags;
            }
            if (bearDogTags.Count > 0)
            {
                taglist["BEAR狗牌"] = bearDogTags;
            }

            ItemTagUtils.InitItemTagData(taglist, context);
        }
    }
    }