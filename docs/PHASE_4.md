# PHASE 4 — Measurement

## Scope
Implements the measurement domain and minimal prototype interaction only. Quest progression, part evaluation, save/load and final inspection flow remain deferred to their planned phases.

## Implementation
- `MeasurementPoint` identifies predefined diameter/length regions.
- `MeasurementSession.TrueDimension` queries the authoritative `TurningProfile`.
- `InstrumentReading` stores the vernier instrument state separately from geometry.
- `RecordedMeasurement` stores the player's manually entered value without correction.
- Precision measurement is rejected while the spindle is running.
- The prototype HUD provides a metal-ruler inspection action, predefined measurement-point buttons, a main/vernier scale indication, numeric entry and record action.
- The metal ruler now has a visible scene placeholder that appears on the workpiece only while the ruler-contact action is active.

## Verification
The runtime source compiles against the installed Unity assemblies with only the existing serialized `GameBootstrap.latheSettings` warning. `Phase4Tests` covers geometry-derived dimensions, independent incorrect player input and the stopped-spindle restriction. Unity EditMode execution remains environment-dependent when the editor license/import limit is reached.

## Placeholders and boundaries
The ruler and vernier are readable immediate-mode HUD placeholders. Graduation density and final vernier visual design remain configurable/TBD per the specification. No quest, evaluator, save or automatic final-number display was added.
