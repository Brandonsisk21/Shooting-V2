# Shooting-V2 — Arena Shooter

First-person, Halo 2–style arena shooter (FFA vs. bots in Phase 1). Unity (C#), PC only.

## Source of truth
- **`docs/GDD.md`** is the game design document. Read it before implementing gameplay.
- If a design value isn't specified there, flag it rather than guessing. Items marked **[OPEN QUESTION]** are not decided.

## Working conventions
- Prefer small, testable vertical slices (e.g. "rifle deals correct damage to a dummy target").
- Keep gameplay logic decoupled from placeholder art — Phase 1 is gray-box only.
- Keep tunable numbers (damage, fire rate, magazine size, respawn timers, movement) in data (ScriptableObjects/serialized fields), not hard-coded.
