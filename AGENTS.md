# Last Machinist — Codex Development Rules

## 1. Authority
This repository is a Unity project for **Last Machinist**.

For MVP development, use the following authority order:
1. `AGENTS.md` — implementation behavior and repository rules.
2. `docs/MVP_SPEC.md` — MVP scope and acceptance boundary.
3. `docs/ARCHITECTURE.md` — architecture and data ownership.
4. Domain specifications such as `LATHE_SPEC.md`, `MEASUREMENT_SPEC.md`, and `QUEST_01_SPEC.md`.
5. `docs/IMPLEMENTATION_PLAN.md` — implementation sequence.
6. `docs/source/Last_Machinist_MVP_Development_Spec_v0.2.txt` — preserved source specification.

If two refined documents conflict, STOP and report the conflict instead of silently choosing an interpretation.

## 2. MVP Goal
The MVP ends when QUEST 01, **멈춰버린 펌프**, can be played from start to finish and the player's manufactured shaft is installed so the pump visibly works again.

Core experience:
Observation → Measurement → Judgment → Machining → Re-measurement → Visible world change.

## 3. General Rules
- Read existing relevant files before modifying code.
- Do not delete or overwrite existing Unity assets/settings without a concrete need.
- Do not break existing public APIs unnecessarily.
- Keep Game State separate from UI.
- Keep Workpiece calculation data separate from rendered Mesh data.
- Do not hardcode Quest progression inside NPC `MonoBehaviour` components.
- Separate static definitions from runtime state.
- Use `ScriptableObject` for static definition data where appropriate.
- Prefer serializable pure C# runtime data for saveable state.
- Never store direct Unity Scene Object references in save DTOs.
- Keep testable domain logic outside `MonoBehaviour` where practical.
- Do not scatter string IDs through code; centralize definitions/data.
- Do not build a giant generic framework for hypothetical future features.
- Do not hardcode Workpiece, Quest, Inventory, or Save systems exclusively to Turning when a small neutral abstraction is sufficient.
- Do not implement later-game systems merely because the Master design may eventually need them.
- Do not leave required core behavior as TODO and report the phase as complete.
- Clearly label placeholders.

## 4. Input Rule
Keyboard, mouse wheel, UI buttons/dials, and future touch input MUST NOT implement separate machining logic.

All inputs route through the same command layer:

Input Source
→ `LatheInputRouter`
→ Lathe Commands
→ `LatheController` / domain logic

UI components must not directly mutate internal `LatheController` state.

## 5. Measurement Invariant
Never merge these concepts:
- `TrueDimension`: actual value derived from Workpiece Geometry.
- `InstrumentReading`: value represented by the measurement instrument.
- `RecordedMeasurement`: value manually entered by the player.

A wrong player reading MUST remain wrong in `RecordedMeasurement`; never auto-correct it.

## 6. Geometry Invariants
- Turning geometry may be calculated internally using Radius.
- Player-facing machining/measurement dimensions use Diameter.
- `DisplayedDiameter = Radius × 2`.
- Machining removes material only. Normal gameplay cannot add removed material back.
- Over-machining is not automatically restored.
- Workpiece reversal should use an Orientation/coordinate transform rather than destructively rewriting geometry where practical.
- Lathe input remains in Machine Coordinates; geometry access accounts for Workpiece Orientation.

## 7. Machining Preconditions
Material removal occurs only when ALL required conditions are true:
- A Workpiece is mounted.
- The spindle is running.
- A cutting-capable Tool is mounted.
- Tool and Workpiece Geometry are in contact.
- Current Lathe state permits machining.

Otherwise movement is positioning only.

## 8. MVP Exclusions
Do NOT implement unless a later explicit task changes scope:
- 3D exploration/world map
- milling
- drilling/reaming/center-drilling systems
- vehicle restoration
- combat, factions, raiders
- apprentice/successor systems
- full settlement growth
- micrometers or advanced measuring tools
- geometric tolerancing
- ISO surface roughness evaluation
- advanced tool-wear simulation
- complex material identification
- coolant types
- economy/shop/crafting recipe systems
- NPC schedules/day-night
- hunger/thirst
- online/multiplayer

Do not create elaborate abstractions solely for excluded features.

## 9. Phase Discipline
Implement ONE phase at a time according to `docs/IMPLEMENTATION_PLAN.md`.

Before coding:
1. Read relevant existing files.
2. State the files expected to be created/modified.
3. Check phase prerequisites.

After coding:
1. Review for compile errors and obvious integration errors.
2. Run available relevant tests/checks.
3. Report created files.
4. Report modified files.
5. Report tests/checks performed.
6. Report placeholders.
7. Report known problems.

Do NOT automatically begin the next phase.

## 10. Unity Baseline
- Unity LTS family.
- C#.
- Single-player.
- PC-first.
- Keyboard + mouse first.
- Core functionality must also be accessible through on-screen UI so future touch support does not require replacing domain logic.
- No external server is required for the MVP.
- Local save/data only.

## 11. Safety of Scope
This is a game abstraction, not a real dangerous-machine operation trainer. Do not expand the MVP into real-world machine-operation instruction.

## 12. Definition of Done
Do not claim MVP completion unless:
- Unity runs without project-blocking errors.
- QUEST 01 is completable end-to-end.
- Workpiece Geometry actually changes during machining.
- Reclamping allows machining the opposite side.
- Vernier markings can be read by the player without automatic final-number display.
- Player-entered measurements are stored separately.
- Part evaluation uses actual Geometry, not the player's recorded answer.
- Material loss/retry works without easy softlock.
- Save/Load preserves required state.
- Pump repair produces a visible world-state change.
