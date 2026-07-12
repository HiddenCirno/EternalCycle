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
using static EternalCycleServer.ContextManager;
using Path = System.IO.Path;
namespace EternalCycleServer
{
    public class SuitUtils
    {


        /// <summary>
        /// 将自定义服装(Suit)注册到加载事件
        /// </summary>
        /// <param name="path">指定的存放服装文件的文件夹路径或单文件路径</param>
        /// <param name="creator">创建者</param>
        /// <param name="modname">Mod名</param>
        /// <param name="traderId">可选：如果这些服装属于特定商人，传入商人ID</param>
        public static void RegisterSuit(string modpath, string path, string traderId = null)
        {
            var correctpath = Path.Combine(modpath, path);
            // 文件夹加载模式
            if (Directory.Exists(correctpath))
            {
                // 事件名请根据实际情况调整，例如 LoadSuitEvent 或 LoadCustomizationEvent
                EventManager.DataLoadEvent.LoadSuitEvent += (context) =>
                {
                    try
                    {
                        InitCustomSuitData(correctpath, context, traderId);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册服装时发生错误：指定的文件夹 {correctpath} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(correctpath))
            {
                EventManager.DataLoadEvent.LoadSuitEvent += (context) =>
                {
                    try
                    {
                        var suitData = context.JsonUtil.Deserialize<List<CustomSuit>>(File.ReadAllText(correctpath));
                        InitCustomSuitData(suitData, context, traderId);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册服装时发生错误：指定的文件 {correctpath} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册服装时发生异常：找不到指定的文件或文件夹 {correctpath}");
            }
        }

        /// <summary>
        /// Init重载 1：处理文件夹路径，读取服装列表并向下传递 traderId
        /// </summary>
        public static void InitCustomSuitData(string folderpath, ContextManager.LoadModContext context, string traderId = null)
        {
            if (!Directory.Exists(folderpath)) return;

            List<string> files = Directory.GetFiles(folderpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var suits = context.ModHelper.GetJsonDataFromFile<List<CustomSuit>>(folderpath, fileName);

                    if (suits != null)
                    {
                        InitCustomSuitData(suits, context, traderId);
                    }
                }
            }
        }

        /// <summary>
        /// Init重载 2：合并了你原本的两个 List 重载，通过判断 traderId 是否为空来分发逻辑
        /// </summary>
        public static void InitCustomSuitData(List<CustomSuit> customSuits, ContextManager.LoadModContext context, string traderId = null)
        {
            if (customSuits == null || customSuits.Count == 0) return;

            foreach (var suit in customSuits)
            {
                if (suit == null) continue;

                // 如果传入了 traderId，就走带商人的核心逻辑，否则走普通核心逻辑
                if (!string.IsNullOrEmpty(traderId))
                {
                    InitCustomSuit(suit, traderId.ConvertHashID(), context);
                }
                else
                {
                    InitCustomSuit(suit, context);
                }
            }
        }
        public static void InitCustomSuit(CustomSuit customSuit, LoadModContext context)
        {
            // 换成 context.DB 调用
            var suit = GenerateSuit(customSuit);
            var trader = context.DB.GetTrader(suit.Tid) ?? context.DB.GetTrader(Traders.RAGMAN);
            var suits = trader.Suits;
            suits.Add(suit);
        }

        public static void InitCustomSuit(CustomSuit customSuit, MongoId traderId, LoadModContext context)
        {
            // 换成 context.DB 调用
            var suit = GenerateSuit(customSuit);
            var suits = context.DB.GetTrader(traderId).Suits;
            suits.Add(suit);
        }

        public static Suit GenerateSuit(CustomSuit customSuit)
        {
            var suit = new Suit
            {
                Id = customSuit.Id,
                SuiteId = customSuit.SuiteId,
                Tid = customSuit.Tid,
                IsActive = customSuit.IsActive,
                InternalObtain = customSuit.InternalObtain,
                IsHiddenInPVE = customSuit.IsHiddenInPVE,
                ExternalObtain = customSuit.ExternalObtain,
                RelatedBattlePassSeason = customSuit.RelatedBattlePassSeason,
                Requirements = new SuitRequirements
                {
                    LoyaltyLevel = customSuit.Requirements.LoyaltyLevel,
                    PrestigeLevel = customSuit.Requirements.PrestigeLevel,
                    ProfileLevel = customSuit.Requirements.ProfileLevel,
                    Standing = customSuit.Requirements.Standing,
                    RequiredTid = customSuit.Requirements.RequiredTid,
                    SkillRequirements = new List<string>(),
                    AchievementRequirements = new List<string>(),
                    ItemRequirements = new List<ItemRequirement>(),
                    QuestRequirements = new List<string>()
                }
            };
            foreach (var item in customSuit.Requirements.ItemRequirements)
            {
                suit.Requirements.ItemRequirements.Add(item);
            }
            foreach (var key in customSuit.Requirements.QuestRequirements)
            {
                suit.Requirements.QuestRequirements.Add(key.ConvertHashID());
            }
            foreach (var key in customSuit.Requirements.AchievementRequirements)
            {
                suit.Requirements.AchievementRequirements.Add(key.ConvertHashID());
            }
            return suit;
        }
    }
}