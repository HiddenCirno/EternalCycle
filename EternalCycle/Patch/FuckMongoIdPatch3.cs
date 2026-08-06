using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Common;
using System;
using System.Reflection;

namespace EternalCycleServer
{
    public class FuckMongoIdPatch3 : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // 精准定位 public MongoId(ReadOnlySpan<char> hex) 构造函数
            return AccessTools.Constructor(typeof(MongoId), new Type[] { typeof(ReadOnlySpan<char>) });
        }

        [PatchPrefix]
        public static void Prefix(ref ReadOnlySpan<char> hex)
        {
            // 如果已经是合法 24 位 Hex，直接跳过
            if (hex.Length == 24 && IsHex24Span(hex))
                return;

            // 转为 string 然后调用你的 ConvertHashID，再转回 Span
            string hexString = hex.ToString();
            hexString = hexString.ConvertHashID();
            // 注意：Span 是传引用的，但 ref 参数不能直接赋新 Span，需要将字符串转为 Span 再赋值？
            // 实际上 Harmony 的 ref 参数可以修改值，但这里只能修改 Span 的内容。
            // 更稳妥的方式是把 Prefix 改为返回 false，然后在内部调用另一个构造函数并跳过原方法。
            // 但为了简单，我们可以直接修改字符串，然后让 Span 指向新字符串。
            // 因为 Span 是只读的，我们不能直接修改它指向的内存，但可以修改 Span 引用本身。
            // Harmony 的 ref 参数允许我们更改 Span 的引用。
            hex = hexString.AsSpan();
        }

        private static bool IsHex24Span(ReadOnlySpan<char> span)
        {
            for (int i = 0; i < 24; i++)
            {
                char c = span[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }
    }
}