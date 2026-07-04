using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using Path = System.IO.Path;

namespace EternalCycle
{
    /// <summary>
    /// 商人部分
    /// </summary>
    public class TraderUtils
    {

        public static Trader GetTrader(string traderid, DatabaseService databaseService)
        {
            return databaseService.GetTraders().FirstOrDefault(x => x.Value.Base.Id == (MongoId)traderid).Value;
        }

        /// <summary>
        /// 将自定义商人注册到加载事件 (提供给依赖此库的 Mod 调用)
        /// </summary>
        /// <param name="path">指定的存放商人文件的文件夹路径或单个商人文件路径</param>
        /// <param name="imagePath">调用者(子Mod)的商人头像图片存放路径</param>
        /// <param name="creator">创建者</param>
        /// <param name="modname">Mod名</param>
        public static void RegisterTrader(string path, string imagePath, string creator, string modname)
        {
            // 文件夹加载模式
            if (Directory.Exists(path))
            {
                EventManager.DataLoadEvent.LoadTraderBaseEvent += (context) =>
                {
                    try
                    {
                        // 对应调用已有的文件夹重载方法，透传 imagePath
                        InitTraders(path, imagePath, creator, modname, context.ConfigServer, context.JsonUtil, context.ModHelper, context.DB, context.Cloner, context.ImageRouter);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册商人时发生错误：指定的文件夹 {path} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(path))
            {
                EventManager.DataLoadEvent.LoadTraderBaseEvent += (context) =>
                {
                    try
                    {
                        // 商人特有：单文件直接反序列化为单体对象
                        var traderbase = context.JsonUtil.Deserialize<TraderBaseWithDesc>(File.ReadAllText(path));

                        if (traderbase != null)
                        {
                            // 直接跳过文件夹遍历，调用底层的数据 Init 方法
                            InitTrader(traderbase, imagePath, creator, modname, context.ConfigServer, context.DB, context.Cloner, context.ImageRouter);
                        }
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册商人时发生错误：指定的文件 {path} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册商人时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        /// <summary>
        /// Init重载 1：处理文件夹路径，遍历文件并解析为单体数据
        /// </summary>
        public static void InitTraders(string folderpath, string imagePath, string creator, string modname, ConfigServer configServer, JsonUtil jsonUtil, ModHelper modHelper, DatabaseService databaseService, ICloner cloner, ImageRouter imageRouter)
        {
            if (Directory.Exists(folderpath))
            {
                List<string> files = Directory.GetFiles(folderpath).ToList();
                if (files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileName(file);
                        var traderbase = modHelper.GetJsonDataFromFile<TraderBaseWithDesc>(folderpath, fileName);

                        if (traderbase != null)
                        {
                            // 解析出单体后，调用底层 Init
                            InitTrader(traderbase, imagePath, creator, modname, configServer, databaseService, cloner, imageRouter);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 基于自定义格式文件创建一个新的商人
        /// </summary>
        /// <param name="traderBase">商人的base文件</param>
        /// <param name="creator">创建者名称（用于标识来源）</param>
        /// <param name="modname">Mod名称（用于标识来源）</param>
        /// <param name="configServer">SPT工具类传入</param>
        /// <param name="databaseService">SPT工具类传入</param>
        /// <param name="cloner">SPT工具类传入</param>
        /// <param name="imageRouter">SPT工具类传入</param>
        public static void InitTrader(TraderBaseWithDesc traderBase, string respath, string creator, string modname, ConfigServer configServer, DatabaseService databaseService, ICloner cloner, ImageRouter imageRouter)
        {
            InsuranceConfig insuranceConfig = configServer.GetConfig<InsuranceConfig>();
            TraderConfig traderConfig = configServer.GetConfig<TraderConfig>();
            RagfairConfig ragfairConfig = configServer.GetConfig<RagfairConfig>();
            Trader traderPattern = cloner.Clone(GetTrader((string)Traders.PRAPOR, databaseService));
            string traderId = (MongoId)traderBase.Id;
            ImageUtils.RegisterAvatarRoute(traderBase.Avatar, Path.Combine(respath, "res/avatar/"), imageRouter);
            //ImageUtils.RegisterImageRoute(traderBase.Avatar.Replace(".jpg", "").Replace(".png", ""), Path.Combine(imagePath, Path.GetFileName(traderBase.Avatar)), imageRouter);
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
            Utils.CopyNonNullProperties(traderBase, traderPattern.Base);
            LocaleUtils.AddTraderToLocales(traderBase, databaseService, creator, modname);
            var insuranceChance = traderBase.InsuranceChance ?? 0;
            if (insuranceChance > 0)
            {
                insuranceConfig.ReturnChancePercent.TryAdd(traderId, (double)insuranceChance);
            }
            traderConfig.UpdateTime.Add(new UpdateTime
            {
                Name = traderBase.Name,
                TraderId = traderId,
                Seconds = new MinMax<int> { Min = traderBase.ReflashMinTime ?? 1800, Max = traderBase.ReflashMaxTime ?? 3600 }
            });
            if (traderBase.ShowInRagfair ?? false)
            {
                ragfairConfig.Traders.TryAdd(traderId, true);
            }
            databaseService.GetTables().Traders[traderBase.Id] = traderPattern;
        }
    }
}