# Last Machinist — Lathe MVP Specification

## 1. Purpose
Implement a game-oriented manual-lathe interaction capable of producing the QUEST 01 stepped pump shaft.

This is not a real-world dangerous-machine operation trainer.

## 2. Required Machining
MVP supports:
- round-bar Workpiece
- external turning
- step turning
- chamfer
- Workpiece reversal/reclamping
- persistent geometry
- over-machining
- material consumption

Minimum cutting tools:
1. Roughing tool
2. Chamfer tool

Advanced tool life and detailed cutting-edge geometry are excluded.

## 3. Input Architecture — Mandatory

```text
Keyboard
Mouse Wheel
UI Button / UI Dial
Future Touch
        ↓
LatheInputRouter
        ↓
Lathe Commands
        ↓
LatheController / Domain Logic
```

No input source may implement its own machining rules.

### 3.1 Axis Selection
PC shortcuts:
- X → select X axis
- Z → select Z axis

Mouse wheel moves the currently selected axis.

Screen always indicates selected axis:
- AXIS: X
- AXIS: Z

Directly manipulating an X or Z on-screen handwheel does not require selected-axis state; the manipulated wheel supplies its own axis.

### 3.2 Movement Scale
Three relative gameplay scales:
- ×1 = Fine
- ×10 = Medium
- ×100 = Coarse

Example keyboard mapping:
- 1 → ×1
- 2 → ×10
- 3 → ×100

Bindings may change later and should be compatible with rebinding through the selected Unity input solution.

These are gameplay-relative multipliers, NOT authoritative replicas of a real machine feed amount.

Actual movement distance per input step is not defined in the source specification; keep it configurable for playtesting.

### 3.3 Mouse Wheel
Mouse wheel:
- acts as selected-axis handwheel input
- sign determines + / - direction
- delta is scaled by current movement scale

Concept:
`MouseWheelDelta × CurrentMovementScale`
→ `ApplyHandwheelDelta(CurrentAxis, delta)`

Mouse wheel MUST NOT directly assign tool coordinates.

### 3.4 On-screen Dial
X and Z handwheels may be displayed separately.

Dragging a dial:
Pointer position
→ angle around dial center via `atan2`
→ previous/current angle difference
→ safe delta-angle handling across ±180°
→ handwheel delta
→ common Lathe Command

Visual dial rotation and domain movement originate from the same interaction/command result.

### 3.5 Future Touch
Future mobile:
- no keyboard
- no mouse wheel
- same UI buttons/dials via touch

Therefore every essential gameplay action must be accessible from screen UI from the MVP architecture onward.

Keyboard cannot be the only access path to an essential action.

## 4. Command Surface
Recommended command boundary:
- `SelectAxis(Axis axis)`
- `SetMovementScale(MovementScale scale)`
- `ApplyHandwheelDelta(Axis axis, float delta)`
- `SetSpindleRunning(bool running)`
- `SelectTool(string toolId)`
- `EnterMeasurementMode()`
- `RequestReclamp()`
- `FinishMachining()`

Exact class ownership (`LatheCommandService` vs public command facade on `LatheController`) may be chosen during implementation, provided the common-command rule is preserved.

UI must not mutate internal controller state directly.

## 5. Input Contexts
Mouse wheel can conflict with ScrollView/camera controls.

Maintain explicit contexts such as:
- GameplayContext
- DialogueContext
- LatheContext
- MeasurementContext
- UIContext

In LatheContext:
- mouse wheel = Handwheel

In MeasurementContext:
- mouse wheel may be reserved for measurement interaction

When pointer is over a relevant UI scrolling region, Handwheel input may be blocked.

Do not simultaneously use mouse wheel for camera zoom during LatheContext.

## 6. Spindle
Required:
- start rotation
- stop rotation
- keyboard access may exist
- UI button access must exist
- both use the same Command

MVP may use one rotation direction. Reverse rotation is not required unless later explicitly added.

## 7. Tool / Measurement / Reclamp Commands
The following should be accessible through the common command architecture:
- Tool selection
- enter measurement
- reclamp
- finish machining

Keyboard shortcuts may supplement UI, never replace it.

## 8. HUD
Minimum:
- AXIS X/Z
- SCALE ×1/×10/×100
- SPINDLE ON/OFF
- TOOL current tool
- MODE current mode

The initial old lathe should NOT continuously expose highly precise absolute X/Z numerical coordinates as a permanent DRO.

Reason: a permanent high-precision DRO would weaken the purpose of the vernier measurement system.

A future DRO may be a progression feature, but it is outside this MVP.

## 9. Cutting Preconditions
Tool movement is not automatically cutting.

Material removal requires:
- Workpiece mounted
- spindle running
- cutting-capable Tool mounted
- Tool/Workpiece geometry contact
- Lathe state permits machining

Otherwise movement is positioning only.

## 10. Turning Geometry
Authoritative turning profile:
- `TotalLength`
- `Segments[]`

Segment:
- `StartZ`
- `EndZ`
- `Radius`

Internal calculation may use Radius.
Player-facing dimensions use Diameter.

`Diameter = Radius × 2`

Material removal only decreases Radius.

Forbidden in normal gameplay:
- adding removed material back
- automatic restoration after over-machining

Undo may exist only as a development/debug tool.

## 11. Reclamping
Required for QUEST 01.

Flow:
- spindle stopped
- detach current Workpiece logically
- change Orientation `Normal ↔ Reversed`
- update coordinate transform/reference
- remount
- continue from Ready

Prefer orientation/coordinate transformation over destructive reversal of authoritative geometry.

The player's perceived X/Z machine controls should remain consistent after reversal.

## 12. Chamfer
Do not create an oversized CAD feature framework.

Required:
- select chamfer tool
- create a simple chamfer at a designated edge through gameplay interaction
- reflect slope in profile/visual geometry
- allow evaluator to determine required chamfer presence

Possible data:
`ChamferFeature`
- Position
- Width
- Angle optional
- IsValid

QUEST 01 chamfer requirement should be forgiving.

## 13. Feedback
Handwheel/dial:
- visible handwheel rotation
- small click/mechanical feedback
- selected-axis highlight
- movement feedback

Scale should feel different:
- ×100 large
- ×10 medium
- ×1 fine

Lathe:
- spindle start/stop
- rotation feedback
- cutting-contact feedback
- clear visual/audio indication when Tool contacts material

Graphics polish is secondary to readable feedback.

## 14. Pump Shaft Target
Target:
- total 100 mm
- 30 mm at Ø40
- 40 mm at Ø50
- 30 mm at Ø40
- symmetric stepped shaft
- required edge chamfer(s), forgiving in MVP

The concrete chamfer dimensions and functional tolerance numbers are not fixed in the source specification and must remain configurable/TBD until explicitly decided.

## 15. Lathe Acceptance Tests
- Tool moves on X and Z.
- Axis selection works.
- ×1/×10/×100 affect movement.
- Mouse wheel uses selected axis.
- X/Z screen dials directly manipulate their own axis.
- UI and keyboard reach the same command/domain behavior.
- spindle OFF prevents cutting.
- no contact prevents cutting.
- non-cutting Tool prevents cutting.
- cutting reduces actual Geometry.
- over-machining is not auto-restored.
- profile persists.
- Orientation changes correctly.
- opposite side can be machined after reclamp.
- chamfer can be represented and evaluated.
- input context prevents obvious mouse-wheel conflicts.
