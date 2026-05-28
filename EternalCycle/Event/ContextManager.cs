using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Utils.Cloners;
using System;

namespace EternalCycle
{
    public class ContextManager
    {
        public class OnRagfairLoadContext
        {
            public required DatabaseService DB { get; init; }

            public required JsonUtil JsonUtil { get; init; }

            public required ConfigServer ConfigServer { get; init; }

            public required ModHelper ModHelper { get; init; }

            public required ECLogger Logger { get; init; }

            public required ICloner Cloner { get; init; }
        }
    }
}