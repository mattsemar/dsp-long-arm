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
        AddFuelAllPlanets,
        AddBots
    }

    /// <summary>Performs actions on components of local factory</summary>
    public class FactoryActionExecutor : MonoBehaviour
    {
        private static readonly int ARTIFICIAL_STAR_ID = 2210;
        private static readonly int THERMAL_POWER_PLANT = 2204;
        private static readonly int RAY_RECEIVER = 2208;
        private static readonly int FUSION_POWER_PLANT = 2211;
        public static FactoryActionExecutor Instance { get; private set; }

        // private ActionType _action = ActionType.None;
        private Queue<ActionType> _requestedActions = new Queue<ActionType>(15);

        public void RequestAddFuel(bool universally)
        {
            ActionType actionType = universally ? ActionType.AddFuelAllPlanets : ActionType.AddFuel;
            if (_requestedActions.Contains(actionType))
                return;
            _requestedActions.Enqueue(actionType);
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
                    Fill(true);
                    break;
                case ActionType.AddFuelAllPlanets :
                    Fill(false);
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
            var stationComponents = FactoryLocationProvider.Instance.GetStations();
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

            Log.Debug($"need to add {allowedMaxDrones} - {currentDroneCnt}");

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
                Log.Debug("Not adding vessels since maxVesselsToAdd == 0");
                return (0, 0);
            }

            var currentVesselCnt = stationComponent.idleShipCount + stationComponent.workShipCount;
            if (currentVesselCnt > 0 && skipNonEmpty)
            {
                Log.Debug($"Not adding vessels since current vessel count > 0 ({currentVesselCnt})");
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

        private void Fill(bool planetOnly = true)
        {
            Log.Debug("executing power network fill");

            var factory = GameMain.localPlanet?.factory;

            if (factory == null && planetOnly)
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
            if (planetOnly)
            {
                var result = FillPlanetFactoryFuel(invMgr, factory);
                alreadyFilledCount += result.alreadyFilledCount;
                needFuelCount += result.needFuelCount;
                actuallyFilledCount += result.actuallyFilledCount;
                totalGenCount += result.totalGenCount;
            }
            else
            {
                foreach (StarData star in GameMain.universeSimulator.galaxyData.stars)
                {
                    if (star?.planets == null)
                        continue;
                    foreach (var planet in star.planets)
                    {
                        if (planet?.factory?.factorySystem != null && planet.factory.transport != null && planet.factory.transport.stationCursor != 0)
                        {
                            var result = FillPlanetFactoryFuel(invMgr, planet.factory);
                            alreadyFilledCount += result.alreadyFilledCount;
                            needFuelCount += result.needFuelCount;
                            actuallyFilledCount += result.actuallyFilledCount;
                            totalGenCount += result.totalGenCount;
                        }
                    }
                }
            }

            Log.Debug($"Total generators: {totalGenCount}, {alreadyFilledCount} already filled, {actuallyFilledCount} actually filled, {needFuelCount} needed fuel");
            Log.LogAndPopupMessage($"Added fuel to {actuallyFilledCount} generators / {needFuelCount} needed fuel ({totalGenCount} total)");
        }

        private (int alreadyFilledCount, int needFuelCount, int actuallyFilledCount, int totalGenCount) FillPlanetFactoryFuel(InventoryManager inventoryManager,
            PlanetFactory factory)
        {
            var alreadyFilledCount = 0;
            var needFuelCount = 0;
            var actuallyFilledCount = 0;
            var totalGenCount = 0;
            for (int i = 1; i < factory.powerSystem.genCursor; i++)
            {
                var generator = factory.powerSystem.genPool[i];
                if (generator.id != i)
                    continue;
                
                var entity = factory.entityPool[generator.entityId];

                totalGenCount++;
                // Log.Debug($"checking generator {JsonUtility.ToJson(generator)} {entity.protoId} {ItemUtil.GetItemName(entity.protoId)}");

                if (IsGeneratorFueled(generator, entity))
                {
                    // Log.Debug($"Generator already has fuel {ItemUtil.GetItemName(entity.protoId)} {factory.planet.displayName}");
                    alreadyFilledCount++;
                    continue;
                }
                
                needFuelCount++;
                int[] fuelNeed = ItemProto.fuelNeeds[generator.fuelMask];
                if (fuelNeed == null)
                {
                    Log.Debug($"generator has no needs {generator.fuelMask}");
                    needFuelCount--;
                    continue;
                }

                var filled = false;
                foreach (var fuelItemId in fuelNeed)
                {
                    if (fuelItemId > 0)
                    {
                        if (inventoryManager.RemoveItemImmediately(fuelItemId, 1))
                        {
                            Log.Debug($"Removed {ItemUtil.GetItemName(fuelItemId)} from inventory for gen on planet {factory.planet.displayName}");
                            generator.SetNewFuel(fuelItemId, 1, 0);
                            factory.powerSystem.genPool[generator.id].SetNewFuel(fuelItemId, 1, 0);
                            filled = true;
                            actuallyFilledCount++;
                            break;
                        }
                        // Log.Debug($"Tried to add fuel {ItemUtil.GetItemName(fuelItemId)} to generator, but none in inv");
                    }
                }

                if (!filled && generator.catalystId > 0)
                {
                    if (generator.catalystPoint > 0)
                    {
                        alreadyFilledCount++;
                        filled = true;
                        
                    } else if (inventoryManager.RemoveItemImmediately(generator.catalystId, 1))
                    {
                        factory.powerSystem.genPool[generator.id].catalystPoint += 3600;
                        filled = true;
                        actuallyFilledCount++;
                    }
                }

                if (!filled)
                {
                    var generatorType = ItemUtil.GetItemName(factory.entityPool[generator.entityId].protoId);
                    Log.Warn($"No fuel found in inventory for generator {generatorType}");
                }
            }

            return (alreadyFilledCount, needFuelCount, actuallyFilledCount, totalGenCount);
        }

        private bool IsGeneratorFueled(PowerGeneratorComponent generator, EntityData entityData)
        {
            if (generator.photovoltaic || generator.wind)
            {
                return true;
            }
            if (entityData.protoId == ARTIFICIAL_STAR_ID)
            {
                return generator.fuelId > 0 || generator.curFuelId > 0;
            }

            if (entityData.protoId == THERMAL_POWER_PLANT)
            {
                return generator.curFuelId > 0 && generator.currentStrength >0;
            }

            if (entityData.protoId == RAY_RECEIVER)
            {
                return generator.catalystId > 0 && generator.catalystPoint > 0;
            }

            if (entityData.protoId == FUSION_POWER_PLANT)
            {
                return generator.currentStrength > 0 && generator.curFuelId > 0;
            }

            Log.Debug($"Generator type not explicitly handled for add fuel case");
            return false;
        }
    }
}