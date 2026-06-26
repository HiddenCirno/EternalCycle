using Path = System.IO.Path;
using SPTarkov.Server.Core.Routers;

namespace EternalCycle
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

        public static void RegisterAvatarRoute(string avatarPath, string imageRoot, ImageRouter router)
        {
            string fileName = Path.GetFileName(avatarPath);

            string routeKey = avatarPath
                .Replace(".png", "")
                .Replace(".jpg", "");

            string fullPath = Path.Combine(imageRoot, fileName);

            router.AddRoute(routeKey, fullPath);
        }

        public static void RegisterIconRoute(string iconPath, string imageRoot, ImageRouter router)
        {
            string fileName = Path.GetFileName(iconPath);

            string routeKey = iconPath
                .Replace(".png", "")
                .Replace(".jpg", "");

            string fullPath = Path.Combine(imageRoot, fileName);

            router.AddRoute(routeKey, fullPath);
        }

        public static void RegisterQuestRoute(string questPath, string imageRoot, ImageRouter router)
        {
            string fileName = Path.GetFileName(questPath);

            string routeKey = questPath
                .Replace(".png", "")
                .Replace(".jpg", "");

            string fullPath = Path.Combine(imageRoot, fileName);

            router.AddRoute(routeKey, fullPath);
        }
    }
}
