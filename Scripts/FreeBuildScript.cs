using System;
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

        void Update()
        {
            if (PluginConfig.buildMode != Mode.FreeBuild)
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
                if (GameMain.data.abnormalityCheck.IsFunctionNormal(GameAbnormalityCheck.BIT_MECHA))
                {
                    Log.LogPopupWithFrequency("Setting abnormality bit for save");
                    GameMain.abnormalityCheck.mechaCheck.abnormalityCheck.NotifyAbnormalityChecked(GameAbnormalityCheck.BIT_MECHA, true);
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