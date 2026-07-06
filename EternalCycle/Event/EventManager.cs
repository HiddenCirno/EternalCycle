using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System;
using static EternalCycle.ContextManager;

namespace EternalCycle
{
    /// <summary>
    /// 事件管理器
    /// </summary>
    public class EventManager
    {
        /// <summary>
        /// 跳蚤市场前置事件
        /// </summary>
        public static Action<LoadModContext> OnBeforeRagfairLoadedEvent;
        /// <summary>
        /// 跳蚤市场后置事件
        /// </summary>
        public static Action<LoadModContext> OnAfterRagfairLoadedEvent;
        /// <summary>
        /// Mod加载后置事件, 位于市场前
        /// </summary>
        public static Action<LoadModContext> OnAfterModLoadedEvent;
        //由于版本问题这两个暂时用不到
        public static Action<LoadModContext> OnBeforeServerStartedEvent;
        public static Action<LoadModContext> OnAfterServerStartedEvent;
        //以下为集合事件
        /// <summary>
        /// Mod数据加载事件
        /// </summary>
        public static class DataLoadEvent
        {
            public static Action<LoadModContext> LoadItemEvent;
            public static Action<LoadModContext> LoadQuestEvent;
            public static Action<LoadModContext> LoadQuestLocaleEvent;
            public static Action<LoadModContext> LoadQuestLogicEvent;
            public static Action<LoadModContext> LoadQuestDataEvent;
            public static Action<LoadModContext> LoadQuestRewardEvent;
            public static Action<LoadModContext> LoadTraderBaseEvent;
            public static Action<LoadModContext> LoadPresetEvent;
            public static Action<LoadModContext> LoadTraderAssortEvent;
            public static Action<LoadModContext> LoadAchievementEvent;
            public static Action<LoadModContext> LoadRecipeEvent;
            public static Action<LoadModContext> LoadLockedRecipeEvent;
            public static Action<LoadModContext> LoadLockedTraderAssortEvent;
            public static Action<LoadModContext> LoadScavCaseRecipeEvent;
            public static Action<LoadModContext> LoadCultCircleRecipeEvent;
            public static Action<LoadModContext> FixItemCompatibleEvent;
            //tbc
        }

        /// <summary>
        /// 事件管理器专用Logger
        /// </summary>
        public static ECLogger EventLogger = new ECLogger("全局事件管理器", true);

        public static void InitPreRagfairLoadEvent(LoadModContext context)
        {
            InitRagfairEvent(OnBeforeRagfairLoadedEvent, context);
        }

        public static void InitPostRagfairLoadEvent(LoadModContext context)
        {
            InitRagfairEvent(OnAfterRagfairLoadedEvent, context);
        }

        public static void InitAfterModLoadedEvent(LoadModContext context)
        {
            InitRagfairEvent(OnAfterModLoadedEvent, context);
        }

        public static void InitLoadItemEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadItemEvent, context);
        }

        public static void InitLoadQuestEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadQuestEvent, context);
        }
        public static void InitLoadQuestLocaleEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadQuestLocaleEvent, context);
        }

        public static void InitLoadQuestDataEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadQuestDataEvent, context);
        }

        public static void InitLoadQuestRewardEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadQuestRewardEvent, context);
        }

        public static void InitLoadQuestLogicEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadQuestLogicEvent, context);
        }

        public static void InitLoadTraderBaseEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadTraderBaseEvent, context);
        }

        public static void InitLoadTraderAssortEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadTraderAssortEvent, context);
        }

        public static void InitLoadLockedTraderAssortEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadLockedTraderAssortEvent, context);
        }

        public static void InitLoadAchievementEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadAchievementEvent, context);
        }

        public static void InitLoadRecipeEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadRecipeEvent, context);
        }

        public static void InitLoadLockedRecipeEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadLockedRecipeEvent, context);
        }

        public static void InitLoadScavCaseRecipeEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadScavCaseRecipeEvent, context);
        }

        public static void InitLoadCultCircleRecipeEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadCultCircleRecipeEvent, context);
        }


        public static void InitFixItemCompatibleEventEvent(LoadModContext context)
        {
            InitRagfairEvent(DataLoadEvent.FixItemCompatibleEvent, context);
        }

        public static void InitRagfairEvent (Action<LoadModContext> targetEvent, LoadModContext context)
        {
            if (targetEvent == null) return;
            InitEvent(targetEvent, context);
        }

        private static void InitEvent<T>(Action<T> eventToFire, T context)
        {
            if (eventToFire == null) return; // 没人订阅就直接跳过

            // 遍历所有挂载的方法，安全执行
            foreach (Action<T> method in eventToFire.GetInvocationList())
            {
                try
                {
                    method.Invoke(context);
                }
                catch (Exception ex)
                {
                    // 打印出具体是哪个方法报错了，排错神器！
                    EventLogger.Error($"执行外部挂载的方法 [{method.Method.Name}] 时发生崩溃: {ex.Message}\n{ex.StackTrace}");
                }
            }
        }
    }
}