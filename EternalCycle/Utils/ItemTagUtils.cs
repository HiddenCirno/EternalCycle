using static EternalCycleServer.ContextManager;
using Path = System.IO.Path;

namespace EternalCycleServer
{
    public class ItemTagUtils
    {
        public static ItemTagDictionary ItemTagDictionarys = new();

        /// <summary>
        /// 将自定义配方注册到加载事件
        /// </summary>
        /// <param name="path">指定的存放配方文件的路径或完整的配方文件路径</param>
        public static void RegisterItemTag(string modpath, string path)
        {
            var correctpath = Path.Combine(modpath, path);
            // 单文件加载模式
            EventManager.DataLoadEvent.LoadItemTagEvent += (context) =>
            {
                try
                {
                    var codeData = context.JsonUtil.Deserialize<ItemTagDictionary>(File.ReadAllText(correctpath));
                    InitItemTagData(codeData, context);
                }
                catch (Exception ex)
                {
                    EventManager.EventLogger.Error($"注册物品词典时发生错误：指定的文件 {correctpath} 存在问题", ex);
                }
            };
        }

        /// <summary>
        /// Init重载 2：处理反序列化好的字典
        /// </summary>
        public static void InitItemTagData(ItemTagDictionary tagDict, LoadModContext context)
        {
            if (tagDict == null || tagDict.Count == 0) return;
            foreach(var kvp in tagDict)
            {
                ItemTagDictionarys.TryGetValue(kvp.Key, out var tagList);
                if(tagList == null)
                {
                    tagList = new ItemTag();
                }
                foreach(var item in kvp.Value)
                {
                    var itemid = item.ConvertHashID();
                    if (tagList.Contains(itemid)) continue;
                    tagList.Add(itemid);
                }
                ItemTagDictionarys[kvp.Key] = tagList;
            }
        }

        public static ItemTag GetTagList(string tag)
        {
            ItemTagDictionarys.TryGetValue(tag, out var list);
            return list ?? new ItemTag();
        }

        public static ItemTag GetTagList(ItemTag tagList)
        {
            //偷懒复用了类型, 这里是根据传入的标签列表返回所有的标签内容
            var result = new ItemTag();
            foreach(var tag in tagList)
            {
                ItemTagDictionarys.TryGetValue(tag, out var list);
                result.UnionWith(list ?? new ItemTag());
            }
            return result;
        }
    }
}