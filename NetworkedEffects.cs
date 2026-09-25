using System.Collections;
using UnityEngine;

namespace KeenCombat
{
    // -----------------------------------------------------------------------
    // NetworkedEffects
    //
    // Universal RPC system for broadcasting VFX, SFX, OGG audio,
    // character animations, and skill-specific effects to all clients.
    //
    // Registered in ObjectDB_Awake_Patch in SE_SkillCooldown.cs.
    // ResetRegistration() called on ZRoutedRpc.Awake so RPCs re-register
    // correctly after logout/login without needing a full game restart.
    // -----------------------------------------------------------------------
    public static class NetworkedEffects
    {
        private const string RpcVfxSfx = "KeenCombat_VfxSfx";
        private const string RpcVfx = "KeenCombat_Vfx";
        private const string RpcSfx = "KeenCombat_Sfx";
        private const string RpcOgg = "KeenCombat_Ogg";
        private const string RpcAnimTrig = "KeenCombat_AnimTrigger";
        private const string RpcAnimSpeed = "KeenCombat_AnimSpeed";
        private const string RpcMeteor = "KeenCombat_Meteor";
        private const string RpcIceFreeze = "KeenCombat_IceFreeze";
        private const string RpcVfxAttached = "KeenCombat_VfxAttached";
        private const string RpcSanctuary = "KeenCombat_Sanctuary";
        private const string RpcRegen = "KeenCombat_Regeneration";
        private const string RpcAnimPlay = "KeenCombat_AnimPlay";
        private const string RpcTracer = "KeenCombat_Tracer";

        // The ZRoutedRpc instance our RPCs are registered on. Valheim creates a
        // new one for every world session, so we register once per instance.
        private static ZRoutedRpc? _registeredOn = null;

        // -----------------------------------------------------------------------
        // Called by ZRoutedRpc.Awake patch — resets flag so RPCs re-register
        // on the new ZRoutedRpc instance after logout/login.
        // -----------------------------------------------------------------------
        // Kept for compatibility — registration now tracks the ZRoutedRpc
        // instance itself, so no reset is needed.
        public static void ResetRegistration() { }

        // -----------------------------------------------------------------------
        // Registration — called from ObjectDB_Awake_Patch
        // -----------------------------------------------------------------------
        public static void Register()
        {
            // No network yet (e.g. main menu) — Game.Start will register later
            if (ZRoutedRpc.instance == null) return;

            // Already registered on this session's instance
            if (ReferenceEquals(_registeredOn, ZRoutedRpc.instance)) return;

            ZRoutedRpc.instance.Register<string, string, Vector3, Quaternion, float>(
                RpcVfxSfx, OnReceiveVfxSfx);
            ZRoutedRpc.instance.Register<string, Vector3, Quaternion, float>(
                RpcVfx, OnReceiveVfx);
            ZRoutedRpc.instance.Register<string, Vector3, Quaternion>(
                RpcSfx, OnReceiveSfx);
            ZRoutedRpc.instance.Register<string, Vector3>(
                RpcOgg, OnReceiveOgg);
            ZRoutedRpc.instance.Register<ZPackage>(
                RpcAnimTrig, OnReceiveAnimTrigger);
            ZRoutedRpc.instance.Register<ZPackage>(
                RpcAnimSpeed, OnReceiveAnimSpeed);
            ZRoutedRpc.instance.Register<ZPackage>(
                RpcMeteor, OnReceiveMeteor);
            ZRoutedRpc.instance.Register<ZPackage>(
                RpcIceFreeze, OnReceiveIceFreeze);
            ZRoutedRpc.instance.Register<ZPackage>(
                RpcVfxAttached, OnReceiveVfxAttached);
            ZRoutedRpc.instance.Register<ZPackage>(
                RpcSanctuary, OnReceiveSanctuary);
            ZRoutedRpc.instance.Register<ZPackage>(
                RpcRegen, OnReceiveRegeneration);
            ZRoutedRpc.instance.Register<ZPackage>(
                RpcAnimPlay, OnReceiveAnimPlay);
            ZRoutedRpc.instance.Register<ZPackage>(
                RpcTracer, OnReceiveTracer);

            _registeredOn = ZRoutedRpc.instance;
            KC_Log.Debug("NetworkedEffects: RPCs registered.");
        }

        // -----------------------------------------------------------------------
        // Broadcast methods
        // -----------------------------------------------------------------------

        public static void BroadcastVfxSfx(string vfxName, string sfxName,
                                            Vector3 pos, Quaternion rot,
                                            float scale = 1f)
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcVfxSfx,
                vfxName, sfxName, pos, rot, scale);
        }

        /// <summary>
        /// Broadcast a VFX to all clients with ALL of its built-in audio removed
        /// and no replacement sound. Use for visuals whose sound you don't want.
        /// </summary>
        public static void BroadcastVfxSilent(string vfxName,
                                               Vector3 pos, Quaternion rot,
                                               float scale = 1f)
        {
            // Reuses the VfxSfx RPC with an empty SFX name — the receiver
            // strips the VFX audio and skips spawning any sound.
            BroadcastVfxSfx(vfxName, "", pos, rot, scale);
        }

        public static void BroadcastVfx(string vfxName,
                                         Vector3 pos, Quaternion rot,
                                         float scale = 1f)
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcVfx,
                vfxName, pos, rot, scale);
        }

        /// <summary>
        /// Broadcast a VFX that attaches to a character on every client, so it
        /// moves with them. suppressAudio strips the VFX's built-in sound.
        /// </summary>
        /// tintHex (optional, "#RRGGBB") recolors the VFX keeping its transparency.
        /// heightOffset (optional) moves it up/down relative to the character.
        public static void BroadcastVfxOnCharacter(Character character, string vfxName,
                                                    bool suppressAudio = false,
                                                    float scale = 1f,
                                                    string tintHex = "",
                                                    float heightOffset = 0f)
        {
            if (ZRoutedRpc.instance == null || character == null) return;
            var pkg = new ZPackage();
            pkg.Write(character.GetZDOID());
            pkg.Write(vfxName);
            pkg.Write(suppressAudio);
            pkg.Write(scale);
            pkg.Write(tintHex ?? "");
            pkg.Write(heightOffset);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcVfxAttached, pkg);
        }

        /// <summary>
        /// Broadcast a Sanctuary dome to all clients. Each client spawns the
        /// dome visual and slows the enemies/projectiles it controls.
        /// </summary>
        public static void BroadcastSanctuary(ZDOID caster, Vector3 center,
                                              float radius, float duration, float slow)
        {
            if (ZRoutedRpc.instance == null) return;
            var pkg = new ZPackage();
            pkg.Write(caster);
            pkg.Write(center);
            pkg.Write(radius);
            pkg.Write(duration);
            pkg.Write(slow);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcSanctuary, pkg);
        }

        /// <summary>
        /// Ask every client to apply Regeneration to a character. Only the
        /// client that controls that character applies it, so the buff and
        /// healing run where Valheim actually simulates the character.
        /// </summary>
        public static void BroadcastRegeneration(ZDOID target, float healPercent, float duration)
        {
            if (ZRoutedRpc.instance == null) return;
            var pkg = new ZPackage();
            pkg.Write(target);
            pkg.Write(healPercent);
            pkg.Write(duration);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcRegen, pkg);
        }

        /// <summary>
        /// Broadcasts a visual-only projectile that flies from 'from' to 'to'
        /// at 'speed' m/s on every client. No collision or damage — pair it
        /// with damage applied after (distance / speed) seconds.
        /// </summary>
        public static void BroadcastTracer(string vfxName, Vector3 from, Vector3 to, float speed)
        {
            if (ZRoutedRpc.instance == null) return;
            var pkg = new ZPackage();
            pkg.Write(vfxName);
            pkg.Write(from);
            pkg.Write(to);
            pkg.Write(speed);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcTracer, pkg);
        }

        public static void BroadcastSfx(string sfxName, Vector3 pos, Quaternion rot)
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcSfx,
                sfxName, pos, rot);
        }

        public static void BroadcastOgg(string filename, Vector3 pos)
        {
            if (ZRoutedRpc.instance == null) return;
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcOgg,
                filename, pos);
        }

        public static void BroadcastAnimationTrigger(Player player, string triggerName)
        {
            if (ZRoutedRpc.instance == null) return;
            var pkg = new ZPackage();
            pkg.Write(player.GetZDOID());
            pkg.Write(triggerName);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcAnimTrig, pkg);
        }

        /// <summary>
        /// Broadcasts animator.Play(stateName, layer, normalizedTime) for a
        /// character to all clients (including the caller). Use instead of a
        /// local animator.Play so other players see the animation.
        /// </summary>
        public static void BroadcastAnimationPlay(Character character, string stateName,
                                                  int layer = 0, float normalizedTime = 0f)
        {
            if (ZRoutedRpc.instance == null || character == null) return;
            var pkg = new ZPackage();
            pkg.Write(character.GetZDOID());
            pkg.Write(stateName);
            pkg.Write(layer);
            pkg.Write(normalizedTime);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcAnimPlay, pkg);
        }

        public static void BroadcastAnimatorSpeed(Player player, float speed)
        {
            if (ZRoutedRpc.instance == null) return;
            var pkg = new ZPackage();
            pkg.Write(player.GetZDOID());
            pkg.Write(speed);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcAnimSpeed, pkg);
        }

        public static void BroadcastMeteor(Vector3 spawnPos, Vector3 targetPos,
                                            float damage, float damageRadius,
                                            ZDOID attackerZdoid)
        {
            if (ZRoutedRpc.instance == null) return;
            var pkg = new ZPackage();
            pkg.Write(spawnPos);
            pkg.Write(targetPos);
            pkg.Write(damage);
            pkg.Write(damageRadius);
            pkg.Write(attackerZdoid);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcMeteor, pkg);
        }

        public static void BroadcastIceFreeze(ZDOID characterZdoid, Vector3 pos,
                                               float freezeDuration, float slowDuration)
        {
            if (ZRoutedRpc.instance == null) return;
            var pkg = new ZPackage();
            pkg.Write(characterZdoid);
            pkg.Write(pos);
            pkg.Write(freezeDuration);
            pkg.Write(slowDuration);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody, RpcIceFreeze, pkg);
        }

        // -----------------------------------------------------------------------
        // RPC handlers
        // -----------------------------------------------------------------------

        private static void OnReceiveVfxSfx(long sender,
                                             string vfxName, string sfxName,
                                             Vector3 pos, Quaternion rot, float scale)
        {
            SpawnVfx(vfxName, pos, rot, scale, suppressAudio: true);
            if (!string.IsNullOrEmpty(sfxName))
                SpawnSfx(sfxName, pos, rot);
        }

        private static void OnReceiveVfx(long sender,
                                          string vfxName,
                                          Vector3 pos, Quaternion rot, float scale)
        {
            SpawnVfx(vfxName, pos, rot, scale, suppressAudio: false);
        }

        private static void OnReceiveRegeneration(long sender, ZPackage pkg)
        {
            ZDOID target = pkg.ReadZDOID();
            float healPercent = pkg.ReadSingle();
            float duration = pkg.ReadSingle();

            var character = FindCharacterByZDOID(target);
            if (character == null) return;

            var nview = character.GetComponent<ZNetView>();
            if (nview == null || !nview.IsOwner()) return;

            HUD.SE_RegenerationBuff.Apply(character, healPercent, duration);
        }

        private static void OnReceiveSanctuary(long sender, ZPackage pkg)
        {
            ZDOID caster = pkg.ReadZDOID();
            Vector3 center = pkg.ReadVector3();
            float radius = pkg.ReadSingle();
            float duration = pkg.ReadSingle();
            float slow = pkg.ReadSingle();

            Skills.SanctuaryDome.Begin(caster, center, radius, duration, slow);
        }

        private static void OnReceiveVfxAttached(long sender, ZPackage pkg)
        {
            var zdoid = pkg.ReadZDOID();
            string vfxName = pkg.ReadString();
            bool suppressAudio = pkg.ReadBool();
            float scale = pkg.ReadSingle();
            string tintHex = pkg.ReadString();
            float heightOffset = pkg.ReadSingle();

            var character = FindCharacterByZDOID(zdoid);
            if (character == null) return;

            var vfx = SpawnVfx(vfxName,
                               character.transform.position + Vector3.up * heightOffset,
                               character.transform.rotation, scale, suppressAudio);
            if (vfx == null) return;

            vfx.transform.SetParent(character.transform, true);

            if (!string.IsNullOrEmpty(tintHex) &&
                ColorUtility.TryParseHtmlString(tintHex, out Color tint))
                TintVfx(vfx, tint);
        }

        /// <summary>
        /// Recolors every supported material color property and particle start
        /// color on a VFX, keeping the original transparency and glow strength.
        /// </summary>
        public static void TintVfx(GameObject go, Color tint)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in r.materials)
                {
                    foreach (var prop in new[] { "_Color", "_TintColor", "_BaseColor" })
                    {
                        if (!mat.HasProperty(prop)) continue;
                        var old = mat.GetColor(prop);
                        mat.SetColor(prop, new Color(tint.r, tint.g, tint.b, old.a));
                    }

                    if (mat.HasProperty("_EmissionColor"))
                    {
                        var e = mat.GetColor("_EmissionColor");
                        float intensity = Mathf.Max(e.r, e.g, e.b);
                        if (intensity > 0f)
                            mat.SetColor("_EmissionColor",
                                new Color(tint.r * intensity, tint.g * intensity, tint.b * intensity, e.a));
                    }
                }
            }

            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var old = main.startColor.color;
                main.startColor = new Color(tint.r, tint.g, tint.b, old.a);
            }
        }

        private static void OnReceiveSfx(long sender,
                                          string sfxName,
                                          Vector3 pos, Quaternion rot)
        {
            SpawnSfx(sfxName, pos, rot);
        }

        // Loaded OGG clips, cached so each file is only read from disk once
        private static readonly System.Collections.Generic.Dictionary<string, AudioClip> _oggCache
            = new System.Collections.Generic.Dictionary<string, AudioClip>();

        private static void OnReceiveOgg(long sender, string filename, Vector3 pos)
        {
            if (_oggCache.TryGetValue(filename, out var cached) && cached != null)
            {
                PlayOggAt(cached, filename, pos);
                return;
            }

            Plugin.instance.StartCoroutine(Plugin.LoadAudioClip(filename, clip =>
            {
                if (clip == null) return;
                _oggCache[filename] = clip;
                PlayOggAt(clip, filename, pos);
            }));
        }

        private static void PlayOggAt(AudioClip clip, string filename, Vector3 pos)
        {
            var go = new GameObject($"KC_Audio_{filename}");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.spatialBlend = 1f;
            src.maxDistance = 40f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.PlayOneShot(clip);
            Object.Destroy(go, clip.length + 0.5f);
        }

        private static void OnReceiveAnimTrigger(long sender, ZPackage pkg)
        {
            var zdoid = pkg.ReadZDOID();
            string trigger = pkg.ReadString();
            var character = FindCharacterByZDOID(zdoid);
            if (character == null) return;
            character.GetComponentInChildren<Animator>()?.SetTrigger(trigger);
        }

        private static void OnReceiveAnimPlay(long sender, ZPackage pkg)
        {
            var zdoid = pkg.ReadZDOID();
            string stateName = pkg.ReadString();
            int layer = pkg.ReadInt();
            float normalizedTime = pkg.ReadSingle();

            var character = FindCharacterByZDOID(zdoid);
            if (character == null) return;

            var animator = character.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.Play(stateName, layer, normalizedTime);
        }

        private static void OnReceiveTracer(long sender, ZPackage pkg)
        {
            string vfxName = pkg.ReadString();
            Vector3 from = pkg.ReadVector3();
            Vector3 to = pkg.ReadVector3();
            float speed = pkg.ReadSingle();

            Plugin.instance.StartCoroutine(TracerRoutine(vfxName, from, to, speed));
        }

        private static IEnumerator TracerRoutine(string vfxName, Vector3 from, Vector3 to, float speed)
        {
            var prefab = ZNetScene.instance?.GetPrefab(vfxName);
            if (prefab == null)
            {
                KC_Log.Warn($"NetworkedEffects: tracer VFX '{vfxName}' not found!");
                yield break;
            }

            Vector3 dir = to - from;
            float distance = dir.magnitude;
            if (distance < 0.01f) yield break;

            var go = SpawnLocal(prefab, from, Quaternion.LookRotation(dir));
            float duration = distance / Mathf.Max(1f, speed);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (go == null) yield break; // removed by its own timer
                go.transform.position = Vector3.Lerp(from, to, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (go != null)
                Object.Destroy(go);
        }

        private static void OnReceiveAnimSpeed(long sender, ZPackage pkg)
        {
            var zdoid = pkg.ReadZDOID();
            float speed = pkg.ReadSingle();
            var character = FindCharacterByZDOID(zdoid);
            if (character == null) return;
            var animator = character.GetComponentInChildren<Animator>();
            if (animator != null)
                animator.speed = speed;
        }

        private static void OnReceiveMeteor(long sender, ZPackage pkg)
        {
            Vector3 spawnPos = pkg.ReadVector3();
            Vector3 targetPos = pkg.ReadVector3();
            float damage = pkg.ReadSingle();
            float damageRadius = pkg.ReadSingle();
            ZDOID attackerZdoid = pkg.ReadZDOID();

            bool applyDamage = (sender == ZNet.GetUID());

            Plugin.instance.StartCoroutine(
                MeteorFallRoutine(spawnPos, targetPos, damage, damageRadius,
                                  attackerZdoid, applyDamage));
        }

        private static void OnReceiveIceFreeze(long sender, ZPackage pkg)
        {
            ZDOID characterZdoid = pkg.ReadZDOID();
            Vector3 pos = pkg.ReadVector3();
            float freezeDuration = pkg.ReadSingle();
            float slowDuration = pkg.ReadSingle();

            Plugin.instance.StartCoroutine(
                IceBlockerRoutine(characterZdoid, pos, freezeDuration));

            // Apply the actual freeze only on the client that controls this
            // enemy — Valheim simulates each creature on one client, which
            // isn't necessarily the caster's.
            var character = FindCharacterByZDOID(characterZdoid);
            var nview = character?.GetComponent<ZNetView>();
            if (character != null && nview != null && nview.IsOwner())
            {
                Plugin.instance.StartCoroutine(
                    Skills.StaffIceShardsSkill.FreezeEnemy(character, freezeDuration, slowDuration));
            }
        }

        // -----------------------------------------------------------------------
        // IceBlocker VFX coroutine — runs on ALL clients
        // -----------------------------------------------------------------------
        private static IEnumerator IceBlockerRoutine(ZDOID characterZdoid,
                                                      Vector3 pos,
                                                      float freezeDuration)
        {
            var iceBlockerPrefab = ZNetScene.instance?.GetPrefab("IceBlocker");
            if (iceBlockerPrefab == null)
            {
                KC_Log.Warn("NetworkedEffects: IceBlocker prefab not found!");
                yield break;
            }

            // Spawn IceBlocker at 70% scale with collision stripped
            var iceBlock = SpawnLocal(iceBlockerPrefab, pos, Quaternion.identity);
            if (iceBlock == null) yield break;

            iceBlock.transform.localScale = Vector3.one * 0.7f;


            // Wait for freeze to expire
            yield return new WaitForSeconds(freezeDuration);

            // Get updated position in case character moved slightly
            var character = FindCharacterByZDOID(characterZdoid);
            Vector3 shatterPos = character != null ? character.transform.position : pos;

            // Destroy IceBlocker via ZNetScene if it has a ZNetView (persists properly)
            // otherwise fall back to local Object.Destroy
            if (iceBlock != null)
                Object.Destroy(iceBlock);

            // Play ice shatter VFX + audio at enemy feet
            var shatterPrefab = ZNetScene.instance?.GetPrefab("fx_DvergerMage_Ice_hit");
            if (shatterPrefab != null)
                SpawnLocal(shatterPrefab, shatterPos, Quaternion.identity);
        }

        // -----------------------------------------------------------------------
        // Meteor fall coroutine — VFX on all clients, damage on sender only
        // -----------------------------------------------------------------------
        private static IEnumerator MeteorFallRoutine(Vector3 spawnPos, Vector3 targetPos,
                                                      float damage, float damageRadius,
                                                      ZDOID attackerZdoid, bool applyDamage)
        {
            var meteorPrefab = ZNetScene.instance?.GetPrefab("projectile_meteor");
            if (meteorPrefab == null)
            {
                KC_Log.Warn("NetworkedEffects: projectile_meteor not found!");
                yield break;
            }

            Vector3 fallDir = (targetPos - spawnPos).normalized;
            float fallDistance = Vector3.Distance(spawnPos, targetPos);
            float fallSpeed = 30f;
            float fallDuration = fallDistance / fallSpeed;

            var meteor = SpawnLocal(meteorPrefab, spawnPos,
                             Quaternion.LookRotation(fallDir));

            float elapsed = 0f;
            while (elapsed < fallDuration)
            {
                if (meteor == null) yield break;
                meteor.transform.position = Vector3.Lerp(spawnPos, targetPos,
                                                          elapsed / fallDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (meteor != null)
                Object.Destroy(meteor);

            var hitPrefab = ZNetScene.instance?.GetPrefab("fx_goblinking_meteor_hit");
            if (hitPrefab != null)
            {
                var hitVfx = SpawnLocal(hitPrefab, targetPos, Quaternion.identity);
                if (hitVfx == null) yield break;
            }

            if (!applyDamage) yield break;

            var attacker = FindCharacterByZDOID(attackerZdoid);

            var cols = Physics.OverlapSphere(targetPos, damageRadius, SkillMasks.Characters);
            var alreadyHit = new System.Collections.Generic.HashSet<Character>();

            foreach (var col in cols)
            {
                var character = col.GetComponentInParent<Character>();
                if (character == null) continue;
                if (!alreadyHit.Add(character)) continue; // one hit per enemy per meteor
                if (character == attacker) continue;
                if (character.IsPlayer() && attacker is Player p && !p.IsPVPEnabled()) continue;
                if (character.m_faction == Character.Faction.Players) continue;

                HitData hit = new HitData();
                hit.m_damage.m_fire = damage;
                hit.m_pushForce = 5f;
                hit.m_staggerMultiplier = 2f;
                hit.m_point = character.transform.position;
                hit.m_dir = Vector3.down;
                hit.m_attacker = attackerZdoid;
                character.Damage(hit);
            }
        }

        // -----------------------------------------------------------------------
        // Internal helpers
        // -----------------------------------------------------------------------

        private static Character? FindCharacterByZDOID(ZDOID id)
        {
            foreach (var character in Character.GetAllCharacters())
            {
                var nview = character.GetComponent<ZNetView>();
                if (nview != null && nview.GetZDO()?.m_uid == id)
                    return character;
            }
            return null;
        }

        private static GameObject? SpawnVfx(string vfxName, Vector3 pos,
                                             Quaternion rot, float scale,
                                             bool suppressAudio = false)
        {
            var prefab = ZNetScene.instance?.GetPrefab(vfxName);
            if (prefab == null)
            {
                KC_Log.Warn($"NetworkedEffects: VFX '{vfxName}' not found!");
                return null;
            }

            var vfx = SpawnLocal(prefab, pos, rot);

            if (scale != 1f)
            {
                foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
                {
                    var shape = ps.shape;
                    shape.radius *= scale;
                    var main = ps.main;
                    main.startSize = new ParticleSystem.MinMaxCurve(
                        main.startSize.constantMin * scale,
                        main.startSize.constantMax * scale);
                }
            }

            if (suppressAudio)
            {
                // Valheim's sound component
                foreach (var zsfx in vfx.GetComponentsInChildren<ZSFX>())
                    Object.Destroy(zsfx);

                // Plain Unity audio sources some prefabs use instead of ZSFX
                foreach (var src in vfx.GetComponentsInChildren<AudioSource>())
                {
                    src.Stop();
                    src.mute = true;
                }
            }

            foreach (var aoe in vfx.GetComponentsInChildren<Aoe>())
                Object.Destroy(aoe);

            return vfx;
        }

        /// <summary>
        /// Instantiates a prefab as a purely LOCAL visual/sound. Networking,
        /// projectile, physics and damage components are removed before the
        /// object wakes up, so it never creates a network copy that other
        /// players would see frozen in place. TimedDestruction is kept, and
        /// falls back to a normal local destroy without a ZNetView.
        /// </summary>
        public static GameObject SpawnLocal(GameObject prefab, Vector3 pos, Quaternion rot)
        {
            bool wasActive = prefab.activeSelf;
            prefab.SetActive(false);
            var go = Object.Instantiate(prefab, pos, rot);
            prefab.SetActive(wasActive);

            // Dependents first, then ZNetView itself
            foreach (var c in go.GetComponentsInChildren<ZSyncTransform>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<ZSyncAnimation>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Projectile>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Aoe>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<CinderSpawner>(true)) Object.DestroyImmediate(c); // spawns real fire, needs ZNetView
            foreach (var c in go.GetComponentsInChildren<ZNetView>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var c in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);

            go.SetActive(true);
            return go;
        }

        private static void SpawnSfx(string sfxName, Vector3 pos, Quaternion rot)
        {
            var prefab = ZNetScene.instance?.GetPrefab(sfxName);
            if (prefab == null)
            {
                KC_Log.Warn($"NetworkedEffects: SFX '{sfxName}' not found!");
                return;
            }
            SpawnLocal(prefab, pos, rot);
        }
    }
}