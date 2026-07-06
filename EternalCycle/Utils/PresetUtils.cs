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
using static EternalCycle.ContextManager;
using Path = System.IO.Path;

namespace EternalCycle;
public class PresetUtils
{
    public static void InitPresetData(List<CustomPresetData> presetData, LoadModContext context)
    {
        foreach (var preset in presetData)
        {
            InitPreset(preset, context);
        }
    }
    public static void InitPresetData(string folderpath, LoadModContext context)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var preset = context.ModHelper.GetJsonDataFromFile<CustomPresetData>(folderpath, fileName);
                InitPreset(preset, context);
            }
        }
    }
    public static void InitPreset(CustomPresetData preset, LoadModContext context)
    {
        var Preset = context.DB.GetGlobals().ItemPresets;
        var zhCNLang = context.DB.GetLocales().Global["ch"];
        var presetname = preset.Name;
        var itempresetdata = ItemUtils.ConvertItemListData(preset.PresetData, context); //new List<Item>();
        var presetid = (MongoId)Utils.ConvertHashID(presetname);
        var realpresetdata = ItemUtils.RegenerateItemListData(itempresetdata, presetname, context);
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
            LootUtils.AddPresetLoot(realpresetdata, preset.SpawnTarget, context);
        }
    }
}










