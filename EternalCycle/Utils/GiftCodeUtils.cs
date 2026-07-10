using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using static EternalCycleServer.ContextManager;
using Path = System.IO.Path;

namespace EternalCycleServer
{
    public class GiftCodeUtils
    {
        /// <summary>
        /// 将自定义配方注册到加载事件
        /// </summary>
        /// <param name="path">指定的存放配方文件的路径或完整的配方文件路径</param>
        public static void RegisterGiftCode(string modpath, string path)
        {
            var correctpath = Path.Combine(modpath, path);
            // 文件夹加载模式
            if (Directory.Exists(correctpath))
            {
                // 假设事件回调中的 context 已经是 ContextManager.LoadModContext 类型
                EventManager.DataLoadEvent.LoadGiftCodeEvent += (context) =>
                {
                    try
                    {
                        InitGiftCodeData(modpath, path, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册兑换码时发生错误：指定的文件夹 {correctpath} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(correctpath))
            {
                EventManager.DataLoadEvent.LoadGiftCodeEvent += (context) =>
                {
                    try
                    {
                        var codeData = context.JsonUtil.Deserialize<Dictionary<string, CustomGiftCodeData>>(File.ReadAllText(correctpath));
                        InitGiftCodeData(codeData, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册兑换码时发生错误：指定的文件 {correctpath} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册兑换码时发生异常：找不到指定的文件或文件夹 {path}");
            }
        }

        /// <summary>
        /// Init重载 1：处理文件夹路径，读取单体对象
        /// </summary>
        public static void InitGiftCodeData(string modpath, string folderpath, LoadModContext context)
        {
            var correctpath = Path.Combine(modpath, folderpath);
            if (!Directory.Exists(correctpath)) return;

            List<string> files = Directory.GetFiles(correctpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    var code = context.ModHelper.GetJsonDataFromFile<CustomGiftCodeData>(correctpath, fileName);

                    if (code != null)
                    {
                        InitGiftCode(code, context);
                    }
                }
            }
        }

        /// <summary>
        /// Init重载 2：处理反序列化好的字典
        /// </summary>
        public static void InitGiftCodeData(Dictionary<string, CustomGiftCodeData> codeData, LoadModContext context)
        {
            if (codeData == null || codeData.Count == 0) return;

            foreach (CustomGiftCodeData code in codeData.Values)
            {
                if (code != null)
                {
                    InitGiftCode(code, context);
                }
            }
        }

        /// <summary>
        /// 核心路由：根据配方类型进行分发处理
        /// </summary>
        private static void InitGiftCode(CustomGiftCodeData codeData, LoadModContext context)
        {
            var gifts = context.ConfigServer.GetConfig<GiftsConfig>().Gifts;
            var parent = codeData.Id.ConvertHashID();
            var gift = new Gift
            {
                Items = new List<Item>(),
                Sender = GiftSenderType.System,
                CollectionTimeHours = codeData.MaxStorageTime,
                MaxToSendPlayer = codeData.MaxUseCount,
                MessageText = codeData.Message,
                AssociatedEvent = SeasonalEventType.Promo
            };
            foreach(var kvp in codeData.Item)
            {
                //保险起见, 用ID做盐, 清洗一遍
                var item = kvp.Value.ConvertItemListData(context).RegenerateItemListData(codeData.Id, context);
                if (item.Count == 0) continue;
                item.First().ParentId = parent;
                gift.Items.AddRange(item);
            }
            gifts.TryAdd(codeData.Code, gift);
        }
    }
}