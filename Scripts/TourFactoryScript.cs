using System;
using LongArm.FactoryLocation;
using LongArm.UI;
using LongArm.Util;
using UnityEngine;

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
    /// <summary>Handles free build functionality</summary>
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
                return;
            FactoryLocationProvider.instance?.Sync();
            if (PluginConfig.TourMode == FactoryTourMode.None)
                return;
            if (FactoryLocationProvider.instance == null || !FactoryLocationProvider.instance.HasWork())
                return;
            if (_currentAction.actionDir == ActionDir.None)
                return;
            
            EnsureFlying();
            if (_currentAction.actionDir == ActionDir.Auto)
            {
                FlyToNextLocation();
            }
        }


        private void FlyToNextLocation()
        {
            if (GameMain.mainPlayer.orders.orderCount > 1)
            {
                Log.Debug("found existing orders, not adding new ones");
                return;
            }

            var locationProvider = FactoryLocationProvider.instance;
            if (locationProvider == null)
                return;

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
            if (GameMain.mainPlayer.controller.movementStateInFrame != EMovementState.Fly && !_issuedFly)
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
            FactoryLocationProvider.instance?.NotifyModeChange(newMode);
        }

        private static Vector3Int ToIntVector(Vector3 position)
        {
            return new Vector3Int((int)position.x, (int)position.y, (int)position.z);
        }

        public static void RequestAuto()
        {
            if (Instance == null)
                return;
            Instance._currentAction.actionDir = ActionDir.Auto;
        }

        public static void RequestNext()
        {
            if (Instance == null)
                return;
            Instance._currentAction.actionDir = ActionDir.Next;
            Instance.FlyToNextLocation();
        }
        public static void RequestPrevious()
        {
            if (Instance == null)
                return;
            Instance._currentAction.actionDir = ActionDir.Previous;
            Instance.FlyToNextLocation();
        }

        public static int GetTotalLocations()
        {
            var provider = FactoryLocationProvider.instance;
            if (provider == null)
                return 0;
            return provider.GetCurrentIndex().totalPoints;
        }
        public static int GetCurrentIndex()
        {
            var provider = FactoryLocationProvider.instance;
            if (provider == null)
                return 0;
            return provider.GetCurrentIndex().curIndex;
        }
    }
}