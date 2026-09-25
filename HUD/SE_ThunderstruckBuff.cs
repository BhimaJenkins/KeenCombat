using System.Collections.Generic;
using UnityEngine;

namespace KeenCombat.HUD
{
    // -----------------------------------------------------------------------
    // SE_ThunderstruckBuff
    //
    // Runs the whole Thunderstruck mechanic. Because the logic lives inside the
    // status effect, it stops automatically when the buff expires, when the
    // player dies (Valheim clears buffs on death) or when they log out.
    //
    // Every PollInterval seconds:
    //   - Enemies inside the radius without a timer get a random 5-8s timer
    //   - Enemies whose timer is up get struck, then get a fresh timer
    //   - Enemies that left the radius, died or became invalid lose their timer
    //
    // Runs on the caster's client only. Strike VFX/SFX are broadcast so all
    // players see them; damage syncs through Valheim's normal hit system.
    // -----------------------------------------------------------------------
    public class SE_ThunderstruckBuff : StatusEffect
    {
        public const string StatusEffectName = "SE_ThunderstruckBuff";

        private static SE_ThunderstruckBuff? _template;

        // Snapshot of the damage per strike, taken at cast time
        public float StrikeDamage = 0f;

        private const float PollInterval = 0.25f;
        private float _pollTimer = 0f;

        // Per-enemy strike time (Time.time at which the next strike lands)
        private readonly Dictionary<Character, float> _nextStrike
            = new Dictionary<Character, float>();

        private readonly List<Character> _toRemove = new List<Character>();

        public static void Register()
        {
            if (_template != null) return;

            _template = ScriptableObject.CreateInstance<SE_ThunderstruckBuff>();
            _template.name = StatusEffectName;
            _template.m_name = "Thunderstruck";
            _template.m_icon = Plugin.LoadEmbeddedSprite("ThunderstruckBuffIcon.png");

            if (ObjectDB.instance != null && !ObjectDB.instance.m_StatusEffects.Contains(_template))
                ObjectDB.instance.m_StatusEffects.Add(_template);
        }

        /// <summary>
        /// Apply Thunderstruck, or refresh its duration and damage if already active.
        /// </summary>
        public static void Apply(Player player, float strikeDamage, float duration)
        {
            if (_template == null)
            {
                Plugin.Log.LogWarning("SE_ThunderstruckBuff not registered yet.");
                return;
            }

            var existing = player.GetSEMan()
                .GetStatusEffect(StatusEffectName.GetStableHashCode()) as SE_ThunderstruckBuff;

            if (existing != null)
            {
                existing.m_ttl = duration;
                existing.StrikeDamage = strikeDamage;

                var field = typeof(StatusEffect).GetField("m_time",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                field?.SetValue(existing, 0f);
                return;
            }

            var inst = ScriptableObject.CreateInstance<SE_ThunderstruckBuff>();
            inst.name = StatusEffectName;
            inst.m_name = "Thunderstruck";
            inst.m_icon = _template.m_icon;
            inst.m_ttl = duration;
            inst.StrikeDamage = strikeDamage;

            player.GetSEMan().AddStatusEffect(inst, true, 0, 0f);
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);

            // Only the caster's own client runs the strike logic
            if (m_character == null || m_character != Player.m_localPlayer) return;
            if (m_character.IsDead()) return;

            _pollTimer += dt;
            if (_pollTimer < PollInterval) return;
            _pollTimer = 0f;

            var caster = (Player)m_character;
            float radius = Plugin.StaffThunderbloodRadius.Value;
            float radiusSq = radius * radius;
            float now = Time.time;
            Vector3 center = caster.transform.position;

            // 1. Drop enemies that died, despawned, became invalid or left the circle
            _toRemove.Clear();
            foreach (var kv in _nextStrike)
            {
                var c = kv.Key;
                if (c == null || c.IsDead() || !IsValidTarget(caster, c) ||
                    (c.transform.position - center).sqrMagnitude > radiusSq)
                    _toRemove.Add(c!);
            }
            foreach (var c in _toRemove)
                _nextStrike.Remove(c);

            // 2. New enemies in the circle get a timer; due enemies get struck
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead()) continue;
                if ((c.transform.position - center).sqrMagnitude > radiusSq) continue;
                if (!IsValidTarget(caster, c)) continue;

                if (!_nextStrike.TryGetValue(c, out float strikeAt))
                {
                    _nextStrike[c] = now + RandomInterval();
                    continue;
                }

                if (now >= strikeAt)
                {
                    Strike(caster, c);
                    _nextStrike[c] = now + RandomInterval();
                }
            }
        }

        public override void Stop()
        {
            base.Stop();
            _nextStrike.Clear();
        }

        private static float RandomInterval()
        {
            return Random.Range(Plugin.StaffThunderbloodIntervalMin.Value,
                                Plugin.StaffThunderbloodIntervalMax.Value);
        }

        // -------------------------------------------------------------------
        // Target rules
        // Never: players, tamed creatures, summons, passive wildlife,
        //        creatures with taming in progress, non-hostiles.
        // -------------------------------------------------------------------
        private static bool IsValidTarget(Player caster, Character c)
        {
            if (c == caster) return false;
            if (c.IsPlayer()) return false;
            if (c.IsTamed()) return false;
            if (c.m_faction == Character.Faction.Players) return false;

            var nview = c.GetComponent<ZNetView>();
            var zdo = nview?.GetZDO();
            if (zdo == null) return false;

            // Primal Rally / Charred Requiem summons
            if (zdo.GetBool("KeenCombat_PrimalRally")) return false;

            // Passive wildlife (deer, hares, etc.) uses AnimalAI and never attacks
            if (c.GetComponent<AnimalAI>() != null) return false;

            // Taming in progress — its taming timer has started counting down
            var tameable = c.GetComponent<Tameable>();
            if (tameable != null)
            {
                float left = zdo.GetFloat(ZDOVars.s_tameTimeLeft, tameable.m_tamingTime);
                if (left < tameable.m_tamingTime - 0.1f) return false;
            }

            // Must actually be hostile to the caster (handles neutral
            // creatures like Dvergr unless they've been provoked)
            if (!BaseAI.IsEnemy(caster, c)) return false;

            return true;
        }

        // -------------------------------------------------------------------
        // Strike
        // -------------------------------------------------------------------
        private void Strike(Player caster, Character target)
        {
            Vector3 pos = target.transform.position;

            string vfx = Plugin.StaffThunderbloodStrikeVfx.Value;
            string sfx = Plugin.StaffThunderbloodStrikeSfx.Value;

            // Spawn above the enemy, tilted 90° to point straight down, so a
            // forward-travelling effect (like Eikthyr's shockwave) slams from
            // the sky into the ground. Random yaw keeps repeat hits varied.
            Vector3 fxPos = pos + Vector3.up * Plugin.StaffThunderbloodStrikeHeight.Value;
            Quaternion fxRot = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);

            // Visuals and sound for all players
            if (!string.IsNullOrEmpty(vfx) && !string.IsNullOrEmpty(sfx))
                NetworkedEffects.BroadcastVfxSfx(vfx, sfx, fxPos, fxRot);
            else if (!string.IsNullOrEmpty(vfx))
                NetworkedEffects.BroadcastVfx(vfx, fxPos, fxRot);
            else if (!string.IsNullOrEmpty(sfx))
                NetworkedEffects.BroadcastSfx(sfx, pos, Quaternion.identity);

            // Damage — caster's client only
            var hit = new HitData();
            hit.m_damage.m_lightning = StrikeDamage;
            hit.m_pushForce = 0f;
            hit.m_staggerMultiplier = 1f;
            hit.m_point = pos;
            hit.m_dir = Vector3.down;
            hit.m_attacker = caster.GetZDOID();
            target.Damage(hit);
        }

        // -------------------------------------------------------------------
        // Buff icon countdown + tooltip
        // -------------------------------------------------------------------
        private float GetRemaining()
        {
            var field = typeof(StatusEffect).GetField("m_time",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            float elapsed = field != null ? (float)field.GetValue(this) : 0f;
            return Mathf.Max(0f, m_ttl - elapsed);
        }

        public override string GetIconText()
        {
            float remaining = GetRemaining();
            if (remaining <= 0.5f) return "";
            // Minutes while over a minute, then seconds
            return remaining >= 60f
                ? $"{Mathf.CeilToInt(remaining / 60f)}m"
                : $"{Mathf.CeilToInt(remaining)}s";
        }

        public override string GetTooltipString()
        {
            return $"<color=#9FD5FF>Striking nearby enemies for {StrikeDamage:0} lightning</color>\n" +
                   $"<color=orange>{GetRemaining() / 60f:0.0} min remaining</color>";
        }
    }
}