using Path = System.IO.Path;
using SPTarkov.Server.Core.Routers;

namespace EternalCycleServer
{
    public class ImageUtils
    {
        public static void RegisterFolderImageRoute(string path, string routepath, ImageRouter imageRouter)
        {
            List<string> fileNames = Directory.GetFiles(routepath).Select(Path.GetFileName).ToList();
            foreach (string fileName in fileNames)
            {
                string pathroute = $"{path}{fileName}";
                imageRouter.AddRoute(pathroute.Replace(".jpg", "").Replace(".png", ""), $"{Path.Combine(routepath, fileName)}");
            }
            //ImageUtils.RegisterImageRoute(traderBase.Avatar.Replace(".jpg", ""), Path.Combine(imagePath, Path.GetFileName(traderBase.Avatar)), imageRouter)
        }

        public static void RegisterImageRoute(string path, string routepath, ImageRouter imageRouter)
        {
            imageRouter.AddRoute(path, routepath);
        }

        private static void RegisterCustomFilteredRoute(string assetPath, string imageRoot, ImageRouter router)
        {
            string fileName = Path.GetFileName(assetPath);
            string fileKey = Path.GetFileNameWithoutExtension(fileName);

            // 过滤原版 24 位 Hex ID
            if (!fileKey.IsHex24())
            {
                // 路由键仍然保留你原本的替换逻辑，以防 assetPath 包含多级目录路径
                string routeKey = assetPath.Replace(".png", "").Replace(".jpg", "");
                string fullPath = Path.Combine(imageRoot, fileName);

                router.AddRoute(routeKey, fullPath);
            }
        }

        public static void RegisterAvatarRoute(string avatarPath, string imageRoot, ImageRouter router)
            => RegisterCustomFilteredRoute(avatarPath, imageRoot, router);

        public static void RegisterIconRoute(string iconPath, string imageRoot, ImageRouter router)
            => RegisterCustomFilteredRoute(iconPath, imageRoot, router);

        public static void RegisterQuestRoute(string questPath, string imageRoot, ImageRouter router)
            => RegisterCustomFilteredRoute(questPath, imageRoot, router);
        public static void RegisterAchievementRoute(string iconPath, string imageRoot, ImageRouter router)
            => RegisterCustomFilteredRoute(iconPath, imageRoot, router);
    }
}
