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
[Obsolete("此类型未使用, 但是我怕产生编译问题, 所以留着")]
public static class LazyLoadUtils
{
    public static void AddItem<T>(this LazyLoad<List<T>> lazyList, T item)
    {
        if (lazyList == null) return;

        lazyList.AddTransformer(list =>
        {
            if (list == null)
                list = new List<T>();

            list.Add(item);
            return list;
        });
    }
}










