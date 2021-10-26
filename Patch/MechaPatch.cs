using System.IO;
using HarmonyLib;
using LongArm.Util;

namespace LongArm.Patch
{
    [HarmonyPatch]
    public static class MechaPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Mecha), "Export")]
        public static bool ExportPrefix(ref Mecha __instance, BinaryWriter w, out float __state)
        {
            if (__instance == null)
            {
                __state = 0f;
                return true;
            }

            if (LongArmPlugin.instance == null || LongArmPlugin.instance.SavedBuildArea < 0.01f)
            {
                Log.Debug($"Unable to get default build area from plugin instance. instance == null {LongArmPlugin.instance == null}");
                __state = 0f;
                return true;
            }

            Log.Debug($"Temporarily reverting mecha build area change prior to save {__instance.buildArea} to {LongArmPlugin.instance.SavedBuildArea}");

            __state = __instance.buildArea;
            __instance.buildArea = LongArmPlugin.instance.SavedBuildArea;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Mecha), "Export")]
        public static void ExportPostfix(ref Mecha __instance, BinaryWriter w, float __state)
        {
            if (__instance == null)
                return;
            if (LongArmPlugin.instance == null || LongArmPlugin.instance.SavedBuildArea < 0.01f)
            {
                Log.Debug($"Unable to get default build area from plugin instance. instance == null {LongArmPlugin.instance == null}");
                return;
            }

            if (__state < 0.01f)
            {
                Log.Debug($"State variable did not get set to the previously saved value {__state}");
                return;
            }

            Log.Debug($"Restoring mecha build area change post save {__instance.buildArea} to {__state}");

            __instance.buildArea = __state;
        }
    }
}