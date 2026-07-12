using HarmonyLib;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Razor.TagHelpers;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
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
using EternalCycleServer;

namespace EternalCycleServer
{
    public class PresetHelperPatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(PresetHelper).GetMethod("GetDefaultPreset", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }
        [PatchPrefix]
        public static bool Prefix(PresetHelper __instance, MongoId templateId, ref Preset __result)
        {
            var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
            var cloner = ServiceLocator.ServiceProvider.GetService<ICloner>();

            var presets = databaseService.GetGlobals().ItemPresets;
            var defaultpreset = presets.FirstOrDefault(x => x.Value.Encyclopedia == templateId).Value;
            if(defaultpreset!=null && defaultpreset.Items.Count > 0)
            {
                __result = cloner.Clone(defaultpreset);
                return false;
            }
            return true;
        }
    }
}