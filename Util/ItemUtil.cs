using System;

namespace LongArm.Util
{
    public static class ItemUtil
    {
        public static string GetItemName(int itemId)
        {
            if (itemId == 0)
            {
                Log.Warn("Tried to lookup item with id == 0");
                return $"__UNKNOWN_ITEM__UNKNOWN__";
            }
            try
            {
                return LDB._items.Select(itemId).Name.Translate();
            }
            catch (Exception e)
            {
                Log.Warn($"failed to get item name {itemId} {e.Message}\n{e.StackTrace}");
            }

            return $"__UNKNOWN_{itemId}__UNKNOWN__";
        }

        public static ItemProto GetItemProto(int itemId)
        {
            return LDB._items.Select(itemId);
        }

        public static int GetItemImageHeight()
        {
            return GetItemProto(1001).iconSprite.texture.height;
        }
    }
}