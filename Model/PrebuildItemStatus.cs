using System;
using System.Collections.Generic;
using UnityEngine;

namespace LongArm.Model
{
    public class PrebuildItemStatus
    {
        public string itemName;
        public Sprite itemImage;
        public int neededCount;
        public int inventoryCount;
    }

    public class PrebuildSummary
    {
        public readonly List<PrebuildItemStatus> items = new List<PrebuildItemStatus>();
        public long updatedAtTicks = DateTime.Now.AddDays(-1).Ticks;
        public int total;
        public int missingCount;

        public void CalculateSummary()
        {
            foreach (var item in items)
            {
                total += item.neededCount;
                if (item.inventoryCount < item.neededCount)
                {
                    missingCount += (item.inventoryCount - item.neededCount) * -1;
                }
            }
        }
    }
}