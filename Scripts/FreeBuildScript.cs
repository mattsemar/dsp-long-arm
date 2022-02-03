using System;
using HarmonyLib;
using LongArm.UI;
using LongArm.Util;
using UnityEngine;

namespace LongArm.Scripts
{
    /// <summary>Handles free build functionality</summary>
    public class FreeBuildScript : MonoBehaviour
    {
        private bool _loggedException = false;
        private PrebuildManager _prebuildManager;
        private static FreeBuildScript _instance;

        private void Awake()
        {
            _instance = this;
        }

        void Update()
        {
            if (PluginConfig.buildBuildHelperMode.Value != BuildHelperMode.FreeBuild)
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

            if (_prebuildManager.HaveWork())
            {
                CompleteBuildPreviews();
            }
        }


        private void CompleteBuildPreviews()
        {
            var startTime = DateTime.Now;

            var outOfTime = false;
            while (_prebuildManager.HaveWork())
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > 600)
                {
                    break;
                }

                var pbId = _prebuildManager.TakePrebuild();
                if (pbId > 0)
                    DoFreeBuild(pbId);
            }
        }

        private void DoFreeBuild(int id)
        {
            try
            {
                if (GameMain.data.gameAbnormality.IsGameNormal() && !PluginConfig.playerConfirmedAbnormalityTrigger)
                {
                    Log.LogPopupWithFrequency("Not doing freebuild until confirmation received. Open UI and switch build mode off of FreeBuild and then back on");
                    return;
                }

                // Log.LogPopupWithFrequency("Setting abnormality bit for save");
                // GameMain.data.gameAbnormality.NotifyAbnormality();


                GameMain.localPlanet.factory.BuildFinally(GameMain.mainPlayer, id);
            }
            catch (Exception e)
            {
                Log.Warn($"Got exception building {id} {e}\r\n{e.StackTrace}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MechaDroneLogic), "UpdateTargets")]
        public static void UpdateDronesPrefix(MechaDroneLogic __instance)
        {
            
            var factory = GameMain.localPlanet.factory;
            if (PluginConfig.buildBuildHelperMode.Value != BuildHelperMode.FreeBuild || _instance == null || __instance.factory.prebuildCount <= 0)
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
                    _instance.DoFreeBuild(factory.prebuildRecycle[i]);
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
                        break;
                    _instance.DoFreeBuild(prebuildData.id);
                }
            }
        }
    }
}