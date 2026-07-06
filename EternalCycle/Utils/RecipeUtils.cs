using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Hideout;
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
namespace EternalCycle;
public class RecipeUtils
{
    public static void InitRecipeData(Dictionary<string, CustomRecipeData> recipeData, ContextManager.LoadModContext context)
    {
        foreach (CustomRecipeData recipe in recipeData.Values)
        {
            switch (recipe)
            {
                case CustomNormalRecipeData customNormalRecipeData:
                    {

                        InitRecipe(recipe, context);
                    }
                    ;
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
    }
    public static void InitRecipeData(string folderpath, ContextManager.LoadModContext context)
    {
        List<string> files = Directory.GetFiles(folderpath).ToList();
        if (files.Count > 0)
        {
            foreach (var file in files)
            {
                string fileName = Path.GetFileName(file);
                var recipe = context.ModHelper.GetJsonDataFromFile<CustomRecipeData>(folderpath, fileName);
                InitRecipe(recipe, context);
            }
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