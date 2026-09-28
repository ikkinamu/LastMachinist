# PHASE 7 — Integration

## Scope
Connects the Phase 4–6 domain systems into a controlled QUEST 01 flow: broken shaft recovery, measurement/inference gates, stock consumption, machining/final inspection transitions, evaluation, rework/retry and pump repair completion.

## Implementation
- `Quest01Integration` coordinates `QuestStateMachine`, `InventoryState`, `MeasurementSession`, `PartEvaluator` and the current `WorkpieceState`.
- Precision inspection requires a stopped spindle.
- Acceptable/Perfect results unlock delivery; Reworkable returns to machining; Rejected consumes a new stock item for retry.
- Installation completion sets `PumpRepaired` through the quest state.

## Verification
`Phase7Tests` covers the integrated completion path, stock consumption, stopped-spindle inspection and the rework path. Runtime source compiles against Unity assemblies with the existing serialized-field warning only.

## Boundaries
This is a domain integration seam for controlled testing. Scene transitions, NPC presentation, physical pump visuals, disk save/load and debug panel remain in PHASE 8 or later integration work.

v0.3 follow-up adds a minimal `MvpWorldPanel` for Workshop/Farm movement, pump status display and broken-shaft recovery. The generated panel is a presentation placeholder and keeps the domain state in `Quest01Integration`.

The panel now exposes the controlled DAY 1 transitions from ruler attempt through vernier, broken-shaft inspection, material selection, delivery and installation; the lathe page exposes machining completion and actual-geometry evaluation entry points.
