using SPTarkov.Server.Core.Models.Enums;

namespace EternalCycle
{

    /// <summary>
    /// 对自定义位图进行转换的工具类
    /// </summary>
    public static class BitMapUtils
    {
        /// <summary>
        /// 地图索引表
        /// </summary>
        private static readonly Dictionary<ELocationType, string> LocationMap = new()
        {
            { ELocationType.Custom, "bigmap" },
            { ELocationType.Woods, "Woods" },
            { ELocationType.Factory_Day, "factory4_day" },
            { ELocationType.Factory_Night, "factory4_night" },
            { ELocationType.Laboratory, "laboratory" },
            { ELocationType.Shoreline, "Shoreline" },
            { ELocationType.ReserveBase, "RezervBase" },
            { ELocationType.Interchange, "Interchange" },
            { ELocationType.Lighthouse, "Lighthouse" },
            { ELocationType.TarkovStreets, "TarkovStreets" },
            { ELocationType.GroundZero, "Sandbox" },
            { ELocationType.GroundZero_High, "Sandbox_high" },
            { ELocationType.Labyrinth, "Labyrinth" }
        };

        //用于运算的预缓存表
        private static readonly EBlackListType[] BlackListTypes = (EBlackListType[])Enum.GetValues(typeof(EBlackListType));
        private static readonly EGameVersionType[] GameVersionTypes = (EGameVersionType[])Enum.GetValues(typeof(EGameVersionType));
        private static readonly EBodyPartType[] BodyPartTypes = (EBodyPartType[])Enum.GetValues(typeof(EBodyPartType));
        private static readonly ELocationType[] LocationTypes = (ELocationType[])Enum.GetValues(typeof(ELocationType));
        private static readonly EExitStatusType[] ExitStatusTypes = (EExitStatusType[])Enum.GetValues(typeof(EExitStatusType));
        private static readonly EQuestStatusType[] QuestStatusTypes = (EQuestStatusType[])Enum.GetValues(typeof(EQuestStatusType));
        private static readonly Dictionary<EBlackListType, string> BlackListNamesMap = BlackListTypes.ToDictionary(e => e, e => e.ToString());
        private static readonly Dictionary<EGameVersionType, string> GameVersionNamesMap = GameVersionTypes.ToDictionary(e => e, e => e.ToString());
        private static readonly Dictionary<EBodyPartType, string> BodyPartNamesMap = BodyPartTypes.ToDictionary(e => e, e => e.ToString());
        private static readonly Dictionary<EExitStatusType, string> ExitStatusNamesMap = ExitStatusTypes.ToDictionary(e => e, e => e.ToString());
        private static readonly Dictionary<EQuestStatusType, QuestStatusEnum> QuestStatusMap = InitializeQuestStatusMap();

        /// <summary>
        /// 初始化任务状态映射表
        /// </summary>
        private static Dictionary<EQuestStatusType, QuestStatusEnum> InitializeQuestStatusMap()
        {
            var map = new Dictionary<EQuestStatusType, QuestStatusEnum>();
            foreach (var status in QuestStatusTypes)
            {
                if (status != EQuestStatusType.None && Enum.TryParse<QuestStatusEnum>(status.ToString(), out var originalStatus))
                {
                    map[status] = originalStatus;
                }
            }
            return map;
        }

        /// <summary>
        /// 根据输入的位图数字计算黑名单
        /// </summary>
        /// <param name="bitmask">位图数据</param>
        /// <returns>黑名单列表对象</returns>
        public static List<string> GetBlackListCode(int bitmask)
        {
            var result = new List<string>();
            foreach (var type in BlackListTypes)
            {
                if (type != EBlackListType.None && (bitmask & (int)type) != 0)
                {
                    result.Add(BlackListNamesMap[type]);
                }
            }
            return result;
        }

        /// <summary>
        /// 根据输入的位图数字计算游戏版本
        /// </summary>
        /// <param name="bitmask">位图数据</param>
        /// <returns>游戏版本列表对象</returns>
        public static List<string> GetGameVersionCode(int bitmask)
        {
            var result = new List<string>();
            foreach (var type in GameVersionTypes)
            {
                if (type != EGameVersionType.none && (bitmask & (int)type) != 0)
                {
                    result.Add(GameVersionNamesMap[type]);
                }
            }
            return result;
        }

        /// <summary>
        /// 根据输入的位图数字计算躯体部位
        /// </summary>
        /// <param name="bitmask">位图数据</param>
        /// <returns>躯体部位列表对象</returns>
        public static List<string> GetBodyPartCode(int bitmask)
        {
            var result = new List<string>();
            foreach (var type in BodyPartTypes)
            {
                if (type != EBodyPartType.None && (bitmask & (int)type) != 0)
                {
                    result.Add(BodyPartNamesMap[type]);
                }
            }
            return result;
        }

        /// <summary>
        /// 根据输入的位图数字计算地图
        /// </summary>
        /// <param name="bitmask">位图数据</param>
        /// <returns>地图列表对象</returns>
        public static List<string> GetLocationCode(int bitmask)
        {
            var result = new List<string>();
            foreach (var type in LocationTypes)
            {
                if (type != ELocationType.None && (bitmask & (int)type) != 0)
                {
                    if (LocationMap.TryGetValue(type, out var locationName))
                    {
                        result.Add(locationName);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 根据输入的位图数字计算撤离状态
        /// </summary>
        /// <param name="bitmask">位图数据</param>
        /// <returns>撤离状态列表对象</returns>
        public static List<string> GetExitStatusCode(int bitmask)
        {
            var result = new List<string>();
            foreach (var type in ExitStatusTypes)
            {
                if (type != EExitStatusType.None && (bitmask & (int)type) != 0)
                {
                    result.Add(ExitStatusNamesMap[type]);
                }
            }
            return result;
        }

        /// <summary>
        /// 根据输入的位图数字计算任务状态
        /// </summary>
        /// <param name="bitmask">位图数据</param>
        /// <returns>一个记录任务状态的哈希表</returns>
        public static HashSet<QuestStatusEnum> GetQuestStatusCode(int bitmask)
        {
            var result = new HashSet<QuestStatusEnum>();
            foreach (var type in QuestStatusTypes)
            {
                if (type != EQuestStatusType.None && (bitmask & (int)type) != 0)
                {
                    if (QuestStatusMap.TryGetValue(type, out var originalStatus))
                    {
                        result.Add(originalStatus);
                    }
                }
            }
            return result;
        }
    }
}