using BepInEx;
using HarmonyLib;
using LongArm.Patch;
using LongArm.Scripts;
using LongArm.UI;
using LongArm.Util;
using UnityEngine;
using static LongArm.Util.Log;
using Debug = UnityEngine.Debug;

namespace LongArm
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("DSPGAME.exe")]
    public class LongArmPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "semarware.dysonsphereprogram.LongArm";
        private const string PluginName = "LongArm";
        private const string PluginVersion = "0.0.1";
        private Harmony _harmony;
        public static LongArmPlugin instance;
        private bool _initted;
        public float SavedBuildArea { get; private set; }
        private FreeBuildScript _freeBuildScript;
        private FastBuildScript _fastBuildScript;
        private PrebuildManager _prebuildManager;
        private FlyBuildScript _flyBuildScript;
        private LongArmUi _longArmUi;

        private void Awake()
        {
            logger = Logger;
            instance = this;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(LongArmPlugin));
            _harmony.PatchAll(typeof(KeyBindPatch));
            _harmony.PatchAll(typeof(MechaPatch));
            _harmony.PatchAll(typeof(PrebuildManager));
            _harmony.PatchAll(typeof(LongArmUi));
            KeyBindPatch.Init();
            Debug.Log($"LongArm Plugin Loaded");
        }


        private void Update()
        {
            if (GameMain.mainPlayer == null || UIRoot.instance == null)
                return;
            if (!GameMain.isRunning)
                return;
            if (!_initted)
            {
                SavedBuildArea = GameMain.mainPlayer.mecha.buildArea;
                if (PluginConfig.buildMode == Mode.ExtendedRange)
                {
                    Enable();
                }
                PluginConfig.InitConfig(Config);

                _initted = true;
            }
            InitScripts();

            if (KeyBindPatch.GetKeyBind("ShowLongArmWindow").keyValue)
            {
                if (_longArmUi != null)
                    _longArmUi.Visible = !_longArmUi.Visible;
            }
        }

        public void Disable()
        {
            GameMain.mainPlayer.mecha.buildArea = SavedBuildArea;
            Configs.freeMode.mechaBuildArea = SavedBuildArea;
            PluginConfig.buildMode = Mode.Disabled;
        }

        private void InitScripts()
        {
            if (_fastBuildScript == null && GameMain.isRunning && GameMain.mainPlayer != null && GameMain.localPlanet != null)
            {
                _fastBuildScript = gameObject.AddComponent<FastBuildScript>();
            }
            if (_freeBuildScript == null && GameMain.isRunning && GameMain.mainPlayer != null && GameMain.localPlanet != null)
            {
                _freeBuildScript = gameObject.AddComponent<FreeBuildScript>();
            }
            if (_flyBuildScript == null && GameMain.isRunning && GameMain.mainPlayer != null && GameMain.localPlanet != null)
            {
                _flyBuildScript = gameObject.AddComponent<FlyBuildScript>();
            }
            if (_prebuildManager == null && GameMain.isRunning && GameMain.mainPlayer != null && GameMain.localPlanet != null)
            {
                _prebuildManager = gameObject.AddComponent<PrebuildManager>();
            }
            if (_longArmUi == null && GameMain.isRunning && GameMain.mainPlayer != null && GameMain.localPlanet != null)
            {
                _longArmUi = gameObject.AddComponent<LongArmUi>();
            }
        }


        private void OnDestroy()
        {
            if (_freeBuildScript != null && _freeBuildScript.gameObject != null)
            {
                Destroy(_freeBuildScript.gameObject);
                _freeBuildScript = null;
            }
            
            if (_fastBuildScript != null && _fastBuildScript.gameObject != null)
            {
                Destroy(_fastBuildScript.gameObject);
                _fastBuildScript = null;
            }

            if (_flyBuildScript != null && _flyBuildScript.gameObject != null)
            {
                Destroy(_flyBuildScript.gameObject);
                _flyBuildScript = null;
            }

            if (_prebuildManager != null && _prebuildManager.gameObject != null)
            {
                Destroy(_prebuildManager.gameObject);
                _prebuildManager = null;
            }
            if (_longArmUi != null && _longArmUi.gameObject != null)
            {
                Destroy(_longArmUi.gameObject);
                _longArmUi = null;
            }


            if (_initted)
            {
                GameMain.mainPlayer.mecha.buildArea = SavedBuildArea;
                Configs.freeMode.mechaBuildArea = SavedBuildArea;
            }

            _initted = false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(GameMain), "End")]
        public static void OnGameEnd()
        {
            if (instance == null || !instance._initted)
            {
                return;
            }

            GameMain.mainPlayer.mecha.buildArea = instance.SavedBuildArea;
            Configs.freeMode.mechaBuildArea = instance.SavedBuildArea;
            PluginConfig.buildMode = Mode.Disabled;
        }
        
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerAction_Inspect), "GetObjectSelectDistance")]
        public static bool InterceptSelectDistance(ref float __result)
        {
            if (instance == null || !instance._initted)
            {
                return true;
            }

            if (!PluginConfig.overrideInspectionRange.Value)
                return true;
            __result = 600f;
            return false;
        }

        public static bool Initted()
        {
            if (instance == null)
            {
                return false;
            }

            return instance._initted;
        }

        public void Enable()
        {
            PluginConfig.buildMode = Mode.ExtendedRange;
            var range = 600;
            if (GameMain.localPlanet != null && GameMain.localPlanet.realRadius > 201)
            {
                range = (int)(GameMain.localPlanet.realRadius * Mathf.PI);
            }

            GameMain.mainPlayer.mecha.buildArea = range;
            Configs.freeMode.mechaBuildArea = range;
        }
    }
}


