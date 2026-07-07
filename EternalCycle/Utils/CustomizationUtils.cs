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
using System.Collections.Generic;
using System.Linq;
using static EternalCycle.ContextManager;
using Path = System.IO.Path;
using System;

namespace EternalCycle
{
    public class CustomizationUtils
    {
        // ==========================================
        // 1. 人物自定义外观/语音 (Customization) 注册与处理
        // ==========================================

        /// <summary>
        /// 将人物自定义外观(Customization)注册到加载事件
        /// </summary>
        public static void RegisterCustomization(string path)
        {
            if (Directory.Exists(path))
            {
                // 注意：事件名根据你的实际框架调整 (例如 LoadCustomizationEvent)
                EventManager.DataLoadEvent.LoadCustomizationEvent += (context) =>
                {
                    try
                    {
                        InitCustomizationData(path, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册自定义外观时发生错误：指定的文件夹 {path} 存在问题", ex);
                    }
                };
            }
            else if (File.Exists(path))
            {
                EventManager.DataLoadEvent.LoadCustomizationEvent += (context) =>
                {
                    try
                    {
                        var customization = context.JsonUtil.Deserialize<Dictionary<string, CustomCustomizationItem>>(File.ReadAllText(path));
                        InitCustomizationData(customization, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册自定义外观时发生错误：指定的文件 {path} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册自定义外观时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        public static void InitCustomizationData(string folderpath, LoadModContext context)
        {
            if (!Directory.Exists(folderpath)) return;

            List<string> files = Directory.GetFiles(folderpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var customization = context.ModHelper.GetJsonDataFromFile<Dictionary<string, CustomCustomizationItem>>(folderpath, fileName);

                    if (customization != null)
                    {
                        InitCustomizationData(customization, context);
                    }
                }
            }
        }

        public static void InitCustomizationData(Dictionary<string, CustomCustomizationItem> customData, LoadModContext context)
        {
            if (customData == null || customData.Count == 0) return;

            foreach (var item in customData)
            {
                InitCustomization(item.Value, context);
            }
        }

        public static void InitCustomization(CustomCustomizationItem customCustomizationItem, LoadModContext context)
        {
            var zhCNLang = context.DB.GetLocales().Global["ch"];
            var customs = context.DB.GetCustomization();
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
                var storage = context.DB.GetTables().Templates.CustomisationStorage;
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


        // ==========================================
        // 2. 藏身处自定义 (Hideout Customization) 注册与处理
        // ==========================================

        /// <summary>
        /// 将藏身处自定义注册到加载事件
        /// </summary>
        public static void RegisterHideoutCustomization(string path)
        {
            if (Directory.Exists(path))
            {
                EventManager.DataLoadEvent.LoadHideoutCustomizationEvent += (context) =>
                {
                    try
                    {
                        InitHideoutCustomizationData(path, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册藏身处自定义时发生错误：指定的文件夹 {path} 存在问题", ex);
                    }
                };
            }
            else if (File.Exists(path))
            {
                EventManager.DataLoadEvent.LoadHideoutCustomizationEvent += (context) =>
                {
                    try
                    {
                        var customData = context.JsonUtil.Deserialize<Dictionary<string, CustomHideoutCustomization>>(File.ReadAllText(path));
                        InitHideoutCustomizationData(customData, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册藏身处自定义时发生错误：指定的文件 {path} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册藏身处自定义时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        // 帮你补齐的文件夹加载重载
        public static void InitHideoutCustomizationData(string folderpath, LoadModContext context)
        {
            if (!Directory.Exists(folderpath)) return;

            List<string> files = Directory.GetFiles(folderpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var customization = context.ModHelper.GetJsonDataFromFile<Dictionary<string, CustomHideoutCustomization>>(folderpath, fileName);

                    if (customization != null)
                    {
                        InitHideoutCustomizationData(customization, context);
                    }
                }
            }
        }

        public static void InitHideoutCustomizationData(Dictionary<string, CustomHideoutCustomization> customData, LoadModContext context)
        {
            if (customData == null || customData.Count == 0) return;

            foreach (var item in customData)
            {
                InitHideoutCustomization(item.Value, context);
            }
        }

        public static void InitHideoutCustomization(CustomHideoutCustomization customCustomHideoutCustomization, LoadModContext context)
        {
            var zhCNLang = context.DB.GetLocales().Global["ch"];
            var customs = context.DB.GetHideout().Customisation.Globals;
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

            QuestUtils.InitQuestConditions(conditions.Conditions, customCustomHideoutCustomization.Conditions, context);
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
}