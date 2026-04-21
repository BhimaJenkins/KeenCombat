using HarmonyLib;

namespace KeenCombat.Patches
{
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
    public static class AutoShieldPatch
    {
        static void Postfix(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result) return;
            if (__instance != Player.m_localPlayer) return;
            if (item == null) return;
            if (!Plugin.AutoShieldEnabled.Value) return;

            if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.OneHandedWeapon) return;

            if (Plugin.ExcludeAxe.Value &&
                item.m_shared.m_skillType == global::Skills.SkillType.Axes) return;

            if (Plugin.ExcludeKnife.Value &&
                item.m_shared.m_skillType == global::Skills.SkillType.Knives) return;

            if (__instance is Player player)
                TryAutoEquipShield(player);
        }

        private static void TryAutoEquipShield(Player player)
        {
            if (player == null) return;

            var leftItem = player.m_leftItem;
            if (leftItem != null &&
                leftItem.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield) return;

            ItemDrop.ItemData? bestShield = null;
            float bestBlock = -1f;

            foreach (var invItem in player.GetInventory().GetAllItems())
            {
                if (invItem.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Shield) continue;

                float block = invItem.m_shared.m_blockPower;
                if (block > bestBlock)
                {
                    bestBlock = block;
                    bestShield = invItem;
                }
            }

            if (bestShield != null)
                player.EquipItem(bestShield);
        }
    }
}