# Space Grunts — Game Design Document (v0.6)

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
- **DECIDED:** On death, a carried Sniper is **dropped where its holder died**, keeping its remaining ammo, so anyone can grab it. The Rifle isn't dropped (everyone respawns with one), and a Sniper with no ammo left is discarded.
- Weapons left on the ground (dropped by death or by a swap) despawn after 30s.
- Note: the center pad keeps its fixed 90s timer, so a dropped Sniper and a fresh pad Sniper can briefly both exist. This follows "regardless of pickup state" in 2.3; flag if it should change.

### 2.3.2 Controls
- **DECIDED:** Keyboard + mouse **and Xbox controller**, usable interchangeably (on-screen prompts follow the last device used). Built on Unity's Input System package.
- **Controller (Halo-style):** LS move, RS look, A jump, RT fire, LT or RS-click scope, X reload (**hold X** to pick up a weapon), Y switch weapon, View (hold) scoreboard, Menu help. Rumble on firing and taking damage.
- Stick look: 15% deadzone, squared response curve, 200°/s yaw and 130°/s pitch at full tilt, with a Halo-style turn boost (up to 1.5x) after holding full sideways. Light **aim assist friction** (look slows to 55% while the crosshair is over an enemy) on controller only.
- **DECIDED:** friction-only aim assist is a good start; revisit magnetism after more playtesting. Aim assist can be turned off in Settings.

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

### 3.1 First Map: "Crash Site" — Midship-inspired, outdoor
- Fully **symmetrical** (either point symmetry/180° rotational, like Midship, or mirror symmetry).
- Sniper spawns at the **exact center**, elevated or in a contested chokepoint, visible/reachable from multiple angles so no single spot dominates.
- Multiple verticality layers (upper walkways, lower tunnels) connecting back to center — classic Halo maps use 2–3 elevation tiers.
- Symmetrical spawn points around the perimeter, equal rotation/distance to center power weapon.

**Gray-box layout (built in `OutdoorArenaMap.cs`):**
- **Theme:** outdoor arena (see 5). An open grassy valley ringed by cliffs, with stone structures, boulders and trees for cover.
- **Size:** 44 m × 64 m playable, sized for 4–8 players FFA. Tall invisible walls at the cliff line.
- **Symmetry:** 180° rotational (point symmetry, like Midship). Every piece is placed through a builder that adds its rotated twin, so the halves can't drift apart.
- **Callouts:** North = **Red base**, South = **Blue base** (colored railings/overlook), useful now for callouts and later for team modes.
- **Center (tier 1, 3 m):** 10 × 10 m open platform with the Sniper pad. Reachable four ways: two ramps (NE and SW) plus boulder "steps" on the east and west sides that you jump up (1.2 m → 2.2 m → 3 m).
- **Bases (tier 1, 3.5 m):** 18 × 8 m deck on pillars at each end, with a covered **tunnel** underneath (tier 0), side landings with ramps down to the field, a back wall, and front railings with a drop-down gap.
- **Overlooks (tier 2, 6.5 m):** small platform above each base, reached by a ramp from the deck. Long sightline to the center, but exposed.
- **Side ridges (tier 1, 2 m):** raised rock ledges along both long sides with partial cover walls, used as flanking routes.
- **Ground cover:** trees, boulders and a low wall break up center-field sightlines.
- **Spawns:** 8 (4 per half: base deck, base landing, two corners), all facing the center. FFA picks the spawn farthest from the nearest living enemy (random when there are none).
- **Name (DECIDED):** "Crash Site" (was working title "Crash Site"). The tier-2 platforms above each base are still called "overlooks".

### 3.2 Player Count
**DECIDED:** 4–8 players FFA (design the map with ~6 spawn points as a middle-ground target, expandable toward 8).

---

## 4. Game Modes

- **Phase 1:** Free-for-All (FFA) only — matches the request to start like classic Halo 2 FFA.

### 4.1 FFA rules (Phase 1 placeholders, all tunable on `ArenaBootstrap` / `MatchManager`)
- +1 per kill, **-1 per suicide** (Halo rule).
- **DECIDED:** match ends at **25 kills or 10 minutes**, whichever comes first. On time-out the leader wins; a shared top score is a **draw**. Results show for 10s, then a new match starts (scores reset, everyone respawns, sniper pad resets).
- Both limits are adjustable on the match setup screen (score 10/15/25/50, time 5/10/15/20 min or none).
- Respawn 3s after death at the spawn farthest from the nearest living enemy.

### 4.2 Bots
- **DECIDED:** Bots act on their own with **no set paths**. They roam to random reachable spots on a navigation mesh baked from the map at runtime, go for the sniper and dropped weapons, react to enemies they see, hear (gunfire within 35 m) or get shot by, and fight everyone (FFA).
- They use the **same movement, health and weapons as the player**, and are held to human-like limits: a vision cone (140°, 70 m), reaction delay, capped turn speed, and aim error that shrinks while tracking a target. In fights they strafe, jump occasionally, keep a preferred range per weapon (rifle 8–22 m, sniper 15–50 m) and swap to the sniper at range.
- Arena match: **5 bots + you = 6 players** (inside the 4–8 target). Difficulty presets Easy / **Normal** (default) / Hard.
- **DECIDED:** keep **Normal** as the default for now. Bot count and difficulty are selectable in the menu.
- **Later phases (not in initial build):** Team Slayer, Capture the Flag, King of the Hill, etc. — worth designing the map with these in mind (symmetry helps enormously here) even though they're out of scope now.

---

## 5. Art Direction

- **DECIDED:** **Outdoor arena theme.** Bright daytime sky, warm sun, light distance fog, grassy ground, stone structures, cliffs and trees. The gray-box pass already uses this palette (`OutdoorPalette.cs`) and lighting (`OutdoorEnvironment.cs`).
- **Style:** Cartoony / stylized — think bold outlines or flat-shaded low-poly, saturated color palette, exaggerated proportions on characters/weapons rather than photorealism.
- **Readability first:** even with a cartoony style, enemy silhouettes must read clearly against map backgrounds (a common arena-shooter art trap is a beautiful map that hides enemies).
- **DECIDED:** No existing art or models. **Phase 1 is gray-box/blockout only** — primitive shapes for the map (cubes, ramps, cylinders), capsule or basic humanoid placeholder for the player character. Final cartoony art pass is a later phase, once movement/combat feel good.
- **HUD (DECIDED):** health and ammo live together in one panel at the **bottom-left**: weapon name + other slot, big magazine count / reserve, a pip per round, and a 10-segment health bar that shimmers while regenerating. Also: crosshair turns red over enemies, hit markers (white body / yellow head / red kill), damage numbers, red arcs pointing at whoever is shooting you, low-health red vignette, kill feed (top-right), score widget (bottom-right), scoreboard (Tab / View), death card and match results.
- **[OPEN QUESTION]** Reference games/art for the *eventual* art pass — e.g., closer to *Splatoon*, *Team Fortress 2*, *Fortnite*, or *Overwatch*'s semi-stylized realism? Not urgent since Phase 1 is gray-box, but useful to note now so future asset requests have a target.

---

### 5.1 Theme & final art style (DECIDED: "Space Grunts")
- **DECIDED:** final art is a **goofy cartoon style**, for characters **and** weapons.
- **DECIDED theme and title: "Space Grunts"**: goofy cartoon space marines. Squat, bobble-headed troopers in oversized fishbowl helmets and chunky armor who crash-landed on a weird little alien planet and settle every argument with toy-like blasters.
  - **Why it fits:** combines both ideas (space + goofy marines); keeps the outdoor arena (it becomes an alien planet surface); gives Red/Blue bases an identity (two crashed dropships), which suits later team modes.
  - **World:** candy-colored alien grass, bulb and mushroom trees instead of oaks, floating rocks at the map edge, a huge ringed planet in the sky. Map "Crash Site" becomes **"Crash Site"**.
  - **Characters:** bobble heads with round visors, stubby legs, a big backpack; each player a bright color. Silhouettes stay chunky and readable (GDD pillar 3).
  - **Weapons:** Rifle becomes the **"Pew Rifle"**, a chunky blaster with a little antenna firing glowing bolts (still hitscan). Sniper becomes the **"Long Zapper"**, an absurdly long barrel with a satellite-dish scope.
  - **Humor, without breaking "crunchy" combat feel:** kills pop the helmet off in a confetti poof, a goofy announcer, "pew" sound design layered over punchy impacts.
  - **Style references:** Ratchet & Clank's creatures, Fortnite's bright shapes, Splatoon's readability.
- Alternatives considered: *backyard toy soldiers* (tiny plastic army men in a giant backyard; strong scale gag, but a crowded theme) and *food-fight arena* (fun, but weapons get harder to read).
- **First art pass (done, still built from primitives in code, no model assets yet):** alien palette and lavender sky with a ringed planet, moons and floating rocks; mushroom trees; crashed dropships behind each base with team stripes, portholes and glowing engines; landing-pad ring and beacons around the Long Zapper; bobble-head grunts in fishbowl helmets (helmet pops off + confetti on death); toy-blaster weapon models; glowing bolts/beam; "pew" sound effects; goofy bot names (Pvt. Pickles, Sgt. Noodle...).
- **Gameplay is unchanged by art:** hitboxes, collision and the map layout are exactly as before; decorations have no collision (or sit outside the play space).
- **3D models (done):** custom Space Grunts models generated by `Tools/ModelGen` (Python): the grunt (with animated legs, aim-following arms and a bobbling head; helmet pops off as a real object), the Pew Rifle and Long Zapper, giant mushrooms, crashed dropships in team colors, lumpy rocks for boulders/cliffs/hills, and supply crates. Previews: `docs/art/grunts-lineup.png`, `docs/art/models-sheet.png`. Collision is still the gray-box shapes underneath.
- Later art steps: textures/decals, skeletal animation (death, reload), more props.

## 6. Audio (placeholder — not blocking Phase 1 code)
- Weapon fire, hit confirmation (headshot vs. body should sound distinct), footsteps, power-weapon spawn callout, death/respawn stingers.

---

## 6.5 Menus (DECIDED)
- On launch the game opens to a **main menu** over a live bots-only match with a slow camera orbit.
- **Main:** Play vs Bots, Multiplayer, Settings, Controls, Quit.
- **Play vs Bots (match setup):** map, bot count (0–7), bot difficulty, score limit, time limit; remembered between sessions.
- **Multiplayer:** Host Game / Join Game, shown as "coming soon" until networking lands (see 9.1).
- **Settings (saved):** mouse sensitivity, controller look speed, invert Y, aim assist, vibration, field of view, volume, controls hint.
- **Pause (Esc / Menu button):** Resume, Settings, Controls, Quit to Main Menu. Pausing freezes the match (single-player).
- Fully usable with mouse, keyboard or controller.

## 7. Technical Scope — Phase 1 Definition of Done

**DECIDED:** Phase 1 target is a **fully playable bot match on one gray-boxed map** — no networked multiplayer yet. This de-risks the hardest problem (netcode) until movement/combat feel good.

Status: everything below is implemented; the owner has played the first slices, and the bot/controller/HUD update still needs a playtest.

- [x] Player controller: classic Halo movement (no sprint), jump, FPS camera
- [x] Rifle: hitscan fire, 40 headshot / 25 body damage, basic muzzle flash/sound
- [x] Sniper: hitscan fire, scope-in, 100 headshot (instant kill) / 50 body damage, pickup/drop logic, 90s fixed respawn timer
- [x] Health system: 100 HP, damage application, death, respawn
- [x] One gray-boxed symmetrical map (Midship-inspired) sized for 4–8 players, with spawns + center sniper spawn
- [x] Bots to fill out the 4–8 player FFA match (autonomous, see 4.2)
- [x] HUD: health, ammo, crosshair, hit marker (plus the extras in 5)
- [x] Xbox controller support (see 2.3.2)

---

## 8. Engine & Repo

- **DECIDED:** Engine is **Unity (C#)**.
- **DECIDED:** Target platform is **PC only** for Phase 1.

---

## 9.1 Online multiplayer (Phase 2, first version DONE: Steam invites)
Goal: host a match from the menu, send a friend a short **join code**, play together (plus bots) over the internet with no port forwarding.

- **Recommended stack:** Unity **Netcode for GameObjects** (host/client: the host's game runs the match, the bots and the rules) + Unity Gaming Services **Relay** (connects players through Unity's servers using join codes) + **Lobby** (optional: browse or quick-join). Free tier is fine for playing with friends. Needs a free Unity Cloud project linked to this Unity project (the owner has to do that step).
- **DECIDED:** go with Unity's stack (owner approved).
- **Steam without listing the game** is also possible and was raised by the owner:
  - *Test app ID 480 ("Spacewar")*: Valve's public sample app. Any unlisted game can use it with Steamworks to get Steam friends invites, lobbies and Steam's relay for free. Common for private play-tests; the downsides are that it shows as "Spacewar" in Steam, lobbies are shared with everyone else using 480 (must be filtered), and it's not meant for a real release.
  - *Own app ID, unreleased*: after the one-time Steam Direct fee, the game can stay unlisted and friends get it through Steam keys (auto-updates via Steam).
  - *"Add a Non-Steam Game"* only puts an .exe in the Steam library; it doesn't provide networking by itself.
- **Design choice:** gameplay networking (Netcode for GameObjects) is the same either way; only the *transport* differs (Unity Transport + Relay vs. a Steam transport). Build on NGO so either can be used.
- **Work involved:** sync player movement (with client-side prediction so it feels responsive), host-authoritative hits/damage/health, weapons, pickups, the sniper pad, score and kill feed; bots run on the host; menu flow for Host / Join-with-code / lobby.
- Both players must run the **same build**.
- **DECIDED:** Steam invites first, using test app ID 480 (owner's choice). Unity Relay join codes can be added later as a second transport.
- **How it works (implemented, custom lightweight netcode rather than NGO, because the whole game is built at runtime without prefabs):**
  - **Lobby:** friends-only Steam lobby (max 8), tagged `game=spacegrunts` + protocol version (app 480 is shared by many games). Invite via the Steam overlay or an in-game friend list; accepting a Steam invite (or `+connect_lobby`) joins automatically; "Join a Friend" lists friends currently hosting.
  - **Transport:** Steam Networking Sockets P2P through Valve's relays (no port forwarding); reliable for events, unreliable for snapshots/movement.
  - **Authority:** the **host** runs the real match (bots, health, damage, pickups, sniper pad, score, respawns) exactly as offline and streams **snapshots at 20 Hz**. Each **client** moves its own grunt locally (sent at 30 Hz), hit-scans locally and **reports** shots ("favor the shooter"; the host checks the weapon, fire rate, range and that both are alive), and **requests** pickups (the host decides). Other grunts are shown **interpolated 100 ms behind** for smooth motion.
  - Joining mid-match works; each joining player replaces a bot. Host leaving ends the game for everyone (clients return to the menu with a message).
- **Known limits (first version):** friends-only trust model (no cheat protection beyond sanity checks); no host migration; no lag compensation for bots' shots at clients; online matches can't be paused.
- **Needs a real two-player playtest** (can't be tested in the cloud dev environment).

## 9. Remaining Open Questions

All core Phase 1 decisions are now locked (see summary below). The only thing left open:

1. Art reference for the *eventual* art pass (not urgent — Phase 1 is gray-box only, this matters once you get to the real art phase).
2. Sniper "power weapon incoming" callout (optional polish, not required for Phase 1).
3. After the first online playtest: netcode feel (interpolation delay, hit registration), and whether to add Unity Relay join codes too.

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
- Sniper drops on death (keeps its ammo)
- Map theme: outdoor arena; first map is a 180°-symmetric Midship-style layout (see 3.1)
- Phase 1 scope: fully playable bot match, one map, no networking yet
- Art: no existing assets — gray-box/blockout only for Phase 1

---

## Appendix: Notes for Claude Code

- Treat this document as the source of truth for game design decisions. If a design decision isn't specified here, flag it rather than assuming.
- Prefer small, testable vertical slices (e.g., "rifle deals correct damage to a dummy target") over large speculative systems.
- Keep gameplay logic decoupled from placeholder art so the visual style can change later without breaking systems.
