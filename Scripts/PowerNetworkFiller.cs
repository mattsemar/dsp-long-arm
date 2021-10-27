using LongArm.Player;
using LongArm.Util;
using UnityEngine;

namespace LongArm.Scripts
{
    /// <summary>Tracks pre-builds that can be built</summary>
    public class PowerNetworkFiller : MonoBehaviour
    {
        public static PowerNetworkFiller Instance { get; private set; }

        public bool _executeRequested;

        public void RequestExecution()
        {
            _executeRequested = true;
        }

        private void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            if (GameMain.mainPlayer == null || GameMain.localPlanet == null || GameMain.localPlanet.factory == null || GameMain.localPlanet.factory.factorySystem == null ||
                !LongArmPlugin.Initted())
                return;
            if (!_executeRequested)
                return;
            Log.Debug("executing power network fill");
            _executeRequested = false;
            Fill();
        }

        private void Fill()
        {
            var factory = GameMain.localPlanet?.factory;
            if (factory == null)
            {
                Log.Warn("Requested filling of generators but no planet no factory found");
                return;
            }

            var invMgr = InventoryManager.instance;
            if (invMgr == null)
            {
                Log.Warn("InvMgr instance not obtained can not fill");
                return;
            }

            var alreadyFilledCount = 0;
            var needFuelCount = 0;
            var actuallyFilledCount = 0;
            var totalGenCount = 0;
            for (int i = 1; i < factory.powerSystem.genCursor; i++)
            {
                var generator = factory.powerSystem.genPool[i];
                if (generator.id != i)
                {
                    continue;
                }

                totalGenCount++;
                if (generator.curFuelId > 0 && generator.fuelId > 0)
                {
                    alreadyFilledCount++;
                    continue;
                }

                needFuelCount++;
                int[] fuelNeed = ItemProto.fuelNeeds[generator.fuelMask];
                if (fuelNeed == null)
                {
                    Log.Debug($"generator has no needs {generator.fuelMask}");
                    continue;
                }

                var filled = false;
                foreach (var fuelItemId in fuelNeed)
                {
                    if (fuelItemId > 0)
                    {
                        if (invMgr.RemoveItemImmediately(fuelItemId, 1))
                        {
                            generator.SetNewFuel(fuelItemId, 1);
                            factory.powerSystem.genPool[generator.id].SetNewFuel(fuelItemId, 1);
                            filled = true;
                            actuallyFilledCount++;
                            break;
                        }
                    }
                }

                if (!filled)
                {
                    var generatorType = LDB.items.Select(factory.entityPool[generator.entityId].protoId).name.Translate();
                    Log.LogPopupWithFrequency($"No fuel found in inventory for generator {generatorType}");
                }
            }
            Log.Debug($"Total generators: {totalGenCount}, {alreadyFilledCount} already filled, {actuallyFilledCount} actually filled, {needFuelCount} need fuel");
        }
    }
}