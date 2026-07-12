using HarmonyLib;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http.HttpResults;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Launcher;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Presets;
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

namespace EternalCycleServer
{
    public class RagfairLoadPatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(RagfairServer).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        public static bool Prefix(RagfairServer __instance)
        {
            var jsonUtil = ServiceLocator.ServiceProvider.GetService<JsonUtil>();
            var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
            var configServer = ServiceLocator.ServiceProvider.GetService<ConfigServer>();
            var modHelper = ServiceLocator.ServiceProvider.GetService<ModHelper>();
            var itemHelper = ServiceLocator.ServiceProvider.GetService<ItemHelper>();
            var cloner = ServiceLocator.ServiceProvider.GetService<ICloner>();
            var localeService = ServiceLocator.ServiceProvider.GetService<LocaleService>();
            var presetHelper = ServiceLocator.ServiceProvider.GetService<PresetHelper>();
            var imageRouter = ServiceLocator.ServiceProvider.GetService<ImageRouter>();
            var context = new ContextManager.LoadModContext
            {
                DB = databaseService,
                JsonUtil = jsonUtil,
                ConfigServer = configServer,
                ModHelper = modHelper,
                Logger = Utils.commonLogger,
                PresetHelper = presetHelper,
                ImageRouter = imageRouter,
                ItemHelper = itemHelper,
                Cloner = cloner
            }; 
            //干他妈的预设缓存
            var itemPresets = context.DB.GetGlobals().ItemPresets;
            var presetHelperInstance = context.PresetHelper;
            Traverse.Create(presetHelper).Field("DefaultWeaponPresets").SetValue(null);
            Traverse.Create(presetHelper).Field("DefaultEquipmentPresets").SetValue(null);
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
            presetHelper.HydratePresetStore(newPresetCache);
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
                    { "投掷物", ERagfairTagsType.投掷物 }
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

            /*
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
            EventManager.InitLoadResourceEventEvent(context);

            EventManager.InitPostDataLoadEvent(context);

            //调试代码
            var items = databaseService.GetItems();
            foreach (var item in items)
            {
                if (item.Value == null || item.Value.Properties == null) continue;
                //item.Value.Properties.ExaminedByDefault = true;
            }
            ItemUtils.RegisterFixItem();
            EventManager.InitFixItemCompatibleEventEvent(context);
            EventManager.InitAfterModLoadedEvent(context);
            EventManager.InitPreRagfairLoadEvent(context);
            LocaleUtils.InitGiftBoxLocale(databaseService, localeService);
            */
            File.WriteAllText(System.IO.Path.Combine(ConfigManager.modPath, "exportidmap.json"), jsonUtil.Serialize(Utils.hashIdList, true));
            File.WriteAllText(System.IO.Path.Combine(ConfigManager.modPath, "exportquest.json"), jsonUtil.Serialize(databaseService.GetQuests(), true));
            File.WriteAllText(System.IO.Path.Combine(ConfigManager.modPath, "exportitem.json"), jsonUtil.Serialize(databaseService.GetItems(), true));
            File.WriteAllText(System.IO.Path.Combine(ConfigManager.modPath, "exportlocale.json"), jsonUtil.Serialize(localeService.GetLocaleDb("ch"), true));
            //试试游戏启动抓到的语言是不是MiniHUD的版本
            //是的话还得改过去(不会出问题吧)
            //看看迷宫的机关怎么回事
            return true;
        }

        [PatchPostfix]
        public static void Postfix(RagfairServer __instance)
        {
            var jsonUtil = ServiceLocator.ServiceProvider.GetService<JsonUtil>();
            var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
            var configServer = ServiceLocator.ServiceProvider.GetService<ConfigServer>();
            var modHelper = ServiceLocator.ServiceProvider.GetService<ModHelper>();
            var cloner = ServiceLocator.ServiceProvider.GetService<ICloner>();
            var itemHelper = ServiceLocator.ServiceProvider.GetService<ItemHelper>();
            var imageRouter = ServiceLocator.ServiceProvider.GetService<ImageRouter>();
            var presetHelper = ServiceLocator.ServiceProvider.GetService<PresetHelper>();
            var logger = new ECLogger("PostRagfairLoadEvent", true);
            var context = new ContextManager.LoadModContext
            {
                DB = databaseService,
                JsonUtil = jsonUtil,
                ConfigServer = configServer,
                ModHelper = modHelper,
                Logger = Utils.commonLogger,
                ImageRouter = imageRouter,
                PresetHelper = presetHelper,
                ItemHelper = itemHelper,
                Cloner = cloner
            };
            EventManager.InitPostRagfairLoadEvent(context);
        }

    }
    public class ProfileHelperPatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SaveServer).GetMethod("LoadAsync", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPrefix]
        public static bool Prefix(SaveServer __instance)
        {
            var jsonUtil = ServiceLocator.ServiceProvider.GetService<JsonUtil>();
            var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
            var configServer = ServiceLocator.ServiceProvider.GetService<ConfigServer>();
            var modHelper = ServiceLocator.ServiceProvider.GetService<ModHelper>();
            var itemHelper = ServiceLocator.ServiceProvider.GetService<ItemHelper>();
            var cloner = ServiceLocator.ServiceProvider.GetService<ICloner>();
            var localeService = ServiceLocator.ServiceProvider.GetService<LocaleService>();
            var presetHelper = ServiceLocator.ServiceProvider.GetService<PresetHelper>();
            var imageRouter = ServiceLocator.ServiceProvider.GetService<ImageRouter>();
            var context = new ContextManager.LoadModContext
            {
                DB = databaseService,
                JsonUtil = jsonUtil,
                ConfigServer = configServer,
                ModHelper = modHelper,
                Logger = Utils.commonLogger,
                ImageRouter = imageRouter,
                PresetHelper = presetHelper,
                ItemHelper = itemHelper,
                Cloner = cloner
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
            EventManager.InitLoadResourceEventEvent(context);

            EventManager.InitPostDataLoadEvent(context);

            //调试代码
            var items = databaseService.GetItems();
            foreach (var item in items)
            {
                if (item.Value == null || item.Value.Properties == null) continue;
                //item.Value.Properties.ExaminedByDefault = true;
            }
            ItemUtils.RegisterFixItem();
            EventManager.InitFixItemCompatibleEventEvent(context);
            EventManager.InitAfterModLoadedEvent(context);
            EventManager.InitPreRagfairLoadEvent(context);
            LocaleUtils.InitGiftBoxLocale(databaseService, localeService);
            //试试游戏启动抓到的语言是不是MiniHUD的版本
            //是的话还得改过去(不会出问题吧)
            //看看迷宫的机关怎么回事
            return true;
        }
    }
    }