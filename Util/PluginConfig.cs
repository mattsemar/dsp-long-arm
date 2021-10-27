using BepInEx.Configuration;
using LongArm.UI;

namespace LongArm.Util
{
    public static class PluginConfig
    {
        // not an actual BepInEx Config because we don't want it to be persisted
        public static BuildHelperMode buildBuildHelperMode = BuildHelperMode.None;
        public static bool playerConfirmedAbnormalityTrigger = false;
        public static ConfigEntry<bool> overrideBuildRange;
        public static ConfigEntry<bool> overrideInspectionRange;

        
        public static void InitConfig(ConfigFile confFile)
        {
            buildBuildHelperMode = BuildHelperMode.None;
            overrideBuildRange = confFile.Bind("Main", "OverrideBuildRange", true,
                "Extend build range for construction bots");
            overrideInspectionRange = confFile.Bind("Main", "OverrideInspectionRange", true,
                "Extend inspection range for opening machine/storage windows from map view");
        }
    }
}