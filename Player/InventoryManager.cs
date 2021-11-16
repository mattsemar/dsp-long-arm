using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using LongArm.Util;

namespace LongArm.Player
{
    public class InventoryManager
    {
        private static InventoryManager _instance;
        public static InventoryManager instance => GetInstance();

        private global::Player _player;

        private InventoryManager(global::Player player)
        {
            _player = player;
        }


        public static void Reset()
        {
            if (_instance == null)
                return;
            _instance._player = null;
            _instance = null;
        }

        private static InventoryManager GetInstance()
        {
            if (_instance == null && GameMain.mainPlayer == null)
                return null;
            var result = _instance ?? (_instance = new InventoryManager(GameMain.mainPlayer));
            if (result._player == null || result._player != GameMain.mainPlayer)
            {
                Log.Debug("Switching player instance for InvMgr");
                result._player = GameMain.mainPlayer;
            }

            return result;
        }

        public bool RemoveItemImmediately(int itemId, int count)
        {
            int cnt = count;
            _player.package.TakeTailItems(ref itemId, ref cnt);
            return cnt == count;
        }

        public int TakeItems(int itemId, int count)
        {
            int cnt = count;
            _player.package.TakeTailItems(ref itemId, ref cnt);
            return cnt;
        }

        public static int GetInventoryCount(int itemId)
        {
            var inventoryManager = GetInstance();
            if (inventoryManager?._player == null)
            {
                Log.Warn("Inventory manager instance is null (or _player) can't tell if item in inventory");
                return -1;
            }

            return inventoryManager._player.package.GetItemCount(itemId);
        }

        public static Guid GetInventoryHash()
        {
            var _player = GameMain.mainPlayer;
            if (_player == null)
            {
                Log.Warn($"player is null");
                return Guid.Empty;
            }

            var inv = _player.package;
            if (inv?.grids == null)
            {
                Log.Warn($"player package is null == {inv == null} || grids is null {inv?.grids == null}");
                return Guid.Empty;
            }

            var itemCounts = new Dictionary<int, int>();
            for (int index = 0; index < inv.size; ++index)
            {
                var itemId = inv.grids[index].itemId;
                if (itemId < 1)
                {
                    continue;
                }

                var count = inv.grids[index].count;
                if (itemCounts.TryGetValue(itemId, out _))
                {
                    itemCounts[itemId] += count;
                }
                else
                {
                    itemCounts[itemId] = count;
                }
            }

            if (_player.inhandItemId > 0 && _player.inhandItemCount > 0)
            {
                itemCounts.TryGetValue(_player.inhandItemId, out var value);
                itemCounts[_player.inhandItemId] = value + _player.inhandItemCount;
            }

            var sb = new StringBuilder();
            foreach (var itemId in itemCounts.Keys)
            {
                sb.Append($"{itemId},{itemCounts[itemId]}\r\n");
            }


            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.Default.GetBytes(sb.ToString()));
                return new Guid(hash);
            }
        }
    }
}