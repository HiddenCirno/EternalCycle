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
    public class RagfairLoadPatch : AbstractPatch
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
        public RagfairLoadPatch(
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
            return typeof(RagfairServer).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        public static bool Prefix(RagfairServer __instance)
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
            //干他妈的预设缓存
            var itemPresets = context.DB.GetGlobals().ItemPresets;
            var presetHelperInstance = context.PresetHelper;
            Traverse.Create(context.PresetHelper).Field("DefaultWeaponPresets").SetValue(null);
            Traverse.Create(context.PresetHelper).Field("DefaultEquipmentPresets").SetValue(null);
            var newPresetCache = new Dictionary<MongoId, PresetCacheDetails>();

            foreach (var kvp in itemPresets)
            {
                var presetId = kvp.Key;
                var preset = kvp.Value;

                // 找到这个预设的根物品 (武器本体/防弹衣本体)
                var rootItem = preset.Items.FirstOrDefault(x => x.Id == preset.Parent);
                if (rootItem == null) continue;

                var tpl = rootItem.Template;

                // 如果字典里还没这个 Tpl，建个档案
                if (!newPresetCache.ContainsKey(tpl))
                {
                    newPresetCache[tpl] = new PresetCacheDetails
                    {
                        PresetIds = new HashSet<MongoId>()
                    };
                }

                // 把当前的预设 ID 加进列表
                newPresetCache[tpl].PresetIds.Add(presetId);

                // 如果这个预设是官方出厂配置 (带有 Encyclopedia)，把它设为默认
                if (preset.Encyclopedia != null)
                {
                    newPresetCache[tpl].DefaultId = presetId;
                }
            }

            // ==========================================
            // 3. 将最新、最全的缓存注入回单例中！
            // ==========================================
            // HydratePresetStore 是 public 的，直接调用，完美覆盖！
            context.PresetHelper.HydratePresetStore(newPresetCache);

            File.WriteAllText(System.IO.Path.Combine(ConfigManager.modPath, "exportidmap.json"), context.JsonUtil.Serialize(Utils.hashIdList, true));
            File.WriteAllText(System.IO.Path.Combine(ConfigManager.modPath, "exportquest.json"), context.JsonUtil.Serialize(context.DB.GetQuests(), true));
            File.WriteAllText(System.IO.Path.Combine(ConfigManager.modPath, "exportitem.json"), context.JsonUtil.Serialize(context.DB.GetItems(), true));
            File.WriteAllText(System.IO.Path.Combine(ConfigManager.modPath, "exportlocale.json"), context.JsonUtil.Serialize(_localeService.GetLocaleDb("ch"), true));
            //试试游戏启动抓到的语言是不是MiniHUD的版本
            //是的话还得改过去(不会出问题吧)
            //看看迷宫的机关怎么回事
            return true;
        }

        [PatchPostfix]
        public static void Postfix(RagfairServer __instance)
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
            EventManager.InitPostRagfairLoadEvent(context);
        }

    }
}