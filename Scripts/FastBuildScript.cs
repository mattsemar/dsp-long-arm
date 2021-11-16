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
        private Guid _inventorySnapShotFromLastPass = Guid.Empty;
        private static FastBuildScript _instance;

        private void Awake()
        {
            if (_instance == null)
                _instance = this;
        }

        void Update()
        {
            if (PluginConfig.buildBuildHelperMode != BuildHelperMode.FastBuild)
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

            if (_prebuildManager.HaveWork())
            {
                CompleteBuildPreviews();
            }
        }


        private void CompleteBuildPreviews()
        {
            var startTime = DateTime.Now;

            var outOfTime = false;
            var builtSomething = false;
            var inventoryHash = InventoryManager.GetInventoryHash();
            if (InventoryUnchangedAndNoPrevBuild(inventoryHash))
                return;
            while (_prebuildManager.HaveWork())
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > 400)
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
                _inventorySnapShotFromLastPass = InventoryManager.GetInventoryHash();
                _lastFailedBuild = DateTime.Now.Ticks;
            }
        }

        private bool InventoryUnchangedAndNoPrevBuild(Guid inventoryHash)
        {
            if (_builtSomethingOnLastPass || _inventorySnapShotFromLastPass != inventoryHash)
            {
                return false;
            }

            return !(new TimeSpan(DateTime.Now.Ticks - _lastFailedBuild).TotalSeconds > 2);
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
                    GameMain.mainPlayer.package.TakeTailItems(ref itemId, ref count);
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
            if (PluginConfig.buildBuildHelperMode == BuildHelperMode.FastBuild && _instance != null)
            {
                var startTime = DateTime.Now;

                foreach (var prebuild in __instance.factory.prebuildPool)
                {  
                    if ((DateTime.Now - startTime).TotalMilliseconds > 600)
                    {
                        break;
                    }
                    _instance.DoFastBuild(prebuild.id, false);
                }
            }
        }
    }
}