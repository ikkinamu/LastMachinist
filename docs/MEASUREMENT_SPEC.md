# Last Machinist — Measurement MVP Specification

## 1. Core Invariant
These three values are separate and MUST NEVER be merged:

### TrueDimension
Actual game-internal dimension calculated from current Workpiece Geometry.

### InstrumentReading
Value represented by the measurement instrument after instrument characteristics are applied.

### RecordedMeasurement
Value manually entered by the player after visually reading the instrument.

If the player reads the scale incorrectly, the recorded value remains incorrect. Do not auto-correct it.

## 2. MVP Tools
Required:
1. Metal ruler
2. Old vernier caliper

Advanced tools such as micrometers are outside the MVP.

## 3. Metal Ruler
Starting item.

Purpose:
- rough length observation
- atmosphere of marking/inspection
- first measurement-tool progression event

During QUEST 01, the protagonist attempts precision inspection using the ruler, leading 박성철 to provide the old vernier.

## 4. Vernier Caliper
Item:
- 낡은 버니어 캘리퍼스

Acquisition:
- received from 박성철 during QUEST 01 analysis.

Required UI:
- main scale
- vernier scale
- measurement target
- measurement-point selection
- player numeric input
- record button

The game does NOT automatically display the final measured number.

Rendering flow:
`InstrumentReading`
→ main-scale position
→ vernier offset
→ matching-scale visual state

The player visually interprets the scale and manually records a number.

The source specification defines one fixed vernier type for the MVP but does NOT define the exact scale design/minimum graduation. Do not invent an authoritative final specification; keep it configurable/TBD until the UI design step.

## 5. Measurement Preconditions
Precision measurement requires spindle stopped.

Measurement mode is distinct from machining mode.

Input context should allow measurement controls to own relevant inputs without simultaneously moving the lathe handwheel.

## 6. Measurement Points
A fully free physical measurement simulation is outside the MVP.

Use meaningful predefined `MeasurementPoint`s.

Broken pump shaft examples:
- `L1_Diameter`
- `L1_Length`
- `L2_Diameter`
- `L2_Length`
- `L3_Diameter`
- `L3_Length`
- `TotalLength` optional

Suggested fields:
- Id
- Type
- GeometryReference
- DisplayHint optional
- CanMeasureWith[]

Selecting a MeasurementPoint obtains the corresponding actual dimension from Geometry and feeds the instrument representation.

## 7. Broken Shaft
Normal target geometry:
- total 100 mm
- L1: 30 mm / Ø40
- L2: 40 mm / Ø50
- L3: 30 mm / Ø40

Broken part:
- L1 near normal
- L2 near normal
- L3 diameter worn
- L3 end/length region worn

Important:
- broken part has CurrentGeometry
- correct TargetGeometry exists separately in Quest/evaluation data
- broken part does NOT expose the correct drawing
- player must infer the original shape from measurements/symmetry

Exact L3 wear values are TBD and must not be silently invented as final design values.

## 8. Measurement Record
Each record should preserve enough information to distinguish:
- Workpiece instance
- MeasurementPoint
- instrument
- InstrumentReading if needed for reconstruction/debug
- player-entered RecordedMeasurement
- relevant timestamp/order if useful

The actual evaluation of the manufactured part MUST NOT treat `RecordedMeasurement` as authoritative geometry.

## 9. Inspection
Final inspection:
- stop spindle
- use vernier
- visually read scale
- manually enter results into inspection sheet
- store records
- evaluate actual Workpiece Geometry separately

A player may write a wrong number while the part itself is correct, or write a plausible number while the part itself is wrong. The data model must preserve this distinction.

## 10. Acceptance Tests
- Geometry-derived TrueDimension is correct.
- Instrument UI represents InstrumentReading.
- RecordedMeasurement is independently stored.
- Wrong manual input is not auto-corrected.
- measurement cannot proceed as precision measurement while spindle runs.
- MeasurementPoint selects the intended geometry reference.
- L3 broken dimensions differ from the normal target once concrete wear values are configured.
- final part evaluation uses actual Geometry rather than RecordedMeasurement.
