using HarmonyLib;
using SDG.Provider;

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
        public static ushort last = 0;
        [HarmonyPatch(typeof(TempSteamworksEconomy), "getInventorySkinID")]
        [HarmonyPrefix]
        public static bool SkinOverridePatch(int item, ref ushort __result)
        {
            if (WebInterface.awaitingSkin == 0) return true;
            __result = WebInterface.awaitingSkin;
            return false;
        }
        [HarmonyPatch(typeof(TempSteamworksEconomy), "getInventorySkinID")]
        [HarmonyPostfix]
        public static void SkinOverridePatchPost(int item, ref ushort __result)
        {
            last = __result;
        }
    }
}
