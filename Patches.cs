using HarmonyLib;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace IconSenderModule
{
    [HarmonyPatch]
    public static class Patches
    {
        public static Harmony Patcher;
        /// <summary>Patch methods</summary>
        public static void DoPatching()
        {
            Patcher = new Harmony("net.blazingflame.iconsender");
            Patcher.PatchAll();
        }
        public static void Unpatch()
        {
            Patcher.UnpatchAll();
        }

        [HarmonyPatch(typeof(PlayerLifeUI), "updateCompass")]
        [HarmonyPrefix]
        public static bool UpdateCompassPatch()
        {
            return Player.player.equipment.itemID > 0;
        }
    }
}
