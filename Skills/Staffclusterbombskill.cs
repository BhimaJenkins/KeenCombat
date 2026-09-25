using System.Collections;
using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // StaffClusterbombSkill — Ragnarök
    //
    // Hold the skill button to charge, release to fire a burst of projectiles.
    // < 2 seconds held  → 1 shot
    // 2-3 seconds held  → 2 shots
    // 3+ seconds held   → 3 shots
    //
    // Each shot is the Staff of Fracturing projectile at 1.3x damage.
    // Eitr: 35 on press, drains to 60 over 3 seconds.
    // Movement slowed 50% while charging.
    // Charge bar shows fill progress on HUD.
    //
    // Animation approach: advance normalizedTime manually each frame on
    // layer 1 (upper body) so the charge animation progresses with charge
    // without affecting locomotion on layer 0.
    // -----------------------------------------------------------------------
    public class StaffClusterbombSkill : IWeaponSkill
    {
        public string SkillName => "Ragnarök";
        public string Description => "Channel the fires of Ragnarök. Hold longer to fire " +
                                        "more explosive projectiles at once.";
        public float Cooldown => 0f;
        public string CooldownSEName => "SE_Cooldown_StaffClusterbomb";

        private static Sprite? _icon = null;
        private static bool _iconLoaded = false;

        private static bool _isCharging = false;
        private static float _costMultiplier = 1f;
        private static Coroutine? _chargeCoroutine = null;
        private static Coroutine? _novaCoroutine = null;

        private static float _savedSpeed = 0f;
        private static float _savedRunSpeed = 0f;

        private const float MinEitrCost = 35f;
        private const float MaxEitrCost = 60f;
        private const float MaxChargeTime = 3f;
        private const float NovaRespawnRate = 0.45f;
        private const float DamageMultiplier = 1.3f;
        private const float BurstDelay = 0.15f;

        public Sprite? Icon
        {
            get
            {
                if (!_iconLoaded)
                {
                    _icon = Plugin.LoadEmbeddedSprite("RagnarokIcon.png");
                    _iconLoaded = true;
                }
                return _icon;
            }
        }

        public bool IsOnCooldown(Player player) => false;

        public void OnPress(Player player, ItemDrop.ItemData weapon)
        {
            _costMultiplier = MagicCostScaler.GetMultiplier(player, weapon);
            float minCost = MinEitrCost * _costMultiplier;

            if (!player.HaveEitr(minCost))
            {
                SkillFeedback.NotEnoughEitr();
                return;
            }

            player.UseEitr(minCost);

            _isCharging = true;
            _savedSpeed = player.m_speed;
            _savedRunSpeed = player.m_runSpeed;

            player.m_speed *= 0.5f;
            player.m_runSpeed *= 0.5f;

            HUD.ChargeBarHud.Show();

            _chargeCoroutine = player.StartCoroutine(ChargeRoutine(player));
            _novaCoroutine = player.StartCoroutine(NovaVfxLoop(player));
        }

        public void OnHold(Player player, ItemDrop.ItemData weapon, float heldDuration)
        {
            HUD.ChargeBarHud.UpdateFill(heldDuration / MaxChargeTime);
        }

        public void OnRelease(Player player, ItemDrop.ItemData weapon, float heldDuration)
        {
            if (!_isCharging) return;
            _isCharging = false;

            // Restore movement
            player.m_speed = _savedSpeed;
            player.m_runSpeed = _savedRunSpeed;

            // Hide charge bar
            HUD.ChargeBarHud.Hide();

            // Stop charge coroutines
            if (_chargeCoroutine != null)
            {
                player.StopCoroutine(_chargeCoroutine);
                _chargeCoroutine = null;
            }
            if (_novaCoroutine != null)
            {
                player.StopCoroutine(_novaCoroutine);
                _novaCoroutine = null;
            }

            // Determine shot count
            int shotCount = 1;
            if (heldDuration >= 3f) shotCount = 3;
            else if (heldDuration >= 2f) shotCount = 2;

            player.StartCoroutine(FireRoutine(player, weapon, shotCount));
        }

        // -----------------------------------------------------------------------
        // Charge coroutine
        // Manually advances animator normalizedTime on layer 1 (upper body)
        // so the charge animation progresses with charge time without slowing
        // locomotion on layer 0. Only drains Eitr — animation is driven by
        // normalizedTime advancement each frame.
        // -----------------------------------------------------------------------
        private static IEnumerator ChargeRoutine(Player player)
        {
            var zsync = player.GetComponent<ZSyncAnimation>();
            if (zsync != null)
                zsync.SetTrigger("emote_challenge");


            float remainingEitr = (MaxEitrCost - MinEitrCost) * _costMultiplier;
            float eitrPerSecond = remainingEitr / MaxChargeTime;
            float elapsed = 0f;

            while (_isCharging && elapsed < MaxChargeTime)
            {
                if (player == null || player.IsDead())
                {
                    _isCharging = false;
                    yield break;
                }

                // Drain Eitr
                float drain = eitrPerSecond * Time.deltaTime;
                if (player.HaveEitr(drain))
                    player.UseEitr(drain);

                elapsed += Time.deltaTime;
                yield return null;
            }

        }

        // -----------------------------------------------------------------------
        // Nova VFX loop — respawns fx_fireskeleton_nova while charging
        // -----------------------------------------------------------------------
        private static IEnumerator NovaVfxLoop(Player player)
        {
            while (_isCharging)
            {
                var novaPrefab = ZNetScene.instance?.GetPrefab("fx_fireskeleton_nova");
                if (novaPrefab != null)
                {
                    var nova = Object.Instantiate(novaPrefab,
                        player.transform.position,
                        player.transform.rotation);

                    foreach (var ps in nova.GetComponentsInChildren<ParticleSystem>())
                    {
                        var main = ps.main;
                        main.simulationSpeed = 2f;
                    }
                }

                NetworkedEffects.BroadcastVfx("fx_fireskeleton_nova",
                    player.transform.position,
                    player.transform.rotation);

                yield return new WaitForSeconds(NovaRespawnRate);
            }
        }

        // -----------------------------------------------------------------------
        // Fire coroutine — fires burst of projectiles on release
        // -----------------------------------------------------------------------
        private static IEnumerator FireRoutine(Player player, ItemDrop.ItemData weapon,
                                                int shotCount)
        {
            yield return new WaitForSeconds(0.3f);

            if (player == null || player.IsDead()) yield break;

            var projectilePrefab = ZNetScene.instance?.GetPrefab(
                "staff_clusterbombstaff_projectile");

            if (projectilePrefab == null)
            {
                KC_Log.Warn("StaffClusterbomb: projectile prefab not found!");
                yield break;
            }

            for (int i = 0; i < shotCount; i++)
            {
                if (player == null || player.IsDead()) yield break;

                Vector3 fireDir = GameCamera.instance != null
                    ? GameCamera.instance.transform.forward
                    : player.transform.forward;

                if (i == 1)
                    fireDir = Quaternion.AngleAxis(-4f, Vector3.up) * fireDir;
                else if (i == 2)
                    fireDir = Quaternion.AngleAxis(4f, Vector3.up) * fireDir;

                Vector3 spawnPos = player.transform.position
                                 + Vector3.up * 1.5f
                                 + fireDir * 1.0f;

                var projectileObj = Object.Instantiate(projectilePrefab, spawnPos,
                                        Quaternion.LookRotation(fireDir));

                var projectile = projectileObj.GetComponent<Projectile>();
                if (projectile != null)
                {
                    HitData hit = new HitData();
                    hit.m_damage = weapon.GetDamage();
                    hit.m_damage.Modify(DamageMultiplier);
                    hit.m_pushForce = 3f;
                    hit.m_staggerMultiplier = 1.5f;
                    hit.m_attacker = player.GetZDOID();
                    hit.m_dir = fireDir;
                    hit.m_point = spawnPos;

                    projectile.Setup(player, fireDir * 30f, -1f, hit, weapon, null);
                }

                if (i < shotCount - 1)
                    yield return new WaitForSeconds(BurstDelay);
            }
        }
    }
}