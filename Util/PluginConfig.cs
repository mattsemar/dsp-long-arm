using System;
using System.Linq;
using BepInEx.Configuration;
using LongArm.Scripts;
using LongArm.UI;

namespace LongArm.Util
{
    public static class PluginConfig
    {
        public static ConfigEntry<BuildHelperMode> buildBuildHelperMode;
        
        // not an actual BepInEx Config because we don't want it to be persisted
        private static FactoryTourMode _factoryTourMode = FactoryTourMode.None;
        public static bool playerConfirmedAbnormalityTrigger = false;


        public static ConfigEntry<bool> overrideBuildRange;
        public static ConfigEntry<bool> overrideInspectionRange;
        public static ConfigEntry<int> dragBuildOverride;
        public static ConfigEntry<bool> sprayStationContents;
        
        public static ConfigEntry<bool> topOffDrones;
        public static ConfigEntry<bool> topOffVessels;

        public static ConfigEntry<int> maxDronesToAdd;
        public static ConfigEntry<int> maxVesselsToAdd;
        
        public static ConfigEntry<int> currentSprayTargetItem;

        public static FactoryTourMode TourMode
        {
            get => _factoryTourMode;
            set
            {
                if (TourFactoryScript.Instance != null)
                    TourFactoryScript.Instance.NotifyModeChange(value);
                _factoryTourMode = value;
            }
        }

        public static void InitConfig(ConfigFile confFile)
        {
            buildBuildHelperMode = confFile.Bind("Main", "Build Helper Mode", BuildHelperMode.None, "Current build helper mode");
            TourMode = FactoryTourMode.None;
            overrideBuildRange = confFile.Bind("Main", "OverrideBuildRange", true,
                "Extend build range for construction bots");
            overrideInspectionRange = confFile.Bind("Main", "OverrideInspectionRange", true,
                "Extend inspection range for opening machine/storage windows from map view");
            dragBuildOverride = confFile.Bind("Main", "Drag Build Override Limit", 0,
                "Set to more than 0 to change how many entities can be drag built at once");
            dragBuildOverride.SettingChanged += OnDragBuildOverrideChange;
            sprayStationContents = confFile.Bind("Main", "SprayStationContents", false,
                "Include items in stations when spraying");
            currentSprayTargetItem = confFile.Bind("UIInternal", "Current spray target item", 1006,
                new ConfigDescription("Internal only, tracks currently selected spray target item",
                    new AcceptableValueList<int>(LDB.items.dataArray.ToList().Select(i => i.ID).ToArray())));
            
            topOffDrones = confFile.Bind("Stations", "Top Off Drones", false,
                "Add drones even when not empty. When set, if there are 2 drones in station then 48 will be added, otherwise none will be added");
            topOffVessels = confFile.Bind("Stations", "Top Off Vessels", false,
                "Add vessels even when not empty. When set, if there are 8 vessels in station then 2 will be added, if not set, none will be added");

            maxDronesToAdd = confFile.Bind("Stations", "Max Percent Drones to Add", 100,
                new ConfigDescription("(percent) Override default behavior of adding max allowed drones. 0-100% supported, 0 skips adding drones",
                    new AcceptableValueRange<int>(0, 100)));

            maxVesselsToAdd = confFile.Bind("Stations", "Max Percent Vessels to Add", 100,
                new ConfigDescription("(percent) Override default behavior of adding max allowed vessels. 0-100% supported, 0 skips adding vessels",
                    new AcceptableValueRange<int>(0, 100)));
            if (LDB.items.Select(currentSprayTargetItem.Value) == null)
            {
                // reset so things don't get weird
                currentSprayTargetItem.Value = 1006;
            }
        }

        private static void OnDragBuildOverrideChange(object sender, EventArgs e)
        {
            if (sender is ConfigEntry<int> entry && dragBuildOverride.Value > 0 && dragBuildOverride.Value < 2000)
            {
                LongArmPlugin.instance.UpdateDragBuildLimit( entry.Value );
            }
        }
    }
}