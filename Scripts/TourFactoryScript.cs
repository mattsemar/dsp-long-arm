using System;
using System.Linq;
using HarmonyLib;
using LongArm.FactoryLocation;
using LongArm.UI;
using LongArm.Util;
using UnityEngine;
using static LongArm.UI.LongArmUi;

namespace LongArm.Scripts
{
    public enum ActionDir
    {
        Next,
        Previous,
        Auto,
        None
    }

    public class Action
    {
        public ActionDir actionDir = ActionDir.None;
    }

    /// <summary>Handles flying player from location to location and showing window for it</summary>
    public class TourFactoryScript : MonoBehaviour
    {
        private bool _loggedException = false;
        private OrderNode _lastOrder;
        private DateTime _lastOrderCreatedAt = DateTime.Now.Subtract(TimeSpan.FromDays(5));
        private Vector3Int _positionWhenLastOrderGiven = Vector3Int.zero;
        private bool _issuedFly;
        private DateTime _issuedFlyTime;
        public static TourFactoryScript Instance { get; private set; }
        private readonly Action _currentAction = new Action();
        private static ItemProto _filteredItem;
        private static bool _filteringEntitiesMissingItem;
        private bool _requestHide;
        private Vector2 _scrollViewVector;
        private Rect windowRect = Rect.zero;

        void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            if (PluginConfig.TourMode == FactoryTourMode.None)
                return;

            if (GameMain.mainPlayer == null || GameMain.localPlanet == null || GameMain.localPlanet.factory == null || GameMain.localPlanet.factory.factorySystem == null ||
                !LongArmPlugin.Initted())
            {
                _requestHide = true;
                return;
            }

            if (FactoryLocationProvider.Instance == null)
            {
                _requestHide = true;
                return;
            }

            FactoryLocationProvider.Instance?.Sync();
            if (PluginConfig.TourMode == FactoryTourMode.None)
                return;
            if (FactoryLocationProvider.Instance == null || !FactoryLocationProvider.Instance.HasWork())
                return;
            if (_currentAction.actionDir == ActionDir.None)
                return;

            if (_currentAction.actionDir == ActionDir.Auto)
            {
                EnsureFlying();
                FlyToNextLocation();
            }
        }

        void OnGUI()
        {
            if (Instance == null)
                Instance = this;

            // Visible = _prebuildSummary.items.Count > 0;

            if (!Visible)
                return;
            if (_requestHide)
            {
                OnClose();
                return;
            }

            var uiGame = UIRoot.instance.uiGame;
            if (uiGame == null)
            {
                return;
            }

            if (uiGame.starmap.active || uiGame.dysonmap.active || uiGame.globemap.active || uiGame.escMenu.active || uiGame.techTree.active)
            {
                return;
            }

            var uiInstance = instance;
            if (uiInstance == null)
                return;


            Init();
            uiInstance.SaveCurrentGuiOptions();
            windowRect = GUILayout.Window(1297895144, windowRect, DrawMainWindowContents, "LongArm Factory Tour");
            uiInstance.RestoreGuiSkinOptions();
            EatInputInRect(windowRect);
        }

        private void DrawMainWindowContents(int id = 1111)
        {
            GUILayout.BeginArea(new Rect(windowRect.width - 25f, 0, 25f, 30f));
            if (GUILayout.Button("X"))
            {
                OnClose();
                return;
            }

            GUILayout.EndArea();
            {
                GUILayout.BeginVertical("Box");
                DrawFactoryTourSection();

                GUILayout.EndVertical();
            }
            GUI.DragWindow();
        }

        private void Init()
        {
            InitWindowRect();
        }

        private void InitWindowRect()
        {
            if (windowRect != Rect.zero)
                return;
            var width = Mathf.Min(Screen.width, ScaleToDefault(300));
            var height = Screen.height < 560 ? Screen.height : ScaleToDefault(560, false);
            var offsetX = Screen.width - Mathf.RoundToInt((Screen.width - width) / 2f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
            windowRect = new Rect(offsetX, offsetY, width, height);


            Mathf.RoundToInt(windowRect.width / 2.5f);
        }

        private void OnClose()
        {
            Visible = false;
            _requestHide = false;

            instance.RestoreGuiSkinOptions();
        }

        public bool Visible { get; set; }


        private void FlyToNextLocation()
        {
            if (GameMain.mainPlayer.orders.orderCount > 1)
            {
                Log.Debug("found existing orders, not adding new ones");
                return;
            }

            var locationProvider = FactoryLocationProvider.Instance;
            if (locationProvider == null)
                return;
            EnsureFlying();
            var curIntPos = ToIntVector(GameMain.mainPlayer.position);
            var possiblyStuck = curIntPos.Equals(_positionWhenLastOrderGiven) && (DateTime.Now - _lastOrderCreatedAt).TotalSeconds > 5;
            if (_lastOrder == null || _lastOrder.targetReached || (DateTime.Now - _lastOrderCreatedAt).TotalSeconds > 5)
            {
                if (!locationProvider.HasWork())
                    return;
                EntityLocation closest = locationProvider.GetEntity(_currentAction.actionDir);

                var nextPos = closest.position;
                _lastOrder = OrderNode.MoveTo(nextPos);
                _lastOrderCreatedAt = DateTime.Now;
                _positionWhenLastOrderGiven = ToIntVector(GameMain.mainPlayer.position);
                GameMain.mainPlayer.Order(_lastOrder, true);
            }
            else
            {
                Log.Debug($"last order {_lastOrder?.targetReached} {_lastOrder?.target} stuck: {possiblyStuck}");
            }
        }

        private void EnsureFlying()
        {
            if (PluginConfig.TourMode == FactoryTourMode.Stopped || !Visible)
                return;
            if (GameMain.mainPlayer.controller.movementStateInFrame != EMovementState.Fly && !_issuedFly && GameMain.mainPlayer.mecha.thrusterLevel > 1)
            {
                GameMain.mainPlayer.movementState = EMovementState.Fly;
                GameMain.mainPlayer.controller.actionWalk.SwitchToFly();
                _issuedFly = true;
                _issuedFlyTime = DateTime.Now;
                Log.Debug("Issued fly command");
            }

            if (_issuedFly && GameMain.mainPlayer.controller.movementStateInFrame == EMovementState.Fly && GameMain.mainPlayer.movementState == EMovementState.Fly)
            {
                // either our fly command worked or the player was already flying, reset the flag if enough time has passed so if they land we'll catch it
                if ((DateTime.Now - _issuedFlyTime).TotalSeconds > 5)
                {
                    _issuedFly = false;
                    Log.Debug("Reset issued fly command flag");
                }
            }
        }


        public void NotifyModeChange(FactoryTourMode newMode)
        {
            FactoryLocationProvider.Instance?.NotifyModeChange(newMode);
            Visible = newMode != FactoryTourMode.None;
        }

        private static Vector3Int ToIntVector(Vector3 position)
        {
            return new Vector3Int((int)position.x, (int)position.y, (int)position.z);
        }

        private void DrawFactoryTourSection()
        {
            GUILayout.BeginVertical("Box");
            var names = Enum.GetNames(typeof(FactoryTourMode));
            var selectedName = Enum.GetName(typeof(FactoryTourMode), PluginConfig.TourMode);
            var guiContents = names.Select(n => GetModeAsGuiContent(n, "", selectedName == n));
            GUILayout.Label("Factory Tour Mode");
            var curIndex = names.ToList().IndexOf(selectedName);
            var index = GUILayout.SelectionGrid(curIndex, guiContents.ToArray(), 2);

            if (index != curIndex)
            {
                if (Enum.TryParse(names[index], out FactoryTourMode newMode))
                {
                    PluginConfig.TourMode = newMode;
                    FactoryLocationProvider.Instance?.Sync();
                }
            }

            AddItemFilter();
            AddNeedItemFilter();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            var prevButton = GUILayout.Button(new GUIContent("Previous", "Go back to previous location in tour"));
            if (prevButton)
            {
                RequestPrevious();
            }

            var nextButton = GUILayout.Button(new GUIContent("Next", "Go back to next location in tour"));
            if (nextButton)
            {
                RequestNext();
            }

            var autoButton = GUILayout.Button(new GUIContent("Auto", "Automatically advance to next location for tour"));
            if (autoButton)
            {
                RequestAuto();
            }

            GUILayout.EndHorizontal();
            GUILayout.Label($"Current location index: {GetCurrentIndex()} / total points {GetTotalLocations()}");
            GUILayout.EndVertical();

            GUILayout.EndVertical();
        }

        private void AddItemFilter()
        {
            GUILayout.BeginVertical("Box");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter by item");

            var maxHeightSz = ItemUtil.GetItemImageHeight() / 2;
            var maxHeight = GUILayout.MaxHeight(maxHeightSz);
            // GUILayout.BeginHorizontal(GUI.skin.label, maxHeight);
            var rect = GUILayoutUtility.GetRect(maxHeightSz, maxHeightSz);
            var currSelected = new GUIContent("None", "No filter active");
            if (_filteredItem != null)
            {
                currSelected = new GUIContent(_filteredItem.iconSprite.texture, $"Currently touring only {_filteredItem.Name.Translate()}");
            }

            GUI.Label(rect, currSelected);
            var button = GUILayout.Button("Set Filter");
            if (button)
            {
                Vector2 pos = new Vector2(-300, 238);

                UIItemPicker.Close();
                UIItemPicker.Popup(pos, SetItemFilter);
            }

            var clearBtn = GUILayout.Button("Clear");
            if (clearBtn)
            {
                SetItemFilter(null);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private void AddNeedItemFilter()
        {
            GUILayout.BeginVertical("Box");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Needing item");

            var toggle = GUILayout.Toggle(_filteringEntitiesMissingItem, new GUIContent("Missing", "Only include entities that are missing items"));
            if (toggle != _filteringEntitiesMissingItem)
            {
                FactoryLocationProvider.Instance?.UseNeedItemFilter(toggle);
                _filteringEntitiesMissingItem = toggle;
            }


            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        void RequestAuto()
        {
            _currentAction.actionDir = ActionDir.Auto;
        }

        void RequestNext()
        {
            _currentAction.actionDir = ActionDir.Next;
            EnsureFlying();
            FlyToNextLocation();
        }

        void RequestPrevious()
        {
            if (Instance == null)
                return;
            Instance._currentAction.actionDir = ActionDir.Previous;
            EnsureFlying();
            Instance.FlyToNextLocation();
        }

        private int GetTotalLocations()
        {
            var provider = FactoryLocationProvider.Instance;
            if (provider == null)
                return 0;
            return provider.GetCurrentIndex().totalPoints;
        }

        private int GetCurrentIndex()
        {
            try
            {
                var provider = FactoryLocationProvider.Instance;
                if (provider == null)
                    return 0;
                return provider.GetCurrentIndex().curIndex;
            }
            catch (Exception e)
            {
                Log.Warn($"Need to fix this {e}\r\n{e.StackTrace}");
            }

            return 0;
        }

        void SetItemFilter(ItemProto proto)
        {
            _filteredItem = proto;
            FactoryLocationProvider.Instance?.SetItemFilter(proto);
        }

        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(PlayerMove_Sail), "GameTick")]
        public static void GameTick_Prefix(PlayerMove_Sail __instance)
        {
            
            var player = __instance.player;
            if (!player.sailing)
            {
                return;
            }

            var tourFactoryScript = Instance;
            if (tourFactoryScript == null || !tourFactoryScript.Visible || FactoryLocationProvider.Instance == null)
            {
                return;
            }

            tourFactoryScript._requestHide = true;
            FactoryLocationProvider.Instance.NotifyModeChange(FactoryTourMode.None);
        }
    }
}