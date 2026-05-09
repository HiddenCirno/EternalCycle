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
using Path = System.IO.Path;
namespace VulcanCore;
public class AchievementUtils
{
    public static Achievement GetAchievement(string achievementId, DatabaseService databaseService)
    {
        return databaseService.GetAchievements().FirstOrDefault(x => x.Id == (MongoId)achievementId);
    }
    public static void InitAchievementData(List<CustomAchievementData> achievementData, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        foreach (var achievement in achievementData)
        {
            InitAchievement(achievement, databaseService, cloner, logger);
        }
    }
    public static void InitAchievementData(string folderpath, DatabaseService databaseService, ModHelper modHelper, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var achievement = modHelper.GetJsonDataFromFile<CustomAchievementData>(folderpath, fileName);
                InitAchievement(achievement, databaseService, cloner, logger);
            }
        }
    }
    public static void InitAchievement(CustomAchievementData achievementData, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        var zhCNLang = databaseService.GetLocales().Global["ch"];
        var achievements = databaseService.GetAchievements();
        var achievementPattern = cloner.Clone(achievements[0]);
        var achievementid = achievementData.Id;
        achievementPattern.Id = achievementid;
        achievementPattern.ImageUrl = achievementData.ImagePath;
        achievementPattern.Conditions = new AchievementQuestConditionTypes
        {
            AvailableForFinish = new List<QuestCondition>(),
            Fail = new List<QuestCondition>()
        };
        QuestUtils.InitQuestConditions(achievementPattern.Conditions.AvailableForFinish, achievementData.Conditions.AchievementFinishData, databaseService, cloner, logger);
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
        QuestUtils.InitQuestRewards(achievementData.AchievementRewards, databaseService, cloner, logger);
    }
}










