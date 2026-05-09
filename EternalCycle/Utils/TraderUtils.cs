using HarmonyLib.Tools;
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


public class TraderUtils
{
    public static Trader GetTrader(string traderid, DatabaseService databaseService)
    {
        return databaseService.GetTraders().FirstOrDefault(x => x.Value.Base.Id == (MongoId)traderid).Value;
    }
    public static void InitTraders(string folderpath, string imagePath, int insuranceChance, int reflashMinTime, int reflashMaxtime, bool addToRagfair, string creator, string modname, ConfigServer configServer, JsonUtil jsonUtil, ModHelper modHelper, DatabaseService databaseService, ICloner cloner, ImageRouter imageRouter)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var traderbase = modHelper.GetJsonDataFromFile<TraderBaseWithDesc>(folderpath, fileName);
                InitTrader(traderbase, imagePath, insuranceChance, reflashMinTime, reflashMaxtime, addToRagfair, creator, modname, configServer, databaseService, cloner, imageRouter);
            }
        }
    }
    /// <summary>
    /// 基于自定义格式文件创建一个新的商人
    /// </summary>
    /// <param name="traderBase">商人的base文件</param>
    /// <param name="imagePath">存放商人头像的文件路径, 用于注册路由</param>
    /// <param name="insuranceChance">保险返还概率（百分比 0~100）</param>
    /// <param name="reflashMinTime">刷新最小间隔（秒）</param>
    /// <param name="reflashMaxtime">刷新最大间隔（秒）</param>
    /// <param name="addToRagfair">是否让商人在跳蚤市场可见</param>
    /// <param name="creator">创建者名称（用于标识来源）</param>
    /// <param name="modname">Mod名称（用于标识来源）</param>
    /// <param name="configServer">SPT工具类传入</param>
    /// <param name="databaseService">SPT工具类传入</param>
    /// <param name="cloner">SPT工具类传入</param>
    /// <param name="imageRouter">SPT工具类传入</param>
    public static void InitTrader(TraderBaseWithDesc traderBase, string imagePath, int insuranceChance, int reflashMinTime, int reflashMaxtime, bool addToRagfair, string creator, string modname, ConfigServer configServer, DatabaseService databaseService, ICloner cloner, ImageRouter imageRouter)
    {
        InsuranceConfig insuranceConfig = configServer.GetConfig<InsuranceConfig>();
        TraderConfig traderConfig = configServer.GetConfig<TraderConfig>();
        RagfairConfig ragfairConfig = configServer.GetConfig<RagfairConfig>();
        Trader traderPattern = cloner.Clone(GetTrader((string)Traders.PRAPOR, databaseService));
        string traderId = (MongoId)traderBase.Id;
        ImageUtils.RegisterImageRoute(traderBase.Avatar.Replace(".jpg", "").Replace(".png", ""), Path.Combine(imagePath, Path.GetFileName(traderBase.Avatar)), imageRouter);
        traderBase.Id = traderId;
        traderPattern.Assort.Items?.Clear();
        //traderPattern.Assort.Items = new List<Item>();
        traderPattern.Assort.BarterScheme?.Clear();
        //traderPattern.Assort.BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>();
        traderPattern.Assort.LoyalLevelItems?.Clear();
        //traderPattern.Assort.LoyalLevelItems = new Dictionary<MongoId, int>();
        traderPattern.QuestAssort["started"].Clear();
        traderPattern.QuestAssort["success"].Clear();
        traderPattern.QuestAssort["fail"].Clear();
        traderPattern.Dialogue.Clear();
        if (traderBase.Dialogue != null)
        {
            traderPattern.Dialogue["insuranceStart"] = traderBase.Dialogue["insuranceStart"];
            traderPattern.Dialogue["insuranceFound"] = traderBase.Dialogue["insuranceFound"];
            traderPattern.Dialogue["insuranceFailedLabs"] = traderBase.Dialogue["insuranceFailedLabs"];
            traderPattern.Dialogue["insuranceExpired"] = traderBase.Dialogue["insuranceExpired"];
            traderPattern.Dialogue["insuranceComplete"] = traderBase.Dialogue["insuranceComplete"];
            traderPattern.Dialogue["insuranceFailed"] = traderBase.Dialogue["insuranceFailed"];
        }
        if (traderBase.CustomizationSeller == true)
        {
            traderPattern.Suits = new List<Suit>();
        }
        traderPattern?.Services?.Clear();
        VulcanUtil.CopyNonNullProperties(traderBase, traderPattern.Base);
        LocaleUtils.AddTraderToLocales(traderBase, databaseService, creator, modname);
        if (insuranceChance > 0)
        {
            insuranceConfig.ReturnChancePercent.TryAdd(traderId, (double)(traderBase.InsuranceChance ?? insuranceChance));
        }
        traderConfig.UpdateTime.Add(new UpdateTime
        {
            Name = traderBase.Name,
            TraderId = traderId,
            Seconds = new MinMax<int> { Min = traderBase.ReflashMinTime ?? reflashMinTime, Max = traderBase.ReflashMaxTime ?? reflashMaxtime }
        });
        if (traderBase.ShowInRagfair ?? addToRagfair)
        {
            ragfairConfig.Traders.TryAdd(traderId, true);
        }
        databaseService.GetTables().Traders[traderBase.Id] = traderPattern;
        //Traders
    }

}










