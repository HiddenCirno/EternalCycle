using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Common; // 确保引用了 MongoId 所在的命名空间
using System;
using System.Reflection;

namespace EternalCycleServer
{
    public class FuckMongoIdPatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // 精准定位 public MongoId(string? hex) 构造函数
            return AccessTools.Constructor(typeof(MongoId), new Type[] { typeof(string) });
        }

        [PatchPrefix]
        public static void Prefix(ref string hex)
        {
            // 如果字符串为空或者长度已经是24，就不管它，让原版逻辑走
            if (!hex.IsHex24())
            {
                // 拦截到非法长度！在原版报错前，强行洗白成 24 位 Hex 字符串
                // 这里调用你的 ConvertHashID() 并转为 string 覆盖掉原参数
                hex = hex.ConvertHashID().ToString();
            }
        }
    }
}