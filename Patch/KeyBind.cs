using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace LongArm.Util
{
    public class PressKeyBind
    {
        public bool keyValue
        {
            get
            {
                if (!VFInput.override_keys[defaultBind.id].IsNull())
                {
                    return ReadKey(VFInput.override_keys[defaultBind.id]);
                }

                return ReadDefaultKey();
            }
        }

        public BuiltinKey defaultBind;

        public void Init(BuiltinKey defaultBind)
        {
            this.defaultBind = defaultBind;
        }

        protected virtual bool ReadDefaultKey()
        {
            return ReadKey(defaultBind.key);
        }

        protected virtual bool ReadKey(CombineKey key)
        {
            return key.GetKeyDown();
        }
    }

    [HarmonyPatch]
    public static class KeyBindPatch
    {
        public static Dictionary<string, PressKeyBind> customKeys = new Dictionary<string, PressKeyBind>();

        public static void UpdateArray<T>(ref T[] array, int newSize)
        {
            T[] oldArray = array;
            array = new T[newSize];
            Array.Copy(oldArray, array, oldArray.Length);
        }

        public static void RegisterKeyBind<T>(BuiltinKey key) where T : PressKeyBind, new()
        {
            var keyBind = new T();
            keyBind.Init(key);
            customKeys.Add("KEY" + key.name, keyBind);
        }

        public static bool HasKeyBind(string id)
        {
            string key = "KEY" + id;
            return customKeys.ContainsKey(key);
        }

        public static PressKeyBind GetKeyBind(string id)
        {
            string key = "KEY" + id;
            if (customKeys.ContainsKey(key))
            {
                return customKeys[key];
            }

            return null;
        }

        public static void Init()
        {
            RegisterKeyBind<PressKeyBind>(new BuiltinKey
                {
                    id = 108,
                    key = new CombineKey((int) KeyCode.L, CombineKey.CTRL_COMB, ECombineKeyAction.OnceClick, false),
                    conflictGroup = 2052,
                    name = "ShowLongArmWindow",
                    canOverride = true
                });
            
            RegisterKeyBind<PressKeyBind>(new BuiltinKey
                {
                    id = 109,
                    key = new CombineKey((int) KeyCode.W, CombineKey.SHIFT_COMB, ECombineKeyAction.OnceClick, false),
                    conflictGroup = 2052,
                    name = "ShowFactoryTour",
                    canOverride = true
                });
            
        }

        [HarmonyPatch(typeof(UIOptionWindow), "_OnCreate")]
        [HarmonyPrefix]
        public static void AddKeyBind(UIOptionWindow __instance)
        {
            PressKeyBind[] newKeys = customKeys.Values.ToArray();
            Log.Debug($"newKeys length {newKeys.Length}");
            if (newKeys.Length == 0) return;

            int index = DSPGame.key.builtinKeys.Length;
            UpdateArray(ref DSPGame.key.builtinKeys, index + customKeys.Count);

            for (int i = 0; i < newKeys.Length; i++)
            {
                DSPGame.key.builtinKeys[index + i] = newKeys[i].defaultBind;
            }
        }
    }
}