# PHASE 8 — Save, Polish and Debug

## Scope
Adds one-slot local JSON save/load, expanded progression DTO fields and a development debug panel. This phase does not claim final MVP acceptance until an end-to-end Unity run is completed.

## Implementation
- `SaveGameData` now preserves quest state, measurement records, scene identifier and pump-repaired state alongside inventory/workpieces.
- `SaveGameStore` writes and validates one local JSON slot and supports load/delete.
- `DevelopmentDebugPanel` (F8 in development builds) exposes save/load, stock recovery, vernier/shape flags, pump toggle and save deletion.
- `SaveGameComposer` captures the integration state into the save DTO without storing scene object references.

## Verification
Runtime source compiles against Unity assemblies with the existing serialized-field warning only. Existing save DTO round-trip tests remain applicable; Unity EditMode execution is still subject to the editor licensing/import limit.

## Known boundaries
The debug panel is intentionally a development placeholder. Scene wiring must assign `SaveGameData` and invoke the store at desired checkpoints. Final audio, NPC presentation and full end-to-end acceptance still require Unity playtesting.

## v0.3 follow-up
The startup HUD now includes Korean `선반`, `측정`, `퀘스트`, `메모`, `인벤토리`, and `결과` pages, fixed four-view camera controls, and a progressive `WorkMemoState` that does not reveal the target shape until the required observations are recorded.
