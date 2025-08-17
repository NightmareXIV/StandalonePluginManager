using ECommons.Configuration;
using ECommons.Singletons;
using StandalonePluginManager.SPMData;
using StandalonePluginManager.SPMServices;

namespace StandalonePluginManager
{
    public class StandalonePluginManager : IDalamudPlugin
    {
        public static StandalonePluginManager P;
        public static Config C;

        public StandalonePluginManager(IDalamudPluginInterface pluginInterface)
        {
            P = this;
            ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector);
            new TickScheduler(() =>
            {
                C = EzConfig.Init<Config>();
                SingletonServiceManager.Initialize(typeof(Services));
            });
        }

        public void Dispose()
        {
            ECommonsMain.Dispose();
            P = null;
            C = null;
        }
    }
}
