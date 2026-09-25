using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace KeenCombat.Skills
{
    // -----------------------------------------------------------------------
    // SanctuaryDome
    //
    // Runs on EVERY client when a Sanctuary is cast (via NetworkedEffects RPC).
    //
    // Each client:
    //   - Spawns a local-only copy of the Shield Generator's dome mesh
    //   - Slows enemy animations inside the dome (visual, every client)
    //   - Slows movement of enemies IT OWNS (Valheim simulates each creature
    //     on one client — usually not the caster's — so each client handles
    //     the enemies it controls)
    //   - Slows enemy projectiles IT OWNS: velocity × k, gravity × k² so the
    //     projectile keeps the same arc in slow motion, lifetime ÷ k
    //   - Restores everything when targets leave, or when the dome ends
    //
    // Recasting replaces the caster's previous dome.
    // -----------------------------------------------------------------------
    public static class SanctuaryDome
    {
        private class DomeInstance
        {
            public bool Cancelled;
        }

        private class SlowState
        {
            public Animator? Anim;
            public bool MovementApplied;
            public float Speed, Run, Walk, Swim, FlySlow, FlyFast;
        }

        private const float TickRate = 0.1f;

        // One active dome per caster
        private static readonly Dictionary<ZDOID, DomeInstance> _active
            = new Dictionary<ZDOID, DomeInstance>();

        // Private Projectile fields, looked up once
        private static readonly FieldInfo? _projVel = AccessTools.Field(typeof(Projectile), "m_vel");
        private static readonly FieldInfo? _projOwner = AccessTools.Field(typeof(Projectile), "m_owner");

        // -------------------------------------------------------------------
        // Entry point — called by the Sanctuary RPC on every client
        // -------------------------------------------------------------------
        public static void Begin(ZDOID caster, Vector3 center, float radius,
                                 float duration, float slow)
        {
            if (_active.TryGetValue(caster, out var old))
                old.Cancelled = true;

            var inst = new DomeInstance();
            _active[caster] = inst;

            Plugin.instance.StartCoroutine(Run(caster, inst, center, radius, duration, slow));
        }

        private static IEnumerator Run(ZDOID caster, DomeInstance inst, Vector3 center,
                                       float radius, float duration, float slow)
        {
            float k = 1f - Mathf.Clamp(slow, 0f, 0.95f);
            float radiusSq = radius * radius;
            float endTime = Time.time + duration;

            var visual = CreateDomeVisual(center, radius);
            float fadeTime = Mathf.Max(0f, Plugin.StaffShieldDomeFadeTime.Value);

            // Start invisible and ease in — the slow is active immediately
            DomeFade? fade = visual != null ? DomeFade.Capture(visual) : null;
            if (fade != null)
            {
                fade.Apply(0f);
                Plugin.instance.StartCoroutine(FadeRoutine(fade, 0f, 1f, fadeTime));
            }
            var slowed = new Dictionary<Character, SlowState>();
            var slowedPrj = new HashSet<Projectile>();
            var toRemove = new List<Character>();

            while (!inst.Cancelled && Time.time < endTime)
            {
                TickCharacters(center, radiusSq, k, slowed, toRemove);
                TickProjectiles(center, radiusSq, k, slowedPrj);
                yield return new WaitForSeconds(TickRate);
            }

            // Dome over — restore everything
            foreach (var kv in slowed)
                if (kv.Key != null) Restore(kv.Key, kv.Value);

            foreach (var p in slowedPrj)
                if (p != null) ScaleProjectile(p, 1f / k);

            // Ease the bubble out, then remove it
            if (fade != null && visual != null)
                yield return FadeRoutine(fade, 1f, 0f, fadeTime);

            if (visual != null)
                Object.Destroy(visual);

            if (_active.TryGetValue(caster, out var current) && current == inst)
                _active.Remove(caster);
        }

        // -------------------------------------------------------------------
        // Fade — scales every color value on the dome's materials (outer,
        // boosted inner and particles) between invisible and full strength
        // -------------------------------------------------------------------
        private class DomeFade
        {
            private readonly List<(Material mat, string prop, Color original)> _entries
                = new List<(Material, string, Color)>();

            private static readonly string[] ColorProps =
                { "_Color", "_TintColor", "_BaseColor", "_EmissionColor" };

            public static DomeFade Capture(GameObject go)
            {
                var fade = new DomeFade();
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var mat in r.materials)
                    {
                        if (mat == null) continue;
                        foreach (var prop in ColorProps)
                            if (mat.HasProperty(prop))
                                fade._entries.Add((mat, prop, mat.GetColor(prop)));
                    }
                }
                return fade;
            }

            /// <param name="t">0 = invisible, 1 = original strength</param>
            public void Apply(float t)
            {
                foreach (var (mat, prop, o) in _entries)
                {
                    if (mat == null) continue;

                    Color c;
                    if (prop == "_EmissionColor")
                        c = new Color(o.r * t, o.g * t, o.b * t, o.a);          // glow strength
                    else if (prop == "_TintColor")
                        c = new Color(o.r * t, o.g * t, o.b * t, o.a * t);      // additive shaders
                    else
                        c = new Color(o.r, o.g, o.b, o.a * t);                  // transparency

                    mat.SetColor(prop, c);
                }
            }
        }

        private static IEnumerator FadeRoutine(DomeFade fade, float from, float to, float time)
        {
            if (time > 0f)
            {
                float elapsed = 0f;
                while (elapsed < time)
                {
                    // Smooth ease — accelerates in, settles out
                    fade.Apply(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / time)));
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            fade.Apply(to);
        }

        // -------------------------------------------------------------------
        // Characters
        // -------------------------------------------------------------------
        private static void TickCharacters(Vector3 center, float radiusSq, float k,
                                           Dictionary<Character, SlowState> slowed,
                                           List<Character> toRemove)
        {
            // Release enemies that died, despawned or left the dome
            toRemove.Clear();
            foreach (var kv in slowed)
            {
                var c = kv.Key;
                if (c == null || c.IsDead() ||
                    (c.transform.position - center).sqrMagnitude > radiusSq)
                {
                    if (c != null) Restore(c, kv.Value);
                    toRemove.Add(c!);
                }
            }
            foreach (var c in toRemove)
                slowed.Remove(c);

            // Slow enemies inside the dome
            foreach (var c in Character.GetAllCharacters())
            {
                if (c == null || c.IsDead() || !IsHostileCreature(c)) continue;
                if ((c.transform.position - center).sqrMagnitude > radiusSq) continue;

                if (!slowed.TryGetValue(c, out var st))
                {
                    st = new SlowState { Anim = c.GetComponentInChildren<Animator>() };
                    slowed[c] = st;
                }

                // Animation slow — every client, reapplied each tick in case
                // the game resets the animator speed
                if (st.Anim != null)
                    st.Anim.speed = k;

                // Movement slow — only on the client that controls this enemy.
                // Ownership can move between players, so handle both directions.
                var nview = c.GetComponent<ZNetView>();
                bool owner = nview != null && nview.IsOwner();

                if (owner && !st.MovementApplied)
                    ApplyMovement(c, st, k);
                else if (!owner && st.MovementApplied)
                    RestoreMovement(c, st);
            }
        }

        private static void ApplyMovement(Character c, SlowState st, float k)
        {
            st.Speed = c.m_speed;
            st.Run = c.m_runSpeed;
            st.Walk = c.m_walkSpeed;
            st.Swim = c.m_swimSpeed;
            st.FlySlow = c.m_flySlowSpeed;
            st.FlyFast = c.m_flyFastSpeed;

            c.m_speed *= k;
            c.m_runSpeed *= k;
            c.m_walkSpeed *= k;
            c.m_swimSpeed *= k;
            c.m_flySlowSpeed *= k;
            c.m_flyFastSpeed *= k;

            st.MovementApplied = true;
        }

        private static void RestoreMovement(Character c, SlowState st)
        {
            c.m_speed = st.Speed;
            c.m_runSpeed = st.Run;
            c.m_walkSpeed = st.Walk;
            c.m_swimSpeed = st.Swim;
            c.m_flySlowSpeed = st.FlySlow;
            c.m_flyFastSpeed = st.FlyFast;

            st.MovementApplied = false;
        }

        private static void Restore(Character c, SlowState st)
        {
            if (st.Anim != null) st.Anim.speed = 1f;
            if (st.MovementApplied) RestoreMovement(c, st);
        }

        // Hostile creature rules — never slows players, tames, summons
        // or passive wildlife
        private static bool IsHostileCreature(Character c)
        {
            if (c.IsPlayer()) return false;
            if (c.IsTamed()) return false;
            if (c.m_faction == Character.Faction.Players) return false;
            if (c.GetComponent<AnimalAI>() != null) return false;

            var zdo = c.GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null) return false;
            if (zdo.GetBool("KeenCombat_PrimalRally")) return false;

            return true;
        }

        // -------------------------------------------------------------------
        // Projectiles
        // -------------------------------------------------------------------
        private static void TickProjectiles(Vector3 center, float radiusSq, float k,
                                            HashSet<Projectile> slowedPrj)
        {
            if (_projVel == null) return;

            slowedPrj.RemoveWhere(p => p == null);

            foreach (var p in Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None))
            {
                if (p == null) continue;

                // Only the client that owns the projectile moves it
                var nview = p.GetComponent<ZNetView>();
                if (nview == null || !nview.IsOwner()) continue;

                bool inside = (p.transform.position - center).sqrMagnitude <= radiusSq;
                bool isSlowed = slowedPrj.Contains(p);

                if (inside && !isSlowed && IsEnemyProjectile(p))
                {
                    ScaleProjectile(p, k);
                    slowedPrj.Add(p);
                }
                else if (!inside && isSlowed)
                {
                    ScaleProjectile(p, 1f / k);
                    slowedPrj.Remove(p);
                }
            }
        }

        private static bool IsEnemyProjectile(Projectile p)
        {
            var owner = _projOwner?.GetValue(p) as Character;
            return owner != null && IsHostileCreature(owner);
        }

        // Scales projectile "time": same arc, different speed
        private static void ScaleProjectile(Projectile p, float f)
        {
            if (_projVel == null) return;

            var vel = (Vector3)_projVel.GetValue(p);
            _projVel.SetValue(p, vel * f);

            p.m_gravity *= f * f;
            p.m_ttl /= f;
        }

        // -------------------------------------------------------------------
        // Dome visual — local copy of the Staff of Protection bubble,
        // scaled, positioned against the ground and recolored
        // -------------------------------------------------------------------
        private static GameObject? CreateDomeVisual(Vector3 center, float radius)
        {
            string vfxName = Plugin.StaffShieldDomeVfx.Value;
            var prefab = ZNetScene.instance?.GetPrefab(vfxName);
            if (prefab == null)
            {
                KC_Log.Warn($"Sanctuary: dome VFX '{vfxName}' not found!");
                return null;
            }

            // Copy while inactive so its network/destruction scripts never start
            bool wasActive = prefab.activeSelf;
            prefab.SetActive(false);
            var copy = Object.Instantiate(prefab, center, Quaternion.identity);
            prefab.SetActive(wasActive);

            copy.name = "KC_SanctuaryDome";

            // Remove networking, sound, auto-destroy and collision — keep the visuals
            foreach (var c in copy.GetComponentsInChildren<ZSyncTransform>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<ZNetView>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<TimedDestruction>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<ZSFX>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<Aoe>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var c in copy.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(c);

            // Particles follow the object's scale
            foreach (var ps in copy.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            copy.SetActive(true);

            // Measure the bubble's current world size from its mesh renderers
            if (TryGetMeshBounds(copy, out Bounds bounds))
            {
                float currentRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                if (currentRadius > 0.01f)
                    copy.transform.localScale *= radius / currentRadius;

                // Re-measure after scaling and place the bubble's center
                // relative to the ground: 0 = center at ground (half dome)
                if (TryGetMeshBounds(copy, out Bounds scaled))
                {
                    float targetCenterY = center.y + radius * Plugin.StaffShieldDomeHeightOffset.Value;
                    copy.transform.position += Vector3.up * (targetCenterY - scaled.center.y);
                }
            }
            else
            {
                KC_Log.Warn("Sanctuary: dome VFX has no mesh to measure — using unscaled size.");
            }

            ApplyColor(copy);
            MakeDoubleSided(copy);
            return copy;
        }

        // -------------------------------------------------------------------
        // Double-sided rendering — the bubble's shader only draws its outer
        // face, so from inside a 10m dome it's invisible. For each part we add
        // an inward-facing sphere fitted to the part's mesh bounds, using
        // boosted copies of its materials so the inside can be tuned
        // separately from the outside (DomeInsideBoost).
        // -------------------------------------------------------------------
        private static Mesh? _innerSphere;

        private static void MakeDoubleSided(GameObject go)
        {
            float boost = Mathf.Max(0f, Plugin.StaffShieldDomeInsideBoost.Value);

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null) continue;

                // Boosted copies of the (already recolored) materials
                var src = mr.materials;
                var mats = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    mats[i] = new Material(src[i]);
                    BoostMaterial(mats[i], boost);
                }

                // Inner sphere, parented to the part so it inherits scale/position
                var inner = new GameObject("KC_SanctuaryDomeInner");
                inner.transform.SetParent(mf.transform, false);
                inner.transform.localPosition = mf.sharedMesh.bounds.center;
                inner.transform.localRotation = Quaternion.identity;
                inner.transform.localScale = mf.sharedMesh.bounds.size;

                inner.AddComponent<MeshFilter>().sharedMesh = GetInnerSphere();
                var innerRenderer = inner.AddComponent<MeshRenderer>();
                innerRenderer.sharedMaterials = mats;
                innerRenderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // Multiplies opacity on color properties and strength on emission
        private static void BoostMaterial(Material mat, float boost)
        {
            foreach (var prop in new[] { "_Color", "_TintColor", "_BaseColor" })
            {
                if (!mat.HasProperty(prop)) continue;
                var c = mat.GetColor(prop);
                c.a = Mathf.Clamp01(c.a * boost);
                mat.SetColor(prop, c);
            }

            if (mat.HasProperty("_EmissionColor"))
            {
                var e = mat.GetColor("_EmissionColor");
                mat.SetColor("_EmissionColor", new Color(e.r * boost, e.g * boost, e.b * boost, e.a));
            }
        }

        // Unit-diameter sphere with every triangle facing inward
        private static Mesh GetInnerSphere()
        {
            if (_innerSphere != null) return _innerSphere;

            const int lat = 24, lon = 48;
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i <= lat; i++)
            {
                float theta = Mathf.PI * i / lat;
                for (int j = 0; j <= lon; j++)
                {
                    float phi = 2f * Mathf.PI * j / lon;
                    var p = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi),
                                        Mathf.Cos(theta),
                                        Mathf.Sin(theta) * Mathf.Sin(phi)) * 0.5f;
                    verts.Add(p);
                    normals.Add(-p.normalized);
                    uvs.Add(new Vector2((float)j / lon, 1f - (float)i / lat));
                }
            }

            for (int i = 0; i < lat; i++)
            {
                for (int j = 0; j < lon; j++)
                {
                    int a = i * (lon + 1) + j;
                    int b = a + lon + 1;
                    AddInwardTri(verts, tris, a, b, a + 1);
                    AddInwardTri(verts, tris, a + 1, b, b + 1);
                }
            }

            _innerSphere = new Mesh { name = "KC_InnerSphere" };
            _innerSphere.SetVertices(verts);
            _innerSphere.SetNormals(normals);
            _innerSphere.SetUVs(0, uvs);
            _innerSphere.SetTriangles(tris, 0);
            _innerSphere.RecalculateBounds();
            return _innerSphere;
        }

        // Adds a triangle, flipping its winding if needed so it faces the center
        private static void AddInwardTri(List<Vector3> v, List<int> tris, int a, int b, int c)
        {
            Vector3 faceNormal = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
            Vector3 centroid = (v[a] + v[b] + v[c]) / 3f;

            // Skip degenerate triangles at the poles
            if (faceNormal.sqrMagnitude < 1e-12f) return;

            // Inward = normal points toward the center (opposite the centroid)
            if (Vector3.Dot(faceNormal, centroid) > 0f)
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
            }
            else
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
            }
        }

        private static bool TryGetMeshBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }

        // Tints every supported color property, keeping original transparency
        private static void ApplyColor(GameObject go)
        {
            if (!ColorUtility.TryParseHtmlString(Plugin.StaffShieldDomeColor.Value, out Color tint))
                return;

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                foreach (var mat in r.materials)
                {
                    TintProperty(mat, "_Color", tint, false);
                    TintProperty(mat, "_TintColor", tint, false);
                    TintProperty(mat, "_BaseColor", tint, false);
                    TintProperty(mat, "_EmissionColor", tint, true);
                }
            }

            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                var old = main.startColor.color;
                main.startColor = new Color(tint.r, tint.g, tint.b, old.a);
            }
        }

        private static void TintProperty(Material mat, string prop, Color tint, bool isEmission)
        {
            if (!mat.HasProperty(prop)) return;
            var old = mat.GetColor(prop);

            if (isEmission)
            {
                // Keep the original glow strength
                float intensity = Mathf.Max(old.r, old.g, old.b);
                if (intensity <= 0f) return;
                mat.SetColor(prop, new Color(tint.r * intensity, tint.g * intensity, tint.b * intensity, old.a));
            }
            else
            {
                mat.SetColor(prop, new Color(tint.r, tint.g, tint.b, old.a));
            }
        }
    }
}