using System;
using HarmonyLib;
using LongArm.Player;
using LongArm.UI;
using LongArm.Util;
using UnityEngine;

namespace LongArm.Scripts
{
    /// <summary>Handles fast build functionality which consumes items but does not wait for bots to do building</summary>
    public class FastBuildScript : MonoBehaviour
    {
        private bool _loggedException = false;
        private PrebuildManager _prebuildManager;
        private bool _builtSomethingOnLastPass = true;
        private long _lastFailedBuild = DateTime.Now.Ticks;
        private static FastBuildScript _instance;

        private void Awake()
        {
            if (_instance == null)
                _instance = this;
        }

        void Update()
        {
            if (PluginConfig.buildBuildHelperMode.Value != BuildHelperMode.FastBuild)
                return;

            if (_prebuildManager == null)
            {
                _prebuildManager = PrebuildManager.Instance;
                if (_prebuildManager == null)
                    return;
            }

            if (GameMain.mainPlayer == null || GameMain.localPlanet == null || GameMain.localPlanet.factory == null || GameMain.localPlanet.factory.factorySystem == null ||
                !LongArmPlugin.Initted())
                return;

            if (GameMain.mainPlayer.sailing) 
                return;
            if (Time.frameCount % 10 != 0)
                return;
            if (_prebuildManager.HaveWork())
            {
                Log.Debug($"looks like there is work to do");
                CompleteBuildPreviews();
            }
        }


        private void CompleteBuildPreviews()
        {
            var startTime = DateTime.Now;

            var outOfTime = false;
            var builtSomething = false;
            if (InventoryUnchangedAndNoPrevBuild())
                return;
            while (_prebuildManager.HaveWork())
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > 300)
                {
                    break;
                }

                var pbId = _prebuildManager.TakePrebuild();
                if (pbId > 0 && DoFastBuild(pbId))
                    builtSomething = true;
            }

            _builtSomethingOnLastPass = builtSomething;
            if (!builtSomething)
            {
                _lastFailedBuild = DateTime.Now.Ticks;
            }
        }

        private bool InventoryUnchangedAndNoPrevBuild()
        {
            bool inventoryChangedSince = InventoryManager.InventoryChangedSince(_lastFailedBuild);
            if (_builtSomethingOnLastPass || inventoryChangedSince)
            {
                return false;
            }

            return true;
        }

        private bool DoFastBuild(int id, bool returnOnNoInv = true)
        {
            try
            {
                var prebuildData = GameMain.localPlanet.factory.prebuildPool[id];
                if (prebuildData.id < 1)
                    return false;
                if (prebuildData.itemRequired > 0)
                {
                    int itemId = prebuildData.protoId;
                    var count = 1;
                    GameMain.mainPlayer.package.TakeTailItems(ref itemId, ref count, out int inc);
                    if (count == 0)
                    {
                        if (returnOnNoInv)
                            _prebuildManager.ReturnPrebuild(id);
                        return false;
                    }
                }

                GameMain.localPlanet.factory.BuildFinally(GameMain.mainPlayer, id);
                return true;
            }
            catch (Exception e)
            {
                Log.Warn($"Got exception building {id} {e}\r\n{e.StackTrace}");
                return false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MechaDroneLogic), "UpdateTargets")]
        public static void UpdateTargets_Postfix(MechaDroneLogic __instance)
        {
            var factory = GameMain.localPlanet.factory;
            if (PluginConfig.buildBuildHelperMode.Value != BuildHelperMode.FastBuild || _instance == null || __instance.factory.prebuildCount <= 0)
            {
                return;
            }

            var startTime = DateTime.Now;
                
            // this prebuild recycle logic adapted from https://github.com/Velociraptor115/DSPMods
            if (factory.prebuildRecycleCursor > 0)
            {
                // This means that we can probably get away with just looking at the recycle instances
                for (int i = factory.prebuildRecycleCursor; i < factory.prebuildCursor; i++)
                {
                    if ((DateTime.Now - startTime).TotalMilliseconds > 600)
                        break;
                    _instance.DoFastBuild(factory.prebuildRecycle[i], false);
                }
            }
            else
            {
                // Highly probable that a prebuildPool resize took place this tick.
                for (int i = 1; i < factory.prebuildCursor; i++)
                {
                    if (factory.prebuildPool[i].id != i)
                        continue;
                    var prebuildData = factory.prebuildPool[i];
                    if (prebuildData.id < 1)
                    {
                        continue;
                    }
                    if ((DateTime.Now - startTime).TotalMilliseconds > 600)
                    {
                        break;
                    }
                    _instance.DoFastBuild(prebuildData.id, false);
                }
            }
        }
    }
}