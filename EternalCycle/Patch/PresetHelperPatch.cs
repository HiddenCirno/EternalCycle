using EternalCycleServer;
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
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
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
    public class PresetHelperPatch : AbstractPatch
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
        private static ICloner _cloner = default!;
        private static PresetHelper _presetHelper = default!;
        private static ImageRouter _imageRouter = default!;
        private static ECLogger _logger = default!;
        public PresetHelperPatch(
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
            _cloner = cloner;
            _presetHelper = presetHelper;
            _imageRouter = imageRouter;
            _logger = new ECLogger("PresetHelper", true);
        }
        protected override MethodBase GetTargetMethod()
        {
            return typeof(PresetHelper).GetMethod("GetDefaultPreset", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }
        [PatchPrefix]
        public static bool Prefix(PresetHelper __instance, MongoId templateId, ref Preset __result)
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
            var presets = context.DB.GetGlobals().ItemPresets;
            var defaultpreset = presets.FirstOrDefault(x => x.Value.Encyclopedia == templateId).Value;
            if(defaultpreset!=null && defaultpreset.Items.Count > 0)
            {
                __result = context.Cloner.Clone(defaultpreset);
                return false;
            }
            return true;
        }
    }
}