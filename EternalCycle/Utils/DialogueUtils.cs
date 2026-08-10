using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using static EternalCycleServer.ContextManager;
using Path = System.IO.Path;

namespace EternalCycleServer
{
    public static class DialogueUtils
    {
        /// <summary>
        /// 注册对话树
        /// 支持文件夹/单文件两种模式
        /// 元素将追加到全局对话树表 GetTemplates().Dialogue.Elements
        /// (即 /client/dialogue 返回的表)
        /// </summary>
        public static void RegisterDialogue(string modpath, string path)
        {
            var correctpath = Path.Combine(modpath, path);

            // 文件夹加载模式
            if (Directory.Exists(correctpath))
            {
                EventManager.DataLoadEvent.LoadDialogueEvent += (context) =>
                {
                    try
                    {
                        InitDialogueData(modpath, path, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册对话树时发生异常，指定的文件夹 {correctpath} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(correctpath))
            {
                EventManager.DataLoadEvent.LoadDialogueEvent += (context) =>
                {
                    try
                    {
                        var elements = context.JsonUtil.Deserialize<List<TraderDialogElement>>(File.ReadAllText(correctpath));
                        if (elements != null)
                        {
                            InitDialogueData(elements, context);
                        }
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册对话树时发生异常，指定的文件 {correctpath} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册对话树时发生异常，找不到指定的文件或文件夹 {correctpath}");
            }
        }

        /// <summary>
        /// 文件夹模式: 逐个文件加载
        /// </summary>
        public static void InitDialogueData(string modpath, string folderpath, LoadModContext context)
        {
            var correctpath = Path.Combine(modpath, folderpath);

            List<string> files = Directory.GetFiles(correctpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var elements = context.ModHelper.GetJsonDataFromFile<List<TraderDialogElement>>(correctpath, fileName);
                    if (elements != null)
                    {
                        InitDialogueData(elements, context);
                    }
                }
            }
        }

        /// <summary>
        /// 核心: 追加元素到全局对话树表, Id 去重
        /// </summary>
        public static void InitDialogueData(List<TraderDialogElement> elements, LoadModContext context)
        {
            var target = context.DB.GetTemplates().Dialogue.Elements;

            foreach (var element in elements)
            {
                // Id 去重: 已存在则跳过(避免与 vanilla 或其他 mod 冲突)
                if (target.Any(x => x.Id == element.Id))
                {
                    EventManager.EventLogger.Warn($"对话树元素 Id 重复, 已跳过: {element.Id}");
                    continue;
                }
                target.Add(element);
            }
        }
    }
}