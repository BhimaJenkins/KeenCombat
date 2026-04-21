using System.Collections;
using UnityEngine;

namespace KeenCombat.Skills
{
    public class CrossbowSkill : IWeaponSkill
    {
        public string SkillName => "Rapid Fire";
        public string Description => "Unleash a rapid volley of six shots, each aimed where your crossbow is pointing.";
        public float Cooldown => Plugin.CrossbowSkillCooldown.Value;
        public string CooldownSEName => "SE_Cooldown_Crossbow";

        private const string VfxPrefabName = "vfx_crossbow_lightning_fire";
        private const string SfxPrefabName = "sfx_arbalest_fire";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("RapidFireIcon.png");
                    _iconLoaded = true;
                }
                return _icon;
            }
        }

        public bool IsOnCooldown(Player player)
            => HUD.SE_SkillCooldown.IsOnCooldown(player, CooldownSEName);

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            HUD.SE_SkillCooldown.Apply(player, Cooldown, CooldownSEName);

            if (player.m_currentAttack != null)
            {
                player.m_currentAttack.Stop();
                player.m_previousAttack = player.m_currentAttack;
                player.m_currentAttack = null;
            }
            player.ClearActionQueue();

            player.StartCoroutine(RapidFireRoutine(player, weapon));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration) { }
        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration) { }

        private static IEnumerator RapidFireRoutine(Player player, ItemDrop.ItemData weapon)
        {
            const int totalShots = 6;
            const float shotInterval = 0.5f;
            const float shotRange = 60f;

            float savedSpeed = player.m_speed;
            float savedRunSpeed = player.m_runSpeed;
            float savedTurnSpeed = player.m_turnSpeed;

            player.m_speed = savedSpeed * 0.5f;
            player.m_runSpeed = savedRunSpeed * 0.5f;
            player.m_turnSpeed = savedTurnSpeed * 0.3f;
            PlayerController.m_mouseSens = PlayerController.m_mouseSens * 0.3f;
            float savedMouseSens = PlayerController.m_mouseSens / 0.3f;

            var animator = player.GetComponentInChildren<Animator>();
            var vfxPrefab = ZNetScene.instance?.GetPrefab(VfxPrefabName);
            var sfxPrefab = ZNetScene.instance?.GetPrefab(SfxPrefabName);

            for (int i = 0; i < totalShots; i++)
            {
                if (player == null || player.IsDead()) break;

                Vector3 shotDir = GameCamera.instance != null
                    ? GameCamera.instance.transform.forward
                    : player.transform.forward;
                shotDir.Normalize();

                Vector3 flatDir = new Vector3(shotDir.x, 0f, shotDir.z).normalized;
                if (flatDir != Vector3.zero)
                    player.transform.rotation = Quaternion.LookRotation(flatDir);

                if (animator != null)
                    animator.SetTrigger("crossbow_fire");

                if (vfxPrefab != null)
                {
                    Vector3 vfxPos = player.transform.position
                                   + shotDir * 2f
                                   + Vector3.up * 1.2f;
                    Object.Instantiate(vfxPrefab, vfxPos,
                                       Quaternion.LookRotation(shotDir));
                }

                if (sfxPrefab != null)
                    Object.Instantiate(sfxPrefab, player.transform.position,
                                       player.transform.rotation);

                ApplyShot(player, weapon, shotDir, shotRange);

                yield return new WaitForSeconds(shotInterval);
            }

            if (player != null)
            {
                player.m_speed = savedSpeed;
                player.m_runSpeed = savedRunSpeed;
                player.m_turnSpeed = savedTurnSpeed;
            }
            PlayerController.m_mouseSens = savedMouseSens;
        }

        private static void ApplyShot(Player player, ItemDrop.ItemData weapon,
                                       Vector3 direction, float range)
        {
            HitData hit = new HitData();
            hit.m_damage = weapon.GetDamage();
            hit.m_damage.Modify(Plugin.CrossbowSkillDamage.Value);
            hit.m_pushForce = 2.0f;
            hit.m_staggerMultiplier = 1.5f;
            hit.m_dir = direction;
            hit.m_attacker = player.GetZDOID();

            Vector3 origin = player.transform.position + Vector3.up * 1.4f;

            int envMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece");
            float actualRange = range;
            if (Physics.Raycast(origin, direction, out RaycastHit envHit, range, envMask))
                actualRange = envHit.distance;

            int characterMask = LayerMask.GetMask("character");
            var hits = Physics.SphereCastAll(origin, 0.3f, direction,
                                              actualRange, characterMask);

            foreach (var rayHit in hits)
            {
                var character = rayHit.collider.GetComponentInParent<Character>();
                if (character == null) continue;
                if (character == player) continue;
                if (character.IsPlayer() && !player.IsPVPEnabled()) continue;
                // Don't hit player-faction allies (Primal Rally summons)
                if (character.m_faction == Character.Faction.Players) continue;

                hit.m_point = rayHit.point;
                character.Damage(hit);

                if (Plugin.TestMode.Value && character.GetHealth() <= 0f)
                    character.SetHealth(1f);

                break; // Stop at first target
            }
        }
    }
}