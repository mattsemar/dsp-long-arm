using System.Collections.Generic;
using LongArm.FactoryLocation;
using LongArm.Player;
using LongArm.Util;
using UnityEngine;

namespace LongArm.Scripts
{
    public enum ActionType
    {
        AddFuel,
        AddBots
    }

    /// <summary>Performs actions on components of local factory</summary>
    public class FactoryActionExecutor : MonoBehaviour
    {
        public static FactoryActionExecutor Instance { get; private set; }

        // private ActionType _action = ActionType.None;
        private Queue<ActionType> _requestedActions = new Queue<ActionType>(15);

        public void RequestAddFuel()
        {
            if (_requestedActions.Contains(ActionType.AddFuel))
                return;
            _requestedActions.Enqueue(ActionType.AddFuel);
        }

        public void RequestAddBots()
        {
            if (_requestedActions.Contains(ActionType.AddBots))
                return;
            _requestedActions.Enqueue(ActionType.AddBots);
        }

        private void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            if (GameMain.mainPlayer == null || GameMain.localPlanet == null || GameMain.localPlanet.factory == null || GameMain.localPlanet.factory.factorySystem == null ||
                !LongArmPlugin.Initted())
                return;
            if (_requestedActions.Count == 0)
                return;

            var actionType = _requestedActions.Dequeue();
            switch (actionType)
            {
                case ActionType.AddFuel:
                    Fill();
                    break;
                case ActionType.AddBots:
                    AddBots();
                    break;
                default:
                    Log.Warn($"unexpected action type encountered {actionType}");
                    break;
            }
        }

        private void AddBots()
        {
            var stationComponents = FactoryLocationProvider.instance.GetStations();
            var inventoryManager = InventoryManager.instance;
            if (inventoryManager == null)
                return;

            var dronesNeeded = 0;
            var dronesAdded = 0;
            var vesselsNeeded = 0;
            var vesselsAdded = 0;
            foreach (var stationComponent in stationComponents)
            {
                if (stationComponent.isCollector)
                    continue;
                ItemProto stationProto = ItemUtil.GetItemProto(GameMain.localPlanet.factory.entityPool[stationComponent.entityId].protoId);
                var (droneNeeded, droneAdded) = FillDrones(stationComponent, stationProto, inventoryManager, !PluginConfig.topOffDrones.Value);
                dronesNeeded += droneNeeded;
                dronesAdded += droneAdded;

                if (stationComponent.isStellar)
                {
                    var (needed, added) = FillVessels(stationComponent, stationProto, inventoryManager, !PluginConfig.topOffVessels.Value);
                    vesselsNeeded += needed;
                    vesselsAdded += added;
                }
            }
            Log.LogAndPopupMessage($"Added {dronesAdded} / {dronesNeeded} drones and {vesselsAdded} / {vesselsNeeded} logistics vessels");
        }

        private (int needed, int added) FillDrones(StationComponent stationComponent, ItemProto stationProto, InventoryManager inventoryManager, bool skipNonEmpty = true)
        {
            if (PluginConfig.maxDronesToAdd.Value == 0)
            {
                return (0, 0);
            }

            // var desiredAmount = 50 - stationComponent.workDroneCount;
            var currentDroneCnt = stationComponent.idleDroneCount + stationComponent.workDroneCount;
            if (currentDroneCnt > 0 && skipNonEmpty)
            {
                return (0, 0);
            }

            var allowedMaxDrones = stationProto != null ? stationProto.prefabDesc.stationMaxDroneCount : 10;
            if (PluginConfig.maxDronesToAdd.Value < 100)
            {
                allowedMaxDrones = Mathf.CeilToInt(allowedMaxDrones * (PluginConfig.maxDronesToAdd.Value / 100f));
                Log.Debug($"Allowed max drones lowered to {allowedMaxDrones}");
            }

            int dronesToAdd = allowedMaxDrones - currentDroneCnt;
            if (dronesToAdd <= 0)
                return (0, 0);

            var dronesRemovedFromInventory = inventoryManager.TakeItems(5001, dronesToAdd);
            if (dronesRemovedFromInventory > 0)
            {
                stationComponent.idleDroneCount += dronesRemovedFromInventory;
                Log.LogPopupWithFrequency("Added {0} drones from inventory", dronesRemovedFromInventory);
            }

            return (dronesToAdd, dronesRemovedFromInventory);
        }

        private (int vesselsNeeded, int vesselsAdded) FillVessels(StationComponent stationComponent, ItemProto stationProto, InventoryManager inventoryManager,
            bool skipNonEmpty = true)
        {
            if (PluginConfig.maxVesselsToAdd.Value == 0)
            {
                return (0, 0);
            }

            var currentVesselCnt = stationComponent.idleShipCount + stationComponent.workShipCount;
            if (currentVesselCnt > 0 && skipNonEmpty)
            {
                return (0, 0);
            }

            var allowedMaxVessels = stationProto != null ? stationProto.prefabDesc.stationMaxShipCount : 10;
            if (PluginConfig.maxVesselsToAdd.Value < 100)
            {
                allowedMaxVessels = Mathf.CeilToInt(allowedMaxVessels * (PluginConfig.maxVesselsToAdd.Value / 100f));
                Log.Debug($"Allowed max vessels lowered to {allowedMaxVessels}");
            }

            int vesselsToAdd = allowedMaxVessels - currentVesselCnt;
            if (vesselsToAdd <= 0)
                return (0, 0);

            var vesselsRemoved = inventoryManager.TakeItems(5002, vesselsToAdd);
            if (vesselsRemoved > 0)
            {
                stationComponent.idleShipCount += vesselsRemoved;
                Log.LogPopupWithFrequency("Added {0} vessels from inventory", vesselsRemoved);
            }

            return (vesselsToAdd, vesselsRemoved);
        }

        private void Fill()
        {
            Log.Debug("executing power network fill");

            var factory = GameMain.localPlanet?.factory;
            if (factory == null)
            {
                Log.Warn("Requested filling of generators but no planet no factory found");
                return;
            }

            var invMgr = InventoryManager.instance;
            if (invMgr == null)
            {
                Log.Warn("InvMgr instance not obtained can not fill");
                return;
            }

            var alreadyFilledCount = 0;
            var needFuelCount = 0;
            var actuallyFilledCount = 0;
            var totalGenCount = 0;
            for (int i = 1; i < factory.powerSystem.genCursor; i++)
            {
                var generator = factory.powerSystem.genPool[i];
                if (generator.id != i)
                {
                    continue;
                }

                totalGenCount++;
                if (generator.curFuelId > 0 && generator.fuelId > 0)
                {
                    alreadyFilledCount++;
                    continue;
                }

                needFuelCount++;
                int[] fuelNeed = ItemProto.fuelNeeds[generator.fuelMask];
                if (fuelNeed == null)
                {
                    Log.Debug($"generator has no needs {generator.fuelMask}");
                    continue;
                }

                var filled = false;
                foreach (var fuelItemId in fuelNeed)
                {
                    if (fuelItemId > 0)
                    {
                        if (invMgr.RemoveItemImmediately(fuelItemId, 1))
                        {
                            generator.SetNewFuel(fuelItemId, 1);
                            factory.powerSystem.genPool[generator.id].SetNewFuel(fuelItemId, 1);
                            filled = true;
                            actuallyFilledCount++;
                            break;
                        }
                    }
                }

                if (!filled)
                {
                    var generatorType = ItemUtil.GetItemName(factory.entityPool[generator.entityId].protoId);
                    Log.LogPopupWithFrequency($"No fuel found in inventory for generator {generatorType}");
                }
            }

            Log.Debug($"Total generators: {totalGenCount}, {alreadyFilledCount} already filled, {actuallyFilledCount} actually filled, {needFuelCount} need fuel");
        }
    }
}