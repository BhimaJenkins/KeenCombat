# KeenCombat Changelog

---

## [0.1.3] - 2026-04-21

### Changed

- Updated README.md with improved mod page formatting and layout
- Updated manifest.json with GitHub repository link

---

## [0.1.2] - 2026-04-21

### Fixed

- Icons and audio files now load correctly when installed via R2ModMan
  (R2ModMan flattens subfolders — added flat path fallback in Plugin.cs)

---

## [0.1.1] - 2026-04-20

### Added

- **Shield** config option: `ExcludeKnife` — toggle auto shield equip for knife/dual knife weapons (default: off)
- **Keen Combat Skills** entry added to the in-game Valheim compendium
- Stealth buff icon now displays during Knife Assassination stealth phase (`AssassinationBuffIcon.png`)

### Fixed

- Controller hold-to-heavy attack no longer blocked after using mouse hold-to-heavy
  (mouse and controller now use independent input state)
- Primal Rally summons no longer damage player structures
- Frenzy move speed bonus now restores correctly on buff expiry (saved baseline approach)
- Frenzy attack speed now correctly applied via `Attack.Start` animator patch
- Sword Blink Strike animation fixed (`atgeir_secondary` + `fx_eikthyr_forwardshockwave`)
- Whirlwind revised to use `animator.Play` for cleaner double spin

### Changed

- 2H Mace normal attack string overridden to greatsword animation chain on vanilla sledges
- Sledge hit VFX opacity set to 0 during normal attacks (full VFX preserved on heavy attack)
- Axe Frenzy emote speed increased by 50%

---

## [0.1.0] - 2026-04-19

### Added

Initial release of KeenCombat with 11 unique weapon skills:

- **Sword — Blink Strike**: Dash forward in the camera direction, striking all enemies in your path
- **Mace — Bulwark**: Reduce incoming damage and regenerate health for a short duration
- **Axe / Dual Axe — Frenzy**: Roar to boost attack speed and movement speed temporarily
- **Atgeir / Spear — Falcon Blitz**: Rapid six-hit flurry while rooted in place
- **Crossbow — Rapid Fire**: Fire six bolts in quick succession, consuming no ammunition
- **Knife — Assassination**: Enter stealth, break enemy awareness, then strike for massive damage
- **2H Sword / 2H Axe — Whirlwind**: Double spin attack moving forward through enemies
- **Bow — Primal Rally**: Summon a tamed creature to fight by your side based on bow type
- **Dual Knife — Poison Mayhem**: Leap backward and drop poison bombs at your original position
- **Fist — Onslaught**: Four-hit combo with a devastating long-range finisher
- **2H Mace — Earthquake**: Ground slam sending four sequential shockwaves forward

### Added (Features)

- Auto Shield Equip system for one-handed weapons (configurable per weapon type)
- Skill slot HUD widget showing current weapon skill icon and cooldown
- All skills fully configurable via BepInEx Configuration Manager (F1)
- Controller support via right trigger
- Therzie's Warfare compatibility
- Client-side only — no server installation required
