using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using System;
using static EternalCycle.ContextManager;

namespace EternalCycle
{
    public class EventManager
    {
        // 委托现在只需要接收这一个 Context 对象
        public static event Action<OnRagfairLoadContext> OnBeforeRagfairLoadedEvent;
        public static event Action<OnRagfairLoadContext> OnAfterRagfairLoadedEvent;
        public static event Action<OnRagfairLoadContext> OnAfterModLoadedEvent;
        public static event Action<OnRagfairLoadContext> OnBeforeServerStartedEvent;
        public static event Action<OnRagfairLoadContext> OnAfterServerStartedEvent;
        public static ECLogger EventLogger = new ECLogger("全局事件管理器", true);

        public static void InitPreRagfairLoadEvent(DatabaseService db, ECLogger logger)
        {
            if (OnBeforeRagfairLoadedEvent != null)
            {
                // 把服务打包成一个 Context 丢出去
                var context = new OnRagfairLoadContext
                {
                    DB = db,
                    Logger = logger
                };
                InitEvent(OnBeforeRagfairLoadedEvent, context);
            }
        }
        public static void InitPostRagfairLoadEvent(DatabaseService db, ECLogger logger)
        {
            if (OnAfterRagfairLoadedEvent != null)
            {
                // 把服务打包成一个 Context 丢出去
                var context = new OnRagfairLoadContext
                {
                    DB = db,
                    Logger = logger
                };

                InitEvent(OnAfterRagfairLoadedEvent, context);
            }
        }
        public static void InitAfterModLoadedEvent(DatabaseService db, ECLogger logger)
        {
            if (OnAfterModLoadedEvent != null)
            {
                // 把服务打包成一个 Context 丢出去
                var context = new OnRagfairLoadContext
                {
                    DB = db,
                    Logger = logger
                };

                InitEvent(OnAfterModLoadedEvent, context);
            }
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