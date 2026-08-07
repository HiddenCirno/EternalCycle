using SPTarkov.Server.Core.Utils.Cloners;
using static EternalCycleServer.ContextManager;
using Path = System.IO.Path;

namespace EternalCycleServer;

public static class QuestZoneUtils
{
    private static readonly List<QuestZone> _zones = new();

    // ======== 虚拟地点映射表 ========
    private static readonly Dictionary<string, string[]> VirtualLocationMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "FactoryCommon",  new[] { "factory4_day", "factory4_night" } },
        { "SandboxCommon",  new[] { "sandbox", "sandbox_high" } },
    };

    // ======== 公开注册入口 ========

    /// <summary>
    /// 从文件夹或单文件注册 QuestZone
    /// </summary>
    public static void RegisterQuestZones(string modpath, string relativePath)
    {
        var fullPath = Path.Combine(modpath, relativePath);

        if (Directory.Exists(fullPath))
        {
            EventManager.DataLoadEvent.LoadQuestZoneEvent += (context) =>
            {
                try
                {
                    LoadZonesFromDirectory(fullPath, context);
                }
                catch (Exception ex)
                {
                    EventManager.EventLogger.Error($"加载 QuestZone 文件夹失败：{fullPath}", ex);
                }
            };
        }
        else if (File.Exists(fullPath))
        {
            EventManager.DataLoadEvent.LoadQuestZoneEvent += (context) =>
            {
                try
                {
                    var zoneList = context.JsonUtil.Deserialize<List<QuestZone>>(File.ReadAllText(fullPath));
                    if (zoneList != null)
                        _zones.AddRange(zoneList);
                }
                catch (Exception ex)
                {
                    EventManager.EventLogger.Error($"加载 QuestZone 文件失败：{fullPath}", ex);
                }
            };
        }
        else
        {
            EventManager.EventLogger.Warn($"QuestZone 路径不存在：{fullPath}");
        }
    }

    /// <summary>
    /// 直接以代码方式添加一个 zone
    /// </summary>
    public static void AddZone(QuestZone zone) => _zones.Add(zone);

    /// <summary>
    /// 获取展开后的全部 zone 列表（虚拟地点已展开为真实地点）
    /// </summary>
    public static IReadOnlyList<QuestZone> GetZones(ICloner cloner)
    {
        var expanded = new List<QuestZone>();

        foreach (var zone in _zones)
        {
            var location = zone.ZoneLocation;

            if (VirtualLocationMap.TryGetValue(location, out var realLocations))
            {
                foreach (var realLocation in realLocations)
                {
                    var clonezone = cloner.Clone(zone);
                    clonezone.ZoneLocation = realLocation;
                    expanded.Add(clonezone);
                }
            }
            else
            {
                expanded.Add(zone);
            }
        }

        return expanded;
    }

    // ======== 内部方法 ========

    private static void LoadZonesFromDirectory(string dir, LoadModContext context)
    {
        foreach (var file in Directory.GetFiles(dir, "*.json*"))
        {
            var zones = context.ModHelper.GetJsonDataFromFile<List<QuestZone>>(dir, Path.GetFileName(file));
            if (zones != null)
                _zones.AddRange(zones);
        }
    }

}