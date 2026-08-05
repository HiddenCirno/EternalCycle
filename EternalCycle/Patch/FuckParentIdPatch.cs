using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System.Reflection;
// 记得 using 你的 Item 所在的命名空间

namespace EternalCycleServer
{
    public class FuckParentIdPatch : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // 精准狙击原版 Item 类的 ParentId 属性的 setter 方法 (底层名称为 set_ParentId)
            return AccessTools.PropertySetter(typeof(Item), "ParentId");
        }

        [PatchPrefix]
        // 这里的 __instance 可以获取到当前正在被反序列化的对象
        public static void Prefix(object __instance, ref string value)
        {
            // 【关键点】如果你只想让这个逻辑对你自己的 CustomItem 生效，加这个判断
            // 这样就不会误伤原版的 Item 反序列化逻辑
            if (__instance is CustomItem)
            {
                // 空值防雷
                if (string.IsNullOrEmpty(value)) return;

                // 你的特殊逻辑：hideout 直接放行
                if (value == "hideout" || value.IsHex24()) return;

                // 截胡！在原版 setter 执行前，把传进来的非标字符串强行替换成 Hash 过的字符串
                value = value.ConvertHashID();
            }
        }
    }
}