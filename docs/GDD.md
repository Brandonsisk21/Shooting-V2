# Arena Shooter — Game Design Document (v0.1 / Phase 1 Draft)

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
- **[OPEN QUESTION]** Regeneration? (e.g., Halo-style shields regen, but plain HP usually does not regen without pickups). Default assumption: **no regen**, health packs may be a Phase 2 feature.

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
**[OPEN QUESTION]** Sniper scope: zoom levels, sway, does scoping slow movement?

### 2.3 Sniper Respawn Logic
- **DECIDED:** Sniper spawns at map center on a **fixed 90-second timer**, regardless of pickup state.
- **[OPEN QUESTION]** Should there be an audio/visual "power weapon incoming" callout (Halo does this) to create map-wide tension) — recommended, but not required for Phase 1.

### 2.4 Movement
**DECIDED:** Classic Halo-style — **no sprint**, fixed jump height, single jump, strafing is the main mobility skill, moderate air control.

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
2. Scope details Claude Code may still need clarified as it works: sniper scope zoom level/sway, sprint-adjacent movement tuning (air control %, jump height in world units), sniper "power weapon incoming" callout (optional polish, not required for Phase 1).

### All decisions locked in for Phase 1:
- **Engine:** Unity (C#)
- **Platform:** PC only
- Movement: classic Halo, no sprint
- Player count: 4–8 FFA
- Weapons: hitscan
- Rifle: 30-round magazine, 5 shots/sec (300 RPM) fire rate, 40 headshot / 25 body damage
- Sniper: 100 headshot (instant kill) / 50 body damage, fixed 90-second respawn timer
- Phase 1 scope: fully playable bot match, one map, no networking yet
- Art: no existing assets — gray-box/blockout only for Phase 1

---

## Appendix: Notes for Claude Code

- Treat this document as the source of truth for game design decisions. If a design decision isn't specified here, flag it rather than assuming.
- Prefer small, testable vertical slices (e.g., "rifle deals correct damage to a dummy target") over large speculative systems.
- Keep gameplay logic decoupled from placeholder art so the visual style can change later without breaking systems.
