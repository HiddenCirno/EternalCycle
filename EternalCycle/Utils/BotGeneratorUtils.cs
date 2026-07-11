using SPTarkov.Server.Core.Models.Spt.Bots;
using static EternalCycleServer.ContextManager;
using Path = System.IO.Path;

namespace EternalCycleServer
{
    public class BotGeneratorUtils
    {
        public static Dictionary<string, List<CustomAlterBot>> AlterBotDictionarys = new();

        public static void RegisterAlterBotData(string modpath, string path)
        {
            var correctpath = Path.Combine(modpath, path);

            // 文件夹加载模式
            if (Directory.Exists(correctpath))
            {
                // 注意：事件名请根据实际情况替换（如 LoadAchievementEvent 或统合在 LoadQuestEvent 中）
                EventManager.DataLoadEvent.LoadAlterBotEvent += (context) =>
                {
                    try
                    {
                        InitAlterBotData(modpath, path, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册自定义Bot时发生错误：指定的文件夹 {correctpath} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(correctpath))
            {
                EventManager.DataLoadEvent.LoadAlterBotEvent += (context) =>
                {
                    try
                    {
                        // 反序列化为 List 集合
                        var alterBotData = context.JsonUtil.Deserialize<CustomAlterBot>(File.ReadAllText(correctpath));

                        if (alterBotData != null)
                        {
                            InitAlterBot(alterBotData, context);
                        }
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册定义Bot时发生错误：指定的文件 {correctpath} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册定义Bot时发生异常：找不到指定的文件或文件夹 {correctpath}");
            }
            // 假设事件回调中的 context 已经是 ContextManager.LoadModContext 类型

        }

        public static void InitAlterBotData(string modpath, string folderpath, LoadModContext context)
        {
            var correctpath = Path.Combine(modpath, folderpath);
            if (!Directory.Exists(correctpath)) return;

            List<string> files = Directory.GetFiles(correctpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var bot = context.ModHelper.GetJsonDataFromFile<CustomAlterBot>(correctpath, fileName);
                    InitAlterBot(bot, context);
                }
            }
        }

        public static void InitAlterBot(CustomAlterBot customAlterBot, LoadModContext context)
        {
            AlterBotDictionarys.TryGetValue(customAlterBot.BotRole, out var bots);
            if (bots == null)
            {
                bots = new List<CustomAlterBot>();
            }
            bots.Add(customAlterBot);
            AlterBotDictionarys[customAlterBot.BotRole] = bots;
            //context.Logger.Success($"成功注册{customAlterBot.BotRole}");


        }
    }
}