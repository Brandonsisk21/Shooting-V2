# Arena Shooter — Game Design Document (v0.2 / Phase 1)

> Status: DRAFT. Sections marked **[OPEN QUESTION]** need a decision before implementation begins.
> This document is meant to be dropped into the project repo (e.g. `/docs/GDD.md`) so Claude Code can reference it across sessions.

---

## 1. Concept

A first-person arena shooter inspired by classic Halo 2 arena maps (e.g. Midship): fast, symmetrical, skill-based combat where map control and power-weapon timing matter as much as aim. Cartoony, stylized visual direction (not gritty/realistic).

**Core pillars:**
1. **Readable, symmetrical arenas** — no team has an inherent advantage; power weapon sits at a contested center point.
2. **Simple, mastery-deep weapon sandbox** — starts with exactly two weapons so skill expression comes from movement and positioning, not loadouts.
3. **Cartoony but crunchy** — bright, stylized art, but combat feel (hit feedback, sound, damage numbers) should feel weighty and precise, not silly.

---

## 2. Player & Combat

### 2.1 Health
- **Max Health:** 100 (no separate shield/armor layer for Phase 1)
- **DECIDED:** Health **regenerates** (Halo-style), after a delay with no damage taken.
  - Regen delay: **5.0s** after the last damage taken (any new damage restarts the delay).
  - Regen rate: **25 HP/s** (0 → 100 in 4s once regen starts).
  - Starting values — tune after playtesting.

### 2.2 Weapons (Phase 1 — exactly two weapons)

| Weapon | Spawn | Headshot Dmg | Body Dmg | Fire Mode | Shots to Kill (headshot) | Shots to Kill (body) |
|---|---|---|---|---|---|---|
| Rifle | Player start (everyone spawns with it) | 40 | 25 | Single-shot / semi-auto | 3 (40+40+any) | 4 (25×4) |
| Sniper | Fixed spawn, center of map | 100 (instant kill) | 50 | Single-shot, bolt-action feel | 1 | 2 |

**DECIDED:** Both weapons are **hitscan** (instant hit, no projectile travel time) — matches classic arena shooter feel.

**DECIDED:** Rifle magazine size = **30 rounds**, reload required when empty.

**DECIDED:** Rifle fire rate = **5 shots/second (300 RPM)**, i.e. 0.2s minimum interval between shots.
- Reference point: Halo 2's Battle Rifle fires 3-round bursts with roughly a 0.26–0.3s delay *between bursts* — effectively ~3.3 "trigger pulls" per second, even though each burst itself fires much faster. 300 RPM for a single-shot weapon lands in that same practical cadence.
- At this rate: best-case kill (2 headshots + 1 any shot) = 2 intervals = **0.4s** between first and last hit; worst-case kill (4 body shots) = 3 intervals = **0.6s**. Tight enough to punish misses, not so fast it feels automatic.
- Treat this as a starting value to tune after playtesting — ±1 shot/sec is a reasonable adjustment range once it's actually in-engine and being played.
**DECIDED:** Sniper scope = **single 2x zoom level**, **no sway**. Right mouse toggles the scope. Mouse sensitivity is scaled down while zoomed so on-screen aim speed feels the same.
- Scoping does **not** slow movement (not specified; flag if this should change).
- The rifle has no scope in Phase 1.

**Weapon handling (Phase 1 starting values, tunable):**

| Weapon | Magazine | Reserve ammo | Fire interval | Reload time | Range |
|---|---|---|---|---|---|
| Rifle | 30 | Unlimited (no ammo pickups exist in Phase 1) | 0.2s | 2.0s | 150 m |
| Sniper | 4 | 8 | 0.8s | 2.5s | 500 m |

- Weapon switch time: 0.4s (can't fire while switching).

### 2.3 Sniper Respawn Logic
- **DECIDED:** Sniper spawns at map center on a **fixed 90-second timer**, regardless of pickup state.
- The first sniper spawns at match start. If an unclaimed sniper is still sitting on the pad when the timer fires, it is refreshed (full ammo) rather than duplicated.
- **[OPEN QUESTION]** Should there be an audio/visual "power weapon incoming" callout (Halo does this) to create map-wide tension) — recommended, but not required for Phase 1.

### 2.3.1 Inventory & Pickup
- **DECIDED:** Players carry up to **2 weapons**. Everyone spawns with only the Rifle, so the second slot is free for the Sniper.
- **DECIDED:** Weapons can be **picked up** (press E near the weapon). There is **no manual drop**: a weapon only leaves your hands when you swap it for a different weapon you're picking up (the swapped-out weapon is left on the ground where you stood).
- Picking up a weapon you already carry takes its ammo instead (up to your reserve max).
- Weapons left on the ground despawn after 30s.
- **[OPEN QUESTION]** What happens to a carried Sniper when its holder dies? Phase 1 assumption: it is **lost** (not dropped). Halo drops it on death, which creates a lot of map play — decide before bots land.

### 2.4 Movement
**DECIDED:** Classic Halo-style — **no sprint**, fixed jump height, single jump, strafing is the main mobility skill, moderate air control.

**Movement values (1 Unity unit = 1 meter; starting values, tunable):**

| Parameter | Value | Notes |
|---|---|---|
| Move speed | 6.0 m/s | Same speed in all directions (no backpedal penalty) |
| Ground acceleration | 50 m/s² | ~0.12s to full speed: responsive, still has a hint of weight |
| Ground deceleration | 50 m/s² | |
| Air acceleration | 15 m/s² | 30% of ground: "moderate" air control |
| Max air speed | 6.0 m/s | Strafing mid-air can't exceed ground speed |
| Jump height | 1.3 m | Jump velocity ≈ 7.2 m/s |
| Gravity | 20 m/s² | Higher than real (9.8) for a snappier arc; airtime ≈ 0.72s |
| Player height / radius | 1.8 m / 0.4 m | Eye height 1.6 m |
| Step height / slope limit | 0.4 m / 45° | |
| Field of view | 60° vertical (≈91° horizontal at 16:9) | Sniper 2x zoom ≈ 32° vertical |

---

## 3. Map Design

### 3.1 First Map: "[Working Title]" — Midship-inspired
- Fully **symmetrical** (either point symmetry/180° rotational, like Midship, or mirror symmetry).
- Sniper spawns at the **exact center**, elevated or in a contested chokepoint, visible/reachable from multiple angles so no single spot dominates.
- Multiple verticality layers (upper walkways, lower tunnels) connecting back to center — classic Halo maps use 2–3 elevation tiers.
- Symmetrical spawn points around the perimeter, equal rotation/distance to center power weapon.

**[OPEN QUESTION]** Map scale/player count (see below) — a map for 4 players is much smaller than one for 8–12.

### 3.2 Player Count
**DECIDED:** 4–8 players FFA (design the map with ~6 spawn points as a middle-ground target, expandable toward 8).

---

## 4. Game Modes

- **Phase 1:** Free-for-All (FFA) only — matches the request to start like classic Halo 2 FFA.
- **Later phases (not in initial build):** Team Slayer, Capture the Flag, King of the Hill, etc. — worth designing the map with these in mind (symmetry helps enormously here) even though they're out of scope now.

---

## 5. Art Direction

- **Style:** Cartoony / stylized — think bold outlines or flat-shaded low-poly, saturated color palette, exaggerated proportions on characters/weapons rather than photorealism.
- **Readability first:** even with a cartoony style, enemy silhouettes must read clearly against map backgrounds (a common arena-shooter art trap is a beautiful map that hides enemies).
- **DECIDED:** No existing art or models. **Phase 1 is gray-box/blockout only** — primitive shapes for the map (cubes, ramps, cylinders), capsule or basic humanoid placeholder for the player character. Final cartoony art pass is a later phase, once movement/combat feel good.
- **[OPEN QUESTION]** Reference games/art for the *eventual* art pass — e.g., closer to *Splatoon*, *Team Fortress 2*, *Fortnite*, or *Overwatch*'s semi-stylized realism? Not urgent since Phase 1 is gray-box, but useful to note now so future asset requests have a target.

---

## 6. Audio (placeholder — not blocking Phase 1 code)
- Weapon fire, hit confirmation (headshot vs. body should sound distinct), footsteps, power-weapon spawn callout, death/respawn stingers.

---

## 7. Technical Scope — Phase 1 Definition of Done

**DECIDED:** Phase 1 target is a **fully playable bot match on one gray-boxed map** — no networked multiplayer yet. This de-risks the hardest problem (netcode) until movement/combat feel good.

- [ ] Player controller: classic Halo movement (no sprint), jump, FPS camera
- [ ] Rifle: hitscan fire, 40 headshot / 25 body damage, basic muzzle flash/sound
- [ ] Sniper: hitscan fire, scope-in, 100 headshot (instant kill) / 50 body damage, pickup/drop logic, 90s fixed respawn timer
- [ ] Health system: 100 HP, damage application, death, respawn
- [ ] One gray-boxed symmetrical map (Midship-inspired) sized for 4–8 players, with spawns + center sniper spawn
- [ ] Bots (simple aim-and-shoot AI) to fill out the 4–8 player FFA match
- [ ] Basic HUD: health, ammo, crosshair, hit marker

---

## 8. Engine & Repo

- **DECIDED:** Engine is **Unity (C#)**.
- **DECIDED:** Target platform is **PC only** for Phase 1.

---

## 9. Remaining Open Questions

All core Phase 1 decisions are now locked (see summary below). The only thing left open:

1. Art reference for the *eventual* art pass (not urgent — Phase 1 is gray-box only, this matters once you get to the real art phase).
2. Sniper "power weapon incoming" callout (optional polish, not required for Phase 1).
3. Carried Sniper on death: lost vs. dropped (see 2.3.1).

### All decisions locked in for Phase 1:
- **Engine:** Unity (C#)
- **Platform:** PC only
- Movement: classic Halo, no sprint
- Player count: 4–8 FFA
- Weapons: hitscan
- Rifle: 30-round magazine, 5 shots/sec (300 RPM) fire rate, 40 headshot / 25 body damage
- Sniper: 100 headshot (instant kill) / 50 body damage, fixed 90-second respawn timer, 2x zoom, no sway
- Health: 100 HP, regenerates 25 HP/s after 5s without damage
- Inventory: 2 weapon slots, pick up with E, no manual drop (swap only)
- Movement values: see 2.4
- Phase 1 scope: fully playable bot match, one map, no networking yet
- Art: no existing assets — gray-box/blockout only for Phase 1

---

## Appendix: Notes for Claude Code

- Treat this document as the source of truth for game design decisions. If a design decision isn't specified here, flag it rather than assuming.
- Prefer small, testable vertical slices (e.g., "rifle deals correct damage to a dummy target") over large speculative systems.
- Keep gameplay logic decoupled from placeholder art so the visual style can change later without breaking systems.
