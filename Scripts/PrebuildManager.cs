﻿using System;
using System.Collections.Generic;
using HarmonyLib;
using LongArm.Model;
using LongArm.Player;
using LongArm.UI;
using LongArm.Util;
using UnityEngine;

namespace LongArm.Scripts
{
    /// <summary>Tracks pre-builds that can be built</summary>
    public class PrebuildManager : MonoBehaviour
    {
        private readonly Queue<int> _preBuildIds = new Queue<int>();
        private readonly HashSet<int> _preBuildIdSet = new HashSet<int>();
        private int _preBuildIdsPlanet;
        private bool _dirty;
        private long _lastCheckTicks = DateTime.Now.AddSeconds(10).Ticks; // set forward a bit to give things time to load
        public static PrebuildManager Instance { get; private set; }

        private PrebuildSummary _latestSummary = new PrebuildSummary();

        private void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            if (GameMain.mainPlayer == null || GameMain.localPlanet == null || GameMain.localPlanet.factory == null || GameMain.localPlanet.factory.factorySystem == null ||
                !LongArmPlugin.Initted())
                return;
            if (Time.frameCount % 10 != 0)
                return;
            if (LastPlanetCheckStale())
            {
                AddPrebuildIdsToWorkList();
            }

            if (Time.frameCount % 100 == 0)
                Prune();
        }

        private void Prune()
        {
            if (_preBuildIds.Count == 0)
                return;
            if (_preBuildIdsPlanet != GameMain.localPlanet?.id)
            {
                _preBuildIds.Clear();
                _preBuildIdSet.Clear();
                _preBuildIdsPlanet = GameMain.localPlanet?.id ?? 0;
            }
        }

        private bool LastPlanetCheckStale()
        {
            if (_dirty)
                Log.Debug($"dirty flag was true in pb mgr {_lastCheckTicks}");
            var result = _dirty || new TimeSpan(DateTime.Now.Ticks - _lastCheckTicks).TotalSeconds > 15;
            if (!result)
            {
                return false;
            }

            _lastCheckTicks = DateTime.Now.Ticks;
            _dirty = false;
            return true;
        }

        public bool HaveWork()
        {
            if (GameMain.localPlanet?.factory?.prebuildPool == null || GameMain.localPlanet.factory.prebuildCount == 0)
            {
                return false;
            }

            if (GameMain.localPlanet.id != _preBuildIdsPlanet)
            {
                Log.Debug($"clearing prebuilds due to planet change");
                _preBuildIds.Clear();
                _preBuildIdSet.Clear();
                _preBuildIdsPlanet = GameMain.localPlanet.id;
                return false;
            }

            return _preBuildIdSet.Count > 0;
        }

        public int TakePrebuild()
        {
            if (GameMain.localPlanet?.factory?.prebuildPool == null || GameMain.localPlanet.id != _preBuildIdsPlanet)
                return 0;
            while (_preBuildIds.Count > 0)
            {
                var pbId = _preBuildIds.Dequeue();
                _preBuildIdSet.Remove(pbId);
                if (GameMain.localPlanet.factory.prebuildPool[pbId].id > 0)
                    return pbId;
            }

            if (PluginConfig.autoShowPreviewStatusWindow.Value)
                BuildPreviewSummary.instance.Visible = false;
            return 0;
        }

        public int RemainingCount()
        {
            return _preBuildIdSet.Count;
        }


        private void AddPrebuildIdsToWorkList()
        {
            if (GameMain.localPlanet?.factory?.prebuildPool == null)
                return;

            _preBuildIdsPlanet = GameMain.localPlanet.id;
            _preBuildIds.Clear();
            _preBuildIdSet.Clear();
            if (GameMain.localPlanet.factory.prebuildCount > 0)
            {
                for (int index = 1; index < GameMain.localPlanet.factory.prebuildCursor; ++index)
                {
                    if (GameMain.localPlanet.factory.prebuildPool[index].id != index)
                        continue;
                    var prebuildData = GameMain.localPlanet.factory.prebuildPool[index];
                    if (prebuildData.id < 1)
                    {
                        continue;
                    }

                    if (_preBuildIdSet.Add(prebuildData.id))
                        _preBuildIds.Enqueue(prebuildData.id);
                }
            }
            else
            {
                _preBuildIds.Clear();
                _preBuildIdSet.Clear();
            }

            _lastCheckTicks = DateTime.Now.Ticks;
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(BuildTool_BlueprintPaste), "CreatePrebuilds")]
        public static void RecordPrebuildsFromBP(BuildTool_BlueprintPaste __instance)
        {
            if (Instance == null)
                return;
            Log.Debug($"Captured some new prebuilds happening");
            Instance._dirty = true;
            if (PluginConfig.autoShowPreviewStatusWindow.Value)
                BuildPreviewSummary.instance.Visible = true;
        }

        public int TakeClosestPrebuild(Vector3 position)
        {
            new List<int>(_preBuildIds).Sort((p1, p2) =>
            {
                var p1Distance = Vector3.Distance(position, GameMain.localPlanet.factory.prebuildPool[p1].pos);
                var p2Distance = Vector3.Distance(position, GameMain.localPlanet.factory.prebuildPool[p2].pos);

                return p1Distance.CompareTo(p2Distance);
            });
            return TakePrebuild();
        }

        public void ReturnPrebuild(int id)
        {
            if (_preBuildIdSet.Add(id))
                _preBuildIds.Enqueue(id);
        }

        public PrebuildSummary GetSummary(bool forceRefresh = false)
        {
            if (!forceRefresh && _latestSummary != null && new TimeSpan(DateTime.Now.Ticks - _latestSummary.updatedAtTicks).TotalSeconds < 4)
            {
                return _latestSummary;
            }

            var inventoryManager = InventoryManager.instance;
            var factory = GameMain.localPlanet?.factory;
            var result = new PrebuildSummary();
            if (factory == null || inventoryManager == null)
            {
                return result;
            }

            if (forceRefresh)
                AddPrebuildIdsToWorkList();
            List<int> localCopy;
            lock (_preBuildIds)
            {
                localCopy = new List<int>(_preBuildIds);
            }

            var itemByType = new Dictionary<int, PrebuildItemStatus>();
            foreach (var preBuildId in localCopy)
            {
                var prebuildData = factory.prebuildPool[preBuildId];
                if (prebuildData.id < 1)
                    continue;

                if (!itemByType.ContainsKey(prebuildData.protoId))
                {
                    var itemStatus = new PrebuildItemStatus
                    {
                        inventoryCount = InventoryManager.GetInventoryCount(prebuildData.protoId),
                        itemName = ItemUtil.GetItemName(prebuildData.protoId),
                        itemImage = ItemUtil.GetItemProto(prebuildData.protoId).iconSprite,
                        neededCount = 1
                    };
                    itemByType[prebuildData.protoId] = itemStatus;
                    result.items.Add(itemStatus);
                }
                else
                {
                    itemByType[prebuildData.protoId].neededCount++;
                }
            }

            result.items.Sort((i1, i2) =>
            {
                var itm1ShortFall = i1.inventoryCount - i1.neededCount;
                var itm2ShortFall = i2.inventoryCount - i2.neededCount;
                return itm1ShortFall.CompareTo(itm2ShortFall);
            });
            result.CalculateSummary();

            result.updatedAtTicks = DateTime.Now.Ticks;
            _latestSummary = result;
            return _latestSummary;
        }
    }
}