using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services;
using static EternalCycle.ContextManager;
using Path = System.IO.Path;

namespace EternalCycle
{
    public class AchievementUtils
    {
        public static Achievement GetAchievement(string achievementId, DatabaseService databaseService)
        {
            return databaseService.GetAchievements().FirstOrDefault(x => x.Id == (MongoId)achievementId);
        }
        /// <summary>
        /// 将自定义成就注册到加载事件
        /// </summary>
        /// <param name="path">指定的存放成就文件的文件夹路径或单个成就文件(列表)路径</param>
        /// <param name="creator">创建者</param>
        /// <param name="modname">Mod名</param>
        public static void RegisterAchievement(string path, string respath)
        {
            // 文件夹加载模式
            if (Directory.Exists(path))
            {
                // 注意：事件名请根据实际情况替换（如 LoadAchievementEvent 或统合在 LoadQuestEvent 中）
                EventManager.DataLoadEvent.LoadAchievementEvent += (context) =>
                {
                    try
                    {
                        InitAchievementData(path, respath, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册成就时发生错误：指定的文件夹 {path} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(path))
            {
                EventManager.DataLoadEvent.LoadAchievementEvent += (context) =>
                {
                    try
                    {
                        // 反序列化为 List 集合
                        var achievementData = context.JsonUtil.Deserialize<List<CustomAchievementData>>(File.ReadAllText(path));

                        if (achievementData != null)
                        {
                            InitAchievementData(achievementData, respath, context);
                        }
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册成就时发生错误：指定的文件 {path} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册成就时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        /// <summary>
        /// Init重载 1：处理文件夹路径，遍历解析为单个成就对象
        /// </summary>
        public static void InitAchievementData(string folderpath, string respath, LoadModContext context)
        {
            if (!Directory.Exists(folderpath)) return;

            List<string> files = Directory.GetFiles(folderpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    // 文件夹模式下，按你的原逻辑，每个文件是一个 CustomAchievementData
                    var achievement = context.ModHelper.GetJsonDataFromFile<CustomAchievementData>(folderpath, fileName);

                    if (achievement != null)
                    {
                        InitAchievement(achievement, respath, context);
                    }
                }
            }
        }

        /// <summary>
        /// Init重载 2：处理单文件反序列化出的成就列表
        /// </summary>
        public static void InitAchievementData(List<CustomAchievementData> achievementData, string respath, LoadModContext context)
        {
            if (achievementData == null || achievementData.Count == 0) return;

            foreach (var achievement in achievementData)
            {
                if (achievement != null)
                {
                    InitAchievement(achievement, respath, context);
                }
            }
        }

        public static void InitAchievement(CustomAchievementData achievementData, string respath, LoadModContext context)
        {
            var zhCNLang = context.DB.GetLocales().Global["ch"];
            var achievements = context.DB.GetAchievements();
            var achievementPattern = context.Cloner.Clone(achievements[0]);
            var achievementid = achievementData.Id;
            achievementPattern.Id = achievementid;
            achievementPattern.ImageUrl = achievementData.ImagePath;
            var imageRouter = ServiceLocator.ServiceProvider.GetService<ImageRouter>();
            ImageUtils.RegisterAchievementRoute(achievementPattern.ImageUrl, Path.Combine(respath, "res/icon/"), imageRouter);
            achievementPattern.Conditions = new AchievementQuestConditionTypes
            {
                AvailableForFinish = new List<QuestCondition>(),
                Fail = new List<QuestCondition>()
            };
            QuestUtils.InitQuestConditions(achievementPattern.Conditions.AvailableForFinish, achievementData.Conditions.AchievementFinishData, context);
            achievementPattern.InstantComplete = achievementData.InstantComplete;
            achievementPattern.ShowConditions = achievementData.ShowConditions;
            achievementPattern.ShowNotificationsInGame = achievementData.ShowNotificationsInGame;
            achievementPattern.ShowProgress = achievementData.ShowProgress;
            achievementPattern.ProgressBarEnabled = achievementData.ProgressBarEnabled;
            achievementPattern.Hidden = achievementData.IsHidden;
            achievementPattern.Rarity = achievementData.Rarity;
            achievementPattern.Side = achievementData.Side;
            var rewards = achievementPattern.Rewards.ToList();
            rewards.Clear();
            achievementPattern.Rewards = rewards;
            zhCNLang.AddTransformer(lang =>
            {

                lang[$"{achievementid} name"] = achievementData.Name;
                lang[$"{achievementid} description"] = achievementData.Description;
                return lang;
            });
            achievements.Add(achievementPattern);
            QuestUtils.InitQuestRewards(achievementData.AchievementRewards, context);
        }
    }
}