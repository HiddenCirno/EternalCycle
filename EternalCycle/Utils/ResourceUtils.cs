using System.Security.Cryptography;

namespace EternalCycleServer
{
    public class ResourceUtils
    {
        public static Dictionary<string, string> BundleHashes { get; private set; } = new();
        public static Dictionary<string, string> BundleBase64Data { get; private set; } = new();

        /// <summary>
        /// 注册 Bundle 资源
        /// </summary>
        public static void RegisterRigLayoutResource(string modPath, string targetFolder)
        {
            EventManager.DataLoadEvent.LoadResourceEvent += (context) =>
            {
                try
                {
                    InitResourceData(modPath, targetFolder, BundleHashes, BundleBase64Data);
                }
                catch (Exception ex)
                {
                    EventManager.EventLogger.Error($"初始化 Bundle 时发生错误：{targetFolder}", ex);
                }
            };
        }

        private static void InitResourceData(string modPath, string folderPath, Dictionary<string, string> hashDict, Dictionary<string, string> base64Dict)
        {
            var correctPath = Path.Combine(modPath, folderPath);
            if (!Directory.Exists(correctPath)) return;

            //你搜尼玛呢, 傻逼吧
            // 依然可以递归搜索服务端所有的子文件夹，但最终提取出来时会拍扁
            List<string> files = Directory.GetFiles(correctPath).ToList();
            if (files.Count == 0) return;

            int loadedCount = 0;
            using (var md5 = MD5.Create())
            {
                foreach (var file in files)
                {
                    // 【核心修改】：直接获取纯文件名 (如 "rig_01.bundle")，抛弃所有路径
                    string fileName = Path.GetFileName(file);

                    byte[] fileData = File.ReadAllBytes(file);
                    var hashBytes = md5.ComputeHash(fileData);

                    string md5String = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                    string base64String = Convert.ToBase64String(fileData);

                    // 字典的 Key 现在变成了纯文件名
                    hashDict[fileName] = md5String;
                    base64Dict[fileName] = base64String;

                    loadedCount++;
                }
            }
            EventManager.EventLogger.Info($"加载了来自{correctPath}的{loadedCount}个资源。");
        }
    }
}