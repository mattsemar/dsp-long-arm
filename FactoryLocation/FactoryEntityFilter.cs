using System;
using System.Collections.Generic;
using System.Linq;
using LongArm.Util;

namespace LongArm.FactoryLocation
{
    public interface IFactoryEntityFilter
    {
        bool Matches(EntityLocation entityLocation);
    }

    public class CompoundEntityFilter : IFactoryEntityFilter
    {
        private readonly List<IFactoryEntityFilter> _filters = new List<IFactoryEntityFilter>();

        public void AddFilter(IFactoryEntityFilter filter)
        {
            _filters.Add(filter);
        }

        public void RemoveFilter(IFactoryEntityFilter filterType)
        {
            var toRemove = _filters.FindAll(f => f.GetType() == filterType.GetType());
            _filters.RemoveAll(f => toRemove.Contains(f));
        }

        public bool Matches(EntityLocation entityLocation)
        {
            foreach (var filter in _filters)
            {
                if (!filter.Matches(entityLocation))
                    return false;
            }

            return true;
        }
    }

    public class MatchAllFilter : IFactoryEntityFilter
    {
        public static readonly MatchAllFilter DEFAULT = new MatchAllFilter();
        public bool Matches(EntityLocation entityLocation) => true;
    }

    public class NeedItemFilter : IFactoryEntityFilter
    {
        private NeedItemFilter()
        {
        }
            
        public static readonly NeedItemFilter DEFAULT = new NeedItemFilter();

        public bool Matches(EntityLocation entityLocation)
        {
            try
            {
                if (entityLocation.assembler.id > 0)
                {
                    for (var k = 0; k < entityLocation.assembler.requires.Length; k++)
                    {
                        if (entityLocation.assembler.served[k] < entityLocation.assembler.requireCounts[k])
                        {
                            return true;
                        }
                    }

                    return false;
                }

                if (entityLocation.generator.id > 0)
                {
                    var generatorComponent = entityLocation.generator;
                    return !(generatorComponent.curFuelId != 0 || generatorComponent.fuelEnergy != 0L || generatorComponent.fuelId != 0);

                }

                if (entityLocation.station != null && entityLocation.station.id > 0)
                {
                    foreach (var store in entityLocation.station.storage)
                    {
                        if (store.itemId < 1)
                        {
                            continue;
                        }

                        if ((store.remoteLogic == ELogisticStorage.Demand || store.localLogic == ELogisticStorage.Demand) && store.count == 0)
                        {
                            return true;
                        }
                    }

                    return false;
                }

                if (entityLocation.storage != null && entityLocation.storage.id > 0)
                {
                    return true;
                }

                if (entityLocation.vein.id > 0)
                {
                    return true;
                }

                if (entityLocation.lab.id > 0)
                {
                    var lab = entityLocation.lab;
                    if (!lab.matrixMode || lab.products == null)
                    {
                        return false;
                    }

                    for (int k = 0; k < entityLocation.lab.requires.Length; k++)
                    {
                        if (entityLocation.lab.served[k] < entityLocation.lab.requireCounts[k])
                        {
                            return true;
                        }
                    }

                    return false;
                }

                Log.Warn($"Something went wrong, we have an unexpected entity type {entityLocation} in NeedItemFilter");
                return true;
            }
            catch (Exception e)
            {
                Log.Warn($"got some bug still ${e.Message} {e.StackTrace} {e}");
                return true;
            }
        }
    }

    public class ItemFilter : IFactoryEntityFilter, IEqualityComparer<IFactoryEntityFilter>
    {
        private readonly int _item;

        public ItemFilter(int itemId)
        {
            if (ItemUtil.GetItemProto(itemId) == null)
                throw new ArgumentOutOfRangeException("Invalid item id for filter");
            _item = itemId;
        }

        public bool Matches(EntityLocation entityLocation)
        {
            if (entityLocation.assembler.id > 0)
            {
                if (entityLocation.assembler.products == null)
                    return true;
                return entityLocation.assembler.products.Contains(_item);
            }

            if (entityLocation.generator.id > 0)
            {
                return entityLocation.generator.curFuelId == _item;
            }

            if (entityLocation.station != null && entityLocation.station.id > 0)
            {
                foreach (var store in entityLocation.station.storage)
                {
                    if (store.itemId < 1)
                    {
                        continue;
                    }

                    if (store.itemId == _item)
                    {
                        return true;
                    }
                }

                return false;
            }

            if (entityLocation.storage != null && entityLocation.storage.id > 0)
            {
                return entityLocation.storage.GetItemCount(_item) > 0;
            }

            if (entityLocation.vein.id > 0)
            {
                return entityLocation.vein.productId == _item;
            }

            if (entityLocation.lab.id > 0)
            {
                var lab = entityLocation.lab;
                if (!lab.matrixMode || lab.products == null)
                {
                    return false;
                }

                return lab.products.Contains(_item);
            }

            Log.Warn($"Something went wrong, we have an unexpected entity type in ItemFilter {entityLocation}");
            return true;
        }

        public bool Equals(IFactoryEntityFilter x, IFactoryEntityFilter y)
        {
            if (x is ItemFilter xi && y is ItemFilter yi)
            {
                return xi._item == yi._item;
            }

            return x == y;
        }

        public int GetHashCode(IFactoryEntityFilter obj)
        {
            if (obj is ItemFilter objItmFlter)
            {
                return objItmFlter._item.GetHashCode();
            }

            return obj.GetHashCode();
        }
    }
}