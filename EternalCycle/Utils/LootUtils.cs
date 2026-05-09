using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System.Text.Json;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services.Mod;
using System.Reflection;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Logger;
using SPTarkov.Server.Core.Utils.Json;
using Microsoft.Extensions.Logging;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils;
using Path = System.IO.Path;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Routers;
using System.IO;
using SPTarkov.Server.Core.Models.Spt.Templates;
namespace VulcanCore;
public class LootUtils
{
    public static void AddStaticLoot(CustomItemTemplate template, DatabaseService databaseService, ISptLogger<VulcanCore> logger)
    {
        if (template.CustomProps is LootableItemProps lootableItemProps)
        {
            var getedlocations = databaseService.GetLocations();
            var locations = new List<Location> {
                getedlocations.Bigmap,
                getedlocations.Woods,
                getedlocations.Factory4Day,
                getedlocations.Factory4Night,
                getedlocations.Laboratory,
                getedlocations.Shoreline,
                getedlocations.RezervBase,
                getedlocations.Interchange,
                getedlocations.Lighthouse,
                getedlocations.TarkovStreets,
                getedlocations.Sandbox,
                getedlocations.SandboxHigh,
                getedlocations.Labyrinth
            };
            MongoId targetid = lootableItemProps.UseCustomData == true && lootableItemProps.StaticLoot == true ? lootableItemProps.CustomStaticLootTarget : template.TargetId;
            MongoId addedid = VulcanUtil.ConvertHashID(template.Id);
            float relative = lootableItemProps.UseCustomData == true && lootableItemProps.StaticLoot == true ? lootableItemProps.StaticLootDivisor : 2;
            //默认生成或仅在StaticLoot为true时生成
            if (lootableItemProps.CanFindInRaid)
            {
                if (lootableItemProps.StaticLoot == null || (lootableItemProps.StaticLoot != null && lootableItemProps.StaticLoot == true))
                {
                    foreach (var location in locations)
                    {
                        //VulcanLog.Debug($"初始化战利品生成流程", logger);
                        //VulcanLog.Debug($"尝试生成战利品: {lootableItemProps.Name}", logger);
                        location.StaticLoot.AddTransformer(delegate (Dictionary<MongoId, StaticLootDetails> staticloot)
                        {
                            foreach (var loot in staticloot)
                            {
                                var lootlist = loot.Value.ItemDistribution.ToList();
                                var loottarget = lootlist.FirstOrDefault(l => l.Tpl == targetid);
                                if (loottarget != null)
                                {
                                    //VulcanLog.Debug($"发现 {lootableItemProps.Name} 匹配的目标: {targetid}", logger);
                                    lootlist.Add(new ItemDistribution
                                    {
                                        Tpl = addedid,
                                        RelativeProbability = (float)loottarget.RelativeProbability / relative
                                    });
                                }
                                //VulcanLog.Access($"战利品添加成功: {lootableItemProps.Name}", logger);
                                loot.Value.ItemDistribution = lootlist;
                            }
                            return staticloot;
                        });
                    }
                }
            }
        }
    }
    public static void AddLooseLoot(CustomItemTemplate template, DatabaseService databaseService, ISptLogger<VulcanCore> logger)
    {
        if (template.CustomProps is LootableItemProps lootableItemProps)
        {
            var getedlocations = databaseService.GetLocations();
            var locations = new List<Location> {
                getedlocations.Bigmap,
                getedlocations.Woods,
                getedlocations.Factory4Day,
                getedlocations.Factory4Night,
                getedlocations.Laboratory,
                getedlocations.Shoreline,
                getedlocations.RezervBase,
                getedlocations.Interchange,
                getedlocations.Lighthouse,
                getedlocations.TarkovStreets,
                getedlocations.Sandbox,
                getedlocations.SandboxHigh,
                getedlocations.Labyrinth
            };
            MongoId targetid = lootableItemProps.UseCustomData == true && lootableItemProps.MapLoot == true ? lootableItemProps.CustomMapLootTarget : template.TargetId;
            MongoId addedid = VulcanUtil.ConvertHashID(template.Id);
            float relative = lootableItemProps.UseCustomData == true && lootableItemProps.MapLoot == true ? lootableItemProps.MapLootDivisor : 4;
            if (lootableItemProps.CanFindInRaid)
            {
                if (lootableItemProps.MapLoot == null || (lootableItemProps.MapLoot != null && lootableItemProps.MapLoot == true))
                {
                    foreach (var location in locations)
                    {
                        //VulcanLog.Debug($"初始化战利品生成流程", logger);
                        //VulcanLog.Debug($"尝试生成战利品: {lootableItemProps.Name}", logger);
                        location.LooseLoot.AddTransformer(delegate (LooseLoot looseloot)
                        {
                            var lootlist = looseloot.Spawnpoints.ToList();
                            foreach (var spawnpoint in lootlist)
                            {
                                var itemlist = spawnpoint.Template.Items.ToList();
                                var distlist = spawnpoint.ItemDistribution.ToList();
                                var loottarget = itemlist.FirstOrDefault(i => i.Template == targetid);
                                if (loottarget != null)
                                {
                                    var lootkey = loottarget.ComposedKey;
                                    var targetkey = $"{addedid}_{loottarget.Id}";
                                    var lootid = (MongoId)VulcanUtil.ConvertHashID(targetkey);
                                    //VulcanLog.Debug($"发现 {lootableItemProps.Name} 匹配的目标: {targetid}", logger);
                                    var disttarget = distlist.FirstOrDefault(i => i.ComposedKey.Key == lootkey);
                                    if (disttarget != null)
                                    {
                                        //VulcanLog.Debug($"目标匹配成功", logger);
                                        itemlist.Add(new SptLootItem
                                        {
                                            ComposedKey = targetkey,
                                            Id = lootid,
                                            Template = addedid
                                        });
                                        distlist.Add(new LooseLootItemDistribution
                                        {
                                            ComposedKey = new ComposedKey
                                            {
                                                Key = targetkey
                                            },
                                            RelativeProbability = (float)disttarget.RelativeProbability / relative
                                        });
                                    }
                                    //VulcanLog.Access($"战利品添加成功: {lootableItemProps.Name}", logger);
                                }
                                spawnpoint.Template.Items = itemlist;
                                spawnpoint.ItemDistribution = distlist;
                            }
                            return looseloot;
                        });
                    }
                }
            }
        }
    }
    public static void AddPresetLoot(List<Item> itemPreset, MongoId targetid, DatabaseService databaseService, ICloner cloner, ISptLogger<VulcanCore> logger)
    {
        var getedlocations = databaseService.GetLocations();
        var locations = new List<Location> {
                getedlocations.Bigmap,
                getedlocations.Woods,
                getedlocations.Factory4Day,
                getedlocations.Factory4Night,
                getedlocations.Laboratory,
                getedlocations.Shoreline,
                getedlocations.RezervBase,
                getedlocations.Interchange,
                getedlocations.Lighthouse,
                getedlocations.TarkovStreets,
                getedlocations.Sandbox,
                getedlocations.SandboxHigh
            };
        foreach (var location in locations)
        {
            //VulcanLog.Debug($"尝试生成战利品: {lootableItemProps.Name}", logger);
            location.LooseLoot.AddTransformer(delegate (LooseLoot looseloot)
            {
                var lootlist = looseloot.Spawnpoints.ToList();
                float relative = 1f;
                foreach (var spawnpoint in lootlist)
                {
                    var itemlist = spawnpoint.Template.Items.ToList();
                    var distlist = spawnpoint.ItemDistribution.ToList();
                    var loottarget = itemlist.FirstOrDefault(i => i.Template == targetid);
                    if (loottarget != null)
                    {
                        var lootkey = loottarget.ComposedKey;
                        var targetkey = $"{lootkey}_{loottarget.Id}";
                        List<Item> presetlist = ItemUtils.RegenerateItemListData(itemPreset, targetkey, cloner);
                        var lootid = (MongoId)VulcanUtil.ConvertHashID(targetkey);
                        VulcanLog.Debug($"发现匹配的目标: {targetid}", logger);
                        var disttarget = distlist.FirstOrDefault(i => i.ComposedKey.Key == lootkey);
                        if (disttarget != null)
                        {
                            //var newitem = (SptLootItem)presetlist[0];
                            var newitem = new SptLootItem
                            {
                                Id = presetlist[0].Id,
                                Template = presetlist[0].Template,
                                Upd = presetlist[0].Upd,
                                ComposedKey = targetkey
                            };
                            itemlist.Add(newitem);
                            for (var i = 1; i < presetlist.Count; i++)
                            {
                                //var convertitem = (SptLootItem)presetlist[i]; 
                                var convertitem = new SptLootItem
                                {
                                    Id = presetlist[i].Id,
                                    Template = presetlist[i].Template,
                                    ParentId = presetlist[i].ParentId,
                                    SlotId = presetlist[i].SlotId,
                                    Upd = presetlist[i].Upd
                                };
                                itemlist.Add(convertitem);
                            }
                            //VulcanLog.Debug($"目标匹配成功", logger);
                            distlist.Add(new LooseLootItemDistribution
                            {
                                ComposedKey = new ComposedKey
                                {
                                    Key = targetkey
                                },
                                RelativeProbability = (float)disttarget.RelativeProbability / relative
                            });
                            //VulcanLog.Access($"战利品添加成功: {lootableItemProps.Name}", logger);
                        }
                    }
                    spawnpoint.Template.Items = itemlist;
                    spawnpoint.ItemDistribution = distlist;
                }
                return looseloot;
            });
        }
    }
}










