# Last Machinist — QUEST 01 Specification
## 「멈춰버린 펌프」

## 1. Quest ID
`quest_pump_01`

## 2. Purpose
Introduce the full MVP causal loop:
broken real-world machine
→ inspect failed part
→ measure
→ infer
→ manufacture
→ inspect
→ install
→ world changes.

## 3. Quest States
Expected explicit states:
- NotStarted
- FarmerArrived
- GoingToFarm
- InspectingPump
- BrokenShaftFound
- ReturnedToWorkshop
- RulerAttempt
- VernierReceived
- InspectingBrokenShaft
- ShapeInferred
- MaterialSelected
- Machining
- FinalInspection
- ReadyToDeliver
- Installing
- Completed

State changes should event-update:
- dialogue
- interactables
- objective UI
- scene/world state

Do not have NPC `Update()` poll the Quest state every frame.

## 4. DAY 1 Sequence

### 4.1 Start
Minimal prologue/text/image sequence.
김영호 and transfer of responsibility may be referenced.

Then:
LAST MACHINIST
DAY 1
삼촌의 작업장

### 4.2 Workshop Introduction
박성철 appears.

Dialogue intent:
- asks whether protagonist knows basic machining.

Player choices:
- 기본 정도는 알고 있습니다.
- 오래돼서 좀 가물가물합니다.
- 잘 모릅니다.

Choice changes tutorial/help amount only.
It does NOT change stats or core difficulty.

### 4.3 Incident
이서연 arrives urgently because the machine/pump is not working.

박성철 and protagonist go to the farm.

### 4.4 Farm Diagnosis
Inspect pump.

박성철 determines:
- motor does not appear to be the main problem
- shaft is unusable
- replacing/manufacturing a new one is preferable

이서연 asks whether the protagonist can make it.

Acquire/recover broken pump shaft.

### 4.5 Return / Ruler Attempt
Return to workshop.

Player attempts measurement with metal ruler.

Required progression beat:
박성철 recognizes the inappropriate precision-measurement attempt and provides the old vernier.

Set:
`HasReceivedVernier = true`

### 4.6 Broken Part Analysis
Player measures L1/L2/L3.

L3 is abnormal because of wear.

Required hint concept from 박성철:
If you copy a worn dimension directly, you only make another worn part.

Player recognizes symmetric structure and infers the target form.

Set:
`PumpShaftInferred = true`

The correct drawing is NOT simply handed to the player.

### 4.7 Material Selection
Show three structural carbon-steel round bars left by 김영호.

Supporting workshop-note clue may indicate that carbon-steel stock was secured because the pump shaft would soon need attention.

Use one bar:
3 → 2

### 4.8 Machining
Enter lathe.

Required manufacturing flow:
- mount Workpiece
- roughing tool
- machine first Ø40 region
- preserve central Ø50 region
- reclamp/reverse
- machine opposite Ø40 region
- chamfer
- stop spindle

Target geometry:
- 100 mm total
- Ø40 × 30
- Ø50 × 40
- Ø40 × 30

### 4.9 Final Inspection
Use old vernier.
Player visually reads it.
Player manually enters inspection values.

`PartEvaluator` evaluates actual Geometry.

Result behavior:
- Perfect / Acceptable → delivery allowed
- Reworkable → recommend/allow additional machining
- Rejected → current Workpiece cannot be recovered; consume another stock for retry

The first Quest uses forgiving functional tolerances. Exact numbers remain TBD/playtest values.

### 4.10 Installation
Return to farm.

박성철 installs the part.

For Acceptable-level rough-but-functional output, dialogue may acknowledge it is not beautiful but will work.

Pump:
- starts
- water flows
- sound/visual state changes

이서연 reacts with relief.

Set:
`PumpRepaired = true`

Quest state:
`Completed`

### 4.11 End
Show:
`QUEST COMPLETE — 멈춰버린 펌프`

Development build may additionally show:
`MVP COMPLETE`

## 5. Inventory Progression
Start:
- structural carbon-steel round stock ×3
- metal ruler ×1
- roughing tool
- chamfer tool
- minimum basic exchange tools

Quest:
- broken pump shaft acquired
- old vernier acquired

Failure:
- each new attempt consumes another stock

MVP testing must provide restart/checkpoint recovery to avoid softlock when stock reaches zero. This debug/testing recovery is distinct from final-game resource rules.

## 6. Part Requirement
`PumpShaftRequirement`

Required geometry:
- 30 mm region Ø40
- 40 mm region Ø50
- 30 mm region Ø40
- total 100 mm

Required features:
- stepped shape
- required edge chamfer(s)

Not evaluated in MVP:
- geometric tolerances
- ISO surface roughness

Functional tolerance:
TBD via playtesting.

## 7. Dialogue Data
Dialogue should be data-driven.

Required fields may include:
- Id
- SpeakerId
- Text
- Choices[]
- NextNodeId
- EventId optional

The source specification includes sample Korean dialogue. Preserve the original source file for exact wording; do not bury long dialogue directly inside C#.

## 8. Save/Restore
Save enough state to resume:
- Quest state
- stock count
- broken shaft/current Workpiece state
- vernier acquisition
- inferred-shape flag
- measurement records
- completed replacement part
- `PumpRepaired`

Re-entering Farm after completion must restore repaired pump state.

## 9. Quest Acceptance Tests
- Quest completes in intended order.
- scene/location transition preserves state.
- vernier-dependent measurement is unavailable before acquisition.
- shape-inference gate affects progression as designed.
- stock decreases on attempt.
- Reworkable can return to machining.
- Rejected can consume new stock/retry.
- completion does not restart on scene reload.
- PumpRepaired persists.
- player can see/hear a clear difference between broken and repaired pump.
