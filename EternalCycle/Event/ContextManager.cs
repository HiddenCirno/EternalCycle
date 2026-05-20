using SPTarkov.Server.Core.Models.Logging;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Mod;
using System;

namespace EternalCycle
{
    public class ContextManager
    {
        public class OnRagfairLoadContext
        {
            public DatabaseService DB { get; init; }
            public ECLogger Logger { get; init; }
        }
    }
}