using System;
using LongArm.Player;
using LongArm.UI;
using LongArm.Util;
using UnityEngine;

namespace LongArm.Scripts
{
    public class FactorySprayer
    {
        private readonly ItemProto _targetItem;
        private readonly int _targetLevelIndex;
        private readonly ItemProto _proliferatorItem;
        private readonly PlanetTransport _transport;
        private readonly CargoTraffic _traffic;
        private readonly FactorySystem _factorySystem;
        private readonly bool _freeMode;
        private int _skippedItems;
        private int _neededSprayAccumulator;
        private int _availableSprays;

        public FactorySprayer(ItemProto targetItem, int targetLevel)
        {
            _targetItem = targetItem;
            var levelNdx = targetLevel;
            if (targetLevel == 3)
            {
                levelNdx = 4;
            }

            _targetLevelIndex = levelNdx;
            var proliferatorItemId = targetLevel > 0 ? 1140 + targetLevel : 0;
            if (proliferatorItemId > 0)
            {
                _proliferatorItem = LDB.items.Select(proliferatorItemId);
            }
            else
            {
                _proliferatorItem = LDB.items.Select(1141);
            }

            _transport = GameMain.localPlanet.factory.transport;
            _factorySystem = GameMain.localPlanet.factory.factorySystem;
            _traffic = GameMain.localPlanet.factory.cargoTraffic;
            _freeMode = PluginConfig.buildBuildHelperMode.Value == BuildHelperMode.FreeBuild;
        }

        public bool Prompt()
        {
            if (PluginConfig.buildBuildHelperMode.Value == BuildHelperMode.FreeBuild)
                return true;
            _skippedItems = 0;
            _neededSprayAccumulator = 0;
            var (prolifCount, prolifInc) = InventoryManager.instance.CountItems(_proliferatorItem.ID);
            _availableSprays = prolifCount * _proliferatorItem.HpMax;

            var sprayNeededForStations = 0;
            var sprayForBelts = SprayBeltItems(true);
            var sprayForAssemblers = SprayAssemblerContents(true);
            var sprayForGenerators = SprayGenerators(true);
            if (PluginConfig.sprayStationContents.Value)
            {
                sprayNeededForStations = SprayStations(true);
            }

            var message = "Spray items";
            if (_neededSprayAccumulator == 0)
            {
                Log.LogAndPopupMessage($"No items found to spray. Found {_skippedItems} already sprayed");
                return false;
            }

            var sprayItemTypeBreakdownMessage = "Items to spray: ";
            if (sprayNeededForStations > 0)
            {
                sprayItemTypeBreakdownMessage += $"\r\n\t{sprayNeededForStations} in stations";
            }

            if (sprayForBelts > 0)
            {
                sprayItemTypeBreakdownMessage += $"\r\n\t{sprayForBelts} on belts";
            }

            if (sprayForAssemblers > 0)
            {
                sprayItemTypeBreakdownMessage += $"\r\n\t{sprayForAssemblers} in assemblers";
            }

            if (sprayForGenerators > 0)
            {
                sprayItemTypeBreakdownMessage += $"\r\n\t{sprayForGenerators} in generators";
            }

            if (_skippedItems > 0)
            {
                sprayItemTypeBreakdownMessage += $"\r\nSkipping {_skippedItems} items already sprayed at or above target level";
            }

            var itemsToUse = Mathf.CeilToInt(  _neededSprayAccumulator / (float)_proliferatorItem.HpMax);
            if (_neededSprayAccumulator % _proliferatorItem.HpMax == 0)
            {
                itemsToUse = _neededSprayAccumulator / _proliferatorItem.HpMax;
            }
            
            message = $"{sprayItemTypeBreakdownMessage}\r\n\r\n";
            message +=
                $"This will use {itemsToUse} of {_proliferatorItem.name} from inventory (at {_proliferatorItem.HpMax} sprays / spray item).";
            message += $"\r\nYou currently have {prolifCount} in inventory";
            if (_neededSprayAccumulator / _proliferatorItem.HpMax > prolifCount)
            {
                message += " so not all items will be sprayed";
            }
            else
            {
                message += " so all targeted items will be sprayed.";
            }

            if (PluginConfig.sprayStationContents.Value && sprayNeededForStations > 0 && _neededSprayAccumulator > _availableSprays)
            {
                message += "\r\n\tNote: station contents are not partially sprayed to avoid waste. Add more spray to inventory if stations contents remain unsprayed";
            }


            UIMessageBox.Show("Spray factory items", message,
                "Ok", "Cancel", 0, DoSprayAction, () => { Log.LogAndPopupMessage("Canceled"); });
            return false;
        }

        public void DoSprayAction()
        {
            if (_freeMode)
            {
                if (PluginConfig.sprayStationContents.Value)
                {
                    SprayStations(false);
                }

                SprayBeltItems(false);
                SprayAssemblerContents(false);
                SprayGenerators(false);
                return;
            }

            _neededSprayAccumulator = 0;
            var availableSpraysRslt = InventoryManager.instance.CountItems(_proliferatorItem.ID);
            _availableSprays = availableSpraysRslt.cnt * _proliferatorItem.HpMax;

            SprayBeltItems(false);
            if (_neededSprayAccumulator >= _availableSprays)
            {
                InventoryManager.instance.RemoveItemImmediately(_proliferatorItem.ID, availableSpraysRslt.cnt, out _);
                Log.Debug("halting after spraying belts");
                return;
            }


            SprayAssemblerContents(false);
            if (_neededSprayAccumulator >= _availableSprays)
            {
                InventoryManager.instance.RemoveItemImmediately(_proliferatorItem.ID, availableSpraysRslt.cnt, out _);
                Log.Debug("halting after spraying assemblers");
                return;
            }


            SprayGenerators(false);
            if (_neededSprayAccumulator >= _availableSprays)
            {
                InventoryManager.instance.RemoveItemImmediately(_proliferatorItem.ID, availableSpraysRslt.cnt, out _);
                Log.Debug("halting after spraying generators");
                return;
            }

            if (PluginConfig.sprayStationContents.Value)
            {
                SprayStations(false);
                if (_neededSprayAccumulator >= _availableSprays)
                {
                    InventoryManager.instance.RemoveItemImmediately(_proliferatorItem.ID, availableSpraysRslt.cnt, out _);
                    Log.Debug("halting during station spray");
                    return;
                }
            }

            var itemsToRemove = _neededSprayAccumulator / _proliferatorItem.HpMax;
            Log.Debug($"Removing {itemsToRemove} spray items from inv");
            InventoryManager.instance.RemoveItemImmediately(_proliferatorItem.ID, itemsToRemove, out _);
        }


        int SprayBeltItems(bool countOnly)
        {
            var cargoTraffic = _traffic;
            var cargoContainer = cargoTraffic.container;
            var cargoPool = cargoContainer.cargoPool;
            var cursor = cargoContainer.cursor;
            var result = 0;
            for (int i = 0; i < cursor; i++)
            {
                ref var cargo = ref cargoPool[i];
                if (cargo.item == 0 || cargo.item != _targetItem.ID)
                    continue;


                var beltItemCount = cargo.stack;
                var targetIncLevel = beltItemCount * _targetLevelIndex - cargo.inc;
                // if item is already sprayed above target level, skip it
                if (targetIncLevel <= 0)
                {
                    _skippedItems += beltItemCount;
                    continue;
                }

                _neededSprayAccumulator += beltItemCount;

                if (!_freeMode)
                {
                    result += beltItemCount;
                }

                if (!countOnly)
                {
                    if (_freeMode || _availableSprays >= _neededSprayAccumulator)
                    {
                        cargo.inc = (byte)(_targetLevelIndex * beltItemCount);
                    }
                    else if (!_freeMode)
                    {
                        return result;
                    }
                }
            }

            return result;
        }

        int SprayAssemblerContents(bool countOnly)
        {
            var resultCountOfSprayedItems = 0;
            for (int i = 0; i < _factorySystem.assemblerCursor; i++)
            {
                ref var assemblerComponent = ref _factorySystem.assemblerPool[i];
                if (assemblerComponent.id == 0 || assemblerComponent.requireCounts == null)
                    continue;

                for (int j = 0; j < assemblerComponent.requireCounts.Length; j++)
                {
                    if (assemblerComponent.requires[j] != _targetItem.ID)
                    {
                        continue;
                    }

                    var sprayTargetLevel = assemblerComponent.served[j] * _targetLevelIndex - assemblerComponent.incServed[j];
                    // if item is already sprayed above target level, skip it
                    if (sprayTargetLevel <= 0)
                    {
                        _skippedItems += assemblerComponent.served[j];
                        continue;
                    }

                    _neededSprayAccumulator += assemblerComponent.served[j];

                    if (!_freeMode)
                    {
                        resultCountOfSprayedItems += assemblerComponent.served[j];
                    }

                    if (!countOnly)
                    {
                        if (_freeMode || _availableSprays >= _neededSprayAccumulator)
                        {
                            assemblerComponent.incServed[j] = assemblerComponent.served[j] * _targetLevelIndex;
                        }
                        else if (!_freeMode)
                        {
                            return resultCountOfSprayedItems;
                        }
                    }
                }
            }

            return resultCountOfSprayedItems;
        }


        int SprayStations(bool countOnly)
        {
            var itemsToSprayInStations = 0;
            for (int i = 1; i < _transport.stationCursor; i++)
            {
                var station = _transport.stationPool[i];
                if (station == null || station.id != i)
                {
                    continue;
                }

                for (int j = 0; j < station.storage.Length; j++)
                {
                    ref var store = ref station.storage[j];
                    if (store.itemId == 0 || store.itemId != _targetItem.ID)
                    {
                        continue;
                    }

                    var sprayTargetLevel = store.count * _targetLevelIndex - store.inc;
                    // if item is already sprayed above target level, skip it
                    if (sprayTargetLevel <= 0)
                    {
                        _skippedItems += store.count;

                        Log.Debug($"skipping spray for item in stations {store.count}");
                        continue;
                    }

                    _neededSprayAccumulator += store.count;

                    if (!_freeMode)
                    {
                        itemsToSprayInStations += store.count;
                    }

                    if (!countOnly)
                    {
                        if (_freeMode || _availableSprays >= _neededSprayAccumulator)
                        {
                            store.inc = _targetLevelIndex * store.count;
                        }
                        else if (!_freeMode)
                        {
                            Log.Debug($"bailing on spraying station {store.count} {_neededSprayAccumulator} {_availableSprays}");
                            return itemsToSprayInStations;
                        }
                    }
                }
            }

            return itemsToSprayInStations;
        }

        // this one needs to track spray used instead of sprayed items to avoid losing too much 
        int SprayGenerators(bool countOnly)
        {
            var result = 0;
            for (int i = 0; i < _factorySystem.factory.powerSystem.genCursor; i++)
            {
                ref var generator = ref _factorySystem.factory.powerSystem.genPool[i];
                if (generator.id == 0)
                    continue;
                if (generator.catalystId == 0 && generator.curFuelId == 0 && generator.fuelId == 0)
                    continue;


                if (generator.catalystId > 0)
                {
                    if (generator.catalystId != _targetItem.ID)
                        continue;
                    var beansInRr = generator.catalystPoint / 3600d;
                    var sprayedBeans = (generator.catalystIncPoint / 3600d);
                    var beansToSpray = Mathf.CeilToInt((float)(beansInRr - sprayedBeans));

                    if (beansToSpray <= 0)
                    {
                        _skippedItems += Mathf.CeilToInt((float)beansInRr);
                        continue;
                    }

                    _neededSprayAccumulator += beansToSpray;
                    if (!_freeMode)
                    {
                        result += beansToSpray;
                    }

                    if (!countOnly)
                    {
                        if (_freeMode || _availableSprays >= _neededSprayAccumulator)
                        {
                            generator.catalystIncPoint = generator.catalystPoint * _targetLevelIndex;
                        }
                        else if (!_freeMode)
                        {
                            return result;
                        }
                    }
                }
                else if (generator.fuelId > 0)
                {
                    if (generator.fuelId != _targetItem.ID)
                        continue;
                    var neededSpray = _targetLevelIndex * generator.fuelCount - generator.fuelInc;
                    if (neededSpray <= 0)
                    {
                        _skippedItems += generator.fuelCount;
                        continue;
                    }

                    _neededSprayAccumulator += generator.fuelCount;

                    if (!_freeMode)
                    {
                        result += generator.fuelCount;
                    }

                    if (!countOnly)
                    {
                        if (_freeMode || _availableSprays >= _neededSprayAccumulator)
                        {
                            generator.fuelInc = (short)(generator.fuelCount * _targetLevelIndex);
                        }
                        else if (!_freeMode)
                        {
                            return result;
                        }
                    }
                }
            }

            return result;
        }
    }
}