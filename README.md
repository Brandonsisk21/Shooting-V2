# Shooting-V2: Arena Shooter

A Halo 2–style first-person arena shooter in Unity (C#), PC only. The design lives in
[`docs/GDD.md`](docs/GDD.md).

## Current state: Phase 1, slice 1 (gray-box test range)

Press Play and you get a runtime-built test range with:

- **Player:** classic Halo movement (no sprint, 6 m/s, 1.3 m jump, moderate air control), FPS camera.
- **Rifle** (spawn weapon): hitscan, 40 head / 25 body, 5 shots/s, 30-round magazine, auto-reload.
- **Sniper** on a raised center pad: hitscan, 100 head / 50 body, 2x scope (no sway), 4 + 8 rounds,
  spawns at start and every 90 s. Pick up with **E**; two weapon slots; no manual drop.
- **Health:** 100 HP, regenerates 25 HP/s after 5 s without damage. Death → respawn with rifle only.
- **Target dummies** at 10 / 25 / 50 / 80 / 105 m, two strafing dummies and one on the platform.
  They respawn 3 s after dying.
- **HUD:** health bar, ammo, crosshair, hit marker (white body / yellow head / red kill), damage
  numbers, scope overlay, pickup prompt, sniper spawn timer.
- Jump-test blocks (0.5 / 1.0 / 1.25 / 1.6 m) near spawn. Only the 1.6 m one is too tall.

Not in yet: the Midship-style map, bots, player hitboxes (needed once bots shoot back).

## Opening the project

1. Install **Unity 6** (any 6000.x LTS) with Unity Hub.
2. Hub → **Add → Add project from disk** → pick this folder. If Hub asks about the editor
   version, choose the Unity 6 version you have installed.
3. Open any scene (the default empty one is fine) and press **Play**. The test range builds itself.
   Click in the Game view to capture the mouse.

If Play throws `InvalidOperationException` about the Input class, set
**Edit → Project Settings → Player → Active Input Handling** to **Input Manager (Old)** or **Both**.

### Controls

| Key | Action |
|---|---|
| WASD / arrows | Move |
| Space | Jump |
| Mouse | Aim |
| Left mouse | Fire |
| Right mouse | Toggle sniper scope |
| R | Reload |
| E | Pick up weapon / ammo |
| Q, mouse wheel, 1, 2 | Switch weapon |
| K | Hurt yourself 25 HP (debug: test regen) |
| F1 | Toggle help |
| Esc | Release mouse |

### Tuning

Numbers are serialized fields, so you can tweak them live in the Inspector during Play:
`Player → PlayerMotor → Settings` (movement), `Player → Health` (regen),
`Player → WeaponHolder → Spawn Weapon` (rifle), `SniperPad → SniperSpawnPad` (sniper + timer).
Defaults are in `MovementSettings.cs` and `WeaponStats.cs`; update the GDD if you change them for good.

## Code layout

```
Assets/Scripts/Core/       Pure C# game rules (no UnityEngine): health/regen, weapon ammo/fire/reload,
                           2-slot loadout + pickup rules, fixed-interval spawner. Shared by player and bots.
Assets/Scripts/Gameplay/   Unity components: motor, look, weapons/hitscan, pickups, dummies, HUD, bootstrap.
Assets/Tests/EditMode/     NUnit tests for Core (run in Unity's Test Runner or with dotnet, below).
Tools/                     .NET projects for checking code without the Unity editor.
```

## Checks without Unity

```sh
dotnet test  Tools/CoreTests           # game-rule unit tests
dotnet build Tools/UnityCompileCheck   # compiles all scripts against Unity reference assemblies
```
