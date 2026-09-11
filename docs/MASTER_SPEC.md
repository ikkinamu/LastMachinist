# Last Machinist — Refined MVP Master Index

## Status
Refined Codex-facing documentation derived from:
`Last_Machinist_MVP_Development_Spec_v0.2.txt`

This file is an index and authority map, not a replacement for every domain specification.

## One-line Definition
Last Machinist MVP is a game in which the player:

**observes a worn pump shaft, reads it with a vernier, infers its original geometry, turns a replacement shaft, and makes the stopped pump run again.**

## Core Design Question
Is the following loop fun?

Observation
→ Measurement
→ Judgment
→ Machining
→ Re-measurement
→ Visible world change

## Documentation Map

### `../AGENTS.md`
Codex behavior, invariants, scope boundaries, and phase discipline.

### `MVP_SPEC.md`
What the MVP must contain, what it excludes, and what counts as completion.

### `ARCHITECTURE.md`
Project/data architecture, geometry ownership, save/state boundaries, and coordinate rules.

### `LATHE_SPEC.md`
Turning mechanics, common input command architecture, X/Z handwheels, spindle, cutting conditions, reclamping, and chamfer.

### `MEASUREMENT_SPEC.md`
Ruler/vernier mechanics and the mandatory separation of TrueDimension, InstrumentReading, and RecordedMeasurement.

### `QUEST_01_SPEC.md`
DAY 1 / 「멈춰버린 펌프」 progression and integration requirements.

### `IMPLEMENTATION_PLAN.md`
PHASE 0–8 execution plan with prerequisites, forbidden scope, tests, and acceptance criteria.

### `source/Last_Machinist_MVP_Development_Spec_v0.2.txt`
Preserved original development specification. Use it to recover wording/detail omitted from refined docs.

## Authority Rule
For MVP implementation:
1. AGENTS.md
2. MVP_SPEC.md
3. ARCHITECTURE.md
4. relevant domain spec
5. IMPLEMENTATION_PLAN.md
6. source specification

If refined documents genuinely conflict, Codex must stop and report the conflict rather than inventing a resolution.

## Explicit Unknowns / TBDs
The source specification intentionally does not finalize:
- exact L3 wear amount
- exact functional tolerance values
- exact vernier scale/minimum graduation
- exact movement distance for ×1/×10/×100
- exact final namespace/.asmdef boundaries
- exact detailed chamfer dimensions where not otherwise specified

These must remain configurable/TBD until explicitly decided. Temporary development values must be clearly labeled as placeholders.

## Non-negotiable MVP Facts
- MVP ends with first pump restoration.
- No 3D exploration in MVP.
- Initial stock: structural carbon-steel round bar ×3.
- First precision tool: old vernier received from 박성철.
- Vernier does not automatically show the final answer.
- Broken shaft does not provide the correct drawing.
- L3 wear drives original-shape inference.
- Target shaft: 100 mm total; Ø40×30 / Ø50×40 / Ø40×30.
- First Quest uses forgiving functional evaluation.
- Actual Geometry determines part evaluation.
- RecordedMeasurement is not actual Geometry.
- Failure is possible, but development testing must avoid easy softlock.
- Manufacturing success must visibly restore the pump.
- Do not implement later systems merely because they may exist in the future.
- Do not hardcode neutral core systems so narrowly that normal later process expansion is blocked.
- Keep the experience as a game abstraction rather than real dangerous-machine operation training.
