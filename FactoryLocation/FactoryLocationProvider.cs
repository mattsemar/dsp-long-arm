using System;
using System.Collections.Generic;
using System.Linq;
using LongArm.Scripts;
using LongArm.UI;
using LongArm.Util;
using UnityEngine;

namespace LongArm.FactoryLocation
{
    public class EntityLocation
    {
        public Vector3 position;
        public AssemblerComponent assembler;
        public PowerGeneratorComponent generator;
        public LabComponent lab;
        public StationComponent station;
        public StorageComponent storage;
        public VeinData vein;

        public bool IsUnpowered()
        {
            if (vein.id > 0)
                return true;
            var factory = GameMain.localPlanet?.factory;
            if (factory == null)
                return false;
            var pcId = -1;
            if (assembler.id != 0)
            {
                pcId = assembler.pcId;
            }
            else if (station.id > 0)
            {
                pcId = station.pcId;
            }

            if (pcId < 1)
            {
                return false;
            }

            PowerConsumerComponent consumerComponent = factory.powerSystem.consumerPool[pcId];
            int networkId = consumerComponent.networkId;
            PowerNetwork powerNetwork = factory.powerSystem.netPool[networkId];
            float powerRatio = powerNetwork == null || networkId <= 0 ? 0.0f : (float)powerNetwork.consumerRatio;
            return powerRatio < 0.1;
        }
    }

    public class FactoryLocationProvider
    {
        private static FactoryLocationProvider _instance;
        public static FactoryLocationProvider Instance => GetInstance();

        private PlanetFactory _factory;
        private FactoryTourMode _currentMode = FactoryTourMode.None;
        private readonly List<EntityLocation> _locations = new List<EntityLocation>();
        private bool _dirty;
        private DateTime _lastSync;
        private int _currentIndex;
        private IFactoryEntityFilter _currentFilter = MatchAllFilter.DEFAULT;

        private FactoryLocationProvider(PlanetFactory factory)
        {
            _factory = factory;
        }


        private static FactoryLocationProvider GetInstance()
        {
            var localPlanet = GameMain.localPlanet;
            if (_instance == null && localPlanet == null)
                return null;
            if (localPlanet.factory == null)
                return null;
            var result = _instance ?? (_instance = new FactoryLocationProvider(localPlanet.factory));
            if (result._factory == null || result._factory != localPlanet?.factory)
            {
                Log.Debug("Switching player instance for InvMgr");
                result._factory = localPlanet.factory;
            }

            return result;
        }

        public void NotifyModeChange(FactoryTourMode newMode)
        {
            if (newMode != _currentMode)
            {
                Clear();
                _dirty = true;
                _currentMode = newMode;
            }
        }

        public void Sync(bool force= false)
        {
            var needUpdate = force || _dirty || (DateTime.Now - _lastSync).TotalSeconds > 10;
            if (!needUpdate)
                return;
            if (_factory != GameMain.localPlanet?.factory)
            {
                Log.Warn($"factory instance out of sync");
                if (GameMain.localPlanet?.factory != null)
                    _factory = GameMain.localPlanet.factory;
                else
                {
                    _factory = null;
                    return;
                }
            }

            if (_currentMode != FactoryTourMode.None && _currentMode != FactoryTourMode.Stopped)
                switch (_currentMode)
                {
                    case FactoryTourMode.Assemblers:
                        UpdateAssemblerLocations();
                        break;
                    case FactoryTourMode.Veins:
                        UpdateVeinLocations();
                        break;
                    case FactoryTourMode.Stations:
                        UpdateStationLocations();
                        break;
                    case FactoryTourMode.Storage:
                        UpdateStorageLocations();
                        break;
                    case FactoryTourMode.Generator:
                        UpdateGeneratorLocations();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

            _dirty = false;
            _lastSync = DateTime.Now;
        }

        private void UpdateGeneratorLocations()
        {
            ApplyNewLocations(GetGeneratorLocations());
        }

        private List<EntityLocation> GetGeneratorLocations()
        {
            var newLocations = new List<EntityLocation>();
            for (int i = 1; i < _factory.entityCursor; i++)
            {
                var entity = _factory.entityPool[i];
                if (entity.id != i)
                    continue;
                if (entity.powerGenId > 0)
                {
                    var generator = _factory.powerSystem.genPool[entity.powerGenId];
                    var isFuelConsumer = generator.fuelHeat > 0 && generator.fuelId > 0 && generator.productId == 0;
                    if (!isFuelConsumer)
                        continue;

                    newLocations.Add(new EntityLocation
                    {
                        generator = generator,
                        position = _factory.entityPool[i].pos,
                    });
                }
            }

            return newLocations.FindAll(el => _currentFilter.Matches(el));
        }

        private void UpdateStorageLocations()
        {
            ApplyNewLocations(GetStorageLocations());
        }

        private void UpdateStationLocations()
        {
            ApplyNewLocations(GetStationLocations());
        }

        private List<EntityLocation> GetStationLocations()
        {
            var newLocations = new List<EntityLocation>();
            for (int i = 1; i < _factory.transport.stationCursor; i++)
            {
                var entity = _factory.transport.stationPool[i];
                if (entity == null || entity.id != i)
                    continue;
                // if (_itemFilter != null)
                // {
                //     var filterMatched = false;
                //     foreach (var store in entity.storage)
                //     {
                //         if (store.itemId < 1)
                //         {
                //             continue;
                //         }
                //
                //         if (store.itemId == _itemFilter.ID)
                //         {
                //             filterMatched = true;
                //             break;
                //         }
                //     }
                //
                //     if (!filterMatched)
                //         continue;
                // }

                var pos = _factory.entityPool[entity.entityId].pos;
                newLocations.Add(new EntityLocation
                {
                    station = entity,
                    position = pos
                });
            }

            return newLocations.FindAll(el => _currentFilter.Matches(el));
        }

        private List<EntityLocation> GetStorageLocations()
        {
            var newLocations = new List<EntityLocation>();
            for (int i = 1; i < _factory.factoryStorage.storageCursor; i++)
            {
                var entity = _factory.factoryStorage.storagePool[i];
                if (entity == null || entity.id != i)
                    continue;
                // if (_itemFilter != null)
                // {
                //     var filterMatched = false;
                //     if (entity.GetItemCount(_itemFilter.ID) < 1)
                //         continue;
                // }

                newLocations.Add(new EntityLocation
                {
                    storage = entity,
                    position = _factory.entityPool[entity.entityId].pos
                });
            }

            return newLocations.FindAll(el => _currentFilter.Matches(el));
        }

        private void UpdateVeinLocations()
        {
            var newLocations = new List<EntityLocation>();
            for (int i = 1; i < _factory.veinCursor; i++)
            {
                var entity = _factory.veinPool[i];
                if (entity.id != i)
                    continue;
                // if (_itemFilter != null)
                // {
                //     if (entity.productId != _itemFilter.ID)
                //         continue;
                // }
                //
                if (HasNearbyPoint(newLocations, entity.pos, 10))
                    continue;
                if (entity.minerCount > 0 && LDB.items.Select(entity.productId).Name.Contains("Crude"))
                    continue;
                newLocations.Add(new EntityLocation
                {
                    vein = entity,
                    // position = _factory.entityPool[i].pos
                    position = entity.pos
                });
            }

            ApplyNewLocations(newLocations.FindAll(el => _currentFilter.Matches(el)));
        }

        private bool HasNearbyPoint(List<EntityLocation> locations, Vector3 position, int maxDistance)
        {
            return locations.Exists(l => Vector3.Distance(l.position, position) < maxDistance);
        }

        private void UpdateAssemblerLocations()
        {
            // var filteredItemId = _itemFilter == null || _itemFilter.ID == 0 ? 0 : _itemFilter.ID;
            var newLocations = new List<EntityLocation>();
            for (int i = 1; i < _factory.entityCursor; i++)
            {
                var entity = _factory.entityPool[i];
                if (entity.id != i)
                    continue;
                if (HasNearbyPoint(newLocations, entity.pos, 10))
                    continue;
                if (entity.assemblerId > 0)
                {
                    var assembler = _factory.factorySystem.assemblerPool[entity.assemblerId];
                    newLocations.Add(new EntityLocation
                    {
                        assembler = assembler,
                        position = entity.pos
                    });
                }

                if (entity.labId > 0)
                {
                    var lab = _factory.factorySystem.labPool[entity.labId];
                    if (!lab.matrixMode || lab.products == null)
                    {
                        continue;
                    }

                    newLocations.Add(new EntityLocation
                    {
                        lab = lab,
                        position = _factory.entityPool[i].pos,
                    });
                }
            }

            ApplyNewLocations(newLocations.FindAll(el => _currentFilter.Matches(el)));
        }

        private void ApplyNewLocations(List<EntityLocation> newLocations)
        {
            newLocations.Sort((loc1, loc2) =>
            {
                var d1 = Vector3.Distance(Vector3.up * GameMain.localPlanet.realRadius, loc1.position);
                var d2 = Vector3.Distance(Vector3.up * GameMain.localPlanet.realRadius, loc2.position);
                return d1.CompareTo(d2);
            });

            var newIndex = _currentIndex;
            if (newIndex < 0 || newIndex >= _locations.Count)
            {
                newIndex = (int)Maths.Clamp(newIndex, 0, _locations.Count - 1);
            }

            if (_locations.Count > 0 && newLocations.Count > 0)
            {
                var curEntityLocation = _locations[newIndex];
                newIndex = newLocations.FindIndex(el => el.position == curEntityLocation.position);
                if (newIndex < 0)
                    newIndex = 0;
            }

            lock (_locations)
            {
                _locations.Clear();
                _locations.AddRange(newLocations);
                _currentIndex = newIndex;
            }
        }

        private void Clear()
        {
            lock (_locations)
            {
                _locations.Clear();
            }
        }

        public EntityLocation GetEntity(ActionDir dir)
        {
            lock (_locations)
            {
                if (dir == ActionDir.Previous)
                {
                    _currentIndex--;
                    if (_currentIndex < 0)
                    {
                        _currentIndex = _locations.Count - 1;
                    }
                }
                else
                {
                    _currentIndex = (_currentIndex + 1) % _locations.Count;
                }

                return _locations[_currentIndex];
            }
        }

        public bool HasWork()
        {
            return _locations.Count > 0;
        }

        public (int curIndex, int totalPoints) GetCurrentIndex()
        {
            lock (_locations)
            {
                return (_currentIndex, _locations.Count);
            }
        }

        public List<StationComponent> GetStations()
        {
            if (_currentMode == FactoryTourMode.Stations)
            {
                lock (_locations)
                {
                    return _locations.FindAll(l => l.station != null).Select(l => l.station).ToList();
                }
            }

            return GetStationLocations().Select(s => s.station).ToList();
        }

        public void SetItemFilter(ItemProto proto)
        {
            if (proto == null)
            {
                _currentFilter = MatchAllFilter.DEFAULT;
            }
            else
            {
                _currentFilter = new ItemFilter(proto.ID);
            }

            _dirty = true;
            Sync();
        }

        public void ClearFilter()
        {
            _currentFilter = MatchAllFilter.DEFAULT;
            _dirty = true;
            Sync();
        }

        public void UseNeedItemFilter(bool toggle)
        {
            if (toggle)
            {
                if (_currentFilter is MatchAllFilter)
                {
                    _currentFilter = NeedItemFilter.DEFAULT;
                }
                else if (_currentFilter is ItemFilter)
                {
                    var newFilter = new CompoundEntityFilter();
                    newFilter.AddFilter(_currentFilter);
                    newFilter.AddFilter(NeedItemFilter.DEFAULT);
                    _currentFilter = newFilter;
                }
                else if (_currentFilter is CompoundEntityFilter cfx)
                {
                    cfx.AddFilter(NeedItemFilter.DEFAULT);
                }
                else if (_currentFilter is NeedItemFilter) // this doesn't make sense
                {
                    // no-op?
                    Log.Warn("Trying to set need item filter, but already have it");
                }
            }
            else
            {
                if (_currentFilter is MatchAllFilter)
                {
                    // doesn't make sense, just warn
                    Log.Warn("Trying to clear need item filter, but not set (MatchAll)");
                }
                else if (_currentFilter is ItemFilter)
                {
                    // doesn't make sense, just warn
                    Log.Warn("Trying to clear need item filter, but not set (ItemFilter)");
                }
                else if (_currentFilter is CompoundEntityFilter cfx)
                {
                    cfx.RemoveFilter(NeedItemFilter.DEFAULT);
                }
                else if (_currentFilter is NeedItemFilter)
                {
                    _currentFilter = MatchAllFilter.DEFAULT;
                }
            }

            _dirty = true;
            Sync();
        }
    }
}