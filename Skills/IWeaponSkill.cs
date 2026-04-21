using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // IWeaponSkill
    //
    // The base contract that every weapon skill must implement.
    // WeaponSkillManager calls these methods based on button input events.
    //
    // For simple instant skills (like Sword Lunge), only OnPress is needed.
    // For two-stage skills (like Dagger Stealth), OnPress tracks which stage.
    // For aim-and-release skills (like AOE spells), OnHold moves the marker
    // and OnRelease commits the action.
    //
    // To add a new skill:
    //   1. Create a new class in the Skills folder (e.g. MaceSkill.cs)
    //   2. Implement this interface
    //   3. Add a case in WeaponSkillManager.GetSkillForWeapon()
    // -----------------------------------------------------------------------
    public interface IWeaponSkill
    {
        // Display name shown in key hints and the HUD tooltip.
        string SkillName { get; }

        // Description shown in the Compendium.
        string Description { get; }

        // Cooldown duration in seconds after the skill fires.
        float Cooldown { get; }

        // Unique status effect name for this skill's cooldown.
        // Each skill must return a different name so cooldowns are independent —
        // weapon swapping does not share or reset another skill's cooldown.
        // Convention: "SE_Cooldown_<WeaponType>" e.g. "SE_Cooldown_Sword"
        string CooldownSEName { get; }

        // Icon shown on the HUD skill slot.
        // Can be null until a real sprite is loaded from an AssetBundle.
        Sprite? Icon { get; }

        // Called once on the frame the skill button is first pressed.
        void OnPress(Player player, ItemDrop.ItemData weapon);

        // Called every frame while the skill button is held down.
        // Used for aim-and-release skills to move targeting indicators.
        // Leave empty for instant skills.
        void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration);

        // Called once on the frame the skill button is released.
        // Used for aim-and-release skills to commit the action.
        // Leave empty for instant skills.
        void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration);

        // Returns true if the skill is currently on cooldown.
        // WeaponSkillManager checks this before calling OnPress.
        bool IsOnCooldown(Player player);
    }
}