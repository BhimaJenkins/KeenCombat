# Changelog

## 1.0.0 — The Magic Update

Updated for Valheim Deep North!

### Added

- **7 new magic staff skills**, powered by Eitr instead of cooldowns:
  - **Meteor Strike** (Staff of Embers) — a 12-second meteor storm on your position
  - **Ice Nova** (Staff of Frost) — freeze nearby enemies in blocks of ice
  - **Ragnarök** (Staff of Fracturing) — hold to charge, release to fire up to three shots
  - **Regeneration** (Staff of the Wild) — heal yourself, allies, tames and summons over time
  - **Charred Requiem** (Dead Raiser) — raise Charred Archers to fight for you
  - **Thunderstruck** (Thunderblood Staff) — lightning strikes enemies that linger near you
  - **Sanctuary** (Staff of Protection) — a dome that slows enemies and their projectiles
- **Full multiplayer sync** — all skill animations, effects and sounds now show for every player
- **Skill tooltips** — hover any weapon to see its skill, damage, costs and cooldown
- **Magic cost scaling** — staff skills cost more at low magic skill levels, down to their base cost at level 100 (configurable)
- **Eitr bar flash** — the Eitr bar blinks red when you don't have enough to cast, just like vanilla spells
- **Rapid Fire projectiles** — visible bolts matching the bolts you have loaded, with damage landing on impact
- **Stealth is visible to other players** — teammates now see you vanish into the shadows
- **2H mace swing sound** — a custom swing sound for the combo (configurable)

### Changed

- **2H mace rework** — every two-handed mace, including new and modded ones, gets a 3-hit combo for its normal attack, while the heavy attack is the full vanilla sledge slam
- **Frenzy's roar** is now tinted a fiery orange
- **Higher resolution buff icons** (64×64)
- **Faster custom sounds** — each sound now loads once and replays instantly

### Fixed

- Skills missing enemies controlled by other players in multiplayer
- Regeneration not healing other players
- Meteors freezing in the sky on other players' screens
- Ice Nova's ice blocks reappearing after logging out and back in
- Ice Nova and Sanctuary not affecting enemies controlled by other players
- Stealth not hiding you from enemies controlled by other players
- The stealth smoke bomb being able to poison allies
- Stealth getting stuck on after dying or logging out while stealthed
- Leaving stealth resetting your run speed, cancelling other speed effects
- Rapid Fire permanently lowering mouse sensitivity if you died mid-volley
- Rapid Fire sometimes hitting a farther enemy instead of the nearest one
- Right Trigger triggering an attack while a skill was on cooldown (controller)
- Staff skills also firing the staff's normal attack after a normal attack had been used
- Bulwark's health regen not working with other players online
- Frenzy's attack speed not showing for other players, and being triggered by nearby enemies' attacks
- Area skills dealing reduced damage to groups, or hitting some creatures more than once
- Primal Rally summons appearing full size to other players
- Animations getting stuck at the wrong speed after dying mid-skill
- Earthquake leaving you rooted if it was interrupted
- 2H mace normal attacks showing the heavy slam's ground shockwave
- Buff icons missing for players who installed with r2modman or Gale
- An error when returning to the main menu

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