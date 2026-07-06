using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Hideout;
using static EternalCycle.ContextManager;
using Path = System.IO.Path;

namespace EternalCycle
{
    public class RecipeUtils
    {
        /// <summary>
        /// 将自定义配方注册到加载事件
        /// </summary>
        /// <param name="path">指定的存放配方文件的路径或完整的配方文件路径</param>
        /// <param name="creator">创建者</param>
        /// <param name="modname">Mod名</param>
        public static void RegisterRecipe(string path)
        {
            // 文件夹加载模式
            if (Directory.Exists(path))
            {
                // 假设事件回调中的 context 已经是 ContextManager.LoadModContext 类型
                EventManager.DataLoadEvent.LoadRecipeEvent += (context) =>
                {
                    try
                    {
                        InitRecipeData(path, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册配方时发生错误：指定的文件夹 {path} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(path))
            {
                EventManager.DataLoadEvent.LoadRecipeEvent += (context) =>
                {
                    try
                    {
                        var recipeData = context.JsonUtil.Deserialize<Dictionary<string, CustomRecipeData>>(File.ReadAllText(path));
                        InitRecipeData(recipeData, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册配方时发生错误：指定的文件 {path} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册配方时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        /// <summary>
        /// Init重载 1：处理文件夹路径，读取单体对象
        /// </summary>
        public static void InitRecipeData(string folderpath, LoadModContext context)
        {
            if (!Directory.Exists(folderpath)) return;

            List<string> files = Directory.GetFiles(folderpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var recipe = context.ModHelper.GetJsonDataFromFile<CustomRecipeData>(folderpath, fileName);

                    if (recipe != null)
                    {
                        InitRecipeData(recipe, context);
                    }
                }
            }
        }

        /// <summary>
        /// Init重载 2：处理反序列化好的字典
        /// </summary>
        public static void InitRecipeData(Dictionary<string, CustomRecipeData> recipeData, ContextManager.LoadModContext context)
        {
            if (recipeData == null || recipeData.Count == 0) return;

            foreach (CustomRecipeData recipe in recipeData.Values)
            {
                if (recipe != null)
                {
                    InitRecipeData(recipe, context);
                }
            }
        }

        /// <summary>
        /// 核心路由：根据配方类型进行分发处理
        /// </summary>
        private static void InitRecipeData(CustomRecipeData recipe, ContextManager.LoadModContext context)
        {
            switch (recipe)
            {
                case CustomNormalRecipeData customNormalRecipeData:
                    {
                        InitRecipe(recipe, context);
                    }
                    break;

                case CustomLockedRecipeData customLockedRecipeData:
                    {
                        var recipeUnlockRewardData = new CustomRecipeUnlockRewardData
                        {
                            Id = (MongoId)Utils.ConvertHashID($"{customLockedRecipeData.Id}_Locked"),
                            QuestId = (MongoId)Utils.ConvertHashID(customLockedRecipeData.QuestId),
                            QuestStage = customLockedRecipeData.QuestStage,
                            IsUnknownReward = customLockedRecipeData.IsUnknownReward,
                            RecipeData = customLockedRecipeData,
                        };
                        QuestUtils.InitRecipeUnlockRewards(recipeUnlockRewardData, context);
                    }
                    break;
            }
        }

        public static void InitRecipe(CustomRecipeData recipeData, ContextManager.LoadModContext context)
        {
            var recipes = context.DB.GetHideout().Production.Recipes;
            var recipe = new HideoutProduction
            {
                Id = recipeData.Id,
                AreaType = recipeData.AreaType,
                Requirements = new List<Requirement>(),
                ProductionTime = recipeData.Time,
                NeedFuelForAllProductionTime = recipeData.NeedFuel,
                Locked = false,
                EndProduct = recipeData.Output,
                Continuous = false,
                Count = recipeData.OutputCount,
                ProductionLimitCount = 0,
                IsEncoded = false
            };
            if (recipeData.IsEncoded == true)
            {
                recipe.IsEncoded = true;
            }
            foreach (var item in recipeData.Require.ToolsRequire)
            {
                recipe.Requirements.Add(new Requirement
                {
                    TemplateId = Utils.ConvertHashID(item.Key),
                    Type = "Tool"
                });
            }
            foreach (var item in recipeData.Require.ItemsRequire)
            {
                recipe.Requirements.Add(new Requirement
                {
                    TemplateId = Utils.ConvertHashID(item.Key),
                    Count = item.Value,
                    IsFunctional = false,
                    IsEncoded = false,
                    Type = "Item"
                });
            }
            recipe.Requirements.Add(new Requirement
            {
                AreaType = (int)recipeData.AreaType,
                RequiredLevel = recipeData.AreaLevel,
                Type = "Area"
            });
            if (recipeData is CustomLockedRecipeData lockedRecipeData)
            {
                recipe.Locked = true;
                recipe.Requirements.Add(new Requirement
                {
                    QuestId = lockedRecipeData.QuestId,
                    Type = "QuestComplete"
                });
            }
            //忘了加任务条件了草
            //got it
            recipes.Add(recipe);
        }

        /// <summary>
        /// 将自定义 Scav 宝箱配方注册到加载事件
        /// </summary>
        /// <param name="path">指定的存放 Scav 配方文件的路径</param>
        public static void RegisterScavCaseRecipe(string path)
        {
            if (Directory.Exists(path))
            {
                EventManager.DataLoadEvent.LoadScavCaseRecipeEvent += (context) =>
                {
                    try
                    {
                        InitScavCaseRecipeData(path, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册 Scav 配方时发生错误：指定的文件夹 {path} 存在问题", ex);
                    }
                };
            }
            else if (File.Exists(path))
            {
                EventManager.DataLoadEvent.LoadScavCaseRecipeEvent += (context) =>
                {
                    try
                    {
                        var recipeData = context.JsonUtil.Deserialize<Dictionary<string, CustomScavCaseRecipeData>>(File.ReadAllText(path));
                        InitScavCaseRecipeData(recipeData, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册 Scav 配方时发生错误：指定的文件 {path} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册 Scav 配方时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        public static void InitScavCaseRecipeData(Dictionary<string, CustomScavCaseRecipeData> recipeData, ContextManager.LoadModContext context)
        {
            foreach (CustomScavCaseRecipeData customScavCaseRecipeData in recipeData.Values)
            {
                InitScavCaseRecipe(customScavCaseRecipeData, context);
            }
        }

        public static void InitScavCaseRecipeData(string folderpath, ContextManager.LoadModContext context)
        {
            List<string> files = Directory.GetFiles(folderpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var scavcase = context.ModHelper.GetJsonDataFromFile<CustomScavCaseRecipeData>(folderpath, fileName);
                    InitScavCaseRecipe(scavcase, context);
                }
            }
        }

        public static void InitScavCaseRecipe(CustomScavCaseRecipeData recipeData, ContextManager.LoadModContext context)
        {
            var recipes = context.DB.GetHideout().Production.ScavRecipes;
            var recipe = new ScavRecipe
            {
                Id = Utils.ConvertHashID(recipeData.Id),
                ProductionTime = recipeData.Time,
                Requirements = new List<Requirement>(),
                EndProducts = new EndProducts
                {
                    Common = new MinMax<int>
                    {
                        Min = recipeData.Reward.Common[0],
                        Max = recipeData.Reward.Common[1]
                    },
                    Rare = new MinMax<int>
                    {
                        Min = recipeData.Reward.Rare[0],
                        Max = recipeData.Reward.Rare[1]
                    },
                    Superrare = new MinMax<int>
                    {
                        Min = recipeData.Reward.SuperRare[0],
                        Max = recipeData.Reward.SuperRare[1]
                    }
                }
            };
            foreach (var item in recipeData.Requirement)
            {
                recipe.Requirements.Add(new Requirement
                {
                    TemplateId = Utils.ConvertHashID(item.Key),
                    Count = item.Value,
                    IsFunctional = false,
                    IsEncoded = false,
                    Type = "Item"
                });
            }
            recipes.Add(recipe);
        }
    }
}