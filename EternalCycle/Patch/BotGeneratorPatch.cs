using System.Reflection;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using HarmonyLib;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Spt.Config;
using System.Text;
using SPTarkov.Server.Core.Utils.Cloners;

namespace EternalCycleServer
{
    public class BotGeneratorPatch
    {
        public static List<string> alterBossName = new List<string>{
                "Dullahan",
                "Golyat",
                "Argus",
                "Sanitar",
                "Ghroth",
                "Punisher"
                };
        public class GenerateBotPatch : AbstractPatch
        {
            public static AlterBossCounter SanitarCounter = new AlterBossCounter
            {
                Chance = 0,
                Counter = 0,
                Access = false
            };

            public static AlterBossCounter GluharCocunter = new AlterBossCounter
            {
                Chance = 0,
                Counter = 0,
                Access = false
            };

            public static AlterBossCounter KolontayCocunter = new AlterBossCounter
            {
                Chance = 0,
                Counter = 0,
                Access = false
            };

            public static AlterGoonsCounter GoonsCounter = new AlterGoonsCounter
            {
                Chance = 0,
                KnightCounter = 0,
                PipeCounter = 0,
                EyesCounter = 0,
                KnightAccess = false,
                PipeAccess = false,
                EyesAccess = false,
                Access = false
            };

            public static string BotLoation = "";
            protected override MethodBase GetTargetMethod()
            {
                return typeof(BotGenerator).GetMethod("GenerateBot", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            }
            [PatchPrefix]
            public static bool Prefix(BotGenerator __instance, MongoId sessionId, BotBase bot, BotType botJsonTemplate, BotGenerationDetails botGenerationDetails, ref BotBase __result)
            {

                var logger = ServiceLocator.ServiceProvider.GetService<ISptLogger<BotGenerator>>();
                var botLevelGenerator = ServiceLocator.ServiceProvider.GetService<BotLevelGenerator>();
                var botEquipmentFilterService = ServiceLocator.ServiceProvider.GetService<BotEquipmentFilterService>();
                var botNameService = ServiceLocator.ServiceProvider.GetService<BotNameService>();
                var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
                var seasonalEventService = ServiceLocator.ServiceProvider.GetService<SeasonalEventService>();
                var weightedRandomHelper = ServiceLocator.ServiceProvider.GetService<WeightedRandomHelper>();
                var botInventoryGenerator = ServiceLocator.ServiceProvider.GetService<BotInventoryGenerator>();
                var modHelper = ServiceLocator.ServiceProvider.GetService<ModHelper>();
                var configServer = ServiceLocator.ServiceProvider.GetService<ConfigServer>();
                var botConfig = configServer.GetConfig<BotConfig>();

                var botRoleLowercase = botGenerationDetails.Role.ToLowerInvariant();
                var botSanitar = modHelper.GetJsonDataFromFile<BotType>(ConfigManager.modPath, "moddata/vulcanmod/bots/Sanitar.json");
                var botKnight = modHelper.GetJsonDataFromFile<BotType>(ConfigManager.modPath, "moddata/vulcanmod/bots/Knight.json");
                var botBigPipe = modHelper.GetJsonDataFromFile<BotType>(ConfigManager.modPath, "moddata/vulcanmod/bots/BigPipe.json");
                var botBirdeye = modHelper.GetJsonDataFromFile<BotType>(ConfigManager.modPath, "moddata/vulcanmod/bots/Birdeye.json");
                var botGluhar = modHelper.GetJsonDataFromFile<BotType>(ConfigManager.modPath, "moddata/vulcanmod/bots/Gluhar.json");
                var botKolontay = modHelper.GetJsonDataFromFile<BotType>(ConfigManager.modPath, "moddata/vulcanmod/bots/Kolontay.json");
                var botObsidian = modHelper.GetJsonDataFromFile<BotType>(ConfigManager.modPath, "moddata/vulcanmod/bots/Obsidian.json");
                try
                {
                    if (BotLoation != botGenerationDetails.Location)
                    {
                        BotLoation = (string)botGenerationDetails.Location;
                        logger.LogWithColor($"Correct Location: {botGenerationDetails.Location}", LogTextColor.Magenta);
                    }
                    //logger.LogWithColor($"Bot Location: {botGenerationDetails.Location}", LogTextColor.Magenta);
                    if (true)
                    {
                        if (botRoleLowercase == "bossknight")
                        {
                            if (GoonsCounter.Chance == 0)
                            {
                                GoonsCounter.Chance = (int)Math.Floor(new Random().NextDouble() * 100);
                            }
                            if (GoonsCounter.Chance <= 50)
                            {
                                GoonsCounter.Access = true;
                            }
                            if (GoonsCounter.Access)
                            {
                                botJsonTemplate.BotAppearance = botKnight.BotAppearance;
                                botJsonTemplate.BotExperience = botKnight.BotExperience;
                                botJsonTemplate.BotHealth = botKnight.BotHealth;
                                botJsonTemplate.BotSkills = botKnight.BotSkills;
                                botJsonTemplate.BotInventory = botKnight.BotInventory;
                                botJsonTemplate.BotChances = botKnight.BotChances;
                                botJsonTemplate.FirstNames = botKnight.FirstNames;
                                botJsonTemplate.BotGeneration = botKnight.BotGeneration;
                                //唱片和箭头预留部分
                                GoonsCounter.KnightAccess = true;
                            }
                            GoonsCounter.KnightCounter++;
                            if (GoonsCounter.KnightCounter >= 5 && GoonsCounter.KnightAccess != true)
                            {
                                logger.LogWithColor("转化失败复位", LogTextColor.Gray);
                                //转化失败复位
                                GoonsCounter.Chance = 0;
                                GoonsCounter.KnightCounter = 0;
                                GoonsCounter.PipeCounter = 0;
                                GoonsCounter.EyesCounter = 0;
                                GoonsCounter.KnightAccess = false;
                                GoonsCounter.PipeAccess = false;
                                GoonsCounter.EyesAccess = false;
                                GoonsCounter.Access = false;
                            }
                        }
                        if (botRoleLowercase == "followerbigpipe")
                        {
                            if (GoonsCounter.Access)
                            {
                                botJsonTemplate.BotAppearance = botBigPipe.BotAppearance;
                                botJsonTemplate.BotExperience = botBigPipe.BotExperience;
                                botJsonTemplate.BotHealth = botBigPipe.BotHealth;
                                botJsonTemplate.BotSkills = botBigPipe.BotSkills;
                                botJsonTemplate.BotInventory = botBigPipe.BotInventory;
                                botJsonTemplate.BotChances = botBigPipe.BotChances;
                                botJsonTemplate.FirstNames = botBigPipe.FirstNames;
                                botJsonTemplate.LastNames = botBigPipe.LastNames;
                                botJsonTemplate.BotGeneration = botBigPipe.BotGeneration;
                                GoonsCounter.PipeAccess = true;
                                //唱片预留
                            }
                            GoonsCounter.PipeCounter++;
                            if (GoonsCounter.PipeCounter >= 5 && GoonsCounter.EyesCounter >= 5)
                            {
                                logger.LogWithColor("转化完成复位, from bigpipe", LogTextColor.Gray);
                                //转化完成复位
                                //大管和鸟眼双计数, 防止提前复位
                                GoonsCounter.Chance = 0;
                                GoonsCounter.KnightCounter = 0;
                                GoonsCounter.PipeCounter = 0;
                                GoonsCounter.EyesCounter = 0;
                                GoonsCounter.KnightAccess = false;
                                GoonsCounter.PipeAccess = false;
                                GoonsCounter.EyesAccess = false;
                                GoonsCounter.Access = false;
                            }
                        }
                        if (botRoleLowercase == "followerbirdeye")
                        {
                            if (GoonsCounter.Access)
                            {
                                botJsonTemplate.BotAppearance = botBirdeye.BotAppearance;
                                botJsonTemplate.BotExperience = botBirdeye.BotExperience;
                                botJsonTemplate.BotHealth = botBirdeye.BotHealth;
                                botJsonTemplate.BotSkills = botBirdeye.BotSkills;
                                botJsonTemplate.BotInventory = botBirdeye.BotInventory;
                                botJsonTemplate.BotChances = botBirdeye.BotChances;
                                botJsonTemplate.FirstNames = botBirdeye.FirstNames;
                                botJsonTemplate.BotGeneration = botBirdeye.BotGeneration;
                                GoonsCounter.EyesAccess = true;
                                //唱片预留
                            }
                            GoonsCounter.EyesCounter++;
                            if (GoonsCounter.PipeCounter >= 5 && GoonsCounter.EyesCounter >= 5)
                            {
                                logger.LogWithColor("转化完成复位, from birdeye", LogTextColor.Gray);
                                //转化完成复位
                                //大管和鸟眼双计数, 防止提前复位
                                GoonsCounter.Chance = 0;
                                GoonsCounter.KnightCounter = 0;
                                GoonsCounter.PipeCounter = 0;
                                GoonsCounter.EyesCounter = 0;
                                GoonsCounter.KnightAccess = false;
                                GoonsCounter.PipeAccess = false;
                                GoonsCounter.EyesAccess = false;
                                GoonsCounter.Access = false;
                            }
                        }
                        if (botRoleLowercase == "bosssanitar")
                        {
                            //从文件读取AI数据
                            if (SanitarCounter.Chance == 0)
                            {
                                //初始化生成概率
                                SanitarCounter.Chance = (int)Math.Floor(new Random().NextDouble() * 100);
                            }
                            if (SanitarCounter.Chance <= 50)
                            {
                                //命中概率
                                //这里的100是测试, 实际从配置读取
                                SanitarCounter.Access = true;
                            }
                            if (SanitarCounter.Access)
                            {
                                //概率命中, 使用文件的数据覆盖原数据
                                botJsonTemplate.BotAppearance = botSanitar.BotAppearance;
                                botJsonTemplate.BotExperience = botSanitar.BotExperience;
                                botJsonTemplate.BotHealth = botSanitar.BotHealth;
                                botJsonTemplate.BotSkills = botSanitar.BotSkills;
                                botJsonTemplate.BotInventory = botSanitar.BotInventory;
                                botJsonTemplate.BotChances = botSanitar.BotChances;
                                botJsonTemplate.FirstNames = botSanitar.FirstNames;
                                botJsonTemplate.BotGeneration = botSanitar.BotGeneration;
                                //配置器计数+1
                            }
                            SanitarCounter.Counter++;
                            //AI生成完成(每个AI的生成会调用5次
                            if (SanitarCounter.Counter >= 5)
                            {
                                //配置器复位
                                SanitarCounter.Chance = 0;
                                SanitarCounter.Counter = 0;
                                SanitarCounter.Access = false;
                            }
                        }
                        if (botRoleLowercase == "bossgluhar")
                        {
                            //从文件读取AI数据
                            if (GluharCocunter.Chance == 0)
                            {
                                //初始化生成概率
                                GluharCocunter.Chance = (int)Math.Floor(new Random().NextDouble() * 100);
                            }
                            if (GluharCocunter.Chance <= 50)
                            {
                                //命中概率
                                //这里的100是测试, 实际从配置读取
                                GluharCocunter.Access = true;
                            }
                            if (GluharCocunter.Access)
                            {
                                //概率命中, 使用文件的数据覆盖原数据
                                botJsonTemplate.BotAppearance = botGluhar.BotAppearance;
                                botJsonTemplate.BotExperience = botGluhar.BotExperience;
                                botJsonTemplate.BotHealth = botGluhar.BotHealth;
                                botJsonTemplate.BotSkills = botGluhar.BotSkills;
                                botJsonTemplate.BotInventory = botGluhar.BotInventory;
                                botJsonTemplate.BotChances = botGluhar.BotChances;
                                botJsonTemplate.FirstNames = botGluhar.FirstNames;
                                botJsonTemplate.BotGeneration = botGluhar.BotGeneration;
                                //配置器计数+1
                            }
                            GluharCocunter.Counter++;
                            //AI生成完成(每个AI的生成会调用5次
                            if (GluharCocunter.Counter >= 5)
                            {
                                //配置器复位
                                GluharCocunter.Chance = 0;
                                GluharCocunter.Counter = 0;
                                GluharCocunter.Access = false;
                            }
                        }
                        if (botRoleLowercase == "bosskolontay")
                        {
                            //从文件读取AI数据
                            if (KolontayCocunter.Chance == 0)
                            {
                                //初始化生成概率
                                KolontayCocunter.Chance = (int)Math.Floor(new Random().NextDouble() * 100);
                            }
                            if (KolontayCocunter.Chance <= 50)
                            {
                                //命中概率
                                //这里的100是测试, 实际从配置读取
                                KolontayCocunter.Access = true;
                            }
                            if (KolontayCocunter.Access)
                            {
                                //概率命中, 使用文件的数据覆盖原数据
                                botJsonTemplate.BotAppearance = botKolontay.BotAppearance;
                                botJsonTemplate.BotExperience = botKolontay.BotExperience;
                                botJsonTemplate.BotHealth = botKolontay.BotHealth;
                                botJsonTemplate.BotSkills = botKolontay.BotSkills;
                                botJsonTemplate.BotInventory = botKolontay.BotInventory;
                                botJsonTemplate.BotChances = botKolontay.BotChances;
                                botJsonTemplate.FirstNames = botKolontay.FirstNames;
                                botJsonTemplate.BotGeneration = botKolontay.BotGeneration;
                                //配置器计数+1
                            }
                            KolontayCocunter.Counter++;
                            //AI生成完成(每个AI的生成会调用5次
                            if (KolontayCocunter.Counter >= 5)
                            {
                                //配置器复位
                                KolontayCocunter.Chance = 0;
                                KolontayCocunter.Counter = 0;
                                KolontayCocunter.Access = false;
                            }
                        }
                    }

                    if (true)
                    {
                        if (botRoleLowercase == "bosskillaagro" && botGenerationDetails.Location != "Labyrinth")
                        {
                            //测试代码, 数据部分暂时搁置
                            //打个时间戳看看我能鸽多久
                            //12.10.2025
                            //从文件读取AI数据
                            botJsonTemplate.BotAppearance = botObsidian.BotAppearance;
                            botJsonTemplate.BotExperience = botObsidian.BotExperience;
                            botJsonTemplate.BotHealth = botObsidian.BotHealth;
                            botJsonTemplate.BotSkills = botObsidian.BotSkills;
                            botJsonTemplate.BotInventory = botObsidian.BotInventory;
                            botJsonTemplate.BotChances = botObsidian.BotChances;
                            botJsonTemplate.FirstNames = botObsidian.FirstNames;
                            botJsonTemplate.LastNames = botObsidian.LastNames;
                            botJsonTemplate.BotGeneration = botObsidian.BotGeneration;
                            //bot.Health.BodyParts;
                            //3c6269d6143b2cf96fb0224f //BD1
                            //3c6269d6143b2cf96fb0224f //BD2
                            //logger.LogWithColor($"Debug: {bot.Customization.Voice}", LogTextColor.Gray);
                        }
                    }
                    //if(alterBossName.Contains(bot.Info.Nickname))
                    //logger.LogWithColor($"[Test]: {botGenerationDetails.RoleLowercase}", LogTextColor.Magenta, LogBackgroundColor.Default);
                    // 方法中的逻辑
                    botGenerationDetails.RoleLowercase = botGenerationDetails.Role.ToLowerInvariant();

                    // 调用 botLevelGenerator 生成 BotLevel
                    RandomisedBotLevelResult randomisedBotLevelResult = botLevelGenerator.GenerateBotLevel(botJsonTemplate.BotExperience.Level, botGenerationDetails, bot);
                    botGenerationDetails.BotLevel = randomisedBotLevelResult.Level.GetValueOrDefault();

                    // 为 Bot 添加 ID
                    AccessTools.Method(typeof(BotGenerator), "AddIdsToBot").Invoke(__instance, new object[] { bot });

                    // 如果不是玩家 Scav，过滤 Bot 装备
                    if (!botGenerationDetails.IsPlayerScav)
                    {
                        botEquipmentFilterService.FilterBotEquipment(sessionId, botJsonTemplate, botGenerationDetails);
                    }

                    // 为 Bot 生成 Nickname
                    bot.Info.Nickname = botNameService.GenerateUniqueBotNickname(botJsonTemplate, botGenerationDetails, botConfig.BotRolesThatMustHaveUniqueName);
                    bot.Info.LowerNickname = (botGenerationDetails.IsPmc ? bot.Info.Nickname.ToLowerInvariant() : string.Empty);

                    // 生成随机 Pmc 名称和设置游戏版本
                    if (!botGenerationDetails.IsPlayerScav && (bool)AccessTools.Method(typeof(BotGenerator), "ShouldSimulatePlayerScav").Invoke(__instance, new object[] { botGenerationDetails.RoleLowercase }))
                    {
                        botNameService.AddRandomPmcNameToBotMainProfileNicknameProperty(bot);
                        AccessTools.Method(typeof(BotGenerator), "SetRandomisedGameVersionAndCategory").Invoke(__instance, new object[] { bot.Info });
                    }

                    // 移除圣诞节物品（如果活动未启用并且不是 "gifter" 角色）
                    if (!seasonalEventService.ChristmasEventEnabled() && botGenerationDetails.Role != "gifter")
                    {
                        seasonalEventService.RemoveChristmasItemsFromBotInventory(botJsonTemplate.BotInventory, botGenerationDetails.Role);
                    }

                    // 从 Bot 模板中移除黑名单物品
                    AccessTools.Method(typeof(BotGenerator), "RemoveBlacklistedLootFromBotTemplate").Invoke(__instance, new object[] { botJsonTemplate.BotInventory });

                    // 如果不是 Pmc 和 Player Scav，清空 Bot 的 Hideout
                    if (!botGenerationDetails.IsPmc && !botGenerationDetails.IsPlayerScav)
                    {
                        bot.Hideout = null;
                    }

                    // 设置 Bot 经验值和等级
                    bot.Info.Experience = randomisedBotLevelResult.Exp;
                    bot.Info.Level = randomisedBotLevelResult.Level;

                    // 设置 Bot 经验奖励和站位变动
                    bot.Info.Settings.Experience = (int)AccessTools.Method(typeof(BotGenerator), "GetExperienceRewardForKillByDifficulty")
                        .Invoke(__instance, new object[] { botJsonTemplate.BotExperience.Reward, botGenerationDetails.BotDifficulty, botGenerationDetails.Role });

                    bot.Info.Settings.StandingForKill = (double)AccessTools.Method(typeof(BotGenerator), "GetStandingChangeForKillByDifficulty")
                        .Invoke(__instance, new object[] { botJsonTemplate.BotExperience.StandingForKill, botGenerationDetails.BotDifficulty, botGenerationDetails.Role });

                    bot.Info.Settings.AggressorBonus = (double)AccessTools.Method(typeof(BotGenerator), "GetAggressorBonusByDifficulty")
                        .Invoke(__instance, new object[] { botJsonTemplate.BotExperience.StandingForKill, botGenerationDetails.BotDifficulty, botGenerationDetails.Role });

                    bot.Info.Settings.UseSimpleAnimator = botJsonTemplate.BotExperience.UseSimpleAnimator;

                    // 随机选择 Bot 声音
                    bot.Customization.Voice = weightedRandomHelper.GetWeightedValue(botJsonTemplate.BotAppearance.Voice);

                    // 生成 Bot 的健康状况
                    bot.Health = (BotBaseHealth)AccessTools.Method(typeof(BotGenerator), "GenerateHealth")
                        .Invoke(__instance, new object[] { botJsonTemplate.BotHealth, botGenerationDetails.IsPlayerScav });

                    // 生成 Bot 的技能
                    bot.Skills = (Skills)AccessTools.Method(typeof(BotGenerator), "GenerateSkills")
                        .Invoke(__instance, new object[] { botJsonTemplate.BotSkills });

                    // 设置 Bot 等级
                    bot.Info.PrestigeLevel = 0;

                    // 如果是 Pmc，设置 StreamerMode 和游戏版本
                    if (botGenerationDetails.IsPmc)
                    {
                        bot.Info.IsStreamerModeAvailable = true;
                        AccessTools.Method(typeof(BotGenerator), "SetRandomisedGameVersionAndCategory").Invoke(__instance, new object[] { bot.Info });

                        if (bot.Info.GameVersion == "unheard_edition")
                        {
                            AccessTools.Method(typeof(BotGenerator), "AddAdditionalPocketLootWeightsForUnheardBot").Invoke(__instance, new object[] { botJsonTemplate });
                        }

                        botGenerationDetails.GameVersion = bot.Info.GameVersion;
                    }

                    // 设置 Bot 外观
                    AccessTools.Method(typeof(BotGenerator), "SetBotAppearance").Invoke(__instance, new object[] { bot, botJsonTemplate.BotAppearance, botGenerationDetails });

                    // 生成 Bot 的背包
                    bot.Inventory = botInventoryGenerator.GenerateInventory(bot.Id.Value, sessionId, botJsonTemplate, botGenerationDetails);

                    // 如果 Bot 角色需要，添加 Dogtag
                    if (botConfig.BotRolesWithDogTags.Contains(botGenerationDetails.RoleLowercase))
                    {
                        AccessTools.Method(typeof(BotGenerator), "AddDogtagToBot").Invoke(__instance, new object[] { bot });
                    }

                    /*
                    //在inv之前独立处理特殊战利品和箭头/唱片
                    var bossList = vulcanConfig.Config.DogTagGenerate.Config.BossList;
                    var botRole = bot.Info.Settings.Role;
                    var botRoleLower = botRole.ToLower();
                    var botNickName = bot.Info.Nickname;
                    if (vulcanConfig.Active)
                    {
                        switch (botRoleLower)
                        {
                            case "bossbully":
                                AddLootToInventory(bot.Inventory, VulcanUtil.ConvertHashID("宿舍楼管理员钥匙"), "Pockets", bot, databaseService, logger);
                                AddLootToInventory(bot.Inventory, ItemTpl.INFO_NOTE_WITH_CODE_WORD_VORON, "Pockets", bot, databaseService, logger);
                                break;
                            case "bosstagilla":
                                AddLootToInventory(bot.Inventory, VulcanUtil.ConvertHashID("仿制工厂钥匙"), "Pockets", bot, databaseService, logger);
                                AddLootToInventory(bot.Inventory, ItemTpl.INFO_NOTE_WITH_CODE_WORD_ARK, "Pockets", bot, databaseService, logger);
                                break;
                            case "bosstagillaagro":
                                AddLootToInventory(bot.Inventory, ItemTpl.KEY_ARIADNE_SYMBOL, "Pockets", bot, databaseService, logger);
                                break;
                            case "bosssanitar":
                                AddLootToInventory(bot.Inventory, VulcanUtil.ConvertHashID("疗养院管理员钥匙"), "Pockets", bot, databaseService, logger);
                                AddLootToInventory(bot.Inventory, ItemTpl.INFO_NOTE_WITH_CODE_WORD_HEARTBEAT, "Pockets", bot, databaseService, logger);
                                break;
                            case "bosskilla":
                                AddLootToInventory(bot.Inventory, VulcanUtil.ConvertHashID("仿制11SR"), "Pockets", bot, databaseService, logger);
                                break;
                            case "bossgluhar":
                                AddLootToInventory(bot.Inventory, VulcanUtil.ConvertHashID("储备站管理员钥匙"), "Pockets", bot, databaseService, logger);
                                AddLootToInventory(bot.Inventory, ItemTpl.INFO_MINEFIELD_MAP_RESERVE, "Pockets", bot, databaseService, logger);
                                break;
                            case "bosskojaniy":
                                {
                                    AddLootToInventory(bot.Inventory, ItemTpl.INFO_MINEFIELD_MAP_WOODS, "Pockets", bot, databaseService, logger);
                                }
                                break;
                            case "bossboar":
                                {
                                    AddLootToInventory(bot.Inventory, ItemTpl.INFO_NOTE_WITH_CODE_WORD_ONYX, "Pockets", bot, databaseService, logger);
                                }
                                break;
                            case "sectantpriest":
                                AddLootToInventory(bot.Inventory, VulcanUtil.ConvertHashID("通用符号钥匙"), "Pockets", bot, databaseService, logger);
                                break;
                        }
                        if (botNickName == "Dullahan")
                        {
                            AddLootToInventory(bot.Inventory, "64d0b40fbe2eed70e254e2d4", "Pockets", bot, databaseService, logger);
                        }
                        if (botNickName == "Obsidian")
                        {
                            AddLootToInventory(bot.Inventory, VulcanUtil.ConvertHashID("实验室管理员钥匙卡"), "Pockets", bot, databaseService, logger);
                            AddLootToInventory(bot.Inventory, VulcanUtil.ConvertHashID("黑L4G24夜视仪支架"), "Backpack", bot, databaseService, logger);
                            AddLootToInventory(bot.Inventory, VulcanUtil.ConvertHashID("PVS31A"), "Backpack", bot, databaseService, logger);

                        }
                        if (alterBossName.Contains(botNickName))
                        {
                            AddLootToInventory(bot.Inventory, VulcanUtil.ConvertHashID("封装英雄之证"), "Pockets", bot, databaseService, logger);
                            if (bot.Inventory != null && bot.Inventory.Items != null && bot.Inventory.Items.Count > 0)
                            {
                                bot.Inventory.Items.ForEach(item =>
                                {
                                    if (item.SlotId == "FirstPrimaryWeapon" || item.SlotId == "SecondPrimaryWeapon" || item.SlotId == "Holster")
                                    {
                                        if (item.Upd != null && item.Upd.Repairable != null)
                                        {
                                            item.Upd.Repairable.Durability = 100;
                                            item.Upd.Repairable.MaxDurability = 100;
                                        }
                                    }
                                });
                            }
                        }
                        if (botNickName == "Obsidian" || botRoleLowercase == "arenafighterevent")
                        {
                            if (bot.Inventory != null && bot.Inventory.Items != null && bot.Inventory.Items.Count > 0)
                            {
                                bot.Inventory.Items.ForEach(item =>
                                {
                                    if (item.SlotId == "FirstPrimaryWeapon" || item.SlotId == "SecondPrimaryWeapon" || item.SlotId == "Holster")
                                    {
                                        if (item.Upd != null && item.Upd.Repairable != null)
                                        {
                                            item.Upd.Repairable.Durability = 100;
                                            item.Upd.Repairable.MaxDurability = 100;
                                        }
                                    }
                                });
                            }
                        }
                    }

                    var arrowConfig = modConfig.Module.BattleModule.ArrowMarker;
                    var musicConfig = modConfig.Module.ItemModule.Music;
                    var arrowcfg = arrowConfig.Config;
                    var musiccfg = musicConfig.Config;
                    //箭头部分, //唱片不在此处生成 //好吧, 唱片还是得在这生成

                    if (musicConfig.Active)
                    {
                        if (bot.Info.Side == "Savage")
                        {
                            foreach (var data in Music.EquipmentData.Values)
                            {
                                if (data.BotList.Contains(botRoleLower))
                                {
                                    var chance = (int)Math.Floor(new Random().NextDouble() * 100);
                                    if (chance <= data.Chance)
                                    {
                                        var itemid = VulcanUtil.ConvertHashID(Music.WeightedRandom(data.EquipmentList));
                                        Music.AddMusicToInventory(bot.Inventory, itemid, "Earpiece", logger);
                                    }
                                }
                            }
                            if (botNickName == "Dullahan")
                            {
                                Music.AddMusicToInventory(bot.Inventory, VulcanUtil.ConvertHashID("三角洲唱片2"), "Earpiece", logger);
                            }
                        }
                    }

                    if (arrowConfig.Active)
                    {
                        var arrowid = VulcanUtil.ConvertHashID("米黄色箭头");
                        var arrowslot = arrowcfg.EnableLootableMode ? "Earpiece" : "ArmBand";
                        if (botRoleLower.Contains("boss"))
                        {
                            arrowid = VulcanUtil.ConvertHashID(arrowcfg.EnableSpecialMarker ? "危标记" : "红色箭头");
                        }
                        if (botRoleLower.Contains("sectant"))
                        {
                            arrowid = VulcanUtil.ConvertHashID(arrowcfg.EnableSpecialMarker ? "邪教徒标记" : "毒绿色箭头");
                        }
                        if (botRoleLower.Contains("follower"))
                        {
                            arrowid = VulcanUtil.ConvertHashID("粉色箭头");
                        }
                        if (bot.Info.Side == "Savage")
                        {
                            switch (botRoleLower)
                            {
                                case "marksman":
                                case "bossboarsniper":
                                    arrowid = VulcanUtil.ConvertHashID(arrowcfg.EnableSpecialMarker ? "狙击标记" : "薄荷色箭头");
                                    break;
                                case "exusec":
                                case "pmcbot":
                                    arrowid = VulcanUtil.ConvertHashID(arrowcfg.EnableSpecialMarker ? "掠夺者标记" : "紫色箭头");
                                    break;
                                case "arenafighterevent":
                                    arrowid = VulcanUtil.ConvertHashID("藏蓝色箭头");
                                    break;
                                case "followerbigpipe":
                                case "followerbirdeye":
                                    arrowid = VulcanUtil.ConvertHashID(arrowcfg.EnableSpecialMarker ? "危标记" : "红色箭头");
                                    break;
                            }
                        }
                        else
                        {
                            switch (bot.Info.Side)
                            {
                                case "Bear":
                                    arrowid = VulcanUtil.ConvertHashID("橙色箭头");
                                    break;
                                case "Usec":
                                    arrowid = VulcanUtil.ConvertHashID("蓝色箭头");
                                    break;
                            }
                        }
                        ArrowMarker.AddMarkerToInventory(bot.Inventory, arrowid, arrowslot, logger);
                    }
                    */
                    // 为 Bot 生成 InventoryId
                    AccessTools.Method(typeof(BotGenerator), "GenerateInventoryId").Invoke(__instance, new object[] { bot });

                    // 如果有 EventRole，设置 Bot 的角色
                    if (botGenerationDetails.EventRole != null)
                    {
                        bot.Info.Settings.Role = botGenerationDetails.EventRole;
                    }

                    // 返回生成的 Bot 对象
                    __result = bot;
                }
                catch (Exception ex)
                {
                    logger.LogWithColor(ex.StackTrace, LogTextColor.Yellow, LogBackgroundColor.Default);
                }

                return false; // 跳过原始方法，直接使用修改后的逻辑
            }
        }
        public class AddDogtagToBotPatch : AbstractPatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(BotGenerator).GetMethod("AddDogtagToBot", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            }
            [PatchPrefix]
            public static bool Prefix(BotGenerator __instance, BotBase bot)
            {
                var logger = ServiceLocator.ServiceProvider.GetService<ISptLogger<BotGenerator>>();
                var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
                var bossList = new List<string>
                {
                    "bosstagilla",
                    "bosstagillaagro",
                    "bossbully",
                    "bossboar",
                    "bossgluhar",
                    "bosssanitar",
                    "bosskilla",
                    "bosskillaagro",
                    "bosskojaniy",
                    "bosszryachiy",
                    "bosskolontay",
                    "bossknight",
                    "followerbigpipe",
                    "followerbirdeye",
                    "bosspartisan"
                };
                var botRole = bot.Info.Settings.Role;
                var botRoleLower = botRole.ToLower();
                var botNickName = bot.Info.Nickname;
                var botName = "Nikita";
                Item dogTagItem = new Item
                {
                    Id = new MongoId(),
                    Template = "Scav狗牌".ConvertHashID()
                };
                try
                {
                    MongoId? equipment = bot.Inventory.Equipment;
                    dogTagItem.ParentId = (equipment.HasValue ? ((string)equipment.GetValueOrDefault()) : null);
                    dogTagItem.SlotId = "Dogtag";
                    dogTagItem.Upd = new Upd
                    {
                        SpawnedInSession = true,
                        Dogtag = new UpdDogtag()
                    };
                    var dogtag = "Scav狗牌".ConvertHashID();
                    if (bot.Info.Side != "Savage")
                    {
                        dogtag = (MongoId)AccessTools.Method(typeof(BotGenerator), "GetDogtagTplByGameVersionAndSide").Invoke(__instance, new object[] { bot.Info.Side, bot.Info.GameVersion });
                    }
                    else
                    {
                        if (bossList.Contains(botRoleLower))
                        {
                            switch (botRoleLower)
                            {
                                case "bossbully":
                                    botName = "Reshala";
                                    break;
                                case "bossboar":
                                    botName = "Kaban";
                                    break;
                                case "bosskojaniy":
                                    botName = "Shturman";
                                    break;
                                case "bossknight":
                                case "followerbigpipe":
                                case "followerbirdeye":
                                    botName = bot.Info.Nickname;
                                    break;
                                case "bossgluhar":
                                    botName = bot.Info.Nickname == "Ghroth" ? bot.Info.Nickname : botRole.Substring(4);
                                    break;
                                case "bosskolontay":
                                    botName = bot.Info.Nickname == "Punisher" ? bot.Info.Nickname : botRole.Substring(4);
                                    break;
                                case "bosskillaagro":
                                    botName = bot.Info.Nickname == "Obsidian" ? bot.Info.Nickname : "Killa";
                                    break;
                                case "bosstagillaagro":
                                    botName = "Tagilla"; //未来会有守门人
                                    break;
                                default:
                                    botName = botRole.Substring(4);
                                    break;
                            }
                            dogtag = "Boss狗牌".ConvertHashID();
                            dogTagItem.Upd.Dogtag.Nickname = botName;
                            dogTagItem.Upd.Dogtag.Side = SPTarkov.Server.Core.Models.Enums.DogtagSide.Bear;
                            dogTagItem.Upd.Dogtag.Level = 100;
                        }
                        else
                        {
                            botName = RussianToLatinApproximation(botNickName);
                            dogTagItem.Upd.Dogtag.Nickname = botName;
                            dogTagItem.Upd.Dogtag.Side = SPTarkov.Server.Core.Models.Enums.DogtagSide.Bear;
                            dogTagItem.Upd.Dogtag.Level = 80;
                        }
                    }
                    if (alterBossName.Contains(botNickName))
                    {
                        dogtag = "水晶狗牌_红".ConvertHashID();
                        dogTagItem.Upd.Dogtag.Nickname = botName;
                        dogTagItem.Upd.Dogtag.Side = SPTarkov.Server.Core.Models.Enums.DogtagSide.Bear;
                        dogTagItem.Upd.Dogtag.Level = 200;
                    }
                    if (botRoleLower.Contains("sectant"))
                    {
                        dogtag = "邪教徒狗牌".ConvertHashID();
                        dogTagItem.Upd.Dogtag.Nickname = botName;
                        dogTagItem.Upd.Dogtag.Side = SPTarkov.Server.Core.Models.Enums.DogtagSide.Usec;
                        dogTagItem.Upd.Dogtag.Level = 99;
                    }
                    dogTagItem.Template = dogtag;
                    Item item = dogTagItem;
                    bot.Inventory.Items.Add(item);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
                return false;
            }
        }
        public static string RussianToLatinApproximation(string russianString)
        {
            // 俄文字母到拉丁字母的映射表
            var russianToLatinMap = new Dictionary<char, string>
                {
                    {'а', "a"}, {'б', "b"}, {'в', "v"}, {'г', "g"}, {'д', "d"},
                    {'е', "e"}, {'ё', "yo"}, {'ж', "zh"}, {'з', "z"}, {'и', "i"},
                    {'й', "j"}, {'к', "k"}, {'л', "l"}, {'м', "m"}, {'н', "n"},
                    {'о', "o"}, {'п', "p"}, {'р', "r"}, {'с', "s"}, {'т', "t"},
                    {'у', "u"}, {'ф', "f"}, {'х', "h"}, {'ц', "c"}, {'ч', "ch"},
                    {'ш', "sh"}, {'щ', "sch"}, {'ъ', "j"}, {'ы', "i"}, {'ь', "j"},
                    {'э', "e"}, {'ю', "yu"}, {'я', "ya"},
                    {'А', "A"}, {'Б', "B"}, {'В', "V"}, {'Г', "G"}, {'Д', "D"},
                    {'Е', "E"}, {'Ё', "Yo"}, {'Ж', "Zh"}, {'З', "Z"}, {'И', "I"},
                    {'Й', "Y"}, {'К', "K"}, {'Л', "L"}, {'М', "M"}, {'Н', "N"},
                    {'О', "O"}, {'П', "P"}, {'Р', "R"}, {'С', "S"}, {'Т', "T"},
                    {'У', "U"}, {'Ф', "F"}, {'Х', "Kh"}, {'Ц', "Ts"}, {'Ч', "Ch"},
                    {'Ш', "Sh"}, {'Щ', "Sch"}, {'Э', "E"}, {'Ю', "Yu"}, {'Я', "Ya"}
                };

            // 如果传入字符串为空
            if (string.IsNullOrEmpty(russianString))
            {
                return "不知道发生了什么, 这个AI没有名字, 也许是尼基塔死了妈妈";
            }

            var latinString = new StringBuilder();

            foreach (var ch in russianString)
            {
                // 如果字符在映射表中，进行替换
                if (russianToLatinMap.ContainsKey(ch))
                {
                    latinString.Append(russianToLatinMap[ch]);
                }
                else
                {
                    // 如果字符不在映射表中，直接添加
                    latinString.Append(ch);
                }
            }

            return latinString.ToString();
        }
        public static void AddLootToInventory(BotBaseInventory inventory, MongoId itemid, string slot, BotBase bot, ContextManager.LoadModContext context)
        {
            //这个方法默认只支持1x1战利品, 因此不再做旋转检测
            var container = inventory.Items.FirstOrDefault(x => x.SlotId == slot);
            var haveitem = inventory.Items.FirstOrDefault(x => x.Template == itemid);
            var containerMap = new Dictionary<string, int[,]>();
            if (container == null || haveitem != null)
            {
                context.Logger.Warn($"错误: 无法在{bot.Info.Settings.Role}身上找到{slot}, 或者{itemid}已经存在");
                return;
            }
            var containerItem = ItemUtils.GetItem(container.Template, context);
            if (containerItem != null && containerItem.Properties != null && containerItem.Properties.Grids != null)
            {
                foreach (var grids in containerItem.Properties.Grids)
                {
                    if (grids.Properties == null || grids == null) continue;
                    containerMap.TryAdd(grids.Name, new int[(int)grids.Properties.CellsV, (int)grids.Properties.CellsH]);
                }
            }
            var itemList = inventory.Items.Where(x => x.ParentId != null && x.ParentId == container.Id).ToList();
            foreach (var item in itemList)
            {
                var itemTemplate = ItemUtils.GetItem(item.Template, context);
                if (itemTemplate == null) continue;
                //出门
                //防止思路断掉, 打个备注
                //接下来取物品大小覆盖到map上完成最终成果
                //回来了, 继续
                if (item.Location == null) continue;
                var location = (ItemLocation)item.Location;
                containerMap.TryGetValue(item.SlotId, out var map);
                if (map == null) continue;
                var itemwidth = location.R == ItemRotation.Horizontal ? itemTemplate.Properties.Width : itemTemplate.Properties.Height;
                var itemheight = location.R == ItemRotation.Horizontal ? itemTemplate.Properties.Height : itemTemplate.Properties.Width;
                var itemlocationx = (int)location.X;
                var itemlocationy = (int)location.Y;
                for (int x = itemlocationy; x < itemlocationy + itemheight; x++)
                {
                    for (int y = itemlocationx; y < itemlocationx + itemwidth; y++)
                    {
                        // 确保不会超出 map 的边界
                        if (x >= 0 && x < map.GetLength(0) && y >= 0 && y < map.GetLength(1))
                        {
                            map[x, y] = 1; // 标记为已占用
                        }
                    }
                }
            }
            var targetlocation = FindPlacementSpace(containerMap, context);
            if (targetlocation == null)
            {
                context.Logger.Warn($"错误: 无法在{bot.Info.Settings.Role}的{slot}上找到有效空间");
                return;
            }
            context.Logger.Warn($"成功在{bot.Info.Settings.Role}的{slot}添加了{itemid}");
            inventory.Items.Add(new Item
            {
                Id = new MongoId(),
                Template = itemid,
                ParentId = container.Id,
                SlotId = targetlocation.Value.Key,
                Upd = new Upd
                {
                    SpawnedInSession = true,
                    StackObjectsCount = 1
                },
                Location = targetlocation.Value.Value
            });
        }
        public static KeyValuePair<string, ItemLocation>? FindPlacementSpace(Dictionary<string, int[,]> containerMap, ContextManager.LoadModContext context)
        {
            foreach (var container in containerMap)
            {
                var map = container.Value;
                for (int x = 0; x < map.GetLength(0); x++)
                {
                    for (int y = 0; y < map.GetLength(1); y++)
                    {
                        if (map[x, y] == 0)
                        {
                            context.Logger.Info($"坐标查找成功");
                            return new KeyValuePair<string, ItemLocation>(container.Key, new ItemLocation
                            {
                                X = y,
                                Y = x,
                                R = ItemRotation.Horizontal  // 默认横向放置，物品是 1x1 所以旋转无意义
                            });
                        }
                    }
                }
            }
            context.Logger.Warn($"错误: 空间已满");
            // 如果没有找到空格子，则返回 null
            return null;
        }
        public class AlterGoonsCounter
        {
            public int Chance { get; set; }
            public int KnightCounter { get; set; }
            public int PipeCounter { get; set; }
            public int EyesCounter { get; set; }
            public bool Access { get; set; }
            public bool KnightAccess { get; set; }
            public bool PipeAccess { get; set; }
            public bool EyesAccess { get; set; }
        }
        public class AlterBossCounter
        {
            public int Chance { get; set; }
            public int Counter { get; set; }
            public bool Access { get; set; }
        }

        //rework
        public class BotGeneratorPatch_GenerateBot : AbstractPatch
        {
            protected override MethodBase GetTargetMethod()
            {
                return typeof(BotGenerator).GetMethod("GenerateBot", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            }
            [PatchPrefix]

            public static bool Prefix(BotGenerator __instance, MongoId sessionId, BotBase bot, ref BotType botJsonTemplate, BotGenerationDetails botGenerationDetails, BotBase __result)
            {

                var botLevelGenerator = ServiceLocator.ServiceProvider.GetService<BotLevelGenerator>();
                var botEquipmentFilterService = ServiceLocator.ServiceProvider.GetService<BotEquipmentFilterService>();
                var botNameService = ServiceLocator.ServiceProvider.GetService<BotNameService>();
                var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
                var seasonalEventService = ServiceLocator.ServiceProvider.GetService<SeasonalEventService>();
                var weightedRandomHelper = ServiceLocator.ServiceProvider.GetService<WeightedRandomHelper>();
                var botInventoryGenerator = ServiceLocator.ServiceProvider.GetService<BotInventoryGenerator>();
                var modHelper = ServiceLocator.ServiceProvider.GetService<ModHelper>();
                var configServer = ServiceLocator.ServiceProvider.GetService<ConfigServer>();
                var cloner = ServiceLocator.ServiceProvider.GetService<ICloner>();
                var botConfig = configServer.GetConfig<BotConfig>();

                var logger = new ECLogger("Generator", true);

                var botRoleLowercase = botGenerationDetails.Role.ToLowerInvariant();

                //logger.Success($"Generating Bot....");

                //logger.Success($"Correct Location: {botGenerationDetails.Location}");

                //logger.Info($"Bot ID : {bot.Id}");

                BotGeneratorUtils.AlterBotDictionarys.TryGetValue(botRoleLowercase, out var alterBots);

                //这里应该预留给Goons的分支的, 先跑通再说
                
                //rnm, 这里必须想办法完成线程隔离....

                //我想想
                //那就得串并计数器
                //也不对, 我不知道哪个是第五次....
                //这咋改啊?

                //不对, 不对不对不对....
                //草啊这里为什么是这样调用的呢???
                try
                {
                    if (alterBots == null || alterBots.Count == 0)
                    {
                        //logger.Warn($"类型{botRoleLowercase}没有匹配的转化可能性");
                        return true;
                    }
                    ;
                    AlterBotCounters.TryGetValue(botRoleLowercase, out var botCounter);
                    if (botCounter == null)
                    {
                        botCounter = new AlterBotCounter()
                        {
                            Data = null,
                            CorrectChance = 0,
                            Chance = 0,
                            Counter = 0,
                            Locations = null,
                            Access = false
                        };

                        AlterBotCounters[botRoleLowercase] = botCounter;
                    }
                    if(botCounter.Counter == 0)
                    {
                        var alterBot = Utils.DrawFromList<CustomAlterBot>(alterBots);
                        if (alterBot.BotType == null)
                        {
                            return true;
                        }
                        botCounter.Data = alterBot;
                        botCounter.Chance = alterBot.Chance;
                        botCounter.Locations = BitMapUtils.GetLocationCode(alterBot.SpawnLocation);
                    }
                    if (botCounter.CorrectChance == 0)
                    {
                        botCounter.CorrectChance = (int)Math.Floor(new Random().NextDouble() * 100);
                    }
                    if (botCounter.CorrectChance <= botCounter.Chance)
                    {
                        botCounter.Access = true;
                    }
                    if (botCounter.Access && botCounter.Locations.Contains(botGenerationDetails.Location))
                    {
                        //logger.Info("尝试替换Boss");
                        //数据覆盖
                        botJsonTemplate.BotAppearance = botCounter.Data.BotType.BotAppearance;
                        botJsonTemplate.BotExperience = botCounter.Data.BotType.BotExperience;
                        botJsonTemplate.BotHealth = botCounter.Data.BotType.BotHealth;
                        botJsonTemplate.BotSkills = botCounter.Data.BotType.BotSkills;
                        botJsonTemplate.BotInventory = botCounter.Data.BotType.BotInventory;
                        botJsonTemplate.BotChances = botCounter.Data.BotType.BotChances;
                        botJsonTemplate.FirstNames = botCounter.Data.BotType.FirstNames;
                        botJsonTemplate.LastNames = botCounter.Data.BotType.LastNames;
                        botJsonTemplate.BotGeneration = botCounter.Data.BotType.BotGeneration;
                    }
                    botCounter.Counter++;
                    if (botCounter.Counter >= 5)
                    {
                        botCounter.CorrectChance = 0;
                        botCounter.Counter = 0;
                        botCounter.Access = false;
                        botCounter.Data = null;
                        botCounter.Locations = null;
                    }
                    //这样不知道为什么会漏
                    //再想想办法吧
                    //算了, 就这样吧, 累了
                }
                catch (Exception ex)
                {
                    //logger.Error("Bot转化失败", ex);
                }
                return true;
            }

            public static Dictionary<string, AlterBotCounter> AlterBotCounters = new();

            public class AlterBotCounter
            {
                public int Chance { get; set; }

                public int CorrectChance { get; set; }

                public int Counter { get; set; }

                public bool Access { get; set; }

                public List<string> Locations { get; set; }

                public CustomAlterBot Data { get; set; }
            }
        }
    }
}