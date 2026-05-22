using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Utils;
using System.Collections.Concurrent;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace EternalCycle
{
    /// <summary>
    /// 泛用型工具类
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// 全局的日志实例
        /// </summary>
        public static ECLogger commonLogger = new ECLogger("火神之心", true);
        //预存储字符串
        private static readonly char[] InvalidFolderChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
        /// <summary>
        /// 全局存储转换后的ID映射表
        /// </summary>
        public static ConcurrentDictionary<string, string> hashIdList = new ConcurrentDictionary<string, string>();
        /// <summary>
        /// 用于预处理jsonRaw的转换规则
        /// </summary>
        public static JsonDocumentOptions convertOptions = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip };
        /// <summary>
        /// 深度拷贝json数据
        /// </summary>
        /// <typeparam name="T">泛型定义, 可以对任意类型拷贝</typeparam>
        /// <param name="obj">拷贝内容</param>
        /// <returns>深拷贝后的对象，若obj为null则返回默认值</returns>
        public static T DeepCopyJson<T>(this T obj)
        {
            if (obj == null) return default;
            //序列化为字符串
            var json = JsonSerializer.Serialize(obj);
            //反序列化
            return JsonSerializer.Deserialize<T>(json);
        }
        /// <summary>
        /// 深度合并, 将源对象中非空的属性值复制到目标对象（仅限公共实例属性）
        /// </summary>
        /// <param name="source">源对象</param>
        /// <param name="target">目标对象</param>
        public static void CopyNonNullProperties(object source, object target)
        {
            if (source == null || target == null)
                return;
            Type sourceType = source.GetType();
            Type targetType = target.GetType();
            foreach (var sourceProp in sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var targetProp = targetType.GetProperty(sourceProp.Name);
                if (targetProp != null && targetProp.CanWrite)
                {
                    var value = sourceProp.GetValue(source);
                    if (value != null)
                    {
                        targetProp.SetValue(target, value);
                    }
                }
            }
        }
        /// <summary>
        /// 拓展方法, 判断字符串是否为24位十六进制字符串(即MongoId的规范形式)
        /// </summary>
        /// <param name="str">待检查的字符串</param>
        /// <returns>是24位十六进制字符串返回true, 否则返回false</returns>
        public static bool IsHex24(this string str)
        {
            //拦截空字符串和长度不对的字符串
            if (str == null || str.Length != 24) return false;
            //暴力解搞定
            for (int i = 0; i < 24; i++)
            {
                char c = str[i];
                bool isHex = (c >= '0' && c <= '9') ||
                             (c >= 'a' && c <= 'f') ||
                             (c >= 'A' && c <= 'F');
                if (!isHex) return false;
            }
            return true;
        }
        /// <summary>
        /// 根据输入字符串转换ID：若已是24位Hex则原样返回，否则生成新的24位哈希
        /// </summary>
        /// <param name="str">输入字符串</param>
        /// <returns>处理后的24位ID字符串</returns>
        public static string ConvertHashID(this string str)
        {
            return str.IsHex24() ? str : str.GenerateHash();
        }
        /// <summary>
        /// 扩展方法, 生成 SHA256 哈希并取前24位（高性能实现，复用哈希实例）
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <returns>24位十六进制哈希字符串</returns>
        public static string GenerateHash(this string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string hashHex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLower();
            //使用范围运算符截取24位
            string result = hashHex[..24];
            //投入映射表
            hashIdList.TryAdd(input, result);
            return result;
        }
        /// <summary>
        /// 转换物品数据（从文件路径+文件名加载，递归处理内部引用ID）
        /// </summary>
        /// <param name="pathToFile">文件夹路径</param>
        /// <param name="fileName">文件名</param>
        /// <param name="jsonutil">JSON工具实例</param>
        /// <returns>转换后的对象</returns>
        public static Dictionary<string, CustomItemTemplate> ConvertItemData(string pathToFile, string fileName, JsonUtil jsonutil)
        {
            string rawJson = File.ReadAllText(Path.Combine(pathToFile, fileName));
            JsonNode rootNode = JsonNode.Parse(rawJson, null, convertOptions);
            var dict = new Dictionary<string, CustomItemTemplate>();
            foreach (var item in rootNode.AsObject())
            {
                //var files = item.Value.AsValue().ToString();
                //草率了, 这里不应该用泛型定义方法返回值的....
                //就这样吧, 反正本来也是给自定义物品用的
                //再改还得改其他mod, 太麻烦了
                //论屎山是怎么形成的.jpg
                //....
                //坏了, 改的话应该怎么改来着?
                //完了
                //那还是继续用吧
                //哦, 我懂了
                //在改了和算了之间选择了懂了
                dict[item.Key] = ResolveJsonNode<CustomItemTemplate>(item.Value, jsonutil);
            }
            return dict;
        }
        /// <summary>
        /// 转换物品数据（直接从JSON字符串加载，递归处理内部引用ID）
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="file">JSON字符串内容</param>
        /// <param name="jsonutil">JSON工具实例</param>
        /// <returns>转换后的对象</returns>
        public static T ConvertItemData<T>(string file, JsonUtil jsonutil)
        {
            JsonNode rootNode = JsonNode.Parse(file, null, convertOptions).AsObject();
            return ResolveJsonNode<T>(rootNode, jsonutil); // 返回处理后的 JsonNode
        }
        /// <summary>
        /// 递归处理JSON节点中的ID字段（Slots/Chambers/Grids等），将非Hex的ID转换为哈希值
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="node">JSON节点</param>
        /// <param name="jsonUtil">JSON工具实例</param>
        /// <returns>转换后的对象</returns>
        public static T ResolveJsonNode<T>(JsonNode node, JsonUtil jsonUtil)
        {
            var props = node?["_props"]?.AsObject();
            if (props != null)
            {
                // Slots
                ModifySlotsOrChambers(props["Slots"]?.AsArray());
                // Chambers
                ModifySlotsOrChambers(props["Chambers"]?.AsArray());
                // Grids
                var grids = props["Grids"]?.AsArray();
                if (grids != null)
                {
                    foreach (var grid in grids)
                    {
                        // _parent & _id
                        if (grid?["_parent"] != null)
                            grid["_parent"] = grid["_parent"]?.GetValue<string>()?.ConvertHashID();
                        if (grid?["_id"] != null)
                            grid["_id"] = grid["_id"]?.GetValue<string>()?.ConvertHashID();

                        var filters = grid?["_props"]?["filters"]?.AsArray();
                        if (filters != null && filters.Count > 0)
                        {
                            var filterArray = filters[0]?["Filter"]?.AsArray();
                            var excludedArray = filters[0]?["ExcludedFilter"]?.AsArray();

                            if (filterArray != null)
                                for (int i = 0; i < filterArray.Count; i++)
                                    filterArray[i] = filterArray[i]?.GetValue<string>()?.ConvertHashID();

                            if (excludedArray != null)
                                for (int i = 0; i < excludedArray.Count; i++)
                                    excludedArray[i] = excludedArray[i]?.GetValue<string>()?.ConvertHashID();
                        }
                    }
                }
                var defAmmo = props["defAmmo"];
                if (defAmmo != null)
                {
                    props["defAmmo"] = defAmmo.GetValue<string>().ConvertHashID();
                }
                var FragmentType = props["FragmentType"];
                if (FragmentType != null)
                {
                    props["FragmentType"] = FragmentType.GetValue<string>().ConvertHashID();
                }
                var conflict = props["ConflictingItems"];
                if (conflict != null)
                {
                    var conflicts = props["ConflictingItems"]?.AsArray();
                    for (int i = 0; i < conflicts.Count; i++)
                    {
                        conflicts[i] = conflicts[i]?.GetValue<string>()?.ConvertHashID();
                    }
                }
                //明天需要整理提取合并
                //sbgpt
                // StackSlots
                ModifySlotsOrChambers(props["StackSlots"]?.AsArray());
            }
            string resultJson = node.ToJsonString();
            return jsonUtil.Deserialize<T>(resultJson); // 返回处理后的 JsonNode
        }
        /// <summary>
        /// 修改配件槽位或枪膛数组中的ID引用（_parent, _id, Filter, ExcludedFilter, Plate等）
        /// </summary>
        /// <param name="array">配件槽位或枪膛的JSON数组</param>
        public static void ModifySlotsOrChambers(JsonArray array)
        {
            if (array == null) return;
            foreach (var slot in array)
            {
                if (slot?["_parent"] != null)
                    slot["_parent"] = slot["_parent"]?.GetValue<string>()?.ConvertHashID();
                if (slot?["_id"] != null)
                    slot["_id"] = slot["_id"]?.GetValue<string>()?.ConvertHashID();

                var filters = slot?["_props"]?["filters"]?.AsArray();
                if (filters != null && filters.Count > 0)
                {
                    var filterArray = filters[0]?["Filter"]?.AsArray();
                    if (filterArray != null)
                        for (int i = 0; i < filterArray.Count; i++)
                            filterArray[i] = filterArray[i]?.GetValue<string>()?.ConvertHashID();
                    if (filters[0]?["Plate"] != null)
                        filters[0]["Plate"] = filters[0]["Plate"].GetValue<string>().ConvertHashID();
                }
            }
        }
        /// <summary>
        /// 向数组中添加元素（若原数组为null则创建新数组）
        /// </summary>
        /// <typeparam name="T">数组元素类型</typeparam>
        /// <param name="array">原数组</param>
        /// <param name="item">要添加的元素</param>
        /// <returns>新数组</returns>
        public static T[] AddToArray<T>(T[] array, T item)
        {
            if (array == null) return [item];
            return [.. array, item];
        }
        /// <summary>
        /// 转换商人基础数据（修改根节点的_id字段）
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="pathToFile">文件夹路径</param>
        /// <param name="fileName">文件名</param>
        /// <param name="jsonutil">JSON工具实例</param>
        /// <returns>转换后的对象</returns>
        /// <exception cref="InvalidOperationException">JSON根节点不是对象时抛出</exception>
        public static T ConvertTraderBaseData<T>(string pathToFile, string fileName, JsonUtil jsonutil)
        {
            //读取json
            string rawJson = File.ReadAllText(Path.Combine(pathToFile, fileName));
            //解析json
            JsonNode rootNode = JsonNode.Parse(rawJson, null, convertOptions);
            //确定对象类型
            if (rootNode is JsonObject objNode)
            {
                //修改ID
                if (objNode["_id"] != null)
                {
                    objNode["_id"] = objNode["_id"].GetValue<string>().ConvertHashID();
                }
            }
            else
            {
                throw new InvalidOperationException("JSON 根节点必须是对象");
            }
            //重新转回字符串
            string resultJson = rootNode.ToJsonString();
            //反序列化并返回
            return jsonutil.Deserialize<T>(resultJson);
        }
        public class MongoIdConverter : JsonConverter<MongoId>
        {
            /// <summary>
            /// 读取JSON中的字符串并转换为MongoId
            /// </summary>
            /// <param name="reader">JSON读取器</param>
            /// <param name="typeToConvert">目标类型</param>
            /// <param name="options">序列化选项</param>
            /// <returns>转换后的MongoId</returns>
            public override MongoId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string str = reader.GetString()!;
                return (MongoId)str.ConvertHashID();
            }
            /// <summary>
            /// 将MongoId写入JSON字符串
            /// </summary>
            /// <param name="writer">JSON写入器</param>
            /// <param name="value">MongoId值</param>
            /// <param name="options">序列化选项</param>
            public override void Write(Utf8JsonWriter writer, MongoId value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString());
            }
        }
        /// <summary>
        /// 从列表中随机抽取一个元素
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="list">源列表</param>
        /// <returns>随机抽取的元素</returns>
        /// <exception cref="ArgumentException">列表为空时抛出</exception>
        public static T DrawFromList<T>(List<T> list)
        {
            if (list.Count == 0) throw new ArgumentException("列表为空", nameof(list));
            return list[Random.Shared.Next(list.Count)];
        }
        /// <summary>
        /// 将双精度浮点数转换为百分比字符串（保留三位小数）
        /// </summary>
        /// <param name="num">浮点数（例如0.5表示50%）</param>
        /// <returns>百分比字符串，如"50.000%"，若输入为NaN则返回"NaN"</returns>
        public static string DoubleToPercent(double num)
        {
            if (double.IsNaN(num))
            {
                return "NaN";
            }
            var percent = (num * 100).ToString("F3");
            return percent + "%";
        }
        /// <summary>
        /// 将字符串中的非法文件名字符替换为下划线
        /// </summary>
        /// <param name="folderName">原始文件夹名称</param>
        /// <returns>处理后的合法文件夹名称</returns>
        public static string GetValidFolderName(string folderName)
        {
            if (string.IsNullOrEmpty(folderName)) return folderName;
            if (folderName.IndexOfAny(InvalidFolderChars) == -1)
            {
                return folderName;
            }
            char[] buffer = folderName.ToCharArray();
            for (int i = 0; i < buffer.Length; i++)
            {
                if (Array.IndexOf(InvalidFolderChars, buffer[i]) >= 0)
                {
                    buffer[i] = '_';
                }
            }
            return new string(buffer);
        }
        /// <summary>
        /// 从文件路径加载JSONC并反序列化为指定类型（自动跳过注释）
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="filepath">文件路径</param>
        /// <returns>反序列化后的对象</returns>
        public static T LoadJsonCFromPath<T>(string filepath)
        {
            var configJsoncContent = File.ReadAllText(filepath);
            return JsonSerializer.Deserialize<T>(configJsoncContent, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip // 启用注释解析
            });
        }
        /// <summary>
        /// 从JSONC字符串反序列化为指定类型（自动跳过注释）
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="content">JSONC字符串内容</param>
        /// <returns>反序列化后的对象</returns>
        public static T LoadJsonC<T>(string content)
        {
            return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip // 启用注释解析
            });
        }
    }
}