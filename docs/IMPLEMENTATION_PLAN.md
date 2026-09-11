# Last Machinist — Codex Implementation Plan

## Operating Rule
Implement exactly ONE phase at a time.

For every phase:

### Before implementation
1. Read `AGENTS.md`.
2. Read this document.
3. Read all domain specs relevant to the phase.
4. Inspect existing relevant project files.
5. Report planned created/modified files.
6. Verify prerequisites.

### After implementation
Report:
1. Created files.
2. Modified files.
3. Tests/checks performed.
4. Placeholder elements.
5. Known problems.
6. Acceptance criteria status.

Do NOT automatically start the next phase.

---

# PHASE 0 — Project Skeleton

## Goal
Establish a clean Unity project foundation without implementing gameplay systems.

## Required
- Create recommended `_Project` folder structure as needed.
- Establish minimal namespace policy.
- Decide whether minimal `.asmdef` usage is beneficial; do not over-fragment.
- Create `GameBootstrap` or equivalent minimal startup composition point.
- Establish basic scene(s) needed to prove project boots.
- Establish locations for static data definitions.
- Preserve Unity-generated project structure/settings.

## Forbidden
- Turning gameplay implementation.
- Quest content implementation.
- Measurement gameplay implementation.
- Future systems outside MVP.

## Acceptance Criteria
- Unity project opens/runs without blocking errors.
- Empty/basic startup scene runs.
- Folder/namespace policy is documented.
- No unnecessary framework has been introduced.

---

# PHASE 1 — Core Data

## Prerequisites
- PHASE 0 complete.

## Read
- `ARCHITECTURE.md`
- `MVP_SPEC.md`

## Required
Implement minimal data/domain structures for:
- Item definition/state
- Inventory
- Material definition
- Workpiece state
- TurningProfile / TurningSegment
- Orientation
- Save DTO foundation

Keep static definitions and runtime state distinct.

## Required Tests
Where feasible using pure C# tests:
- create a Workpiece
- create segmented round profile
- serialize/deserialize required DTO/state
- inventory quantity add/remove behavior
- orientation survives serialization

## Forbidden
- Visual Mesh.
- Lathe controls.
- Quest flow.
- Vernier UI.

## Acceptance Criteria
- Workpiece can be created in code.
- Geometry can represent round stepped profiles.
- Required runtime state can be serialized/restored.
- No direct Scene Object reference exists in save DTO.

---

# PHASE 2 — Turning Prototype

## Prerequisites
- PHASE 1 complete.
- WorkpieceState exists.
- TurningProfile exists.

## Read
- `LATHE_SPEC.md`
- relevant sections of `ARCHITECTURE.md`

## Required
- LatheController/domain boundary.
- Tool position/movement.
- X/Z axis handling.
- spindle state.
- minimum roughing Tool definition/state.
- Tool/Workpiece contact decision.
- Radius removal.
- TurningProfile change notification.
- TurningProfile → visible rotational Mesh.
- minimal Lathe HUD.
- common Lathe Command boundary.
- keyboard/mouse-wheel input routing sufficient for prototype.
- minimal UI controls for essential commands.

## Important
Material removal only when all machining preconditions are true.

## Required Tests
- spindle OFF + contact → no removal.
- spindle ON + no contact → no removal.
- spindle ON + cutting Tool + contact → Radius decreases.
- removed material cannot reappear through normal machining commands.
- generated Mesh updates when profile changes.
- a visible step can be produced.

## Forbidden
- Reclamp.
- Chamfer.
- Vernier.
- Quest integration.
- Tool wear.
- Coolant.
- Milling.

## Acceptance Criteria
- Player/tester can turn a round bar and create a visible step.
- Geometry, not merely Mesh deformation, changes.
- Unity compiles/runs without blocking errors.

---

# PHASE 3 — Reclamp + Chamfer

## Prerequisites
- PHASE 2 complete.

## Read
- `LATHE_SPEC.md`
- `ARCHITECTURE.md`

## Required
- `Normal` / `Reversed` Workpiece Orientation.
- Machine ↔ Workpiece coordinate conversion.
- Reclamp command/state.
- spindle-stop requirement for reclamp.
- opposite-side access after reversal.
- chamfer Tool.
- simple chamfer representation.
- evaluator-readable chamfer feature/state where needed.
- on-screen X/Z dial direct manipulation.
- safe angular delta handling.
- complete required common input paths.

## Required Tests
- reversal does not corrupt authoritative geometry.
- machine control direction remains consistent.
- opposite side becomes machinable.
- chamfer changes geometry/feature representation.
- UI dial and other input routes reach the same command/domain behavior.

## Forbidden
- freeform CAD feature system.
- advanced tool geometry.
- future mobile implementation itself.

## Acceptance Criteria
A player/tester can produce the basic:
Ø40 / Ø50 / Ø40
stepped form using reclamping and can add the required MVP chamfer.

---

# PHASE 4 — Measurement

## Prerequisites
- PHASE 3 complete.
- stable Workpiece Geometry queries exist.

## Read
- `MEASUREMENT_SPEC.md`
- `QUEST_01_SPEC.md`

## Required
- MeasurementPoint model.
- TrueDimension query.
- InstrumentReading model.
- RecordedMeasurement model.
- metal ruler minimal interaction.
- old vernier model.
- main/vernier scale visualization.
- measurement target/point selection.
- manual numeric entry.
- record action.
- measurement input context.
- spindle-stop precision-measurement restriction.

## Critical Invariant
`TrueDimension != InstrumentReading != RecordedMeasurement` as concepts, even if two numeric values happen to match.

## Required Tests
- Geometry query produces correct TrueDimension.
- instrument visual state corresponds to InstrumentReading.
- wrong manual entry remains wrong.
- record persists separately.
- evaluation authority is not RecordedMeasurement.
- measurement mode does not simultaneously drive lathe handwheel input.

## TBD
Exact vernier graduation/design is not specified. Keep it configurable and do not claim a final design choice unless explicitly approved.

## Acceptance Criteria
Tester can select a pump-shaft measurement point, visually read the vernier, type a value, and store it.

---

# PHASE 5 — Part Evaluation

## Prerequisites
- PHASE 4 complete.

## Read
- `MVP_SPEC.md`
- `QUEST_01_SPEC.md`
- `MEASUREMENT_SPEC.md`

## Required
- PartRequirement.
- PumpShaftRequirement.
- required geometry comparison.
- required chamfer check.
- result:
  - Perfect
  - Acceptable
  - Reworkable
  - Rejected
- distinguish recoverable oversize from irrecoverable over-machining.

## Important
Evaluate actual Workpiece Geometry.
Do NOT evaluate correctness from the player's RecordedMeasurement.

## TBD
Concrete functional tolerance values are playtest values and are not fixed by the source specification. Implement them as configurable data.

## Required Tests
Construct test Workpieces that produce each result class.

## Acceptance Criteria
Known test geometries reliably produce the intended result category.

---

# PHASE 6 — Quest + Dialogue

## Prerequisites
- Core inventory/state systems stable.
- Part evaluation API stable enough for later integration.

## Read
- `QUEST_01_SPEC.md`
- `MVP_SPEC.md`

## Required
- Quest state model for `quest_pump_01`.
- Quest-state events.
- minimal Dialogue system.
- 박성철.
- 이서연.
- minimal protagonist presentation.
- Workshop/Farm transition.
- pump broken state.
- ruler → vernier progression event.
- shape-inference progression gate.
- material selection step.
- ability to progress Quest using debug/stub machining completion if necessary for isolated Quest testing.

## Required Tests
- Quest state transitions in correct order.
- vernier acquisition flag/state.
- shape-inference flag/state.
- scene transitions preserve Quest state.
- Quest can reach completion using controlled test/stub result without requiring manual machining during automated flow test.

## Forbidden
- NPC schedules.
- 3D exploration.
- combat.
- unrelated Quest 02 content.

## Acceptance Criteria
Quest flow can be tested end-to-end independently of full integration.

---

# PHASE 7 — Integration

## Prerequisites
- PHASES 1–6 complete.

## Read
All refined specifications.

## Required
Connect:
- broken pump shaft
- measurement points
- L3 wear data
- symmetry inference
- material consumption
- lathe machining
- reclamp/chamfer
- final inspection
- part evaluation
- Reworkable loop
- Rejected/new-stock retry
- delivery
- installation
- pump repair world-state change

## Important TBD
Exact L3 wear values are not fixed by the source. Choose only temporary clearly labeled test values if integration requires them; do not silently promote them to final design constants.

## Required End-to-End Test
DAY 1 start
→ farm diagnosis
→ broken shaft
→ ruler attempt
→ vernier
→ measurements
→ inference
→ material
→ machining
→ inspection
→ evaluation
→ delivery
→ pump running

Also test:
- Reworkable loop.
- Rejected retry.
- stock count.
- no easy softlock in development testing.

## Acceptance Criteria
QUEST 01 is playable from start to finish and the manufactured part causes visible pump restoration.

---

# PHASE 8 — Save + Polish + Debug

## Prerequisites
- PHASE 7 end-to-end path works.

## Required
- one-slot local Save/Load.
- checkpoints.
- Quest-state persistence.
- inventory persistence.
- Workpiece Geometry/Orientation persistence.
- measurement-record persistence.
- vernier/inference flags.
- PumpRepaired persistence.
- development/editor Debug Panel.
- minimum audio/feedback.
- improve placeholder readability where required.

Debug Panel:
- force Quest State
- add items
- restore stock
- force vernier
- reset Workpiece
- toggle PumpRepaired
- inspect Geometry
- delete save
- direct lathe test access
- create/test evaluation outcomes where practical

## Required Tests
Save/reload at key checkpoints:
- DAY 1
- vernier obtained
- before machining
- before delivery
- Quest complete

Confirm restored:
- Quest
- Inventory
- Workpiece
- Orientation
- Measurement records
- flags
- PumpRepaired

## Acceptance Criteria
- Restarting the game preserves required progression.
- MVP can be completed after save/load.
- Pump remains repaired after re-entry/reload.
- Debug tools allow subsystem testing without replaying DAY 1.

---

# FINAL MVP ACCEPTANCE GATE

Do not declare MVP complete until all are true:

- [ ] Unity runs without blocking errors.
- [ ] QUEST 01 completes end-to-end.
- [ ] Workpiece Geometry changes from machining.
- [ ] Reclamping allows opposite-side machining.
- [ ] Vernier requires visual reading/manual entry.
- [ ] RecordedMeasurement is separate from actual Geometry.
- [ ] Evaluation uses actual Geometry.
- [ ] Perfect/Acceptable/Reworkable/Rejected work.
- [ ] Material consumption/retry works.
- [ ] Development path avoids easy softlock.
- [ ] Save/Load preserves required state.
- [ ] Pump visibly changes from broken to repaired.
- [ ] No excluded major system was unnecessarily implemented.
