using System;
using System.Reflection;
using HarmonyLib;
using SPTarkov.Reflection.Patching;

namespace EternalCycleServer
{
    public class StartupLogPatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // 1. 通过字符串反射获取编译器看不见的类型
            // 格式："完整命名空间.类名, 所在程序集(不需要.dll后缀)"
            Type targetType = Type.GetType("SPTarkov.Server.Core.Services.Hosted.SPTStartupHostedService");

            // 防御性编程：如果当前上下文找不到，就去遍历所有已加载的程序集强行搜
            if (targetType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name == "SPTarkov.Server.Core")
                    {
                        targetType = assembly.GetType("SPTarkov.Server.Core.Services.Hosted.SPTStartupHostedService");
                        break;
                    }
                }
            }

            if (targetType == null)
            {
                // 找不到类直接抛出异常，方便排错
                throw new Exception("StartupLogPatch failed: Cannot find SPTStartupHostedService.");
            }

            // 2. 返回我们要拦截的私有方法 GetRandomisedStartMessage
            return targetType.GetMethod("GetRandomisedStartMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [PatchPostfix]
        public static void Postfix(ref string __result)
        {
            // 这是目标方法原版执行完、刚刚准备把返回的文字丢给 logger.Success 打印时的瞬间
            Utils.commonLogger.Info("123123123");
            Console.WriteLine("======================================");
            Console.WriteLine("【完美拦截】紧贴着绿字执行你的逻辑！");
            Console.WriteLine("======================================");

            // 【高级操作】你可以篡改即将打印的绿字内容
            // __result = __result + " [EternalCycle Mod 已就绪]";
        }
    }
}