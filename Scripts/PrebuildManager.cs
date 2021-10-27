using System;
using System.Collections.Generic;
using HarmonyLib;
using LongArm.UI;
using LongArm.Util;
using UnityEngine;

namespace LongArm.Scripts
{
    /// <summary>Tracks pre-builds that can be built</summary>
    public class PrebuildManager : MonoBehaviour
    {
        private readonly List<int> _preBuildIds = new List<int>();
        private int _preBuildIdsPlanet;
        private long _lastCheckTicks = DateTime.Now.AddSeconds(10).Ticks; // set forward a bit to give things time to load
        public int InitialWorkCount { get; private set; }
        public static PrebuildManager Instance { get; private set; }


        private void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            if (GameMain.mainPlayer == null || GameMain.localPlanet == null || GameMain.localPlanet.factory == null || GameMain.localPlanet.factory.factorySystem == null ||
                !LongArmPlugin.Initted())
                return;
            if (LastPlanetCheckStale())
            {
                AddPrebuildIdsToWorkList(!HaveWork());
            }

            if (Time.frameCount % 105 == 0)
                Prune();
        }

        private void Prune()
        {
            if (_preBuildIds.Count == 0)
                return;
            if (_preBuildIdsPlanet != GameMain.localPlanet.id)
            {
                _preBuildIds.Clear();
                _preBuildIdsPlanet = GameMain.localPlanet.id;
                return;
            }

            var factory = GameMain.localPlanet?.factory;
            if (factory == null)
                return;
            Log.Debug($"Pruning {_preBuildIds.Count} for planet {_preBuildIdsPlanet}. Should have {factory.prebuildCount}");
            var idsToRemove = new List<int>();
            foreach (var pbId in _preBuildIds)
            {
                if (factory.prebuildPool[pbId].id != pbId)
                {
                    idsToRemove.Add(pbId);
                }
            }

            foreach (var pbId in idsToRemove)
            {
                _preBuildIds.Remove(pbId);
            }
            
        }

        private bool LastPlanetCheckStale()
        {
            var result = new TimeSpan(DateTime.Now.Ticks - _lastCheckTicks).TotalSeconds > 20;
            if (result)
                _lastCheckTicks = DateTime.Now.Ticks;
            return result;
        }

        public bool HaveWork()
        {
            if (GameMain.localPlanet?.factory?.prebuildPool == null)
                return false;
            if (GameMain.localPlanet.id != _preBuildIdsPlanet)
            {
                _preBuildIds.Clear();
                _preBuildIdsPlanet = GameMain.localPlanet.id;
                return false;
            }

            return _preBuildIds.Count > 0;
        }

        public int TakePrebuild()
        {
            if (GameMain.localPlanet?.factory?.prebuildPool == null || GameMain.localPlanet.id != _preBuildIdsPlanet)
                return 0;
            while (_preBuildIds.Count > 0)
            {
                var pbId = _preBuildIds[0];
                _preBuildIds.RemoveAt(0);
                if (GameMain.localPlanet.factory.prebuildPool[pbId].id > 0)
                    return pbId;
            }

            return 0;
        }

        public int RemainingCount()
        {
            return _preBuildIds.Count;
        }


        private void AddPrebuildIdsToWorkList(bool resetTotal = true)
        {
            if (GameMain.localPlanet?.factory?.prebuildPool == null)
                return;

            _preBuildIdsPlanet = GameMain.localPlanet.id;
            _preBuildIds.Clear();

            foreach (var prebuildData in GameMain.localPlanet.factory.prebuildPool)
            {
                if (prebuildData.id < 1)
                {
                    continue;
                }

                _preBuildIds.Add(prebuildData.id);
            }

            if (resetTotal)
                InitialWorkCount = _preBuildIds.Count;
            _lastCheckTicks = DateTime.Now.Ticks;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildTool_BlueprintPaste), "CreatePrebuilds")]
        public static void RecordPrebuildsFromBP(BuildTool_BlueprintPaste __instance)
        {
            if (Instance == null)
                return;
            Instance.AddPrebuildIdsToWorkList();
        }

        public string GetPercentDone()
        {
            if (_preBuildIds.Count == 0)
            {
                return "100";
            }

            return (100 - (int)(100 * _preBuildIds.Count / (double)InitialWorkCount)).ToString();
        }

        public int GetNumBuilt()
        {
            return InitialWorkCount - _preBuildIds.Count;
        }

        public int TakeClosestPrebuild(Vector3 position)
        {
            _preBuildIds.Sort((p1, p2) =>
            {
                var p1Distance = Vector3.Distance(position, GameMain.localPlanet.factory.prebuildPool[p1].pos);
                var p2Distance = Vector3.Distance(position, GameMain.localPlanet.factory.prebuildPool[p2].pos);

                return p1Distance.CompareTo(p2Distance);
            });
            return TakePrebuild();
        }

        public void ReturnPrebuild(int id)
        {
            _preBuildIds.Add(id);
        }
    }
}