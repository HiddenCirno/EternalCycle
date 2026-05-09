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
using VulcanCore;
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

namespace VulcanCore
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
            var localeService = ServiceLocator.ServiceProvider.GetService<LocaleService>();
            LocaleUtils.InitGiftBoxLocale(databaseService, localeService);
            File.WriteAllText(System.IO.Path.Combine(ConfigManager.modPath, "exportidmap.json"), jsonUtil.Serialize(VulcanUtil.HashIdList, true));
            //试试游戏启动抓到的语言是不是MiniHUD的版本
            //是的话还得改过去(不会出问题吧)
            //看看迷宫的机关怎么回事
            return true;
        }

    }
}