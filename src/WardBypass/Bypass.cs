using System;
using HarmonyLib;
using UnityEngine;

namespace WardBypass
{
    // Every vanilla ward check (building, removing, doors, chests, portals, item stands, terrain and so on) goes
    // through the static PrivateArea.CheckAccess, which asks each enabled ward in range whether the local player
    // has access. Granting that access for player-built wards bypasses them all with one narrow patch and leaves
    // CheckAccess itself, ward flashing and non-player (location) wards vanilla.
    internal static class Bypass
    {
        internal static bool Active;

        // Attack.SpawnOnHitTerrain also runs for creatures this client simulates. While it runs for anyone but
        // the local player, the bypass is off so creatures cannot reshape terrain inside wards. The call does not
        // nest, so one flag is enough; the finalizer clears it even when the call throws.
        private static bool foreignTerrainHit;

        internal static bool Applies => Active && !foreignTerrainHit;

        [HarmonyPatch(typeof(PrivateArea), "HaveLocalAccess", new Type[0])]
        private static class HaveLocalAccessPatch
        {
            private static void Postfix(PrivateArea __instance, ref bool __result)
            {
                if (__result || !Applies) return;
                if (__instance.m_ownerFaction == Character.Faction.Players) __result = true;
            }
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.SpawnOnHitTerrain), new[]
        {
            typeof(Vector3), typeof(GameObject), typeof(Character), typeof(float), typeof(ItemDrop.ItemData), typeof(ItemDrop.ItemData), typeof(bool)
        })]
        private static class SpawnOnHitTerrainPatch
        {
            private static void Prefix(Character character)
            {
                Player local = Player.m_localPlayer;
                foreignTerrainHit = !local || !character || character != local;
            }

            private static Exception Finalizer(Exception __exception)
            {
                foreignTerrainHit = false;
                return __exception;
            }
        }
    }
}
