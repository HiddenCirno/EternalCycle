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
            ItemTagUtils.InitItemTagData(taglist, context);

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