using UnityEngine;

namespace KeenCombat.Skills
{
    public static class WeaponSkillManager
    {
        private static readonly SwordSkill _swordSkill = new SwordSkill();
        private static readonly MaceSkill _maceSkill = new MaceSkill();
        private static readonly AxeSkill _axeSkill = new AxeSkill();
        private static readonly AtgeirSkill _atgeirSkill = new AtgeirSkill();
        private static readonly CrossbowSkill _crossbowSkill = new CrossbowSkill();
        private static readonly KnifeSkill _knifeSkill = new KnifeSkill();
        private static readonly GreatswordSkill _greatswordSkill = new GreatswordSkill();
        private static readonly BowSkill _bowSkill = new BowSkill();
        private static readonly DualKnifeSkill _dualKnifeSkill = new DualKnifeSkill();
        private static readonly FistSkill _fistSkill = new FistSkill();
        private static readonly TwoHandedMaceSkill _twoHandedMaceSkill = new TwoHandedMaceSkill();

        public static IWeaponSkill? GetSkillForWeapon(ItemDrop.ItemData weapon)
        {
            if (weapon == null) return null;

            string anim = weapon.m_shared.m_attack.m_attackAnimation;
            var itemType = weapon.m_shared.m_itemType;
            var skillType = weapon.m_shared.m_skillType;

            // ---------------------------------------------------------------
            // Type + skill checks FIRST — these take priority over anim names
            // so modded weapons with reused animations route correctly.
            // ---------------------------------------------------------------

            // 2H Mace — must come before greatsword/battleaxe anim checks
            // since Therzie's 2H maces may use those animation names
            if (itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon &&
                skillType == global::Skills.SkillType.Clubs)
                return _twoHandedMaceSkill;

            // Fist weapons — TwoHandedWeapon + Unarmed (excludes bare hands)
            if (itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon &&
                skillType == global::Skills.SkillType.Unarmed)
                return _fistSkill;

            // ---------------------------------------------------------------
            // Animation name checks — for weapons that share SkillType with
            // their 1H counterparts
            // ---------------------------------------------------------------

            // Pickaxe — exclude entirely
            if (anim.StartsWith("swing_pickaxe")) return null;

            // 2H Sword — shares SkillType.Swords with regular swords
            if (anim.StartsWith("greatsword")) return _greatswordSkill;

            // 2H Axe — shares SkillType.Axes with regular axes
            if (anim.StartsWith("battleaxe")) return _greatswordSkill;

            // Dual Knife — shares SkillType.Knives with regular knives
            if (anim.StartsWith("dual_knives")) return _dualKnifeSkill;

            // ---------------------------------------------------------------
            // Unique weapon name checks — add specific overrides here.
            // e.g. if (weapon.m_shared.m_name == "$item_sword_dyrnwyn")
            //          return _dyrnwynSkill;
            // ---------------------------------------------------------------

            switch (skillType)
            {
                case global::Skills.SkillType.Swords:
                    return _swordSkill;

                case global::Skills.SkillType.Clubs:
                    return _maceSkill;

                case global::Skills.SkillType.Axes:
                    return _axeSkill;

                case global::Skills.SkillType.Polearms:
                case global::Skills.SkillType.Spears:
                    return _atgeirSkill;

                case global::Skills.SkillType.Crossbows:
                    return _crossbowSkill;

                case global::Skills.SkillType.Knives:
                    return _knifeSkill;

                case global::Skills.SkillType.Bows:
                    return _bowSkill;

                default:
                    return null;
            }
        }

        public static Sprite? GetSkillIcon(ItemDrop.ItemData? weapon)
        {
            if (weapon == null) return null;
            return GetSkillForWeapon(weapon)?.Icon;
        }
    }
}