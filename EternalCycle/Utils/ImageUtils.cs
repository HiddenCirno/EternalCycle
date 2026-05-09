using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System.Text.Json;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Services.Mod;
using System.Reflection;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils.Logger;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Utils.Json;
using Microsoft.Extensions.Logging;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Utils;
using Path = System.IO.Path;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Routers;
using System.IO;
using SPTarkov.Server.Core.Models.Spt.Templates;

namespace VulcanCore;


public class ImageUtils
{
    public static void RegisterFolderImageRoute(string path, string routepath, ImageRouter imageRouter)
    {
        List<string> fileNames = Directory.GetFiles(routepath).Select(Path.GetFileName).ToList();
        foreach (string fileName in fileNames) {
            string pathroute = $"{path}{fileName}";
            imageRouter.AddRoute(pathroute.Replace(".jpg", "").Replace(".png", ""), $"{Path.Combine(routepath, fileName)}");
        }
        //ImageUtils.RegisterImageRoute(traderBase.Avatar.Replace(".jpg", ""), Path.Combine(imagePath, Path.GetFileName(traderBase.Avatar)), imageRouter)
    }
    public static void RegisterImageRoute(string path, string routepath, ImageRouter imageRouter)
    {
        imageRouter.AddRoute(path, routepath);
    }

}










