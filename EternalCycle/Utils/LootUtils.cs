using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using static EternalCycle.ContextManager;

namespace EternalCycle
{
    /// <summary>
    /// 对战利品生成进行操作处理的工具类
    /// </summary>
    public static class LootUtils
    {
        /// <summary>
        /// 统一获取地图引用
        /// </summary>
        private static IEnumerable<Location> GetValidLocations(LoadModContext context)
        {
            //直接调用SPT内部的字典方法
            return context.DB.GetLocations()
                .GetDictionary()
                .Values
                .Where(loc => loc != null);
        }

        /// <summary>
        /// 为自定义物品添加静态战利品生成
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="context">上下文实例</param>
        /// <returns>自定义物品对象</returns>
        public static CustomItemTemplate AddStaticLoot(this CustomItemTemplate template, LoadModContext context)
        {
            if (template.CustomProps is not LootableItemProps lootableItemProps || !lootableItemProps.CanFindInRaid) return template;
            if (lootableItemProps.StaticLoot == false) return template;
            MongoId targetid = (lootableItemProps.UseCustomData == true && lootableItemProps.StaticLoot == true)
             ? lootableItemProps.CustomStaticLootTarget
             : template.TargetId;
            MongoId addedid = template.Id.ConvertHashID();
            float relative = (lootableItemProps.UseCustomData == true && lootableItemProps.StaticLoot == true)
             ? lootableItemProps.StaticLootDivisor
             : 2f;
            //默认生成或仅在StaticLoot为true时生成
            foreach (var location in GetValidLocations(context))
            {
                if (location.StaticLoot == null) continue;
                //VulcanLog.Debug($"初始化战利品生成流程", logger);
                //VulcanLog.Debug($"尝试生成战利品: {lootableItemProps.Name}", logger);
                location.StaticLoot.AddTransformer(staticlootDict =>
                {
                    foreach (var loot in staticlootDict.Values)
                    {
                        //防止重复战利品
                        if (loot.ItemDistribution.Any(d => d.Tpl == addedid)) continue;
                        //判断目标
                        var loottarget = loot.ItemDistribution.FirstOrDefault(l => l.Tpl == targetid);
                        if (loottarget != null)
                        {
                            //用工具类避免GC问题
                            loot.ItemDistribution = Utils.AddToArray(loot.ItemDistribution.ToArray(), new ItemDistribution
                            {
                                Tpl = addedid,
                                RelativeProbability = loottarget.RelativeProbability / relative
                            });
                        }
                    }
                    return staticlootDict;
                });
            }
            return template;
        }

        /// <summary>
        /// 为自定义物品添加动态战利品生成
        /// </summary>
        /// <param name="template">自定义物品对象</param>
        /// <param name="context">上下文实例</param>
        /// <returns>自定义物品对象</returns>
        public static CustomItemTemplate AddLooseLoot(this CustomItemTemplate template, LoadModContext context)
        {
            if (template.CustomProps is not LootableItemProps lootableItemProps || !lootableItemProps.CanFindInRaid) return template;
            if (lootableItemProps.MapLoot == false) return template;

            MongoId targetid = (lootableItemProps.UseCustomData == true && lootableItemProps.MapLoot == true)
                ? lootableItemProps.CustomMapLootTarget
                : template.TargetId;

            MongoId addedid = template.Id.ConvertHashID();
            float relative = (lootableItemProps.UseCustomData == true && lootableItemProps.MapLoot == true)
                ? lootableItemProps.MapLootDivisor
                : 4f;
            foreach (var location in GetValidLocations(context))
            {
                //忘了防御....
                if (location.LooseLoot == null) continue;
                location.LooseLoot.AddTransformer(looseloot =>
                {
                    foreach (var spawnpoint in looseloot.Spawnpoints)
                    {
                        if (spawnpoint.Template?.Items == null || spawnpoint.ItemDistribution == null) continue;
                        //重复检查
                        if (spawnpoint.Template.Items.Any(i => i.Template == addedid)) continue;

                        var loottarget = spawnpoint.Template.Items.FirstOrDefault(i => i.Template == targetid);
                        if (loottarget != null)
                        {
                            var targetkey = $"{addedid}_{loottarget.Id}";
                            var lootid = targetkey.ConvertHashID();

                            var disttarget = spawnpoint.ItemDistribution.FirstOrDefault(i => i.ComposedKey.Key == loottarget.ComposedKey);
                            if (disttarget != null)
                            {
                                //双数组添加物品刷新
                                spawnpoint.Template.Items = Utils.AddToArray(spawnpoint.Template.Items.ToArray(), new SptLootItem
                                {
                                    ComposedKey = targetkey,
                                    Id = lootid,
                                    Template = addedid
                                });
                                spawnpoint.ItemDistribution = Utils.AddToArray(spawnpoint.ItemDistribution.ToArray(), new LooseLootItemDistribution
                                {
                                    ComposedKey = new ComposedKey { Key = targetkey },
                                    RelativeProbability = disttarget.RelativeProbability / relative
                                });
                            }
                        }
                    }
                    return looseloot;
                });
            }
            return template;
        }

        /// <summary>
        /// 为预设处理动态战利品生成
        /// </summary>
        /// <param name="itemPreset">预设内容</param>
        /// <param name="targetid">目标ID</param>
        /// <param name="context">上下文实例</param>
        public static void AddPresetLoot(List<Item> itemPreset, MongoId targetid, LoadModContext context)
        {
            if (itemPreset == null || itemPreset.Count == 0) return;
            foreach (var location in GetValidLocations(context))
            {
                if (location.LooseLoot == null) continue;
                //VulcanLog.Debug($"尝试生成战利品: {lootableItemProps.Name}", logger);
                location.LooseLoot.AddTransformer(looseloot =>
                {
                    foreach (var spawnpoint in looseloot.Spawnpoints)
                    {
                        if (spawnpoint.Template?.Items == null || spawnpoint.ItemDistribution == null) continue;
                        //查找目标
                        var loottarget = spawnpoint.Template.Items.FirstOrDefault(i => i.Template == targetid);
                        if (loottarget != null)
                        {
                            var lootkey = loottarget.ComposedKey;
                            var targetkey = ($"{lootkey}_{loottarget.Id}_{DateTime.Now.Ticks.ToString()}").ConvertHashID();

                            var disttarget = spawnpoint.ItemDistribution.FirstOrDefault(i => i.ComposedKey.Key == lootkey);
                            if (disttarget != null)
                            {
                                //解析武器树
                                List<Item> presetlist = itemPreset.RegenerateItemListData(targetkey, context);
                                if (presetlist == null || presetlist.Count == 0) continue;

                                var itemsArray = spawnpoint.Template.Items.ToArray();
                                //添加物品
                                itemsArray = Utils.AddToArray(itemsArray, new SptLootItem
                                {
                                    Id = presetlist[0].Id,
                                    Template = presetlist[0].Template,
                                    Upd = presetlist[0].Upd,
                                    ComposedKey = targetkey
                                });
                                for (var i = 1; i < presetlist.Count; i++)
                                {
                                    itemsArray = Utils.AddToArray(itemsArray, new SptLootItem
                                    {
                                        Id = presetlist[i].Id,
                                        Template = presetlist[i].Template,
                                        ParentId = presetlist[i].ParentId,
                                        SlotId = presetlist[i].SlotId,
                                        Upd = presetlist[i].Upd
                                    });
                                }
                                //返回物品树
                                spawnpoint.Template.Items = itemsArray;
                                //分摊权重
                                spawnpoint.ItemDistribution = Utils.AddToArray(spawnpoint.ItemDistribution.ToArray(), new LooseLootItemDistribution
                                {
                                    ComposedKey = new ComposedKey { Key = targetkey },
                                    RelativeProbability = disttarget.RelativeProbability / 1f // 保持原版独立几率
                                });
                            }
                        }
                    }
                    return looseloot;
                });
            }
        }
    }
}