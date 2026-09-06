using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace EternalCycleServer
{
    /// <summary>
    /// 无 Patch 的 MongoId 预规范化器。
    /// 在进入 SPT 严格反序列化(StringToMongoIdConverter)之前, 把 JSON 里注定要落到
    /// MongoId / Item 位置的"可读名"提前哈希成合法 24hex, 从而:
    ///   - 不修改任何 JSON 数据文件
    ///   - 不修改任何字段/类定义
    ///   - 不触碰/绕开 SPT 自身的 MongoId 校验(校验仍在, 只是喂进去的值已经合法)
    /// 语义与旧版 override+[JsonConverter] 转换层完全一致, 也等价于被删除的
    /// FuckMongoIdPatch 系列在"只针对本框架自己读入的数据"范围内的行为。
    /// </summary>
    public static class MongoNormalizer
    {
        private static readonly JsonDocumentOptions ParseOptions = new() { CommentHandling = JsonCommentHandling.Skip };

        /// <summary>
        /// 规范化后交给 jsonUtil 严格反序列化(等价替换 context.JsonUtil.Deserialize&lt;T&gt;)。
        /// </summary>
        public static T? Deserialize<T>(JsonUtil jsonUtil, string? rawJson)
        {
            if (string.IsNullOrEmpty(rawJson)) return jsonUtil.Deserialize<T>(rawJson);
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(rawJson, null, ParseOptions);
            }
            catch
            {
                return jsonUtil.Deserialize<T>(rawJson);
            }
            if (node == null) return jsonUtil.Deserialize<T>(rawJson);

            Normalize(node, typeof(T), 0);

            // 序列化回字符串交给 SPT 严格 options 反序列化。
            // 此时所有 MongoId/Item 位置的值都已是合法 24hex。
            return jsonUtil.Deserialize<T>(node.ToJsonString());
        }

        /// <summary>
        /// 只做规范化(不改动调用方已经 parse 好的 node)。
        /// </summary>
        public static void Normalize(JsonNode? node, Type type, int depth)
        {
            if (node == null || depth > 32) return;

            // 全局兜底: 任何带 _tpl 的对象都是 Item 线格式实例, 先整体转换(不受 schema 限制)。
            if (node is JsonObject sweepObj && sweepObj.ContainsKey("_tpl"))
            {
                TransformItemNode(sweepObj);
            }

            type = Nullable.GetUnderlyingType(type) ?? type;

            // 叶子类型直接处理
            if (type == typeof(MongoId))
            {
                if (node is JsonValue mv && mv.GetValueKind() == JsonValueKind.String)
                    HashStringValue(mv);
                return;
            }
            if (type == typeof(string) || type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(object))
                return;
            // 派生/引用官方 Item 的类型 -> 顶层就是 Item 实例
            if (typeof(Item).IsAssignableFrom(type))
            {
                if (node is JsonObject itemObj) TransformItemNode(itemObj);
                return;
            }

            if (node is JsonArray arr)
            {
                Type? el = ElementTypeOf(type);
                if (el != null)
                {
                    foreach (var child in arr.ToArray())
                        Normalize(child, el, depth + 1);
                }
                return;
            }

            if (node is not JsonObject obj) return;

            // Dictionary<string, V> / Dictionary<MongoId, V> ...
            var (keyType, valType) = DictionaryArgsOf(type);
            if (valType != null)
            {
                if (keyType == typeof(MongoId))
                {
                    // 字典键是 MongoId: 键名必须哈希成合法 24hex
                    foreach (var pair in obj.ToArray())
                    {
                        string oldKey = pair.Key;
                        string newKey = oldKey.ConvertHashID();
                        if (newKey != oldKey)
                        {
                            obj.Remove(oldKey);
                            obj[newKey] = pair.Value;
                        }
                    }
                }
                foreach (var child in obj.Select(x => x.Value).ToArray())
                    Normalize(child, valType, depth + 1);
                return;
            }

            // 多态 $type 判定(JsonDerivedType)
            Type concrete = ResolveConcrete(obj, type);
            if (concrete != type) type = concrete;

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null) continue;

                string jname = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
                if (!obj.TryGetPropertyValue(jname, out var value) || value == null) continue;

                Type pt = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                if (pt == typeof(MongoId))
                {
                    if (value is JsonValue jv && jv.GetValueKind() == JsonValueKind.String)
                        HashStringValue(jv);
                }
                else if (typeof(Item).IsAssignableFrom(pt))
                {
                    if (value is JsonObject io) TransformItemNode(io);
                }
                else
                {
                    Normalize(value, pt, depth + 1);
                }
            }
        }

        /// <summary>
        /// 转换一个 Item 实例对象节点: _id / _tpl 必为 MongoId; parentId 语义同旧 StringHashConverter,
        /// 空值与 "hideout" 放行。
        /// </summary>
        private static void TransformItemNode(JsonObject item)
        {
            HashKey(item, "_id");
            HashKey(item, "_tpl");
            if (item.TryGetPropertyValue("parentId", out var pv) && pv is JsonValue pjv && pjv.GetValueKind() == JsonValueKind.String)
            {
                string? s = pjv.GetValue<string>();
                if (s != null && s.Length > 0 && s != "hideout" && !s.IsHex24())
                {
                    string h = s.ConvertHashID();
                    if (h != s) pjv.ReplaceWith(JsonValue.Create(h));
                }
            }
        }

        private static void HashKey(JsonObject obj, string key)
        {
            if (obj.TryGetPropertyValue(key, out var v) && v is JsonValue jv && jv.GetValueKind() == JsonValueKind.String)
                HashStringValue(jv);
        }

        private static void HashStringValue(JsonValue value)
        {
            string? s = value.GetValue<string>();
            if (s == null) return;
            string h = s.ConvertHashID();
            if (h != s) value.ReplaceWith(JsonValue.Create(h));
        }

        private static Type ResolveConcrete(JsonObject obj, Type declared)
        {
            if (!declared.IsDefined(typeof(JsonDerivedTypeAttribute), false)) return declared;
            if (!obj.TryGetPropertyValue("$type", out var tv) || tv is not JsonValue jtv || jtv.GetValueKind() != JsonValueKind.String)
                return declared;

            string tag = jtv.GetValue<string>();
            foreach (var attr in declared.GetCustomAttributes<JsonDerivedTypeAttribute>(false))
            {
                if (attr.TypeDiscriminator != null && attr.TypeDiscriminator.ToString() == tag)
                    return attr.DerivedType;
            }
            return declared;
        }

        private static Type? ElementTypeOf(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (type.IsGenericType)
            {
                Type def = type.GetGenericTypeDefinition();
                if (def == typeof(List<>) || def == typeof(HashSet<>) || def == typeof(IEnumerable<>) ||
                    def == typeof(IList<>) || def == typeof(ICollection<>) || def == typeof(IReadOnlyList<>))
                    return type.GetGenericArguments()[0];
            }
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    return i.GetGenericArguments()[0];
            }
            return null;
        }

        private static (Type?, Type?) DictionaryArgsOf(Type type)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var a = type.GetGenericArguments();
                return (a[0], a[1]);
            }
            foreach (var i in type.GetInterfaces())
            {
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                {
                    var a = i.GetGenericArguments();
                    return (a[0], a[1]);
                }
            }
            return (null, null);
        }
    }
}
