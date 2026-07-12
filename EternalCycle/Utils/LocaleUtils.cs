using Microsoft.AspNetCore.DataProtection.KeyManagement;
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
using static System.Net.Mime.MediaTypeNames;
using Path = System.IO.Path;

namespace EternalCycleServer
{
    /// <summary>
    /// 对本地化生成进行操作处理的工具类
    /// </summary>
    public class LocaleUtils
    {

        /// <summary>
        /// 将自定义任务本地化文本注册到加载事件
        /// </summary>
        /// <param name="path">存放本地化文件的文件夹路径，或单个多语言文件的路径</param>
        /// <param name="creator">创建者</param>
        /// <param name="modname">Mod名</param>
        public static void RegisterQuestLocale(string modpath, string path, string creator, string modname)
        {
            var correctpath = Path.Combine(modpath, path);
            // 文件夹加载模式 (文件夹里是 ch.json, en.json...)
            if (Directory.Exists(correctpath))
            {
                // 注意：挂载的事件请根据你的实际情况调整（可能是 LoadLocaleEvent 或与任务同级）
                EventManager.DataLoadEvent.LoadQuestLocaleEvent += (context) =>
                {
                    try
                    {
                        InitQuestLocale(correctpath, creator, modname, context.DB, context.ModHelper);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册任务本地化时发生错误：指定的文件夹 {correctpath} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式 (单个文件里包含了所有语言的数据)
            else if (File.Exists(correctpath))
            {
                EventManager.DataLoadEvent.LoadQuestLocaleEvent += (context) =>
                {
                    try
                    {
                        // 解析为: Dictionary<语言Key, Dictionary<任务ID, 本地化数据>>
                        var customLocaleData = context.JsonUtil.Deserialize<Dictionary<string, Dictionary<string, CustomQuestLocaleData>>>(File.ReadAllText(correctpath));
                        InitQuestLocale(customLocaleData, creator, modname, context.DB);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册任务本地化时发生错误：指定的文件 {correctpath} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册任务本地化时发生异常：找不到指定的文件或文件夹 {correctpath}");
            }
        }

        /// <summary>
        /// Init重载 1：处理文件夹路径，提取文件名作为语言Key
        /// </summary>
        public static void InitQuestLocale(string folderpath, string creator, string modname, DatabaseService databaseService, ModHelper modHelper)
        {
            if (!Directory.Exists(folderpath)) return;

            List<string> files = Directory.GetFiles(folderpath).ToList();
            foreach (var file in files)
            {
                // 文件名就是语言Key (比如 ch.json -> ch)
                string langKey = Path.GetFileNameWithoutExtension(file);
                var quests = modHelper.GetJsonDataFromFile<Dictionary<string, CustomQuestLocaleData>>(folderpath, Path.GetFileName(file));

                if (quests != null)
                {
                    // 调用底层核心方法
                    InitQuestLocale(langKey, quests, creator, modname, databaseService);
                }
            }
        }

        /// <summary>
        /// Init重载 2：处理反序列化好的多语言字典
        /// </summary>
        public static void InitQuestLocale(Dictionary<string, Dictionary<string, CustomQuestLocaleData>> customLocaleData, string creator, string modname, DatabaseService databaseService)
        {
            if (customLocaleData == null || customLocaleData.Count == 0) return;

            foreach (var languageEntry in customLocaleData)
            {
                string langKey = languageEntry.Key; // "ch", "en", etc.
                var quests = languageEntry.Value;

                if (quests != null)
                {
                    // 调用底层核心方法
                    InitQuestLocale(langKey, quests, creator, modname, databaseService);
                }
            }
        }

        /// <summary>
        /// 底层核心方法：负责给特定的语言注入任务文本（解决代码重复的根本）
        /// </summary>
        public static void InitQuestLocale(string langKey, Dictionary<string, CustomQuestLocaleData> quests, string creator, string modname, DatabaseService databaseService)
        {
            // 获取目标语言对应的全局本地化 LazyLoad
            if (!databaseService.GetLocales().Global.TryGetValue(langKey, out var lazyLocale))
            {
                return; // 找不到对应语言（比如玩家端没有这种语言）直接跳过
            }

            // 添加 Transformer 进行延迟加载
            lazyLocale.AddTransformer(localeData =>
            {
                foreach (var questEntry in quests)
                {
                    string questId = Utils.ConvertHashID(questEntry.Key);
                    var locale = questEntry.Value;
                    var modstring = $"<color=#FFFFFF><b>\n由{creator}创建\n添加者: {modname}\n任务API：永恒时序\n任务ID：{questId}</b></color>";

                    // 写入任务主要字段
                    localeData.TryAdd($"{questId} name", locale.QuestName);
                    localeData.TryAdd($"{questId} description", $"{locale.QuestDescription}{modstring}");
                    localeData.TryAdd($"{questId} note", locale.QuestNote ?? "");
                    localeData.TryAdd($"{questId} failMessageText", locale.QuestFailMessage ?? "");
                    localeData.TryAdd($"{questId} startedMessageText", locale.QuestStartMessaage ?? "");
                    localeData.TryAdd($"{questId} successMessageText", locale.QuestSuccessMessage ?? "");
                    localeData.TryAdd($"{questId} location", locale.QuestLocation ?? "");

                    // 写入条件文本
                    if (locale.QuestConditions != null)
                    {
                        foreach (var cond in locale.QuestConditions)
                        {
                            localeData.TryAdd(Utils.ConvertHashID(cond.Key), cond.Value);
                        }
                    }
                }
                return localeData;
            });
        }

        /// <summary>
        /// 为物品构建本地化数据
        /// </summary>
        /// <param name="props">自定义属性对象</param>
        /// <param name="creator">创建者</param>
        /// <param name="modname">Mod名</param>
        /// <returns></returns>
        public static Dictionary<string, LocaleDetails> BuildItemLocales(CustomProps props, string creator, string modname)
        {
            //这玩意居然没啥好改的
            var locales = new Dictionary<string, LocaleDetails>();
            var modstring = $"<color=#FFFFFF><b>\n由{creator}创建\n添加者: {modname}\n物品API：永恒时序\n物品ID：{{0}}</b></color>";
            //Creted By: xxx, Added By: xxx, ModAPI: EternalCycle, Item Id: xxx
            var chdescription = $"{props.Description}{modstring}";
            //zhcn
            locales["ch"] = new LocaleDetails
            {
                Name = props.Name,
                ShortName = props.ShortName,
                Description = chdescription
            };
            //Eng
            locales["en"] = new LocaleDetails
            {
                Name = string.IsNullOrEmpty(props.EName) ? props.Name : props.EName,
                ShortName = string.IsNullOrEmpty(props.EShortName) ? props.ShortName : props.EShortName,
                Description = string.IsNullOrEmpty(props.EDescription) ? chdescription : props.EDescription
            };
            //jp
            locales["jp"] = new LocaleDetails
            {
                Name = string.IsNullOrEmpty(props.JName) ? props.Name : props.JName,
                ShortName = string.IsNullOrEmpty(props.JShortName) ? props.ShortName : props.JShortName,
                Description = string.IsNullOrEmpty(props.JDescription) ? chdescription : props.JDescription
            };
            return locales;
        }

        /// <summary>
        /// 完成自定义物品的本地化加载
        /// </summary>
        /// <param name="localeDetails">本地化数据</param>
        /// <param name="newItemId">物品ID</param>
        /// <param name="databaseService">数据库实例</param>
        public static void AddItemToLocales(Dictionary<string, LocaleDetails> localeDetails, string newItemId, DatabaseService databaseService)
        {
            if (localeDetails == null || localeDetails.Count == 0) return;
            //遍历SPT的语言索引
            foreach (var language in databaseService.GetLocales().Languages)
            {
                //尝试从自定义本地化数据获取索引
                localeDetails.TryGetValue(language.Key, out var lang);
                if (lang == null)
                {
                    //默认回调
                    lang = localeDetails["ch"];
                }
                //找到对应的语言文件
                if (databaseService.GetLocales().Global.TryGetValue(language.Key, out var localeValue))
                {
                    //添加修改器
                    localeValue.AddTransformer(localeData =>
                    {
                        localeData[$"{newItemId} Name"] = lang.Name;
                        localeData[$"{newItemId} ShortName"] = lang.ShortName;
                        localeData[$"{newItemId} Description"] = string.Format(lang.Description, newItemId);
                        return localeData;
                    });
                }
            }
        }

        public static void AddTraderToLocales(TraderBaseWithDesc baseJson, DatabaseService databaseService, string creator, string modname)
        {
            var locales = databaseService.GetTables().Locales.Global;
            var newTraderId = baseJson.Id;
            var modstring = $"<color=#FFFFFF><b>\n由{creator}创建\n添加者: {modname}\n商人API：永恒时序\n商人ID：{newTraderId}</b></color>";

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

        /// <summary>
        /// 将自定义全局本地化文本注册到加载事件
        /// </summary>
        /// <param name="modpath">Mod根目录路径</param>
        /// <param name="path">存放本地化文件的文件夹路径，或单个多语言文件的路径</param>
        public static void RegisterLocaleText(string modpath, string path)
        {
            var correctpath = System.IO.Path.Combine(modpath, path);

            // 文件夹加载模式 (例如文件夹里是 ch.json, en.json...)
            if (Directory.Exists(correctpath))
            {
                // 注意：挂载的事件请根据你的实际情况调整（例如 LoadLocaleEvent 或统合在 LoadTextEvent 中）
                EventManager.DataLoadEvent.LoadLocaleEvent += (context) =>
                {
                    try
                    {
                        // 直接调用你已经写好的重载 2：处理文件夹路径
                        InitLocaleText(correctpath, context.DB, context.ModHelper);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册本地化文本时发生错误：指定的文件夹 {correctpath} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式 (例如单个 jsonc 文件里包含了所有语言的数据)
            else if (File.Exists(correctpath))
            {
                EventManager.DataLoadEvent.LoadLocaleEvent += (context) =>
                {
                    try
                    {
                        // 解析为: Dictionary<语言Key, Dictionary<文本Key, 文本Value>>
                        var customLocaleData = context.JsonUtil.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(correctpath));

                        if (customLocaleData != null)
                        {
                            // 直接调用你已经写好的重载 1：处理反序列化好的多语言字典
                            InitLocaleText(customLocaleData, context.DB);
                        }
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册本地化文本时发生错误：指定的文件 {correctpath} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册本地化文本时发生异常：找不到指定的文件或文件夹 {correctpath}");
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

                InitLocale(lazyLocale, langValue);
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
                    InitLocale(locales, text);
                }
            }
        }

        public static void InitLocale(LazyLoad<Dictionary<string, string>> lang, Dictionary<string, string> text)
        {
            lang.AddTransformer(language =>
            {
                foreach (var kvp in text)
                {
                    language[kvp.Key] = kvp.Value;
                    if (kvp.Key.StartsWith("MOD_"))
                    {
                        //Utils.commonLogger.Success("slot locale register");
                        for (var i = 0; i < 30; i++)
                        {
                            language[$"{kvp.Key}_{i:D3}"] = kvp.Value;
                        }
                    }
                }
                return language;
            });
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
                var srchance = Utils.DoubleToPercent(sr.Chance);
                var srupchance = Utils.DoubleToPercent(sr.UpChance);
                var srnormalchance = Utils.DoubleToPercent(1 - sr.UpChance);
                var srbasecount = (int)(sr.ChanceGrowCount + 1 + ((1 - sr.Chance) / sr.ChanceGrowPerCount));
                var sraddchance = Utils.DoubleToPercent(sr.UpAddChance);
                var srrealchance = Utils.DoubleToPercent(1 / (double)srbasecount);
                var srgrowcount = sr.ChanceGrowCount;
                var srgrowchance = Utils.DoubleToPercent(sr.ChanceGrowPerCount);
                var rchance = Utils.DoubleToPercent(r.Chance);
                var rbasecount = (int)(1 / r.Chance);
                var rupchance = Utils.DoubleToPercent(r.UpChance);
                var rnormalchance = Utils.DoubleToPercent(1 - r.UpChance);
                var raddchance = Utils.DoubleToPercent(r.UpAddChance);
                var srupstring = "";
                var srnormalstring = "";
                var rupstring = "";
                var rnormalstring = "";
                var normalstring = "";
                foreach (var gift in srpool.ChanceUp)
                {
                    switch (gift)
                    {
                        case GiftItemData itemData:
                            {
                                srupstring += $"{LocaleUtils.GetItemName(itemData.ItemId, localeService)}x{itemData.Count}, ";
                            }
                            break;
                        case GiftVanillaPresetData vanillaPreset:
                            {
                                srupstring += $"{LocaleUtils.GetItemName(vanillaPreset.Item, localeService)}x1, ";
                            }
                            break;
                        case GiftCustomPresetData customPreset:
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
                        case GiftItemData itemData:
                            {
                                srnormalstring += $"{LocaleUtils.GetItemName(itemData.ItemId, localeService)}x{itemData.Count}, ";
                            }
                            break;
                        case GiftVanillaPresetData vanillaPreset:
                            {
                                srnormalstring += $"{LocaleUtils.GetItemName(vanillaPreset.Item, localeService)}x1, ";
                            }
                            break;
                        case GiftCustomPresetData customPreset:
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
                        case GiftItemData itemData:
                            {
                                rupstring += $"{LocaleUtils.GetItemName(itemData.ItemId, localeService)}x{itemData.Count}, ";
                            }
                            break;
                        case GiftVanillaPresetData vanillaPreset:
                            {
                                rupstring += $"{LocaleUtils.GetItemName(vanillaPreset.Item, localeService)}x1, ";
                            }
                            break;
                        case GiftCustomPresetData customPreset:
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
                        case GiftItemData itemData:
                            {
                                rnormalstring += $"{LocaleUtils.GetItemName(itemData.ItemId, localeService)}x{itemData.Count}, ";
                            }
                            break;
                        case GiftVanillaPresetData vanillaPreset:
                            {
                                rnormalstring += $"{LocaleUtils.GetItemName(vanillaPreset.Item, localeService)}x1, ";
                            }
                            break;
                        case GiftCustomPresetData customPreset:
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
                        case GiftItemData itemData:
                            {
                                normalstring += $"{LocaleUtils.GetItemName(itemData.ItemId, localeService)}x{itemData.Count}, ";
                            }
                            break;
                        case GiftVanillaPresetData vanillaPreset:
                            {
                                normalstring += $"{LocaleUtils.GetItemName(vanillaPreset.Item, localeService)}x1, ";
                            }
                            break;
                        case GiftCustomPresetData customPreset:
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
                        if (kvp.Value != null && kvp.Value.Contains("<color=#FFFFFF><b>\n由") && itemlist.Contains(kvp.Key))
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
}