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
        public static Action<OnRagfairLoadContext> OnBeforeRagfairLoadedEvent;
        /// <summary>
        /// 跳蚤市场后置事件
        /// </summary>
        public static Action<OnRagfairLoadContext> OnAfterRagfairLoadedEvent;
        /// <summary>
        /// Mod加载后置事件, 位于市场前
        /// </summary>
        public static Action<OnRagfairLoadContext> OnAfterModLoadedEvent;
        //由于版本问题这两个暂时用不到
        public static Action<OnRagfairLoadContext> OnBeforeServerStartedEvent;
        public static Action<OnRagfairLoadContext> OnAfterServerStartedEvent;
        //以下为集合事件
        /// <summary>
        /// Mod数据加载事件
        /// </summary>
        public static class DataLoadEvent
        {
            public static Action<OnRagfairLoadContext> LoadItemEvent;
            public static Action<OnRagfairLoadContext> LoadQuestEvent;
            public static Action<OnRagfairLoadContext> LoadQuestLogicEvent;
            public static Action<OnRagfairLoadContext> LoadQuestDataEvent;
            public static Action<OnRagfairLoadContext> LoadTraderBaseEvent;
            public static Action<OnRagfairLoadContext> LoadPresetEvent;
            public static Action<OnRagfairLoadContext> LoadQuestRewardsEvent;
            public static Action<OnRagfairLoadContext> LoadTraderAssortEvent;
            public static Action<OnRagfairLoadContext> FixItemCompatibleEvent;
            //tbc
        }

        /// <summary>
        /// 事件管理器专用Logger
        /// </summary>
        public static ECLogger EventLogger = new ECLogger("全局事件管理器", true);

        public static void InitPreRagfairLoadEvent(OnRagfairLoadContext context)
        {
            InitRagfairEvent(OnBeforeRagfairLoadedEvent, context);
        }

        public static void InitPostRagfairLoadEvent(OnRagfairLoadContext context)
        {
            InitRagfairEvent(OnAfterRagfairLoadedEvent, context);
        }

        public static void InitAfterModLoadedEvent(OnRagfairLoadContext context)
        {
            InitRagfairEvent(OnAfterModLoadedEvent, context);
        }

        public static void InitLoadItemEvent(OnRagfairLoadContext context)
        {
            InitRagfairEvent(DataLoadEvent.LoadItemEvent, context);
        }

        public static void InitRagfairEvent (Action<OnRagfairLoadContext> targetEvent, OnRagfairLoadContext context)
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