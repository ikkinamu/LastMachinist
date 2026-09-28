# PHASE 1 — Core data

## Scope and prerequisites
The user confirmed Phase 0 runs. Read AGENTS.md, MVP_SPEC.md, ARCHITECTURE.md and IMPLEMENTATION_PLAN.md; no Phase 1 conflict was found. Authority remains as defined in AGENTS.md. This change implements only Phase 1. No scenes, bootstrap behavior, render settings, mesh, controls, quest flow, measuring UI or disk save service are changed.

## Files and assembly policy
- Scripts/Core/DefinitionIds.cs centralizes the specified material ID.
- Scripts/Inventory/ItemDefinition.cs holds static authoring data; InventoryState.cs holds owned item IDs, aggregate quantities and unique instance IDs.
- Scripts/Machining/Core/MaterialDefinition.cs holds static material data, with the prescribed structural carbon steel asset under Data/Materials.
- Scripts/Machining/Core/WorkpieceState.cs holds per-instance runtime state, geometry kind/payload and orientation.
- Scripts/Machining/Turning/TurningProfile.cs represents contiguous round segments in millimetres, internally using radius. Diameter is twice radius.
- Scripts/Save/SaveGameData.cs provides a serializable data container, without file IO or Unity scene references.
- Tests/Editor/CoreDataTests.cs checks quantities, independent instances, geometry, JSON round trips, validation and absence of Unity object references in the save graph.

Namespaces follow the Phase 0 LastMachinist directory policy. One LastMachinist.Runtime assembly now covers all project runtime scripts (including the unchanged bootstrap); one Editor-only test assembly references it. This is the concrete testing need anticipated by Phase 0, not a per-domain assembly split.

## Data contract
Static definitions are ScriptableObjects. Saveable state uses serializable pure C# classes, public fields and lists supported by JsonUtility. Resolve definition IDs outside the DTO graph. Inventory entries with InstanceId refer to independently owned objects; workpieces can also exist outside inventory, e.g. mounted on a machine in a later phase. Inventory aggregates fungible quantities; MaxStack is authoring metadata for future stack presentation, not a slot limit in this model.

Call Validate after deserialization and before consuming loaded state. Unsupported save versions, invalid geometry and duplicate instance IDs are rejected. Fields remain serializer-friendly data; they are not a machining API. Segment boundaries must match exactly and cover [0, TotalLength], with nonnegative finite radii. Share boundary values when constructing adjacent segments. Zero radius allows later representation of fully removed material; no removal operation is implemented here.

Orientation is persisted as Normal/Reversed and never rewrites the profile. Machine coordinate conversion and reclamping belong to Phase 3. Geometry uses a small tagged payload, leaving item/inventory/save identity independent of a specific machining process.

## Verification
Run Unity Test Runner > EditMode > LastMachinist.Tests.CoreDataTests. Tests include the real Unity JsonUtility serializer. Test shaft dimensions and IDs are test fixtures only, not a broken-shaft asset or an evaluation target. No wear/tolerance/movement values are invented. Material identity/name follow the specification; optional material simulation values are omitted.

This phase does not make gameplay visible. Phase 2 must be separately authorized.

## Verification status (2026-09-12)
Applied to the LastMachinist repository. Unity 6000.6.0f1 EditMode tests passed: 6 total, 6 passed, 0 failed. Results are preserved in docs/PHASE_1_TEST_RESULTS.xml. These tests ran against identical Phase 1 source files in a separate minimal Unity project using the installed Test Framework 1.8.0, preserving the user's open editor session. Unity JsonUtility round-trip verification passed, including geometry, identity, inventory, completion state, process history and reversed orientation. The earlier licensing failure was resolved by running the validation under the licensed account.

All new C# sources also compiled against the installed Unity/NUnit assemblies with Roslyn. Five domain checks and an additional System.Text.Json round trip passed in a .NET harness. Patch applicability and whitespace checks passed. Full gameplay/build testing is not part of this data-only phase; no scene or gameplay changes were introduced. Phase 1 data acceptance criteria are met. Phase 2 remains unstarted.
