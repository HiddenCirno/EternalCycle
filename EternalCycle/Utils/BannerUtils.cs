using SPTarkov.Server.Core.Models.Eft.Common;
using System.IO;
using System.Linq;
using static EternalCycleServer.ContextManager;
using Path = System.IO.Path;

namespace EternalCycleServer
{
    /// <summary>
    /// 自定义地图加载横幅工具
    /// 将自定义横幅(图片 + 生效地图位掩码)注入到 LocationTable 的 LocationBase.Banners 中,
    /// 客户端地图选择/加载画面即可显示。
    /// </summary>
    public class BannerUtils
    {
        /// <summary>
        /// 将自定义横幅注册到加载事件
        /// </summary>
        /// <param name="modpath">Mod 根目录</param>
        /// <param name="path">指定存放横幅文件的文件夹路径或单个横幅文件(列表)路径</param>
        /// <param name="respath">横幅图片资源所在目录(相对 modpath)</param>
        public static void RegisterBanner(string modpath, string path, string respath)
        {
            var correctpath = Path.Combine(modpath, path);

            // 文件夹加载模式
            if (Directory.Exists(correctpath))
            {
                EventManager.DataLoadEvent.LoadBannerEvent += (context) =>
                {
                    try
                    {
                        InitBannerData(modpath, path, respath, context);
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册横幅时发生错误：指定的文件夹 {correctpath} 存在问题", ex);
                    }
                };
            }
            // 单文件加载模式
            else if (File.Exists(correctpath))
            {
                EventManager.DataLoadEvent.LoadBannerEvent += (context) =>
                {
                    try
                    {
                        // 反序列化为 List 集合
                        var bannerData = context.JsonUtil.Deserialize<List<CustomBannerData>>(File.ReadAllText(correctpath));

                        if (bannerData != null)
                        {
                            InitBannerData(bannerData, modpath, respath, context);
                        }
                    }
                    catch (Exception ex)
                    {
                        EventManager.EventLogger.Error($"注册横幅时发生错误：指定的文件 {correctpath} 存在问题", ex);
                    }
                };
            }
            else
            {
                EventManager.EventLogger.Warn($"注册横幅时发生异常：找不到指定的文件或文件夹 {correctpath}");
            }
        }

        /// <summary>
        /// Init重载 1：处理文件夹路径，遍历解析为单个横幅对象
        /// </summary>
        public static void InitBannerData(string modpath, string folderpath, string respath, LoadModContext context)
        {
            var correctpath = Path.Combine(modpath, folderpath);

            if (!Directory.Exists(correctpath)) return;

            List<string> files = Directory.GetFiles(correctpath).ToList();
            if (files.Count > 0)
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    // 文件夹模式下，每个文件是一个 CustomBannerData
                    var banners = context.ModHelper.GetJsonDataFromFile<List<CustomBannerData>>(correctpath, fileName);

                    if (banners != null)
                    {
                        foreach(var banner in banners)
                        {
                            InitBanner(banner, modpath, respath, context);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Init重载 2：处理单文件反序列化出的横幅列表
        /// </summary>
        public static void InitBannerData(List<CustomBannerData> bannerData, string modpath, string respath, LoadModContext context)
        {
            if (bannerData == null || bannerData.Count == 0) return;

            foreach (var banner in bannerData)
            {
                if (banner != null)
                {
                    InitBanner(banner, modpath, respath, context);
                }
            }
        }

        /// <summary>
        /// 将单个自定义横幅注入到对应地图数据中
        /// </summary>
        public static void InitBanner(CustomBannerData bannerData, string modpath, string respath, LoadModContext context)
        {
            // 1. 位掩码 → 目标地图 key 列表(LocationBase 风格)
            List<string> locationKeys = BitMapUtils.GetFuckSptLocationCode(bannerData.Map);
            if (locationKeys == null || locationKeys.Count == 0)
            {
                context.Logger.Warn($"横幅 {bannerData.Id} 未指定任何生效地图(map={bannerData.Map})，已跳过");
                return;
            }

            // 2. 注册图片下载路由: /files/banners/{文件名(无扩展名)}
            string fileName = Path.GetFileName(bannerData.ImagePath);
            string routeKey = "/files/banners/" + Path.GetFileNameWithoutExtension(fileName);
            string fullPath = Path.Combine(modpath, respath, fileName);
            ImageUtils.RegisterImageRoute(routeKey, fullPath, context.ImageRouter);

            // 3. 构造 Banner 模型
            var banner = new Banner
            {
                Id = bannerData.Id.ToString(),
                Picture = new Pic
                {
                    File = fileName,
                    Path = "banners/" + fileName,
                    Rcid = "",
                    Type = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant()
                }
            };

            // 4. 注入到每个目标地图的 LocationBase.Banners
            var locations = context.DB.GetLocations().GetDictionary();
            foreach (var locationKey in locationKeys)
            {
                foreach (var kvp in locations)
                {
                    if (kvp.Value?.Base == null) continue;
                    if (kvp.Key != locationKey) continue;

                    var banners = kvp.Value.Base.Banners;
                    if (banners == null)
                    {
                        banners = new List<Banner>();
                        kvp.Value.Base.Banners = banners;
                    }

                    // 已存在同 id 则替换图片，否则追加
                    var existing = banners.FirstOrDefault(x => x.Id == banner.Id);
                    if (existing != null)
                    {
                        existing.Picture = banner.Picture;
                    }
                    else
                    {
                        banners.Add(banner);
                    }
                }
            }
            var zhCNLang = context.DB.GetLocales().Global["ch"];
            zhCNLang.AddTransformer(lang =>
            {
                lang[bannerData.Id + " Name"] = bannerData.Name;
                lang[bannerData.Id + " Description"] = bannerData.Description;
                return lang;
            });
        }
    }
}
