using HarmonyLib;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http.HttpResults;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
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
            return typeof(SaveServer).GetMethod("LoadAsync", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        public static bool Prefix(SaveServer __instance)
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
            EventManager.InitLoadtemTagEvent(context);
            EventManager.InitLoadDrawPoolEventEvent(context);
            EventManager.InitLoadTraderAssortEvent(context);
            EventManager.InitLoadQuestDataEvent(context);
            EventManager.InitLoadQuestRewardEvent(context);
            EventManager.InitLoadLockedTraderAssortEvent(context);
            EventManager.InitLoadLockedRecipeEvent(context);
            EventManager.InitLoadQuestLogicEvent(context);
            EventManager.InitLoadQuestLocaleEvent(context);
            EventManager.InitLoadLocaleEvent(context);
            EventManager.InitLoadPresetEvent(context);
            EventManager.InitLoadCustomizationEvent(context);
            EventManager.InitLoadSuitEvent(context);
            EventManager.InitLoadHideoutCustomizationEvent(context);
            EventManager.InitLoadQuestZoneEvent(context);
            EventManager.InitLoadResourceEventEvent(context);

            EventManager.InitPostDataLoadEvent(context);

            //调试代码
            var items = context.DB.GetItems();
            foreach (var item in items)
            {
                if (item.Value == null || item.Value.Properties == null) continue;
                //item.Value.Properties.ExaminedByDefault = true;
            }
            ItemUtils.RegisterFixItem();
            EventManager.InitFixItemCompatibleEventEvent(context);
            EventManager.InitAfterModLoadedEvent(context);
            EventManager.InitPreRagfairLoadEvent(context);
            LocaleUtils.InitGiftBoxLocale(context.DB, _localeService);
            //试试游戏启动抓到的语言是不是MiniHUD的版本
            //是的话还得改过去(不会出问题吧)
            //看看迷宫的机关怎么回事
            return true;
        }
    }
    }