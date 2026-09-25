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

        // Staff skills
        private static readonly StaffFireballSkill _staffFireballSkill = new StaffFireballSkill();
        private static readonly StaffIceShardsSkill _staffIceShardsSkill = new StaffIceShardsSkill();
        private static readonly StaffClusterbombSkill _staffClusterbombSkill = new StaffClusterbombSkill();
        private static readonly StaffGreenRootsSkill _staffGreenRootsSkill = new StaffGreenRootsSkill();
        private static readonly StaffSkeletonSkill _staffSkeletonSkill = new StaffSkeletonSkill();
        private static readonly StaffThunderbloodSkill _staffThunderbloodSkill = new StaffThunderbloodSkill();
        private static readonly StaffShieldSkill _staffShieldSkill = new StaffShieldSkill();

        public static IWeaponSkill? GetSkillForWeapon(ItemDrop.ItemData weapon)
        {
            if (weapon == null) return null;

            string anim = weapon.m_shared.m_attack.m_attackAnimation;
            var itemType = weapon.m_shared.m_itemType;
            var skillType = weapon.m_shared.m_skillType;
            string itemName = weapon.m_shared.m_name;

            // ---------------------------------------------------------------
            // Staff skills — routed by item name FIRST
            // ---------------------------------------------------------------
            if (anim.StartsWith("staff_"))
            {
                return itemName switch
                {
                    "$item_stafffireball" => _staffFireballSkill,
                    "$item_stafficeshards" => _staffIceShardsSkill,
                    "$item_staffclusterbomb" => _staffClusterbombSkill,
                    "$item_staffgreenroots" => _staffGreenRootsSkill,
                    "$item_staffskeleton" => _staffSkeletonSkill,
                    "$item_staff_thunderblood" => _staffThunderbloodSkill,
                    "$item_staffshield" => _staffShieldSkill,
                    // Future staves:
                    // "$item_staff_spiritcaller" => _staffSpiritCallerSkill,
                    // "$item_staff_frostorbs"    => _staffFrostOrbsSkill,
                    _ => null,
                };
            }

            // ---------------------------------------------------------------
            // Type + skill checks
            // ---------------------------------------------------------------
            if (itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon &&
                skillType == global::Skills.SkillType.Clubs)
                return _twoHandedMaceSkill;

            if (itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon &&
                skillType == global::Skills.SkillType.Unarmed)
                return _fistSkill;

            // ---------------------------------------------------------------
            // Animation name checks
            // ---------------------------------------------------------------
            if (anim.StartsWith("swing_pickaxe")) return null;
            if (anim.StartsWith("greatsword")) return _greatswordSkill;
            if (anim.StartsWith("battleaxe")) return _greatswordSkill;
            if (anim.StartsWith("dual_knives")) return _dualKnifeSkill;

            // ---------------------------------------------------------------
            // Skill type switch
            // ---------------------------------------------------------------
            switch (skillType)
            {
                case global::Skills.SkillType.Swords: return _swordSkill;
                case global::Skills.SkillType.Clubs: return _maceSkill;
                case global::Skills.SkillType.Axes: return _axeSkill;
                case global::Skills.SkillType.Polearms:
                case global::Skills.SkillType.Spears: return _atgeirSkill;
                case global::Skills.SkillType.Crossbows: return _crossbowSkill;
                case global::Skills.SkillType.Knives: return _knifeSkill;
                case global::Skills.SkillType.Bows: return _bowSkill;
                default: return null;
            }
        }

        public static Sprite? GetSkillIcon(ItemDrop.ItemData? weapon)
        {
            if (weapon == null) return null;
            return GetSkillForWeapon(weapon)?.Icon;
        }
    }
}