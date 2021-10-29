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
        public AssemblerComponent assembler;
        public VeinData vein;
        public StationComponent station;
        public Vector3 position;

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
        public static FactoryLocationProvider instance => GetInstance();

        private PlanetFactory _factory;
        private FactoryTourMode _currentMode = FactoryTourMode.None;
        private readonly List<EntityLocation> _locations = new List<EntityLocation>();
        private bool _dirty;
        private DateTime _lastSync;
        private int _currentIndex;

        private FactoryLocationProvider(PlanetFactory factory)
        {
            _factory = factory;
        }


        private static FactoryLocationProvider GetInstance()
        {
            if (_instance == null && GameMain.localPlanet == null)
                return null;
            if (GameMain.localPlanet.factory == null)
                return null;
            var result = _instance ?? (_instance = new FactoryLocationProvider(GameMain.localPlanet.factory));
            if (result._factory == null || result._factory != GameMain.localPlanet?.factory)
            {
                Log.Debug("Switching player instance for InvMgr");
                result._factory = GameMain.localPlanet.factory;
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

        public void Sync()
        {
            var needUpdate = _dirty || (DateTime.Now - _lastSync).TotalSeconds > 10;
            if (!needUpdate)
                return;

            if (_currentMode != FactoryTourMode.None)
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
                    default:
                        throw new ArgumentOutOfRangeException();
                }

            _dirty = false;
            _lastSync = DateTime.Now;
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

                newLocations.Add(new EntityLocation
                {
                    station = entity,
                    position = entity.shipDockPos
                });
            }

            return newLocations;
        }

        private void UpdateVeinLocations()
        {
            var newLocations = new List<EntityLocation>();
            for (int i = 1; i < _factory.veinCursor; i++)
            {
                var entity = _factory.veinPool[i];
                if (entity.id != i)
                    continue;

                newLocations.Add(new EntityLocation
                {
                    vein = entity,
                    // position = _factory.entityPool[i].pos
                    position = entity.pos
                });
            }

            ApplyNewLocations(newLocations);
        }

        private void UpdateAssemblerLocations()
        {
            var newLocations = new List<EntityLocation>();
            for (int i = 1; i < _factory.entityCursor; i++)
            {
                var entity = _factory.entityPool[i];
                if (entity.id != i)
                    continue;
                if (entity.assemblerId > 0)
                {
                    newLocations.Add(new EntityLocation
                    {
                        assembler = _factory.factorySystem.assemblerPool[entity.assemblerId],
                        position = _factory.entityPool[i].pos
                    });
                }
            }

            ApplyNewLocations(newLocations);
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
            if (_locations.Count > 0 && newLocations.Count > 0)
            {
                var curEntityLocation = _locations[_currentIndex];
                newIndex = newLocations.FindIndex(0, el => el.position == curEntityLocation.position);
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
    }
}