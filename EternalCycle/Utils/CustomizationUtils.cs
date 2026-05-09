using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
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
public class CustomizationUtils
{
    public static void InitCustomiaztionData(Dictionary<string, CustomCustomizationItem> customData, DatabaseService databaseService, ICloner cloner)
    {
        foreach (var item in customData)
        {
            InitCustomization(item.Value, databaseService, cloner);
        }
    }
    public static void InitCustomiaztionData(string folderpath, DatabaseService databaseService, ModHelper modHelper, ICloner cloner)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var customization = modHelper.GetJsonDataFromFile<Dictionary<string, CustomCustomizationItem>>(folderpath, fileName);
                InitCustomiaztionData(customization, databaseService, cloner);
            }
        }
    }
    public static void InitCustomization(CustomCustomizationItem customCustomizationItem, DatabaseService databaseService, ICloner cloner)
    {
        var zhCNLang = databaseService.GetLocales().Global["ch"];
        var customs = databaseService.GetCustomization();
        var customid = customCustomizationItem.Id;
        customs.TryAdd(customid, new CustomizationItem
        {
            Id = customid,
            Name = customCustomizationItem.Name,
            Parent = customCustomizationItem.ParentId,
            Properties = customCustomizationItem.Properties,
            Type = customCustomizationItem.Type,
            Prototype = customCustomizationItem.Proto
        });
        if (customCustomizationItem.Properties.Prefab != null && customCustomizationItem.Properties.IsVoice == true)
        {
            var storage = databaseService.GetTables().Templates.CustomisationStorage;
            storage.Add(new CustomisationStorage
            {
                Id = customid,
                Source = CustomisationSource.DEFAULT,
                Type = CustomisationType.VOICE
            });
        }
        zhCNLang.AddTransformer(lang =>
        {
            lang[$"{customid} Name"] = customCustomizationItem.Properties.Name;
            lang[$"{customid} ShortName"] = customCustomizationItem.Properties.ShortName;
            lang[$"{customid} Description"] = customCustomizationItem.Properties.Description;
            return lang;
        });
    }
    public static void InitHideoutCustomiaztionData(Dictionary<string, CustomHideoutCustomization> customData, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        foreach (var item in customData)
        {
            InitHideoutCustomization(item.Value, databaseService, cloner, logger);
        }
    }
    public static void InitHideoutCustomization(CustomHideoutCustomization customCustomHideoutCustomization, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        var zhCNLang = databaseService.GetLocales().Global["ch"];
        var customs = databaseService.GetHideout().Customisation.Globals;
        var customid = customCustomHideoutCustomization.Id;
        var conditions = new HideoutCustomisationGlobal
        {
            Id = customid,
            SystemName = customCustomHideoutCustomization.Name,
            Conditions = new List<QuestCondition>(),
            IsEnabled = customCustomHideoutCustomization.IsEnable,
            Index = 0,
            ItemId = customCustomHideoutCustomization.Target,
            Type = customCustomHideoutCustomization.Type,
        };
        QuestUtils.InitQuestConditions(conditions.Conditions, customCustomHideoutCustomization.Conditions, databaseService, cloner, logger);
        customs.Add(conditions);
        zhCNLang.AddTransformer(lang =>
        {
            lang[$"{customid} name"] = customCustomHideoutCustomization.Name;
            lang[$"{customid} shortname"] = customCustomHideoutCustomization.ShortName;
            lang[$"{customid} description"] = customCustomHideoutCustomization.Description;
            return lang;
        });
    }
}










