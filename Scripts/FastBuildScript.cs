using System;
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
            while (_prebuildManager.HaveWork())
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > 400)
                {
                    break;
                }

                var pbId = _prebuildManager.TakePrebuild();
                if (pbId > 0)
                    DoFastBuild(pbId);
            }
        }

        private void DoFastBuild(int id)
        {
            try
            {
                var prebuildData = GameMain.localPlanet.factory.prebuildPool[id];
                if (prebuildData.id < 1)
                    return;
                if (prebuildData.itemRequired > 0)
                {
                    int itemId = prebuildData.protoId;
                    var count = 1;
                    GameMain.mainPlayer.package.TakeTailItems(ref itemId, ref count);
                    if (count == 0)
                        return;
                }
                GameMain.localPlanet.factory.BuildFinally(GameMain.mainPlayer, id);
            }
            catch (Exception e)
            {
                Log.Warn($"Got exception building {id} {e}\r\n{e.StackTrace}");
            }
        }
    }
}