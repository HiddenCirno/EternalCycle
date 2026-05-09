using HarmonyLib;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Razor.TagHelpers;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Constants;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Generators;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Bot;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Bots;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System;
using System.Net;
using System.Reflection;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Text.Json;
using VulcanCore;

namespace VulcanCore
{
    public class ReplaceFleaBasePricesPatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(RagfairPriceService).GetMethod("ReplaceFleaBasePrices", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }
        [PatchPrefix]
        public static bool Prefix(RagfairPriceService __instance)
        {
            var databaseService = ServiceLocator.ServiceProvider.GetService<DatabaseService>();
            var databaseServer = ServiceLocator.ServiceProvider.GetService<DatabaseServer>();
            var logger = ServiceLocator.ServiceProvider.GetService<ISptLogger<VulcanCore>>();
            var configServer = ServiceLocator.ServiceProvider.GetService<ConfigServer>();
            var itemHelper = ServiceLocator.ServiceProvider.GetService<ItemHelper>();
            var inventoryHelper = ServiceLocator.ServiceProvider.GetService<InventoryHelper>();
            var profileHelper = ServiceLocator.ServiceProvider.GetService<ProfileHelper>();
            var traderHelper = ServiceLocator.ServiceProvider.GetService<TraderHelper>();
            var lootGenerator = ServiceLocator.ServiceProvider.GetService<LootGenerator>();
            var cloner = ServiceLocator.ServiceProvider.GetService<ICloner>();
            var ragfiarConfig = configServer.GetConfig<RagfairConfig>();
            //
            var config = ragfiarConfig.Dynamic.GenerateBaseFleaPrices;
            var pricePool = databaseServer.GetTables().Templates.Prices;
            var hideoutCraftItems = AccessTools.Method(typeof(RagfairPriceService), "GetHideoutCraftItemTpls")
                        .Invoke(__instance, new object[] { });

            //__instance.GetHideoutCraftItemTpls();
            var staticprices = typeof(RagfairPriceService).GetField("StaticPrices", BindingFlags.NonPublic | BindingFlags.Instance);
            var staticPrices = staticprices.GetValue(__instance) as Dictionary<MongoId, double>;
            foreach (var (itemTpl, handbookPrice) in staticPrices)
            {
                // Get new price to use
                //var newBasePrice =
                //    handbookPrice
                //    * (__instance.GetFleaBasePriceMultiplier(itemTpl, config) + __instance.GetHideoutCraftMultiplier(itemTpl, config, hideoutCraftItems));
                var newBasePrice =
                    handbookPrice
                    * ((double)AccessTools.Method(typeof(RagfairPriceService), "GetFleaBasePriceMultiplier")
                        .Invoke(__instance, new object[] { itemTpl, config })
                        + (double)AccessTools.Method(typeof(RagfairPriceService), "GetHideoutCraftMultiplier")
                        .Invoke(__instance, new object[] { itemTpl, config, hideoutCraftItems }));
                if (newBasePrice == 0)
                {
                    continue;
                }

                if (config.PreventPriceBeingBelowTraderBuyPrice)
                {
                    // Check if item can be sold to trader for a higher price than what we're going to set
                    var highestSellToTraderPrice = traderHelper.GetHighestSellToTraderPrice(itemTpl);
                    if (highestSellToTraderPrice > newBasePrice)
                    {
                        // Trader has higher sell price, use that value
                        newBasePrice = highestSellToTraderPrice;
                    }
                }
                if (!pricePool.ContainsKey(itemTpl))
                {
                    pricePool.Add(itemTpl, newBasePrice);
                }
            }
            return false;
        }
    }
}