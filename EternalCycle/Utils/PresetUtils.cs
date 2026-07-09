using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using static EternalCycleServer.ContextManager;
using Path = System.IO.Path;

namespace EternalCycleServer
{
    public class PresetUtils
    {
        /// <summary>
        /// 将自定义预设(Preset)注册到加载事件
        /// </summary>
        /// <param name="path">指定的存放预设文件的文件夹路径或单个预设文件(列表)路径</param>
        /// <param name="creator">创建者</param>
        /// <param name="modname">Mod名</param>
        public static void RegisterPreset(string path)
        {
            // 文件夹加载模式
            if (Directory.Exists(path))
            {
                // 事件名请根据你实际情况调整，例如 LoadPresetEvent
                EventManager.DataLoadEvent.LoadPresetEvent += (context) =>
                {
                    try
                    {
                        InitPresetData(path, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册预设时发生错误：指定的文件夹 {path} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(path))
            {
                EventManager.DataLoadEvent.LoadPresetEvent += (context) =>
                {
                    try
                    {
                        // 反序列化为 List 集合，对接已有的重载方法
                        var presetData = context.JsonUtil.Deserialize<List<CustomPresetData>>(File.ReadAllText(path));

                        if (presetData != null)
                        {
                            InitPresetData(presetData, context);
                        }
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册预设时发生错误：指定的文件 {path} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册预设时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        /// <summary>
        /// Init重载 1：处理反序列化好的预设列表 (对应单文件加载)
        /// </summary>
        public static void InitPresetData(List<CustomPresetData> presetData, LoadModContext context)
        {
            if (presetData == null || presetData.Count == 0) return;

            foreach (var preset in presetData)
            {
                if (preset != null)
                {
                    InitPreset(preset, context);
                }
            }
        }

        /// <summary>
        /// Init重载 2：处理文件夹路径，读取单体对象并初始化
        /// </summary>
        public static void InitPresetData(string folderpath, LoadModContext context)
        {
            if (!Directory.Exists(folderpath)) return;

            List<string> files = Directory.GetFiles(folderpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var preset = context.ModHelper.GetJsonDataFromFile<CustomPresetData>(folderpath, fileName);

                    if (preset != null)
                    {
                        InitPreset(preset, context);
                    }
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
}