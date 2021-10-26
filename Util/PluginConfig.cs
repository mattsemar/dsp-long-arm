using BepInEx.Configuration;
using LongArm.UI;

namespace LongArm.Util
{
    public static class PluginConfig
    {
        // not an actual BepInEx Config because we don't want it to be persisted
        public static Mode buildMode = Mode.Disabled;
        public static ConfigEntry<bool> overrideInspectionRange;

        
        public static void InitConfig(ConfigFile confFile)
        {
            buildMode = Mode.Disabled;
            overrideInspectionRange = confFile.Bind("Main", "OverrideInspectionRange", true,
                "Extend inspection range for opening machine/storage windows from map view");
        }
    }
}