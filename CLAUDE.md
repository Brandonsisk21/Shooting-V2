# Shooting-V2 — Arena Shooter

First-person, Halo 2–style arena shooter (FFA vs. bots in Phase 1). Unity (C#), PC only.

## Source of truth
- **`docs/GDD.md`** is the game design document. Read it before implementing gameplay.
- If a design value isn't specified there, flag it rather than guessing. Items marked **[OPEN QUESTION]** are not decided.

## Working conventions
- Prefer small, testable vertical slices (e.g. "rifle deals correct damage to a dummy target").
- Keep gameplay logic decoupled from placeholder art — Phase 1 is gray-box only.
- Keep tunable numbers (damage, fire rate, magazine size, respawn timers, movement) in data (ScriptableObjects/serialized fields), not hard-coded.

## Layout
- `Assets/Scripts/Core/`: engine-independent rules (no `UnityEngine`; asmdef has `noEngineReferences`). Put testable logic here.
- `Assets/Scripts/Gameplay/`: Unity components. `ArenaBootstrap` builds the gray-box test range at runtime on Play.
- `Assets/Tests/EditMode/`: NUnit tests for Core.

## Checks (no Unity editor in cloud sessions)
- `dotnet test Tools/CoreTests`: runs the Core unit tests.
- `dotnet build Tools/UnityCompileCheck`: compiles all scripts against Unity reference assemblies (2021.3 API surface). Stick to APIs that exist there and in Unity 6.
- Use C# 9 (Unity's language version) and the legacy Input Manager (`Input.*`).
