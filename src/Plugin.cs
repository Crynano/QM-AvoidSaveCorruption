using HarmonyLib;
using MGSC;
using System.IO;

namespace AvoidSaveCorruption
{
    public static class Plugin
    {
        public static ConfigDirectories ConfigDirectories = new ConfigDirectories();
        public static Logger Logger = new Logger();

        [Hook(ModHookType.AfterConfigsLoaded)]
        public static void AfterConfig(IModContext context)
        {
            Directory.CreateDirectory(ConfigDirectories.ModPersistenceFolder);
            new Harmony("Crynano_" + ConfigDirectories.ModAssemblyName).PatchAll();
        }

    }
}
