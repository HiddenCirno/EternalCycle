using SPTarkov.Common.Extensions;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;

namespace EternalCycleServer
{
    public class ContextManager
    {
        public class DatabaseService
        {
            private readonly TemplateTable _templates;
            private readonly LocaleTable _locales;
            private readonly GlobalTable _globals;
            private readonly TradersTable _traders;
            private readonly HideoutTable _hiedouts;
            private readonly LocationTable _locations;
            private readonly BotTable _bots;

            public DatabaseService(TemplateTable templates, LocaleTable locales, GlobalTable globals, TradersTable traders, HideoutTable hideouts, LocationTable locations, BotTable bots)
            {
                _templates = templates;
                _locales = locales;
                _globals = globals;
                _traders = traders;
                _hiedouts = hideouts;
                _locations = locations;
                _bots = bots;
            }

            public Dictionary<MongoId, TemplateItem> GetItems() => _templates.Items;
            public HandbookBase GetHandbook() => _templates.Handbook;
            public Dictionary<MongoId, Quest> GetQuests() => _templates.Quests;
            public Dictionary<MongoId, double> GetPrices() => _templates.Prices;
            public LocaleTable GetLocales() => _locales;
            public List<Achievement> GetAchievements() => _templates.Achievements;
            public TradersTable GetTraders() => _traders;
            public TemplateTable GetTemplates() => _templates;
            public GlobalTable GetGlobals() => _globals;
            public Dictionary<MongoId, CustomizationItem> GetCustomization() => _templates.Customization;
            public HideoutTable GetHideout() => _hiedouts;
            public LocationTable GetLocations() => _locations;
            public BotTable GetBots() => _bots;

            public Trader GetTrader(MongoId traderid) => _traders.GetTrader(traderid);

            public Location? GetLocation(string locationId)
            {
                var desiredLocation = GetLocations().GetByJsonProperty<Location>(locationId.ToLowerInvariant());
                if (desiredLocation == null)
                {
                    //logger.Error(serverLocalisationService.GetText("database-no_location_found_with_id", locationId));

                    return null;
                }

                return desiredLocation;
            }
        }


        public class ConfigServer
        {
            private readonly Dictionary<Type, object> _configs = new();
            // 直接注入 IServiceProvider
            public ConfigServer(IServiceProvider serviceProvider)
            {
                var configAssembly = typeof(BaseConfig).Assembly;
                var configTypes = configAssembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(BaseConfig).IsAssignableFrom(t));
                foreach (var type in configTypes)
                {
                    var config = serviceProvider.GetService(type); // 逐个解析
                    if (config != null)
                    {
                        _configs[type] = config;
                    }
                }
            }

            public T GetConfig<T>() where T : BaseConfig
            {
                if (_configs.TryGetValue(typeof(T), out var config))
                    return (T)config;
                throw new KeyNotFoundException($"Configuration {typeof(T).Name} not found.");
            }

            // 保留手动注册，留给你的自定义配置
            public void Register<T>(T config) where T : class
            {
                _configs[typeof(T)] = config;
            }
        }

        // ============= 你的 LoadModContext (对外接口完全不变) =============
        public class LoadModContext
        {
            public required DatabaseService DB { get; init; }
            public required JsonUtil JsonUtil { get; init; }
            public required ConfigServer ConfigServer { get; init; }
            public required ModHelper ModHelper { get; init; }
            public required ECLogger Logger { get; init; }
            public required ImageRouter ImageRouter { get; init; }
            public required ItemHelper ItemHelper { get; init; }
            public required PresetHelper PresetHelper { get; init; }
            public required ICloner Cloner { get; init; }
        }
    }
}