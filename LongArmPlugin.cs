using System;
using System.Collections.Generic;
using BepInEx;
using CommonAPI;
using CommonAPI.Systems;
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
    [BepInDependency(CommonAPIPlugin.GUID)]
    [CommonAPISubmoduleDependency(nameof(CustomKeyBindSystem))]
    public class LongArmPlugin : BaseUnityPlugin
    {
        private const string PluginGuid = "semarware.dysonsphereprogram.LongArm";
        private const string PluginName = "LongArm";
        private const string PluginVersion = "1.2.0";
        private Harmony _harmony;
        public static LongArmPlugin instance;
        private bool _initted;
        public float SavedBuildArea { get; private set; }
        private HashSet<Type> _scriptTypesInitted = new HashSet<Type>();
        private List<Component> _scripts = new List<Component>();

        private static readonly Type[] _scriptTypes =
        {
            typeof(FreeBuildScript),
            typeof(FastBuildScript),
            typeof(PrebuildManager),
            typeof(FlyBuildScript),
            typeof(FactoryActionExecutor),
            typeof(LongArmUi),
            typeof(BuildPreviewSummary),
            typeof(TourFactoryScript)
        };

        private TourFactoryScript _tourFactoryScript;

        private void Awake()
        {
            logger = Logger;
            instance = this;
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(LongArmPlugin));
            // _harmony.PatchAll(typeof(KeyBindPatch));
            _harmony.PatchAll(typeof(MechaPatch));
            _harmony.PatchAll(typeof(PrebuildManager));
            _harmony.PatchAll(typeof(LongArmUi));
            _harmony.PatchAll(typeof(TourFactoryScript));
            _harmony.PatchAll(typeof(FastBuildScript));
            _harmony.PatchAll(typeof(FreeBuildScript));
            // KeyBindPatch.Init();
            PluginConfig.InitConfig(Config);
            RegisterKeyBinds();
            Debug.Log($"LongArm Plugin Loaded");
        }

        private void Update()
        {
            if (GameMain.mainPlayer == null || UIRoot.instance == null)
                return;
            if (GameMain.instance.isMenuDemo)
                return;
            if (!GameMain.isRunning)
                return;
            if (!_initted)
            {
                SavedBuildArea = Math.Max(Math.Min(GameMain.mainPlayer.mecha.buildArea, Configs.freeMode.mechaBuildArea = SavedBuildArea), 80);
                if (PluginConfig.overrideBuildRange.Value)
                {
                    Enable();
                }
                else
                {
                    Disable();
                }

                _initted = true;
            }

            InitScripts();
        }

        public void Disable()
        {
            GameMain.mainPlayer.mecha.buildArea = Math.Max(Math.Min(Configs.freeMode.mechaBuildArea, SavedBuildArea), 80);
        }

        private void InitScripts()
        {
            if (GameMain.isRunning && GameMain.mainPlayer != null && GameMain.localPlanet != null && _scriptTypesInitted.Count == 0)
            {
                foreach (var scriptType in _scriptTypes)
                {
                    if (_scriptTypesInitted.Contains(scriptType))
                        continue;
                    var script = gameObject.AddComponent(scriptType);
                    _scriptTypesInitted.Add(scriptType);
                    _scripts.Add(script);
                    if (script is TourFactoryScript factoryScript)
                    {
                        _tourFactoryScript = factoryScript;
                    }
                }
            }
        }


        private void OnDestroy()
        {
            foreach (var script in _scripts)
            {
                if (script != null && script.gameObject != null)
                {
                    Destroy(script.gameObject);
                }
            }

            _scripts.Clear();
            _scriptTypesInitted.Clear();
            if (_initted)
            {
                GameMain.mainPlayer.mecha.buildArea = Math.Max(Math.Min(Configs.freeMode.mechaBuildArea, SavedBuildArea), 80);
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

            if (instance._tourFactoryScript != null)
            {
                instance._tourFactoryScript.Visible = false;
            }

            GameMain.mainPlayer.mecha.buildArea = instance.SavedBuildArea;
            PluginConfig.buildBuildHelperMode = BuildHelperMode.None;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(GameMain), "Start")]
        public static void OnGameStart()
        {
            if (GameMain.instance.isMenuDemo)
                return;
            PluginConfig.buildBuildHelperMode = BuildHelperMode.None;
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
            var range = 600;
            if (GameMain.localPlanet != null && GameMain.localPlanet.realRadius > 201)
            {
                range = (int)(GameMain.localPlanet.realRadius * Mathf.PI);
            }

            GameMain.mainPlayer.mecha.buildArea = range;
        }

        private void RegisterKeyBinds()
        {
            CustomKeyBindSystem.RegisterKeyBind<PressKeyBind>(new BuiltinKey
            {
                id = 108,
                key = new CombineKey((int)KeyCode.L, CombineKey.CTRL_COMB, ECombineKeyAction.OnceClick, false),
                conflictGroup = 2052,
                name = "Show LongArm Window",
                canOverride = true
            });
            CustomKeyBindSystem.RegisterKeyBind<PressKeyBind>(new BuiltinKey
            {
                id = 109,
                key = new CombineKey((int)KeyCode.W, CombineKey.CTRL_COMB, ECombineKeyAction.OnceClick, false),
                conflictGroup = 2052,
                name = "Show LongArm Factory Tour",
                canOverride = true
            });
        }
    }
}

