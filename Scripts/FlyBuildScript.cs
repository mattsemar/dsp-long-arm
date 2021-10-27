using System;
using LongArm.UI;
using LongArm.Util;
using UnityEngine;

namespace LongArm.Scripts
{
    /// <summary>Handles free build functionality</summary>
    public class FlyBuildScript : MonoBehaviour
    {
        private bool _loggedException = false;
        private OrderNode _lastOrder;
        private DateTime _lastOrderCreatedAt = DateTime.Now.Subtract(TimeSpan.FromDays(5));
        private Vector3Int _positionWhenLastOrderGiven = Vector3Int.zero;
        private bool _issuedFly;
        private DateTime _issuedFlyTime;
        private bool _requestingConfirmationOfAbnormalityTrigger;
        private bool _gotConfirmationOfAbnormality;
        private bool _declinedAbnormalityCheck;
        private bool _popupOpened;
        private PrebuildManager _prebuildManager;

        void Update()
        {
            if (PluginConfig.buildBuildHelperMode != BuildHelperMode.FlyToBuild)
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
                EnsureFlying();
                if (Time.frameCount % 105 == 0)
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

            if (!_prebuildManager.HaveWork())
                return;
            var curIntPos = ToIntVector(GameMain.mainPlayer.position);
            var possiblyStuck = curIntPos.Equals(_positionWhenLastOrderGiven) && (DateTime.Now - _lastOrderCreatedAt).TotalSeconds > 5;
            if (_lastOrder == null || _lastOrder.targetReached || (DateTime.Now - _lastOrderCreatedAt).TotalSeconds > 5)
            {
                var closestPrebuild = _prebuildManager.TakeClosestPrebuild(GameMain.mainPlayer.position);
                if (closestPrebuild < 1)
                {
                    Log.Debug("got back 0 for closest prebuild");
                    return;
                }

                var nextPos = GameMain.localPlanet.factory.prebuildPool[closestPrebuild].pos;
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

        private static Vector3Int ToIntVector(Vector3 position)
        {
            return new Vector3Int((int)position.x, (int)position.y, (int)position.z);
        }
    }
}