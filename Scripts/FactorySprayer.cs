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
            _proliferatorItem = LDB.items.Select(proliferatorItemId > 0 ? proliferatorItemId : 1141);

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
            int originalSprayAvail;
            _availableSprays = originalSprayAvail = DetermineNumbersOfSprays(_proliferatorItem, prolifCount, prolifInc);
            var spraysPerSprayItem = (originalSprayAvail / (float)prolifCount);
            var sprayNeededForStations = 0;
            var sprayNeededForInventory = 0;
            var sprayForBelts = SprayBeltItems(true);
            var sprayForAssemblers = SprayAssemblerContents(true);
            var sprayForGenerators = SprayGenerators(true);
            if (PluginConfig.sprayInventoryContents.Value)
            {
                sprayNeededForInventory = SprayInventory(true);
            }

            if (PluginConfig.sprayStationContents.Value)
            {
                sprayNeededForStations = SprayStations(true);
            }

            var message = "(Actual values depend on state of factory after Ok button is pressed)\r\n";
            if (!PluginConfig.sprayStationContents.Value || !PluginConfig.sprayInventoryContents.Value)
            {
                var configMessage = "";
                if (!PluginConfig.sprayStationContents.Value && !PluginConfig.sprayInventoryContents.Value)
                {
                    message += "\tNote: Spraying inventory items and station contents can be enabled using config\r\n";
                }
                else if (!PluginConfig.sprayStationContents.Value)
                {
                    message += "\tNote: Spraying items in stations can be enabled in config\r\n";
                }
                else
                {
                    message += "\tNote: Spraying inventory items can be enabled using config\r\n";
                }
            }

            if (_neededSprayAccumulator == 0)
            {
                Log.LogAndPopupMessage($"No items found to spray. Found {_skippedItems} already sprayed");
                return false;
            }

            var sprayItemTypeBreakdownMessage = "Items to spray: ";
            if (PluginConfig.sprayStationContents.Value)
            {
                sprayItemTypeBreakdownMessage += $"\r\n\t{sprayNeededForStations} in stations";
            }

            if (PluginConfig.sprayInventoryContents.Value)
            {
                sprayItemTypeBreakdownMessage += $"\r\n\t{sprayNeededForInventory} in inventory";
            }

            sprayItemTypeBreakdownMessage += $"\r\n\t{sprayForBelts} on belts";
            sprayItemTypeBreakdownMessage += $"\r\n\t{sprayForAssemblers} in assemblers";

            if (sprayForGenerators > 0 || _targetItem.FuelType > 0 || _targetItem.ID == 1209)
            {
                sprayItemTypeBreakdownMessage += $"\r\n\t{sprayForGenerators} in generators";
            }

            if (_skippedItems > 0)
            {
                sprayItemTypeBreakdownMessage += $"\r\nSkipping {_skippedItems} items already sprayed at or above target level";
            }


            var itemsToUse = Mathf.CeilToInt(_neededSprayAccumulator / spraysPerSprayItem);
            if (_neededSprayAccumulator % Mathf.FloorToInt(spraysPerSprayItem) == 0)
            {
                itemsToUse = _neededSprayAccumulator / Mathf.FloorToInt(spraysPerSprayItem);
            }

            message += $"{sprayItemTypeBreakdownMessage}\r\n";
            message +=
                $"This will use {itemsToUse} of {_proliferatorItem.name} from inventory";
            var actualItemsToUseRate = Mathf.FloorToInt(spraysPerSprayItem);
            if (prolifInc > 0)
            {
                message += $" (at boosted rate of {actualItemsToUseRate} sprays / spray item).";
            }
            else
            {
                message += $" (at {_proliferatorItem.HpMax} sprays / spray item).";
            }

            message += $"\r\nYou currently have {prolifCount} in inventory";
            if (itemsToUse > prolifCount)
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

                if (PluginConfig.sprayInventoryContents.Value)
                {
                    SprayInventory(false);
                }

                SprayBeltItems(false);
                SprayAssemblerContents(false);
                SprayGenerators(false);
                return;
            }

            _neededSprayAccumulator = 0;
            var availableSpraysRslt = InventoryManager.instance.CountItems(_proliferatorItem.ID);
            _availableSprays = DetermineNumbersOfSprays(_proliferatorItem, availableSpraysRslt.cnt, availableSpraysRslt.inc);


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

            if (PluginConfig.sprayInventoryContents.Value)
            {
                SprayInventory(false);
                if (_neededSprayAccumulator >= _availableSprays)
                {
                    InventoryManager.instance.RemoveItemImmediately(_proliferatorItem.ID, availableSpraysRslt.cnt, out _);
                    Log.Debug("halting during inventory spray");
                    return;
                }
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

            // This is where it can be a bit complicated. Need to make sure we account for partially sprayed items in inv
            // we gave estimate based on the average spray level of all spray in inventory, but
            // when we only remove some items from inv they may be at different level than average used for estimate
            RemoveSpraysFromInventory(_neededSprayAccumulator, GameMain.mainPlayer.package, _proliferatorItem);
        }

        private static void RemoveSpraysFromInventory(int spraysToRemove, StorageComponent inv, ItemProto sprayItem)
        {
            var sprayItemId = sprayItem.ID;
            for (int i = 0; i < inv.grids.Length; i++)
            {
                if (spraysToRemove <= 0)
                    break;
                ref var grid = ref inv.grids[i];
                if (grid.itemId == 0 || grid.count == 0 || grid.itemId != sprayItem.ID)
                {
                    continue;
                }

                var numbersOfSpraysAtGridLocation = DetermineNumbersOfSprays(sprayItem, grid.count, grid.inc);
                if (numbersOfSpraysAtGridLocation <= spraysToRemove)
                {
                    //this is easy, remove all
                    int count = grid.count;
                    inv.TakeItemFromGrid(i, ref sprayItemId, ref count, out int inc);

                    spraysToRemove -= numbersOfSpraysAtGridLocation;
                    Log.Debug($"Removed {count} of spray at grid index: {i} remaining: {spraysToRemove}, inc={inc}");
                }
                else
                {
                    // take some items from this grid index, so use ratio to decrement
                    // if there are 96 sprays here from 4 spray items and we need 58 sprays
                    // then we have ratio of 24 sprays per spray item
                    //  so 58 / 25 = 2.4 so we need to take 3 items to cover.
                    var spraysPerSprayItemAtLocation = numbersOfSpraysAtGridLocation / grid.count;
                    // if evenly divisible, no need to worry about fractional
                    int itemsToRemove;
                    if (spraysToRemove % spraysPerSprayItemAtLocation == 0)
                    {
                        itemsToRemove = spraysToRemove / spraysPerSprayItemAtLocation;
                    }
                    else
                    {
                        itemsToRemove = Mathf.CeilToInt(spraysToRemove / (float)spraysPerSprayItemAtLocation);
                    }
                    
                    inv.TakeItemFromGrid(i, ref sprayItemId, ref itemsToRemove, out int inc);
                    spraysToRemove -= itemsToRemove * spraysPerSprayItemAtLocation;
                    Log.Debug($"Removed {itemsToRemove} of spray at grid index: {i}, remainInc={grid.inc}, remainCount={grid.count}. inc={inc}");

                    break;
                }
            }

            if (spraysToRemove > 0)
            {
                Log.Warn($"Somehow failed to remove enough spray from inventory. Leftover: {spraysToRemove}");
            }
        }


        private int SprayBeltItems(bool countOnly)
        {
            var cargoTraffic = _traffic;
            var cargoContainer = cargoTraffic.container;
            var cargoPool = cargoContainer.cargoPool;
            var cursor = cargoContainer.cursor;
            var resultItemsNeedingSpray = 0;
            for (int i = 0; i < cursor; i++)
            {
                ref var cargo = ref cargoPool[i];
                if (cargo.item == 0 || cargo.item != _targetItem.ID)
                    continue;


                var beltItemCount = cargo.stack;
                var currentlySprayedItems = CountSprayedItems(beltItemCount, _targetLevelIndex, cargo.inc);
                // if item is already sprayed above target level, skip it
                if (currentlySprayedItems >= beltItemCount)
                {
                    _skippedItems += beltItemCount;
                    continue;
                }

                _neededSprayAccumulator += beltItemCount - currentlySprayedItems;

                if (!_freeMode)
                {
                    resultItemsNeedingSpray += beltItemCount - currentlySprayedItems;
                    _skippedItems += currentlySprayedItems;
                }

                if (!countOnly)
                {
                    if (_freeMode || _availableSprays >= _neededSprayAccumulator)
                    {
                        cargo.inc = (byte)(_targetLevelIndex * beltItemCount);
                    }
                    else if (!_freeMode)
                    {
                        return resultItemsNeedingSpray;
                    }
                }
            }

            return resultItemsNeedingSpray;
        }

        // given a stack of items find out how many you could consider to be sprayed at the target level
        // 
        // example, 20 items, targetLvl 3
        // currentInc = 3 return 0
        // currentInc = 4 return 1
        // currentInc = 7 return 1
        // currentInc = 19 * 4, return 19
        // currentInc = 19 * 4 + 1, return 19
        private static int CountSprayedItems(int count, int targetLevelIndex, int currentInc)
        {
            if (currentInc == 0 || count == 0)
                return 0;
            var sprayPerItem = currentInc / count;
            if (sprayPerItem >= targetLevelIndex)
                return count;
            return currentInc / targetLevelIndex;
        }

        // See GetPropValue in ItemProto for about how the 'Numbers of Sprays' thing is calculated, but
        // for level 3 spray sprayed at level 3 you get 60 + 15 = 75 sprays 
        // level 2 spray sprayed at level 3: 24 + 6 = 30
        // here we're actually returning that times the number of items so for 2 items fully sprayed we'd return 150
        private static int DetermineNumbersOfSprays(ItemProto sprayItem, int count, int inc)
        {
            if (count == 0)
            {
                return 0;
            }

            if (inc == 0)
            {
                return count * sprayItem.HpMax;
            }

            // ok, figure out how much extra spray we should get
            int incLevel = inc / count;
            double incMilli = Cargo.incTableMilli[incLevel] + 1.0;
            return (int)(sprayItem.HpMax * incMilli + 0.1) * count;
        }

        int SprayAssemblerContents(bool countOnly)
        {
            var resultItemsNeedingSpray = 0;
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

                    var sprayedItemCount = CountSprayedItems(assemblerComponent.served[j], _targetLevelIndex, assemblerComponent.incServed[j]);
                    // if item is already sprayed above target level, skip it
                    if (sprayedItemCount >= assemblerComponent.served[j])
                    {
                        _skippedItems += assemblerComponent.served[j];
                        continue;
                    }

                    _neededSprayAccumulator += assemblerComponent.served[j] - _targetLevelIndex;

                    if (!_freeMode)
                    {
                        resultItemsNeedingSpray += assemblerComponent.served[j] - sprayedItemCount;
                        _skippedItems += sprayedItemCount;
                    }

                    if (!countOnly)
                    {
                        if (_freeMode || _availableSprays >= _neededSprayAccumulator)
                        {
                            assemblerComponent.incServed[j] = assemblerComponent.served[j] * _targetLevelIndex;
                        }
                        else if (!_freeMode)
                        {
                            return resultItemsNeedingSpray;
                        }
                    }
                }
            }

            return resultItemsNeedingSpray;
        }


        int SprayStations(bool countOnly)
        {
            var resultItemsNeedingSpray = 0;
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

                    var sprayedItemCount = CountSprayedItems(store.count, _targetLevelIndex, store.inc);
                    // if item is already sprayed above target level, skip it
                    if (sprayedItemCount >= store.count)
                    {
                        _skippedItems += store.count;
                        continue;
                    }

                    _neededSprayAccumulator += store.count - sprayedItemCount;

                    if (!_freeMode)
                    {
                        resultItemsNeedingSpray += store.count - sprayedItemCount;
                        _skippedItems += sprayedItemCount;
                    }

                    if (!countOnly)
                    {
                        if (_freeMode || _availableSprays >= _neededSprayAccumulator)
                        {
                            store.inc = _targetLevelIndex * store.count;
                        }
                        else if (!_freeMode)
                        {
                            return resultItemsNeedingSpray;
                        }
                    }
                }
            }

            return resultItemsNeedingSpray;
        }


        int SprayInventory(bool countOnly)
        {
            var resultItemsNeedingSpray = 0;
            var inv = GameMain.mainPlayer.package;
            for (int i = 0; i < inv.grids.Length; i++)
            {
                ref var grid = ref inv.grids[i];
                if (grid.itemId == 0 || grid.count == 0 || grid.itemId != _targetItem.ID)
                {
                    continue;
                }


                var sprayedItemCount = CountSprayedItems(grid.count, _targetLevelIndex, grid.inc);
                // if item is already sprayed above target level, skip it
                if (sprayedItemCount >= grid.count)
                {
                    _skippedItems += grid.count;
                    continue;
                }

                _neededSprayAccumulator += grid.count - sprayedItemCount;

                if (!_freeMode)
                {
                    resultItemsNeedingSpray += grid.count - sprayedItemCount;
                    _skippedItems += sprayedItemCount;
                }

                if (!countOnly)
                {
                    if (_freeMode || _availableSprays >= _neededSprayAccumulator)
                    {
                        grid.inc = _targetLevelIndex * grid.count;
                    }
                    else if (!_freeMode)
                    {
                        return resultItemsNeedingSpray;
                    }
                }
            }

            return resultItemsNeedingSpray;
        }

        // this one works with the catalyst point (count *3600) to avoid too much loss of precision 
        int SprayGenerators(bool countOnly)
        {
            var resultItemsNeedingSpray = 0;
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
                    var beansInRr = generator.catalystPoint / 3600;
                    var sprayedItemCountPoint = CountSprayedItems(generator.catalystPoint, _targetLevelIndex, generator.catalystIncPoint);

                    if (sprayedItemCountPoint >= generator.catalystPoint)
                    {
                        _skippedItems += beansInRr;
                        continue;
                    }

                    _neededSprayAccumulator += beansInRr - (sprayedItemCountPoint / 3600);
                    if (!_freeMode)
                    {
                        resultItemsNeedingSpray += beansInRr - (sprayedItemCountPoint / 3600);
                        _skippedItems += sprayedItemCountPoint / 3600;
                    }

                    if (!countOnly)
                    {
                        if (_freeMode || _availableSprays >= _neededSprayAccumulator)
                        {
                            generator.catalystIncPoint = generator.catalystPoint * _targetLevelIndex;
                        }
                        else if (!_freeMode)
                        {
                            return resultItemsNeedingSpray;
                        }
                    }
                }
                else if (generator.fuelId > 0)
                {
                    if (generator.fuelId != _targetItem.ID)
                        continue;
                    var sprayedItemCount = CountSprayedItems(generator.fuelCount, _targetLevelIndex, generator.fuelInc);
                    if (sprayedItemCount >= generator.fuelCount)
                    {
                        _skippedItems += generator.fuelCount;
                        continue;
                    }

                    _neededSprayAccumulator += generator.fuelCount - sprayedItemCount;

                    if (!_freeMode)
                    {
                        resultItemsNeedingSpray += generator.fuelCount - sprayedItemCount;
                        _skippedItems += sprayedItemCount;
                    }

                    if (!countOnly)
                    {
                        if (_freeMode || _availableSprays >= _neededSprayAccumulator)
                        {
                            generator.fuelInc = (short)(generator.fuelCount * _targetLevelIndex);
                        }
                        else if (!_freeMode)
                        {
                            return resultItemsNeedingSpray;
                        }
                    }
                }
            }

            return resultItemsNeedingSpray;
        }
    }
}