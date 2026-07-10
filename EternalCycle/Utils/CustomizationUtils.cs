using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using static EternalCycleServer.ContextManager;
using Path = System.IO.Path;

namespace EternalCycleServer
{
    public class CustomizationUtils
    {
        /// <summary>
        /// 将人物自定义外观(Customization)注册到加载事件
        /// </summary>
        public static void RegisterCustomization(string modpath, string path, string respath)
        {
            var correctPath = Path.Combine(modpath, path);
            if (Directory.Exists(correctPath))
            {
                // 注意：事件名根据你的实际框架调整 (例如 LoadCustomizationEvent)
                EventManager.DataLoadEvent.LoadCustomizationEvent += (context) =>
                {
                    try
                    {
                        InitCustomizationData(modpath, path, respath, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册自定义外观时发生错误：指定的文件夹 {correctPath} 存在问题", ex);
                    }
                };
            }
            else if (File.Exists(correctPath))
            {
                EventManager.DataLoadEvent.LoadCustomizationEvent += (context) =>
                {
                    try
                    {
                        var customization = context.JsonUtil.Deserialize<Dictionary<string, CustomCustomizationItem>>(File.ReadAllText(correctPath));
                        InitCustomizationData(customization, modpath, respath, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册自定义外观时发生错误：指定的文件 {correctPath} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册自定义外观时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        public static void InitCustomizationData(string modpath, string folderpath, string respath, LoadModContext context)
        {
            var correctpath = Path.Combine(modpath, folderpath);
            if (!Directory.Exists(correctpath)) return;

            List<string> files = Directory.GetFiles(correctpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var customization = context.ModHelper.GetJsonDataFromFile<Dictionary<string, CustomCustomizationItem>>(correctpath, fileName);

                    if (customization != null)
                    {
                        InitCustomizationData(customization, modpath, respath, context);
                    }
                }
            }
        }

        public static void InitCustomizationData(Dictionary<string, CustomCustomizationItem> customData, string modpath, string respath, LoadModContext context)
        {
            if (customData == null || customData.Count == 0) return;

            foreach (var item in customData)
            {
                InitCustomization(item.Value, modpath, respath, context);
            }
        }

        public static void InitCustomization(CustomCustomizationItem customCustomizationItem, string modpath, string respath, LoadModContext context)
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
                if (customCustomizationItem.Properties.VoicePath != null)
                {
                    ResourceUtils.VoicePath.TryAdd(customCustomizationItem.Properties.Prefab.ToString(), customCustomizationItem.Properties.VoicePath);
                }
            }

            if (customCustomizationItem.Properties.IsDeco==true)
            {
                ResourceUtils.RegisterDecoIconResource(modpath, Path.Combine(respath, $"{customCustomizationItem.Name}.png"));
            }

            if(customCustomizationItem.Properties.IsTarget == true)
            {
                ResourceUtils.RegisterTargetResource(modpath, Path.Combine(respath, $"{customCustomizationItem.Properties.AssetPath.Rcid}.png"));
                ResourceUtils.RegisterDecoIconResource(modpath, Path.Combine(respath, $"{customCustomizationItem.Name}.png"));
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