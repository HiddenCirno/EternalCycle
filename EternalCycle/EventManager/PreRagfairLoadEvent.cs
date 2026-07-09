using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using System;

namespace EternalCycleServer
{
    public class PreRagfairLoadContext
    {
        public DatabaseService DB { get; init; }
        public ECLogger Logger { get; init; }
        // 未来就算加了100个服务，也不需要改委托的签名！
    }
    public class PreRagfairLoadEventManager
    {
        // 委托现在只需要接收这一个 Context 对象
        public static event Action<PreRagfairLoadContext> OnPreRagfairLoadEvent;

        public static void ExecuteEvent(DatabaseService db, ECLogger logger)
        {
            if (OnPreRagfairLoadEvent != null)
            {
                // 把服务打包成一个 Context 丢出去
                var context = new PreRagfairLoadContext
                {
                    DB = db,
                    Logger = logger
                };

                foreach (Action<PreRagfairLoadContext> method in OnPreRagfairLoadEvent.GetInvocationList())
                {
                    try
                    {
                        method.Invoke(context); // 只需要传一个参数
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.StackTrace);
                        logger.Error($"执行失败: {ex.Message}");
                    }
                }
            }
        }
    }
}