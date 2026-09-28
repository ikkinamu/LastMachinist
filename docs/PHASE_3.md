# PHASE 3 — Reclamp and chamfer

## Scope
PHASE 2 was completed and the user requested the next phase. This phase implements only the PHASE 3 items in IMPLEMENTATION_PLAN.md: explicit machine/workpiece coordinate conversion, spindle stopped reclamping, opposite-side access, a minimal chamfer feature/tool, and safe on-screen X/Z dial interaction. Measurement, quest, save IO, advanced tool geometry and freeform CAD are not included.

## Implementation
- `TurningCoordinateConverter` maps machine Z to local workpiece Z using `Normal` or `Reversed` orientation. Intervals are normalized after conversion, so control direction stays consistent while the authoritative profile remains unchanged.
- `LatheController` accepts optional `ChamferToolState`, exposes orientation/tool identity, and routes `SelectTool`, `Reclamp` and `ApplyChamfer` through `LatheInputRouter`. Reclamp is ignored while the spindle runs and toggles the saved orientation without rewriting segments. Chamfer is allowed only with the chamfer tool, stopped spindle and an end position; its small `ChamferFeature` is evaluator-readable and profile change notifications update the view.
- `LatheDialMath` uses `Mathf.DeltaAngle` to avoid ±180 degree jumps. The prototype HUD now has X/Z draggable dial regions; their pointer angle deltas go through the same router command as keyboard and wheel input. Tool selection and end chamfer are available from the HUD.
- The HUD explicitly exposes `Select roughing tool`, `Select chamfer tool`, `Mount selected tool`, `Remove current tool`, `Apply end chamfer` and `Reclamp / reverse workpiece`. These controls are enabled only with the spindle stopped, and the current tool mounting state plus Normal/Reversed orientation are shown beside them.
- `ChamferToolDefinition` and `PrototypeChamferTool.asset` are static authoring data. The existing prototype settings reference the asset. No final chamfer width or angle is claimed; the one millimetre asset value is a temporary playtest placeholder.

## Verification
The complete PHASE 2 source set plus PHASE 3 source set compiles with the installed Unity 6 assemblies using Roslyn; only the existing serialized `GameBootstrap.latheSettings` field warning is emitted. `git diff --check` passes. The prior validation run passed all 18 PHASE 2 tests and the player smoke test.

The new PHASE 3 Unity EditMode run could not complete in this session: Unity's licensed batch process terminated during project import/licensing after the usage limit was reached, before producing a test result XML. This is an environment limitation, not a claimed test pass. The runtime source was compiled against Unity assemblies, and a standalone PHASE3_HARNESS_PASS check covered coordinate mapping, stopped-spindle reclamp, unchanged profile orientation, chamfer creation and dial wrap handling. Run the EditMode suite in Unity after it finishes importing; `Phase3Tests` covers the full Phase 3 set, including opposite-side cutting and invalid feature rejection.

## Placeholders and boundaries
Primitive machine graphics, immediate-mode HUD and temporary English labels remain placeholders. Chamfer is represented as a simple end feature for later evaluation; the rotational mesh currently reflects the profile and feature notification, while a detailed chamfer slope is intentionally deferred. There is no measurement, vernier, quest, save, material consumption, tool wear, coolant or gameplay undo. PHASE 4 remains unstarted.
