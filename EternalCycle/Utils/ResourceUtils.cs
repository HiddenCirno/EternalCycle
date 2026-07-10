using System.Security.Cryptography;

namespace EternalCycleServer
{
    public class ResourceUtils
    {
        public static Dictionary<string, string> BundleHashes { get; private set; } = new();
        public static Dictionary<string, string> BundleBase64Data { get; private set; } = new();
        public static Dictionary<string, string> SlotIconHashes { get; private set; } = new();
        public static Dictionary<string, string> SlotIconBase64Data { get; private set; } = new();
        public static Dictionary<string, string> DecoIconHashes { get; private set; } = new();
        public static Dictionary<string, string> DecoIconBase64Data { get; private set; } = new();
        public static Dictionary<string, string> TargetHashes { get; private set; } = new();
        public static Dictionary<string, string> TargetBase64Data { get; private set; } = new();
        public static Dictionary<string, string> VoicePath { get; private set; } = new();

        /// <summary>
        /// 注册 Bundle 资源
        /// </summary>
        public static void RegisterRigLayoutResource(string modPath, string path)
        {
            EventManager.DataLoadEvent.LoadResourceEvent += (context) =>
            {
                try
                {
                    InitResourceData(modPath, path, BundleHashes, BundleBase64Data);
                }
                catch (Exception ex)
                {
                    EventManager.EventLogger.Error($"初始化 Bundle 时发生错误：{path}", ex);
                }
            };
        }

        /// <summary>
        /// 注册 Slot 资源
        /// </summary>
        public static void RegisterSlotIconResource(string modPath, string path)
        {
            EventManager.DataLoadEvent.LoadResourceEvent += (context) =>
            {
                try
                {
                    InitResourceData(modPath, path, SlotIconHashes, SlotIconBase64Data);
                }
                catch (Exception ex)
                {
                    EventManager.EventLogger.Error($"初始化 Slot 时发生错误：{path}", ex);
                }
            };
        }

        /// <summary>
        /// 注册 Deco 资源
        /// </summary>
        public static void RegisterDecoIconResource(string modPath, string path)
        {
            EventManager.DataLoadEvent.LoadResourceEvent += (context) =>
            {
                try
                {
                    InitResourceData(modPath, path, DecoIconHashes, DecoIconBase64Data);
                }
                catch (Exception ex)
                {
                    EventManager.EventLogger.Error($"初始化 Deco 时发生错误：{path}", ex);
                }
            };
        }

        /// <summary>
        /// 注册 靶纸 资源
        /// </summary>
        public static void RegisterTargetResource(string modPath, string path)
        {
            EventManager.DataLoadEvent.LoadResourceEvent += (context) =>
            {
                try
                {
                    InitResourceData(modPath, path, TargetHashes, TargetBase64Data);
                }
                catch (Exception ex)
                {
                    EventManager.EventLogger.Error($"初始化 Target 时发生错误：{path}", ex);
                }
            };
        }

        private static void InitResourceData(string modPath, string path, Dictionary<string, string> hashDict, Dictionary<string, string> base64Dict)
        {
            var correctPath = Path.Combine(modPath, path);
            List<string> filesToProcess = new List<string>();

            // 1. 判断是文件夹还是单文件
            if (Directory.Exists(correctPath))
            {
                // 文件夹模式：带上 AllDirectories 防止漏搜子目录
                //你搜你妈的子目录, 傻逼吧
                filesToProcess = Directory.GetFiles(correctPath).ToList();
            }
            else if (File.Exists(correctPath))
            {
                // 单文件模式：直接把文件塞进列表
                filesToProcess.Add(correctPath);
            }
            else
            {
                EventManager.EventLogger.Warn($"注册资源异常：找不到指定的文件或文件夹 {correctPath}");
                return;
            }

            if (filesToProcess.Count == 0)
            {
                EventManager.EventLogger.Warn($"资源为空：{correctPath}");
                return;
            }

            // 2. 统一进行 MD5 和 Base64 处理
            int loadedCount = 0;
            using (var md5 = MD5.Create())
            {
                foreach (var file in filesToProcess)
                {
                    // 拍扁：只要文件名
                    string fileName = Path.GetFileName(file);

                    byte[] fileData = File.ReadAllBytes(file);
                    var hashBytes = md5.ComputeHash(fileData);

                    string md5String = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    string base64String = Convert.ToBase64String(fileData);

                    // 写入指定的字典
                    hashDict[fileName] = md5String;
                    base64Dict[fileName] = base64String;

                    loadedCount++;
                }
            }

            EventManager.EventLogger.Info($"成功加载了来自 {correctPath} 的 {loadedCount} 个资源。");
        }
    }
}