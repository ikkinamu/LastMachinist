# Last Machinist — MVP Architecture

## 1. Project Layout
Recommended Unity-owned project area:

```text
Assets/
└─ _Project/
   ├─ Scripts/
   │  ├─ Core/
   │  ├─ Dialogue/
   │  ├─ Quest/
   │  ├─ Inventory/
   │  ├─ Machining/
   │  │  ├─ Core/
   │  │  └─ Turning/
   │  ├─ Measurement/
   │  ├─ World/
   │  ├─ Save/
   │  └─ UI/
   ├─ Data/
   │  ├─ Items/
   │  ├─ Materials/
   │  ├─ Tools/
   │  ├─ Quests/
   │  └─ Dialogue/
   ├─ Prefabs/
   ├─ Scenes/
   ├─ Art/
   ├─ Audio/
   └─ Tests/
```

Unity-generated folders and external packages may remain outside `_Project`.

Possible scenes:
- Boot
- Workshop
- Farm
- Lathe

A separate Lathe scene is not mandatory; Workshop may transition into a Lathe mode/UI. Low coupling matters more than scene count.

## 2. Architecture Principles
1. Separate Game State and UI.
2. Separate Workpiece calculation data and visual Mesh.
3. Do not put Quest progression directly into NPC MonoBehaviours.
4. Separate Item definition from owned Item state.
5. Separate `TrueDimension`, `InstrumentReading`, and `RecordedMeasurement`.
6. Save DTOs contain no direct Scene Object references.
7. Use ScriptableObjects for static definition data where appropriate.
8. Runtime/save state is primarily serializable pure C# data.
9. Turning is only the first process; Workpiece/Quest/Save should not inherently mean “Turning only.”
10. Do not build an oversized generic framework for the MVP.

## 3. Core Data Models

### ItemDefinition
Suggested fields:
- `Id`
- `DisplayName`
- `Description`
- `ItemType`
- `Icon`
- `Stackable`
- `MaxStack`

Suggested ItemType:
- Material
- Workpiece
- MeasurementTool
- CuttingTool
- QuestItem
- Misc

### InventoryEntry
- `ItemId`
- `Quantity`
- `InstanceId` optional
- `RuntimeState` optional

Use instance-based state when two items with the same definition need independent runtime state.

### MaterialDefinition
MVP minimum:
- Id: `structural_carbon_steel`
- DisplayName: `기계구조용 탄소강`

Possible fields:
- `Id`
- `DisplayName`
- `Density` optional
- `MachiningDifficulty`
- `Description`

Do not invent unnecessary material simulation for the MVP.

### WorkpieceState
- `InstanceId`
- `MaterialId`
- `GeometryType`
- `GeometryData`
- `ProcessHistory`
- `IsFinished`
- `SourceItemId`
- `Orientation`

MVP GeometryType:
- `RoundProfile`

MVP geometry payload:
- `TurningRoundProfile`

## 4. Turning Geometry

### TurningProfile
- `TotalLength`
- `Segments[]`

### TurningSegment
- `StartZ`
- `EndZ`
- `Radius`

Recommended MVP representation:
segment-based profile, with sampling only where required for calculation/rendering.

Player-facing diameter:
`Diameter = Radius × 2`

Machining:
when a cutting tool traverses a Z range and enters inside the current radius, material in the affected region can be reduced.

Never add removed material back during normal gameplay.

## 5. Geometry vs Rendering
`TurningProfile` is authoritative machining data.

Visual Mesh:
- is generated from the profile
- updates when profile changes
- can use a render resolution independent of calculation precision
- supports round bar, steps, chamfer, and rotation visualization

MVP does NOT implement true physical cutting simulation.

## 6. Orientation / Coordinates
Coordinate concepts must remain explicit:
- Machine Coordinate
- Workpiece Local Coordinate
- Workpiece Orientation: `Normal` / `Reversed`

Lathe input uses Machine Coordinates consistently.

When a Workpiece is reversed, convert Machine ↔ Workpiece coordinates using Orientation. Prefer this to destructively reversing the authoritative geometry array/profile.

X geometry may internally represent Radius while player-facing dimensions represent Diameter. Put conversion responsibility in one clear place, e.g. `TurningCoordinateConverter`.

## 7. Lathe State Model
Minimum states:
- Idle
- Setup
- Ready
- SpindleRunning
- Machining
- Measuring
- Reclamping
- Finished

Typical flow:
Idle
→ mount Workpiece
→ Setup
→ mount Tool
→ Ready
→ spindle ON
→ SpindleRunning
→ Tool contact
→ Machining

Measurement:
- precision measurement is unavailable while spindle runs
- stop spindle before entering Measuring

Reclamp:
- stop spindle
- enter Reclamping
- reverse Workpiece Orientation
- update coordinate basis
- return to Ready

## 8. Quest Architecture
Quest state is explicit enum/state data, not an implicit set of NPC booleans.

Quest-state changes update through events:
- dialogue availability
- interactable objects
- UI objective
- scene/world state

Avoid polling Quest state every frame from NPC `Update()`.

## 9. Dialogue Architecture
MVP needs:
- speaker
- text
- next
- choices
- Quest-dependent branches
- optional event invocation

Suggested:
`DialogueNode`
- Id
- SpeakerId
- Text
- Choices[]
- NextNodeId
- EventId optional

`Choice`
- Text
- NextNodeId
- EventId optional

Long dialogue should be data-driven rather than embedded directly in C#.

## 10. Save DTO
Suggested `SaveGameData`:
- `SaveVersion`
- `CurrentSceneId`
- `QuestStates`
- `Inventory`
- `WorkpieceInstances`
- `MeasurementRecords`
- `Flags`

Flags include:
- `HasReceivedVernier`
- `PumpShaftInferred`
- `PumpRepaired`

Saved Workpiece:
- InstanceId
- MaterialId
- Geometry
- Orientation
- ProcessHistory
- IsFinished

## 11. Events
Useful domain events:
- QuestStateChanged
- ItemAdded
- ItemRemoved
- WorkpieceChanged
- MeasurementRecorded
- PartEvaluated
- PumpRepaired

Avoid an oversized global EventBus.

Choose a small, consistent mechanism appropriate to the project, such as:
- C# events
- explicit service references
- ScriptableObject event channels

## 12. Assembly / Namespace Policy
PHASE 0 must establish a consistent namespace and assembly policy before the codebase grows.

The source specification does not mandate exact namespace strings or `.asmdef` boundaries. Codex may propose a minimal scheme, but must:
- keep it simple
- avoid circular dependencies
- avoid premature assembly fragmentation
- report the chosen scheme before implementation
