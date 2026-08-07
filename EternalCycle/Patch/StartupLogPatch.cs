using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Services.Hosted;
using SPTarkov.Server.Core.Services.Locales;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System.Reflection;
using static EternalCycleServer.ContextManager;

namespace EternalCycleServer
{
    [Injectable]
    public class StartupLogPatch : AbstractPatch
    {
        private static TemplateTable _templateTable = default!;
        private static LocaleTable _localeTable = default!;
        private static GlobalTable _globalTable = default!;
        private static TradersTable _tradersTable = default!;
        private static HideoutTable _hideoutTable = default!;
        private static LocationTable _locationTable = default!;
        private static JsonUtil _jsonUtil = default!;
        private static ConfigServer _configServer = default!;
        private static ModHelper _modHelper = default!;
        private static ItemHelper _itemHelper = default!;
        private static LocaleService _localeService = default!;
        private static ICloner _cloner = default!;
        private static PresetHelper _presetHelper = default!;
        private static ImageRouter _imageRouter = default!;
        private static ECLogger _logger = default!;
        public StartupLogPatch(
        TemplateTable templateTable,
        LocaleTable localeTable,
        GlobalTable globalTable,
        TradersTable tradersTable,
        HideoutTable hideoutTable,
        LocationTable locationTable,
        JsonUtil jsonUtil,
        ConfigServer configServer,
        ModHelper modHelper,
        ItemHelper itemHelper,
        LocaleService localeService,
        ICloner cloner,
        PresetHelper presetHelper,
        ImageRouter imageRouter)
        {
            _templateTable = templateTable;
            _localeTable = localeTable;
            _globalTable = globalTable;
            _tradersTable = tradersTable;
            _hideoutTable = hideoutTable;
            _locationTable = locationTable;
            _jsonUtil = jsonUtil;
            _configServer = configServer;
            _modHelper = modHelper;
            _itemHelper = itemHelper;
            _localeService = localeService;
            _cloner = cloner;
            _presetHelper = presetHelper;
            _imageRouter = imageRouter;
            _logger = new ECLogger("SPTStartupHostedService", true);
        }
        private static readonly Random random = new Random();

        private static readonly Dictionary<string, string> LoadingTextDicrt = new Dictionary<string, string>()
        {
            { "loading_text_001", "汝将最后一次沐浴，在温热耀眼的黄金中。" },
            { "loading_text_002", "汝将碎作千片，凋零在他乡的土壤。" },
            { "loading_text_003", "终有一日，汝将背后负创而死。" },
            { "loading_text_004", "花海尽头，生者的魂灵将温暖汝之指尖……相拥过后，便是永恒的离别。" },
            { "loading_text_005", "汝将超越至纯粹之终极，回归腐败枯黑。" },
            { "loading_text_006", "在彩虹桥的尽头，天空之子将缝补晨昏。" },
            { "loading_text_007", "汝将与贪婪同行，亦将亡于分文。" },
            { "loading_text_008", "汝将肩负骄阳，直至灰白的黎明显著。" },
            { "loading_text_009", "汝将长眠于涛声中，于天地境界之海完成征服。" },
            { "loading_text_011", "汝将于天地境界之海完成征服，长眠于涛声中。" },
            { "loading_text_012", "岁月泰坦并没有留下神谕，因为无漏主早已知晓一切。" },
            { "loading_text_013", "汝将自掘坟墓,焚毁于叛逆的熔炉。" },
            { "loading_text_014", "汝将收梢于花开时，一如终结诞下起始。" },
            { "loading_text_015", "如果这就是一切的尽头，那么这一宿命就由我来划上休止符。" },
            { "loading_text_016", "即使死亡也无法将我们分离。" },
            { "loading_text_017", "我曾目睹星辰焚寂，沉入永夜……" },
            { "loading_text_018", "英雄可不能临阵脱逃啊！" },
            { "loading_text_019", "为什么你每次都能在危险之下逃生？为什么你总是能安全返回藏身处？这一切的真相只有一个！" }
        };
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SPTStartupHostedService).GetMethod("GetRandomisedStartMessage", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        }

        [PatchPostfix]
        public static void Postfix(ref string __result)
        {

            // 这是目标方法原版执行完、刚刚准备把返回的文字丢给 logger.Success 打印时的瞬间
            //Utils.commonLogger.Info("======================================");
            Utils.commonLogger.Info((LoadingTextDicrt.TryGetValue($"loading_text_{random.Next(1, 20):D3}", out var val) && random.Next(1, 100) <=1 ? val : "即是起点也是终点，即是过去也是未来，即为「永恒」"));
            //Utils.commonLogger.Info("======================================");

            // 【高级操作】你可以篡改即将打印的绿字内容
            // __result = __result + " [EternalCycle Mod 已就绪]";
        }
    }
}