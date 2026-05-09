using HarmonyLib;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http.HttpResults;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Loaders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Launcher;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json;
using VulcanCore;

namespace VulcanCore
{
    public class AddBundlePatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BundleLoader).GetMethod("AddBundle", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }
        [PatchPrefix]
        public static bool Prefix(BundleLoader __instance, string key, BundleInfo bundle)
        {

            // 获取构造函数参数中的 logger
            //var logger = GetLogger(__instance);
            var bundlesField = AccessTools.Field(typeof(BundleLoader), "_bundles");
            var bundles = (ConcurrentDictionary<string, BundleInfo>)bundlesField.GetValue(__instance);

            var success = bundles.TryAdd(key, bundle);
            if (!success)
            {
                //logger.Warning($"Failed to add bundle: {key} is already exist.");
            }

            return false; // 跳过原始方法执行
        }

    }
}