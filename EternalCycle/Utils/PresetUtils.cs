using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Templates;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Json;
using SPTarkov.Server.Core.Utils.Logger;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Path = System.IO.Path;
namespace VulcanCore;
public class PresetUtils
{
    public static void InitPresetData(List<CustomPresetData> presetData, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        foreach (var preset in presetData)
        {
            InitPreset(preset, databaseService, cloner, logger);
        }
    }
    public static void InitPresetData(string folderpath, DatabaseService databaseService, ModHelper modHelper, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var preset = modHelper.GetJsonDataFromFile<CustomPresetData>(folderpath, fileName);
                InitPreset(preset, databaseService, cloner, logger);
            }
        }
    }
    public static void InitPreset(CustomPresetData preset, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        var Preset = databaseService.GetGlobals().ItemPresets;
        var zhCNLang = databaseService.GetLocales().Global["ch"];
        var presetname = preset.Name;
        var itempresetdata = ItemUtils.ConvertItemListData(preset.PresetData, cloner); //new List<Item>();
        var presetid = (MongoId)VulcanUtil.ConvertHashID(presetname);
        var realpresetdata = ItemUtils.RegenerateItemListData(itempresetdata, presetname, cloner);
        if (preset.IsBasePreset)
        {
            Preset.TryAdd(presetid, new Preset
            {
                ChangeWeaponName = preset.ChangePresetName,
                Encyclopedia = realpresetdata[0].Template,
                Id = presetid,
                Items = realpresetdata,
                Name = preset.PresetName,
                Parent = realpresetdata[0].Id,
                Type = "Preset"
            });
        }
        else
        {
            Preset.TryAdd(presetid, new Preset
            {
                ChangeWeaponName = preset.ChangePresetName,
                Id = presetid,
                Items = realpresetdata,
                Name = preset.PresetName,
                Parent = realpresetdata[0].Id,
                Type = "Preset"
            });
        }
        zhCNLang.AddTransformer(lang =>
        {
            lang.TryAdd(presetid, preset.PresetName);
            return lang;
        });
        if (preset.SpawnInRaid)
        {
            LootUtils.AddPresetLoot(realpresetdata, preset.SpawnTarget, databaseService, cloner, logger);
        }
    }
}










