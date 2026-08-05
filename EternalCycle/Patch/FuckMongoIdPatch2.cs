using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Common;
using System;
using System.Reflection;

namespace EternalCycleServer
{
    public class FuckMongoIdPatch2 : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // 在 IL 中，隐式类型转换方法的名字叫 "op_Implicit"
            return AccessTools.Method(typeof(MongoId), "op_Implicit", new Type[] { typeof(string) });
        }

        [PatchPrefix]
        public static void Prefix(ref string mongoId) // 注意这里的参数名必须和原版源码里的参数名一致，原版是 mongoId
        {
            if (!mongoId.IsHex24())
            {
                mongoId = mongoId.ConvertHashID().ToString();
            }
        }
    }
}