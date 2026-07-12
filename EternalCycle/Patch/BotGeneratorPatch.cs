using HarmonyLib;
using Microsoft.AspNetCore.Razor.TagHelpers;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System.Reflection;
using System.Text;

namespace EternalCycleServer
{
    public class BotGeneratorPatch
    {
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

                var jsonUtil = ServiceLocator.ServiceProvider.GetService<JsonUtil>();
                var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
                var configServer = ServiceLocator.ServiceProvider.GetService<ConfigServer>();
                var modHelper = ServiceLocator.ServiceProvider.GetService<ModHelper>();
                var itemHelper = ServiceLocator.ServiceProvider.GetService<ItemHelper>();
                var cloner = ServiceLocator.ServiceProvider.GetService<ICloner>();
                var localeService = ServiceLocator.ServiceProvider.GetService<LocaleService>();
                var presetHelper = ServiceLocator.ServiceProvider.GetService<PresetHelper>();
                var imageRouter = ServiceLocator.ServiceProvider.GetService<ImageRouter>();

                var logger = new ECLogger("Generator", true);
                var context = new ContextManager.LoadModContext
                {
                    DB = databaseService,
                    JsonUtil = jsonUtil,
                    ConfigServer = configServer,
                    ModHelper = modHelper,
                    Logger = Utils.commonLogger,
                    PresetHelper = presetHelper,
                    ImageRouter = imageRouter,
                    ItemHelper = itemHelper,
                    Cloner = cloner
                };

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
                EventManager.OnPreBotGenerateEvent?.Invoke(bot, botJsonTemplate, botGenerationDetails, context);
                return true;
            }

            [PatchPostfix]
            public static void Postfix(BotGenerator __instance, MongoId sessionId, BotBase bot, BotType botJsonTemplate, BotGenerationDetails botGenerationDetails, ref BotBase __result)
            {

                var jsonUtil = ServiceLocator.ServiceProvider.GetService<JsonUtil>();
                var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
                var configServer = ServiceLocator.ServiceProvider.GetService<ConfigServer>();
                var modHelper = ServiceLocator.ServiceProvider.GetService<ModHelper>();
                var itemHelper = ServiceLocator.ServiceProvider.GetService<ItemHelper>();
                var presetHelper = ServiceLocator.ServiceProvider.GetService<PresetHelper>();
                var cloner = ServiceLocator.ServiceProvider.GetService<ICloner>();
                var localeService = ServiceLocator.ServiceProvider.GetService<LocaleService>();
                var imageRouter = ServiceLocator.ServiceProvider.GetService<ImageRouter>();

                var logger = new ECLogger("Generator", true);
                var context = new ContextManager.LoadModContext
                {
                    DB = databaseService,
                    JsonUtil = jsonUtil,
                    ConfigServer = configServer,
                    ModHelper = modHelper,
                    Logger = Utils.commonLogger,
                    ImageRouter = imageRouter,
                    PresetHelper = presetHelper,
                    ItemHelper = itemHelper,
                    Cloner = cloner
                };

                EventManager.OnPreBotGenerateEvent?.Invoke(__result, botJsonTemplate, botGenerationDetails, context);
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