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

            // Run on the plugin (not the player) so the routine always finishes
            // and restores mouse sensitivity, even if the player dies mid-volley
            Plugin.instance.StartCoroutine(RapidFireRoutine(player, weapon));
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

            // Save the exact original sensitivity BEFORE changing it
            float savedMouseSens = PlayerController.m_mouseSens;

            player.m_speed = savedSpeed * 0.5f;
            player.m_runSpeed = savedRunSpeed * 0.5f;
            player.m_turnSpeed = savedTurnSpeed * 0.3f;
            PlayerController.m_mouseSens = savedMouseSens * 0.3f;

            // ZSyncAnimation syncs triggers to all players
            var zsync = player.GetComponent<ZSyncAnimation>();

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

                // Fire animation — synced to all players
                if (zsync != null)
                    zsync.SetTrigger("crossbow_fire");

                // Muzzle VFX and firing sound — broadcast to all players
                Vector3 vfxPos = player.transform.position
                               + shotDir * 2f
                               + Vector3.up * 1.2f;
                NetworkedEffects.BroadcastVfx(VfxPrefabName, vfxPos,
                    Quaternion.LookRotation(shotDir));
                NetworkedEffects.BroadcastSfx(SfxPrefabName,
                    player.transform.position, player.transform.rotation);

                FireShot(player, weapon, shotDir, shotRange);

                yield return new WaitForSeconds(shotInterval);
            }

            if (player != null)
            {
                player.m_speed = savedSpeed;
                player.m_runSpeed = savedRunSpeed;
                player.m_turnSpeed = savedTurnSpeed;
            }

            // Always restored — this runs even if the player died
            PlayerController.m_mouseSens = savedMouseSens;
        }

        // -------------------------------------------------------------------
        // One shot: find the target now, then either damage instantly
        // (no ProjectileVfx) or send a visible projectile and damage on arrival
        // -------------------------------------------------------------------
        private static void FireShot(Player player, ItemDrop.ItemData weapon,
                                     Vector3 direction, float range)
        {
            Vector3 origin = player.transform.position + Vector3.up * 1.4f;

            var (target, endPoint) = FindShotTarget(player, origin, direction, range);

            string vfx = ResolveProjectile(player, weapon);
            if (string.IsNullOrEmpty(vfx))
            {
                // Instant hitscan
                if (target != null)
                    DamageTarget(player, weapon, target, direction, endPoint);
                return;
            }

            // Visible projectile from just in front of the crossbow
            float speed = Mathf.Max(1f, Plugin.CrossbowProjectileSpeed.Value);
            Vector3 muzzle = origin + direction * 0.6f;
            NetworkedEffects.BroadcastTracer(vfx, muzzle, endPoint, speed);

            // Damage lands when the projectile arrives
            if (target != null)
            {
                float travelTime = Vector3.Distance(muzzle, endPoint) / speed;
                Plugin.instance.StartCoroutine(
                    DamageOnArrival(player, weapon, target, direction, endPoint, travelTime));
            }
        }

        // -------------------------------------------------------------------
        // Projectile prefab for the shot visual:
        //   blank  → none (instant hitscan)
        //   "auto" → the loaded bolt's projectile, else the first bolt type
        //            the game knows that matches this crossbow's ammo
        //   other  → that prefab name as-is
        // -------------------------------------------------------------------
        private static string ResolveProjectile(Player player, ItemDrop.ItemData weapon)
        {
            string setting = Plugin.CrossbowProjectileVfx.Value?.Trim() ?? "";
            if (!setting.Equals("auto", System.StringComparison.OrdinalIgnoreCase))
                return setting;

            string ammoType = weapon.m_shared.m_ammoType;

            // The bolt currently loaded
            var ammo = player.GetAmmoItem();
            if (ammo != null && ammo.m_shared.m_ammoType == ammoType)
            {
                var proj = ammo.m_shared.m_attack.m_attackProjectile;
                if (proj != null) return proj.name;
            }

            // No bolts loaded — first matching bolt type in the game
            if (ObjectDB.instance != null)
            {
                foreach (var prefab in ObjectDB.instance.m_items)
                {
                    var drop = prefab?.GetComponent<ItemDrop>();
                    if (drop == null) continue;
                    var shared = drop.m_itemData.m_shared;
                    if (shared.m_itemType != ItemDrop.ItemData.ItemType.Ammo) continue;
                    if (shared.m_ammoType != ammoType) continue;

                    var proj = shared.m_attack.m_attackProjectile;
                    if (proj != null) return proj.name;
                }
            }

            return ""; // nothing found — fall back to hitscan
        }

        // Nearest valid enemy along the shot, and where the shot ends:
        // the enemy, a wall/terrain, or max range
        private static (Character? target, Vector3 endPoint) FindShotTarget(
            Player player, Vector3 origin, Vector3 direction, float range)
        {
            int envMask = LayerMask.GetMask("Default", "static_solid", "terrain", "piece");
            float actualRange = range;
            if (Physics.Raycast(origin, direction, out RaycastHit envHit, range, envMask))
                actualRange = envHit.distance;

            var hits = Physics.SphereCastAll(origin, 0.3f, direction,
                                              actualRange, SkillMasks.Characters);

            // SphereCastAll returns hits in no particular order — sort nearest first
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var rayHit in hits)
            {
                var character = rayHit.collider.GetComponentInParent<Character>();
                if (character == null) continue;
                if (character == player) continue;
                if (character.IsPlayer() && !player.IsPVPEnabled()) continue;
                // Don't hit player-faction allies (Primal Rally summons)
                if (character.m_faction == Character.Faction.Players) continue;

                // Overlap at the start reports a zero point — use the enemy's center
                Vector3 point = rayHit.distance > 0f
                    ? rayHit.point
                    : character.transform.position + Vector3.up;
                return (character, point);
            }

            return (null, origin + direction * actualRange);
        }

        private static IEnumerator DamageOnArrival(Player player, ItemDrop.ItemData weapon,
                                                   Character target, Vector3 direction,
                                                   Vector3 point, float delay)
        {
            yield return new WaitForSeconds(delay);

            // Target died from something else mid-flight — nothing to hit
            if (target == null || target.IsDead()) yield break;

            // Shooter despawned mid-flight (e.g. logged out) — no attacker to credit
            if (player == null) yield break;

            DamageTarget(player, weapon, target, direction, target.transform.position + Vector3.up);
        }

        private static void DamageTarget(Player player, ItemDrop.ItemData weapon,
                                         Character target, Vector3 direction, Vector3 point)
        {
            HitData hit = new HitData();
            hit.m_damage = weapon.GetDamage();
            hit.m_damage.Modify(Plugin.CrossbowSkillDamage.Value);
            hit.m_pushForce = 2.0f;
            hit.m_staggerMultiplier = 1.5f;
            hit.m_dir = direction;
            hit.m_attacker = player.GetZDOID();
            hit.m_point = point;

            target.Damage(hit);

            if (Plugin.TestMode.Value && target.GetHealth() <= 0f)
                target.SetHealth(1f);
        }
    }
}