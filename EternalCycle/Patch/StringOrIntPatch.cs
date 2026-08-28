using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Loaders;
using System.Collections.Concurrent;
using System.Reflection;
using SPTarkov.Server.Core.Models.Spt.Bundles;
using SPTarkov.Server.Core.Utils.Json;

namespace EternalCycleServer
{
    public class StringOrIntPatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(StringOrInt).GetMethod("ToString", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }
        [PatchPrefix]
        public static bool Prefix(StringOrInt __instance, ref string? __result)
        {
            if (__instance.String is null && __instance.Int is null)
            {
                __result = null;
                return false;
            }   

            if (__instance.IsInt)
            {
                __result = __instance.Int?.ToString();
                return false;
            }
            __result = __instance.String;
            return false;
        }
    }
}