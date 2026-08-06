using HarmonyLib;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Utils.Json.Converters;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;

namespace EternalCycleServer
{
    public class FuckMongoIdPatch4 : AbstractPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            // 精准定位，这里你写得很对，必须用 MakeByRefType
            return AccessTools.Method(
                typeof(StringToMongoIdConverter),
                "Read",
                new Type[] { typeof(Utf8JsonReader).MakeByRefType(), typeof(Type), typeof(JsonSerializerOptions) }
            );
        }

        [PatchTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // 核心 IL 黑魔法：我们无视原方法传进来的 instructions (相当于清空原方法体)
            // 直接重写底层的指令，让它调用我们的 MyCustomRead 方法

            // 参数说明：
            // ldarg.0 = this (StringToMongoIdConverter 实例)
            // ldarg.1 = ref Utf8JsonReader reader (这正是我们需要的！)
            // ldarg.2 = Type typeToConvert
            // ldarg.3 = JsonSerializerOptions options

            // 1. 把 ref reader 推入求值栈
            yield return new CodeInstruction(OpCodes.Ldarg_1);

            // 2. 调用我们自定义的静态方法 MyCustomRead，并把栈上的 ref reader 传给它
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FuckMongoIdPatch4), nameof(MyCustomRead)));

            // 3. 直接返回 MyCustomRead 产生的结果 (MongoId)
            yield return new CodeInstruction(OpCodes.Ret);
        }

        // 这个方法将在真实的运行环境中被直接 Call，完全避开了 Prefix 的委托生成限制
        public static MongoId MyCustomRead(ref Utf8JsonReader reader)
        {
            // 补上原方法的第一步防呆校验
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"The JsonTokenType was not of type string, it was: {reader.TokenType}");
            }

            // 1. 安全读取字符串
            string hex = reader.GetString();

            if (string.IsNullOrEmpty(hex))
            {
                return default;
            }

            // 2. 你的核心转换逻辑
            if (!hex.IsHex24())
            {
                hex = hex.ConvertHashID();
            }

            // 3. 安全返回合法的 MongoId，此时绝不会抛错
            return new MongoId(hex);
        }
    }
}