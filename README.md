# Shooting-V2: Arena Shooter

A Halo 2–style first-person arena shooter in Unity (C#), PC only. The design lives in
[`docs/GDD.md`](docs/GDD.md).

## Current state: Phase 1 complete (gray-box), needs playtesting

Press Play and you drop into a **Free-for-All match against 5 bots** on **Overlook** (working
title): an outdoor, Midship-style arena with 180° symmetry ([layout sketch](docs/maps/overlook-layout.svg)),
a center sniper platform, Red (north) and Blue (south) bases with tunnels and overlooks, and side ridges.

- **Bots** play on their own (no set paths): they roam, grab the sniper, hear gunfire, strafe,
  and fight you and each other, with human-like reaction time and aim. First to 25 kills wins.
- **Weapons:** Rifle (spawn weapon, 40 head / 25 body, 5 shots/s, 30 rounds) and Sniper
  (center pad every 90 s, 100 head / 50 body, 2x scope, dropped on death).
- **Health:** 100 HP, regenerates after 5 s without damage.
- **HUD:** health + ammo panel bottom-left, kill feed, score, scoreboard, damage-direction arcs,
  red crosshair over enemies.
- **Controls:** keyboard + mouse or an **Xbox controller**, switchable at any time.
- **F10** swaps to the **test range** (dummies at known distances, jump-test blocks, no bots).

## Opening the project

1. Install **Unity 6** (any 6000.x LTS) with Unity Hub.
2. Hub → **Add → Add project from disk** → pick this folder. If Hub asks about the editor
   version, choose the Unity 6 version you have installed.
3. Unity installs the **Input System** package on first open. If it asks to enable the new input
   backends and restart, click **Yes**.
4. Press **Play**. The map, bots and match build themselves. Click the Game view to capture the mouse,
   or just pick up a controller.

**No input at all?** Edit → Project Settings → Player → Other Settings → **Active Input Handling**
must be **Input System Package (New)** or **Both** (not "Input Manager (Old)").

### Controls

| Action | Keyboard + mouse | Xbox controller |
|---|---|---|
| Move / look | WASD / mouse | Left stick / right stick |
| Jump | Space | A |
| Fire | Left mouse | RT |
| Sniper scope | Right mouse | LT or click right stick |
| Reload | R | X |
| Pick up weapon | E | Hold X |
| Switch weapon | Q, mouse wheel, 1, 2 | Y |
| Scoreboard | Tab (hold) | View (hold) |
| Help | F1 | Menu |
| Switch map | F10 | — |
| Hurt yourself (debug) | K | — |
| Release mouse | Esc | — |

Controller extras: rumble, a turn boost when holding the stick fully sideways, and light aim
assist (look slows while the crosshair is on an enemy). Tune them on `Player → PlayerInputReader`.

### Tuning

Numbers are serialized fields, so you can tweak them live in the Inspector during Play:
`ArenaBootstrap` (bot count, difficulty, score limit — set before pressing Play),
`Bot_* → BotController → Skill` (reaction, aim, vision), `Player → PlayerInputReader` (sensitivity,
deadzones, aim assist, rumble), `Player → PlayerMotor → Settings` (movement), `Player → Health` (regen),
`Player → WeaponHolder → Spawn Weapon` (rifle), `SniperPad → SniperSpawnPad` (sniper + timer).
Defaults are in `MovementSettings.cs` and `WeaponStats.cs`; update the GDD if you change them for good.

## Code layout

```
Assets/Scripts/Core/       Pure C# game rules (no UnityEngine): health/regen, weapon ammo/fire/reload,
                           2-slot loadout + pickup rules, spawn timer + spawn choice, FFA scoring,
                           bot skill/aim, stick response. Shared by player and bots.
Assets/Scripts/Gameplay/   Unity components: motor, look, input (keyboard/mouse + gamepad), weapons/hitscan,
                           combatants, bots (BotController), match (MatchManager), HUD, bootstrap,
                           map builders (OutdoorArenaMap, TestRangeMap) and the outdoor look.
Assets/Tests/EditMode/     NUnit tests for Core (run in Unity's Test Runner or with dotnet, below).
Tools/                     .NET projects for checking code without the Unity editor.
```

## Checks without Unity

```sh
dotnet test  Tools/CoreTests           # game-rule unit tests
dotnet build Tools/UnityCompileCheck   # compiles all scripts against Unity reference assemblies
                                       # (+ Input System stubs mirroring the real package API)
```
