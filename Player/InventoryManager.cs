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
    }
}