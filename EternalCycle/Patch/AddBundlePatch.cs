using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Loaders;
using System.Collections.Concurrent;
using System.Reflection;
using SPTarkov.Server.Core.Models.Spt.Bundles;

namespace EternalCycleServer
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