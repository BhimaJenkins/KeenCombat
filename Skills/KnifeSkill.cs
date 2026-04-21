using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Skills
{
    public class KnifeSkill : IWeaponSkill
    {
        public string SkillName => "Assassination";
        public string Description => "Vanish into the shadows. Strike from stealth for massive damage.";
        public float Cooldown => Plugin.KnifeSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_Knife";

        public static bool InStealth = false;
        public static bool StrikeReady = false;
        public static bool IsExecutingStrike = false;

        private static bool _wasCrouching = false;

        private static Sprite? _stealthIcon = null;
        private static Sprite? _strikeIcon = null;
        private static bool _iconsLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconsLoaded)
                {
                    _stealthIcon = Plugin.LoadEmbeddedSprite("AssassinationStealthIcon.png");
                    _strikeIcon = Plugin.LoadEmbeddedSprite("AssassinationStrikeIcon.png");
                    _iconsLoaded = true;
                }
                return InStealth ? _strikeIcon : _stealthIcon;
            }
        }

        public bool IsOnCooldown(Player player)
            => HUD.SE_SkillCooldown.IsOnCooldown(player, CooldownSEName);

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            if (InStealth)
            {
                if (!StrikeReady) return;
                player.StartCoroutine(ExecuteStrike(player, weapon));
            }
            else
            {
                player.StartCoroutine(EnterStealth(player, weapon));
            }
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator EnterStealth(Player player, ItemDrop.ItemData weapon)
        {
            InStealth = true;
            StrikeReady = false;

            _wasCrouching = player.IsCrouching();
            if (!_wasCrouching)
                player.SetCrouch(true);

            player.m_runSpeed = player.m_speed * 0.8f;

            var vfxPrefab = ZNetScene.instance?.GetPrefab("smokebomb_explosion");
            if (vfxPrefab != null)
            {
                var vfx = Object.Instantiate(vfxPrefab,
                    player.transform.position,
                    player.transform.rotation);
                vfx.transform.localScale = Vector3.one * 0.5f;
            }

            player.StartCoroutine(Plugin.LoadAudioClip("Stealth.ogg", clip =>
            {
                if (clip == null) return;
                var src = player.GetComponent<AudioSource>()
                       ?? player.gameObject.AddComponent<AudioSource>();
                src.spatialBlend = 1f;
                src.PlayOneShot(clip);
            }));

            SetVisual(player, true);

            // Apply stealth buff icon
            HUD.SE_KnifeStealthBuff.Apply(player, Plugin.KnifeStealthDuration.Value);

            yield return new WaitForSeconds(0.5f);
            StrikeReady = true;

            float elapsed = 0.5f;
            float maxDuration = Plugin.KnifeStealthDuration.Value;

            while (elapsed < maxDuration)
            {
                if (player == null || player.IsDead() || IsExecutingStrike) yield break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (InStealth && !IsExecutingStrike)
            {
                ExitStealth(player);
                HUD.SE_SkillCooldown.Apply(player, Plugin.KnifeSkillCooldown.Value,
                                            "SE_Cooldown_Knife");
            }
        }

        private static IEnumerator ExecuteStrike(Player player, ItemDrop.ItemData weapon)
        {
            IsExecutingStrike = true;

            HUD.SE_SkillCooldown.Apply(player, Plugin.KnifeSkillCooldown.Value,
                                        "SE_Cooldown_Knife");

            ExitStealth(player);

            Vector3 strikeDir = GameCamera.instance != null
                ? GameCamera.instance.transform.forward
                : player.transform.forward;

            strikeDir.y = 0f;
            strikeDir.Normalize();
            if (strikeDir != Vector3.zero)
                player.transform.rotation = Quaternion.LookRotation(strikeDir);

            var animator = player.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.SetTrigger("knife_stab2");

            yield return new WaitForSeconds(0.15f);

            var backstabPrefab = ZNetScene.instance?.GetPrefab("fx_backstab");
            if (backstabPrefab != null)
                Object.Instantiate(backstabPrefab,
                    player.transform.position + strikeDir * 1.5f + Vector3.up,
                    Quaternion.LookRotation(strikeDir));

            ApplyStrike(player, weapon, strikeDir);

            IsExecutingStrike = false;
        }

        private static void ApplyStrike(Player player, ItemDrop.ItemData weapon,
                                         Vector3 direction)
        {
            HitData hit = new HitData();
            hit.m_damage = weapon.GetDamage();
            hit.m_damage.Modify(Plugin.KnifeSkillDamage.Value);
            hit.m_pushForce = 3.0f;
            hit.m_staggerMultiplier = 3.0f;
            hit.m_dir = direction;
            hit.m_attacker = player.GetZDOID();
            hit.m_backstabBonus = 4f;

            Vector3 origin = player.transform.position + Vector3.up * 1.2f;

            int characterMask = LayerMask.GetMask("character");
            var colliders = Physics.OverlapSphere(
                origin + direction * 2f, 1.5f, characterMask);

            foreach (var col in colliders)
            {
                var character = col.GetComponentInParent<Character>();
                if (character == null) continue;
                if (character == player) continue;
                if (character.IsPlayer() && !player.IsPVPEnabled()) continue;
                if (character.m_faction == Character.Faction.Players) continue;

                hit.m_point = character.transform.position;
                character.Damage(hit);

                if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                    character.SetHealth(1f);
            }
        }

        private static readonly List<(Renderer r, Color original)> _savedColors
            = new List<(Renderer, Color)>();

        private static void SetVisual(Player player, bool stealth)
        {
            if (stealth)
            {
                _savedColors.Clear();
                foreach (var r in player.GetComponentsInChildren<Renderer>())
                {
                    if (r.material == null) continue;
                    if (!r.material.HasProperty("_Color")) continue;
                    _savedColors.Add((r, r.material.color));
                    r.material.color = new Color(0.15f, 0.15f, 0.15f, 1f);
                }
            }
            else
            {
                foreach (var (r, color) in _savedColors)
                    if (r != null && r.material != null && r.material.HasProperty("_Color"))
                        r.material.color = color;
                _savedColors.Clear();
            }
        }

        private static void ExitStealth(Player player)
        {
            InStealth = false;
            StrikeReady = false;

            if (!_wasCrouching)
                player.SetCrouch(false);

            player.m_runSpeed = 8f;
            SetVisual(player, false);

            // Remove stealth buff icon
            HUD.SE_KnifeStealthBuff.Remove(player);
        }
    }

    [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.CanSenseTarget),
        new[] { typeof(Character) })]
    public static class BaseAI_CanSenseTarget_Stealth_Patch
    {
        static bool Prefix(Character target, ref bool __result)
        {
            if (target != Player.m_localPlayer) return true;
            if (!KnifeSkill.InStealth) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateTarget))]
    public static class MonsterAI_UpdateTarget_Stealth_Patch
    {
        static bool Prefix(MonsterAI __instance)
        {
            if (!KnifeSkill.InStealth) return true;
            if (__instance.m_targetCreature != Player.m_localPlayer) return true;
            __instance.m_targetCreature = null;
            return false;
        }
    }
}