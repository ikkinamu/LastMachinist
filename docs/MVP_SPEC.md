# Last Machinist — MVP Specification

## 1. Purpose
This document defines the minimum vertical slice required to test the core fun of Last Machinist. It is not the complete final-game specification.

The MVP asks:

> Is it fun to observe a broken part, measure it directly, infer its original shape, manufacture a replacement, and make a real facility work again?

## 2. End Condition
The MVP is complete when QUEST 01 **「멈춰버린 펌프」** can be played from beginning to end and the player can install a self-manufactured pump shaft so the pump visibly operates again.

## 3. Core Loop
Workshop / DAY 1
→ meet 박성철
→ 이서연 requests help
→ visit farm
→ inspect failed pump
→ diagnose broken shaft
→ recover shaft
→ return to workshop
→ attempt measurement with metal ruler
→ receive old vernier caliper
→ measure broken shaft
→ discover L3 wear
→ infer original symmetric geometry
→ select structural carbon-steel round stock
→ turn replacement shaft
→ reclamp Workpiece
→ final measurement
→ record inspection
→ return to farm
→ install shaft
→ pump runs
→ visible water flow
→ QUEST COMPLETE / MVP END

Core experience:
Observation → Measurement → Judgment → Machining → Re-measurement → Visible world change.

## 4. Required Scope

### 4.1 Scenes / Progression
Required:
- Minimal title/start screen.
- DAY 1.
- Workshop.
- Farm/pump location.
- Minimal location transition.
- QUEST 01 progression.
- Pump restoration ending.

3D exploration is NOT required.

### 4.2 Characters
Required:
- 박성철
- 이서연
- protagonist

김영호 may appear only through prologue, records, or dialogue in the MVP.

### 4.3 Turning
Required:
- One manual lathe.
- Round-bar Workpiece.
- External turning.
- Step turning.
- Chamfer.
- Workpiece reversal/reclamping.
- Persistent machining-result geometry.
- Over-machining.
- Material consumption.

### 4.4 Measurement
Required:
- Metal ruler.
- Old vernier caliper.
- Main-scale and vernier-scale visualization.
- Player visually reads the instrument.
- Player manually enters the measured value.
- `RecordedMeasurement` storage.

The game MUST NOT simply display the final vernier answer as a number.

### 4.5 First Target Part
Pump shaft:
- Total length: 100 mm.
- L1: Ø40 × 30 mm.
- L2: Ø50 × 40 mm.
- L3: Ø40 × 30 mm.
- Symmetric left/right structure.

Broken shaft:
- L1 near normal.
- L2 near normal.
- L3 has diameter wear and end/length-direction wear.
- Exact wear amount is NOT defined by this specification and must not be invented as a final design constant without a later decision.

The broken shaft does NOT carry the correct drawing/answer. Its current geometry and the target/evaluation geometry remain separate.

### 4.6 Inventory / Resources
Minimum:
- Structural carbon-steel round stock ×3.
- Broken pump shaft.
- Metal ruler.
- Old vernier.
- Roughing tool.
- Chamfer tool.
- Minimal additional basic exchange tools if required by UI/flow.

No grid/weight economy is required.

### 4.7 Part Evaluation
Result classes:
- `Perfect`
- `Acceptable`
- `Reworkable`
- `Rejected`

Meaning:
- Perfect: very close to target.
- Acceptable: rough but functionally installable.
- Reworkable: oversized/material remains and additional machining can correct it.
- Rejected: over-machined or otherwise irrecoverable.

The first Quest must be forgiving rather than a strict precision-machining examination.

Concrete functional tolerance numbers are NOT specified yet; determine them during playtesting rather than inventing them as authoritative design values.

### 4.8 Save
Preserve at minimum:
- current Quest state
- material quantity
- whether vernier was received
- current Workpiece
- finished-part state
- minimum measurement records
- pump repaired state

One save slot is sufficient.
JSON/local save is acceptable.
Unity `persistentDataPath` may be used.

Suggested checkpoints:
- DAY 1 start
- vernier obtained
- before machining
- before delivery
- Quest complete

## 5. Explicitly Out of Scope
The MVP excludes:
- 3D exploration
- world map
- milling
- drill/reamer/center-drill systems
- vehicle restoration
- distant regions
- factions/raiders/combat
- apprentice/successor
- full settlement growth
- micrometer/advanced measuring tools
- full book/manual system
- complex material identification
- multiple complex MaterialData sets
- geometric tolerances
- ISO surface roughness evaluation
- advanced tool-wear simulation
- complex environmental/system errors
- coolant varieties
- economy/shop
- crafting recipe system
- vehicle inventory
- NPC schedules
- day/night
- hunger/thirst
- full city building
- online/multiplayer

Do not over-engineer abstractions for excluded systems. Do not, however, hardcode neutral core data so narrowly that normal later multi-process expansion becomes impossible.

## 6. World-State Reward
Pump state before repair:
- stopped
- no water
- 이서연 anxious state

After repair:
- pump animation/operation
- water effect
- operating sound
- relief dialogue
- Quest complete
- `PumpRepaired = true`

Returning to the scene must restore the repaired state from save data.

The MVP must visibly communicate:
**the world changed because of the part the player manufactured.**

## 7. Placeholder Policy
Allowed during development:
- primitive meshes
- simple sprites
- temporary portraits/icons
- basic UI

Even as placeholders, these must remain readable:
- Workpiece shape
- vernier markings
- measurement position
- pump stopped/running difference
- NPC identity

Art replacement should not require domain-code rewrites.

## 8. Debug Requirements
Editor/development-only debug tools must allow:
- force Quest state
- add item
- restore stock count
- force vernier acquisition
- reset Workpiece
- toggle `PumpRepaired`
- inspect current Geometry
- delete save
- enter lathe testing without replaying all of DAY 1

## 9. MVP Success Criteria
Technical:
- Unity runs without blocking errors.
- QUEST 01 completes end-to-end.
- Turning changes actual Workpiece Geometry.
- Reclamping enables opposite-side machining.
- Vernier can be visually read.
- Manual player measurement is stored separately.
- Evaluation uses actual Geometry.
- Material failure/retry works.
- Save/Load preserves required state.
- Pump visibly changes to repaired state.

Experience:
The player understands:
Why is this part needed?
→ What failed?
→ What was its original shape?
→ How should I make it?
→ Did I make it correctly?
→ Where did my manufactured part matter?

Final intended feeling:
**“내가 깎은 축 때문에 펌프가 다시 돌아갔다.”**
