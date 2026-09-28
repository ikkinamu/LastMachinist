# PHASE 5 — Part Evaluation

## Scope
Adds configurable geometry-based part evaluation. Quest flow, inventory retry, save/load and installation remain deferred.

## Implementation
- `PartRequirement` holds target diameter intervals, chamfer positions and playtest tolerance values.
- `PartEvaluator` reads the authoritative `TurningProfile`, never measurement records.
- Results are `Perfect`, `Acceptable`, `Reworkable` or `Rejected`.
- Undersize/over-machined geometry is rejected; recoverable oversize and missing chamfers are reworkable.

## Verification
`Phase5Tests` covers all four result classes, required chamfer handling and geometry-only evaluation. Runtime source compiles against Unity assemblies with the existing serialized-field warning only.

## Boundaries
Tolerance defaults are explicitly playtest placeholders. No quest progression, material consumption, retry loop, save system or pump installation was added.
