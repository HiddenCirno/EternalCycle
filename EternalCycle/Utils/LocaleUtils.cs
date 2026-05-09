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
using System.Runtime.InteropServices;
using System.Text.Json;
using Path = System.IO.Path;

namespace VulcanCore;


public class LocaleUtils
{
    public static void InitQuestLocale(string folderpath, string creator, string modname, DatabaseService databaseService, ModHelper modHelper)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var quests = modHelper.GetJsonDataFromFile<Dictionary<string, CustomQuestLocaleData>>(folderpath, fileName);
                string lang = Path.GetFileNameWithoutExtension(file);
                if(!databaseService.GetLocales().Global.TryGetValue(lang, out var locales))
                {
                    continue;
                }
                locales.AddTransformer(language =>
                {
                    foreach (var questEntry in quests)
                    {
                        string questId = VulcanUtil.ConvertHashID(questEntry.Key);
                        var modstring = $"<color=#FFFFFF><b>\n由{creator}创建\n添加者: {modname}\n任务API：火神之心\n任务ID：{questId}</b></color>";         // 例如 "PersicariaTask1"
                        var locale = questEntry.Value;                 // CustomQuestLocaleData 对象

                        // 写入任务主要字段
                        language.TryAdd($"{questId} name", locale.QuestName);
                        language.TryAdd($"{questId} description", $"{locale.QuestDescription}{modstring}");
                        language.TryAdd($"{questId} note", locale.QuestNote ?? "");
                        language.TryAdd($"{questId} failMessageText", locale.QuestFailMessage ?? "");
                        language.TryAdd($"{questId} startedMessageText", locale.QuestStartMessaage ?? "");
                        language.TryAdd($"{questId} successMessageText", locale.QuestSuccessMessage ?? "");
                        language.TryAdd($"{questId} location", locale.QuestLocation ?? "");

                        // 写入每个条件文本（如 Hand/Find 条件）
                        if (locale.QuestConditions != null)
                        {
                            foreach (var cond in locale.QuestConditions)
                            {
                                // cond.Key = "PersicariaTask1Find1"
                                // cond.Value = "在战局中找到电线"
                                language.TryAdd(VulcanUtil.ConvertHashID(cond.Key), cond.Value);
                                //localeData[VulcanUtil.ConvertHashID(cond.Key)] = cond.Value;
                            }
                        }
                    }
                    return language;
                });

            }
        }
    }
    public static void InitQuestLocale(Dictionary<string, Dictionary<string, CustomQuestLocaleData>> customLocaleData, string creator, string modname, DatabaseService databaseService)
    {
        // 遍历语言，例如 ch / en / ru ...
        foreach (var languageEntry in customLocaleData)
        {
            string langKey = languageEntry.Key; // "ch"
            var quests = languageEntry.Value;   // Dictionary<string, CustomQuestLocaleData>

            // 获取目标语言对应的全局本地化 LazyLoad
            if (!databaseService.GetLocales().Global.TryGetValue(langKey, out LazyLoad<Dictionary<string, string>> lazyLocale))
                continue;

            // 为该语言添加 transformer（延迟加载时注入翻译数据）
            lazyLocale.AddTransformer(localeData =>
            {
                foreach (var questEntry in quests)
                {
                    string questId = VulcanUtil.ConvertHashID(questEntry.Key);
                    var modstring = $"<color=#FFFFFF><b>\n由{creator}创建\n添加者: {modname}\n任务API：火神之心\n任务ID：{questId}</b></color>";         // 例如 "PersicariaTask1"
                    var locale = questEntry.Value;                 // CustomQuestLocaleData 对象

                    // 写入任务主要字段
                    localeData.TryAdd($"{questId} name", locale.QuestName);
                    localeData.TryAdd($"{questId} description", $"{locale.QuestDescription}{modstring}");
                    localeData.TryAdd($"{questId} note", locale.QuestNote ?? "");
                    localeData.TryAdd($"{questId} failMessageText", locale.QuestFailMessage ?? "");
                    localeData.TryAdd($"{questId} startedMessageText", locale.QuestStartMessaage ?? "");
                    localeData.TryAdd($"{questId} successMessageText", locale.QuestSuccessMessage ?? "");
                    localeData.TryAdd($"{questId} location", locale.QuestLocation ?? "");

                    // 写入每个条件文本（如 Hand/Find 条件）
                    if (locale.QuestConditions != null)
                    {
                        foreach (var cond in locale.QuestConditions)
                        {
                            // cond.Key = "PersicariaTask1Find1"
                            // cond.Value = "在战局中找到电线"
                            localeData.TryAdd(VulcanUtil.ConvertHashID(cond.Key), cond.Value);
                            //localeData[VulcanUtil.ConvertHashID(cond.Key)] = cond.Value;
                        }
                    }
                }

                return localeData;
            });
        }
    }
    public static void AddItemToLocales(Dictionary<string, LocaleDetails> localeDetails, string newItemId, DatabaseService databaseService)
    {
        string newItemId2 = newItemId;
        foreach (KeyValuePair<string, string> language in databaseService.GetLocales().Languages)
        {
            localeDetails.TryGetValue(language.Key, out LocaleDetails newLocaleDetails);
            if ((object)newLocaleDetails == null)
            {
                newLocaleDetails = localeDetails[localeDetails.Keys.FirstOrDefault()];
            }

            if (databaseService.GetLocales().Global.TryGetValue(language.Key, out LazyLoad<Dictionary<string, string>> value))
            {
                value.AddTransformer(delegate (Dictionary<string, string>? localeData)
                {
                    localeData.TryAdd(newItemId2 + " Name", newLocaleDetails.Name);
                    localeData.TryAdd(newItemId2 + " ShortName", newLocaleDetails.ShortName);
                    localeData.TryAdd(newItemId2 + " Description", newLocaleDetails.Description.Replace("#ItemId", newItemId2));
                    return localeData;
                });
            }
        }
    }
    public static void AddTraderToLocales(TraderBaseWithDesc baseJson, DatabaseService databaseService, string creator, string modname)
    {
        var locales = databaseService.GetTables().Locales.Global;
        var newTraderId = baseJson.Id;
        var modstring = $"<color=#FFFFFF><b>\n由{creator}创建\n添加者: {modname}\n商人API：火神之心\n商人ID：{newTraderId}</b></color>";

        foreach (var (localeKey, localeKvP) in locales)
        {
            localeKvP.AddTransformer(lazyloadedLocaleData =>
            {
                lazyloadedLocaleData.TryAdd($"{newTraderId} FullName", baseJson?.Surname);
                lazyloadedLocaleData.TryAdd($"{newTraderId} FirstName", baseJson.Name);
                lazyloadedLocaleData.TryAdd($"{newTraderId} Nickname", baseJson?.Nickname);
                lazyloadedLocaleData.TryAdd($"{newTraderId} Location", baseJson?.Location);
                lazyloadedLocaleData.TryAdd($"{newTraderId} Description", $"{baseJson.Description}{modstring}");
                return lazyloadedLocaleData;
            });
        }
    }
    public static void InitLocaleText(Dictionary<string, Dictionary<string, string>> locales, DatabaseService databaseService)
    {
        foreach (var languageEntry in locales)
        {
            string langKey = languageEntry.Key; // "ch"
            var langValue = languageEntry.Value;   // Dictionary<string, CustomQuestLocaleData>

            // 获取目标语言对应的全局本地化 LazyLoad
            if (!databaseService.GetLocales().Global.TryGetValue(langKey, out LazyLoad<Dictionary<string, string>> lazyLocale))
                continue;

            // 为该语言添加 transformer（延迟加载时注入翻译数据）
            lazyLocale.AddTransformer(localeData =>
            {
                foreach (var key in langValue)
                {
                    //localeData.TryAdd(key.Key, key.Value);
                    localeData[key.Key] = key.Value;
                }
                return localeData;
            });
        }
    }
    public static void InitLocaleText(string folderpath, DatabaseService databaseService, ModHelper modHelper)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var text = modHelper.GetJsonDataFromFile<Dictionary<string, string>>(folderpath, fileName);
                string lang = Path.GetFileNameWithoutExtension(file);
                if (!databaseService.GetLocales().Global.TryGetValue(lang, out var locales))
                {
                    continue;
                }
                locales.AddTransformer(language =>
                {
                    foreach(var kvp in text)
                    {
                        language[kvp.Key] = kvp.Value;
                    }
                    return language;
                });
            }
        }
    }
    public static string GetItemName(MongoId itemid, LocaleService localeService)
    {
        var lang = localeService.GetLocaleDb("ch");
        var name = lang.TryGetValue($"{itemid} Name", out var result);
        if (result != null && result != "")
        {
            return result;
        }
        return "";
    }
    public static string GetItemShortName(MongoId itemid, LocaleService localeService)
    {
        var lang = localeService.GetLocaleDb("ch");
        var name = lang.TryGetValue($"{itemid} ShortName", out var result);
        if (result != null && result != "")
        {
            return result;
        }
        return "";
    }
    public static string GetQuestName(MongoId questid, LocaleService localeService)
    {
        var lang = localeService.GetLocaleDb("ch");
        var name = lang.TryGetValue($"{questid} name", out var result);
        if (result != null && result != "")
        {
            return result;
        }
        return "";
    }
    public static void InitGiftBoxLocale(DatabaseService databaseService, LocaleService localeService)
    {
        foreach (var pool in ItemUtils.DrawPoolData.Values)
        {

            var zhCNLang = databaseService.GetLocales().Global["ch"];
            var basedata = pool.BaseReward;
            var itempool = pool.ItemPool;
            var sr = basedata.SuperRare;
            var srpool = itempool.SuperRare;
            var r = basedata.Rare;
            var rpool = itempool.Rare;
            var normal = basedata.Normal;
            var normalpool = itempool.Normal;
            var poolname = pool.Name;
            var gold = "<color=#FFFF55>★★★★★</color>内容";
            var epic = "<color=#FF55FF>★★★★</color>内容";
            var normalstr = "<color=#FFFFFF>★★★</color>内容";
            var srchance = VulcanUtil.DoubleToPercent(sr.Chance);
            var srupchance = VulcanUtil.DoubleToPercent(sr.UpChance);
            var srnormalchance = VulcanUtil.DoubleToPercent(1 - sr.UpChance);
            var srbasecount = (int)(sr.ChanceGrowCount + 1 + ((1 - sr.Chance) / sr.ChanceGrowPerCount));
            var sraddchance = VulcanUtil.DoubleToPercent(sr.UpAddChance);
            var srrealchance = VulcanUtil.DoubleToPercent(1 / (double)srbasecount);
            var srgrowcount = sr.ChanceGrowCount;
            var srgrowchance = VulcanUtil.DoubleToPercent(sr.ChanceGrowPerCount);
            var rchance = VulcanUtil.DoubleToPercent(r.Chance);
            var rbasecount = (int)(1 / r.Chance);
            var rupchance = VulcanUtil.DoubleToPercent(r.UpChance);
            var rnormalchance = VulcanUtil.DoubleToPercent(1 - r.UpChance);
            var raddchance = VulcanUtil.DoubleToPercent(r.UpAddChance);
            var srupstring = "";
            var srnormalstring = "";
            var rupstring = "";
            var rnormalstring = "";
            var normalstring = "";
            foreach (var gift in srpool.ChanceUp)
            {
                switch (gift)
                {
                    case GiftDataItemData itemData:
                        {
                            srupstring += $"{LocaleUtils.GetItemName(itemData.ItemId, localeService)}x{itemData.Count}, ";
                        }
                        break;
                    case GiftDataVanillaPreset vanillaPreset:
                        {
                            srupstring += $"{LocaleUtils.GetItemName(vanillaPreset.Item, localeService)}x1, ";
                        }
                        break;
                    case GiftDataCustomPreset customPreset:
                        {
                            srupstring += $"{LocaleUtils.GetItemName(customPreset.Item.First().Template, localeService)}x1, ";
                        }
                        break;
                }
            }
            foreach (var gift in srpool.Normal)
            {
                switch (gift)
                {
                    case GiftDataItemData itemData:
                        {
                            srnormalstring += $"{LocaleUtils.GetItemName(itemData.ItemId, localeService)}x{itemData.Count}, ";
                        }
                        break;
                    case GiftDataVanillaPreset vanillaPreset:
                        {
                            srnormalstring += $"{LocaleUtils.GetItemName(vanillaPreset.Item, localeService)}x1, ";
                        }
                        break;
                    case GiftDataCustomPreset customPreset:
                        {
                            srnormalstring += $"{LocaleUtils.GetItemName(customPreset.Item.First().Template, localeService)}x1, ";
                        }
                        break;
                }
            }
            foreach (var gift in rpool.ChanceUp)
            {
                switch (gift)
                {
                    case GiftDataItemData itemData:
                        {
                            rupstring += $"{LocaleUtils.GetItemName(itemData.ItemId, localeService)}x{itemData.Count}, ";
                        }
                        break;
                    case GiftDataVanillaPreset vanillaPreset:
                        {
                            rupstring += $"{LocaleUtils.GetItemName(vanillaPreset.Item, localeService)}x1, ";
                        }
                        break;
                    case GiftDataCustomPreset customPreset:
                        {
                            rupstring += $"{LocaleUtils.GetItemName(customPreset.Item.First().Template, localeService)}x1, ";
                        }
                        break;
                }
            }
            foreach (var gift in rpool.Normal)
            {
                switch (gift)
                {
                    case GiftDataItemData itemData:
                        {
                            rnormalstring += $"{LocaleUtils.GetItemName(itemData.ItemId, localeService)}x{itemData.Count}, ";
                        }
                        break;
                    case GiftDataVanillaPreset vanillaPreset:
                        {
                            rnormalstring += $"{LocaleUtils.GetItemName(vanillaPreset.Item, localeService)}x1, ";
                        }
                        break;
                    case GiftDataCustomPreset customPreset:
                        {
                            rnormalstring += $"{LocaleUtils.GetItemName(customPreset.Item.First().Template, localeService)}x1, ";
                        }
                        break;
                }
            }
            foreach (var gift in normalpool.Normal)
            {
                switch (gift)
                {
                    case GiftDataItemData itemData:
                        {
                            normalstring += $"{LocaleUtils.GetItemName(itemData.ItemId, localeService)}x{itemData.Count}, ";
                        }
                        break;
                    case GiftDataVanillaPreset vanillaPreset:
                        {
                            normalstring += $"{LocaleUtils.GetItemName(vanillaPreset.Item, localeService)}x1, ";
                        }
                        break;
                    case GiftDataCustomPreset customPreset:
                        {
                            normalstring += $"{LocaleUtils.GetItemName(customPreset.Item.First().Template, localeService)}x1, ";
                        }
                        break;
                }
            }
            string result = $@"
抽奖概率公示: 
{gold}: 
抽奖概率: 
本奖池中，每次抽奖获得{gold}的基础概率为{srchance}, 含保底综合概率为{srrealchance}, 最多{srbasecount}次抽奖必定能通过保底获得{gold}
概率提升: 
获得{gold}时, 有{srupchance}概率为当前up内容, 另有{srnormalchance}概率为本奖池可获得的全部{gold}, 若本次抽奖获得的{gold}非当前up内容. 则下次抽奖获得当前up内容的概率提升{sraddchance}
若连续{srgrowcount}次抽奖仍未获得{gold}, 则从下次开始, 每次抽奖获得{gold}的概率提升{srgrowchance}
{epic}: 
抽奖概率: 
本奖池中，获得{epic}的基础概率为{rchance}, 含保底综合概率为{rchance}, 最多{rbasecount}次抽奖必定能通过保底获得{epic}
概率提升: 
获得{epic}时, 有{rupchance}概率为当前up内容, 另有{rnormalchance}概率为本奖池可获得的全部{epic}, 若本次抽奖获得的{epic}非当前up内容. 则下次抽奖获得当前up内容的概率提升{raddchance}
奖池公示: 
{gold}: 
当前up内容: {srupstring}
可获得内容: {srnormalstring}
{epic}: 
当前up内容: {rupstring}
可获得内容: {rnormalstring}
{normalstr}: 
可获得内容: {normalstring}";
            var itemlist = new List<string>();
            foreach (var kvp in ItemUtils.AdvancedBoxData)
            {
                if (kvp.Value.PoolName == poolname)
                {
                    itemlist.Add($"{kvp.Key} Description");
                }
            }
            zhCNLang.AddTransformer(lang =>
            {
                foreach (var kvp in lang)
                {
                    if (kvp.Value!=null && kvp.Value.Contains("<color=#FFFFFF><b>\n由") && itemlist.Contains(kvp.Key))
                    {
                        //lang[kvp.Key] = $"{lang[kvp.Key]}\n{result}";
                        lang[kvp.Key] = kvp.Value.Replace("<color=#FFFFFF><b>\n由", $"{result}\n<color=#FFFFFF><b>\n由");
                    }
                }
                return lang;
            });
        }
    }
}










