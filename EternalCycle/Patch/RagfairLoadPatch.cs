using System.Reflection;
using System;
using Microsoft.AspNetCore.Http.HttpResults;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Reflection.Patching;
using System.Reflection;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using HarmonyLib;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Eft.Common;
using System.Text;
using JetBrains.Annotations;
using SPTarkov.Server.Core.Constants;
using System.Runtime.Intrinsics.Arm;
using System.Net;
using System.Text.Json;
using System.Runtime.InteropServices;
using SPTarkov.Server.Core.Models.Spt.Launcher;

namespace EternalCycle
{
    public class RagfairLoadPatch : AbstractPatch
    {
        public static bool firststart = false;
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
            var imageRouter = ServiceLocator.ServiceProvider.GetService<ImageRouter>();
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

            EventManager.InitLoadItemEvent(context);
            EventManager.InitLoadTraderBaseEvent(context);
            EventManager.InitLoadQuestEvent(context);
            EventManager.InitLoadAchievementEvent(context);
            EventManager.InitLoadRecipeEvent(context);
            EventManager.InitLoadScavCaseRecipeEvent(context);
            EventManager.InitLoadCultCircleRecipeEvent(context);
            EventManager.InitLoadTraderAssortEvent(context);
            EventManager.InitLoadQuestDataEvent(context);
            EventManager.InitLoadQuestRewardEvent(context);
            EventManager.InitLoadLockedTraderAssortEvent(context);
            EventManager.InitLoadLockedRecipeEvent(context);
            EventManager.InitLoadQuestLogicEvent(context);
            EventManager.InitLoadQuestLocaleEvent(context);

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
            var logger = new ECLogger("PostRagfairLoadEvent", true);
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
            EventManager.InitPostRagfairLoadEvent(context);
        }

    }
}