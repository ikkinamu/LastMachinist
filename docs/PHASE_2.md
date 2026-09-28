# PHASE 2 — Turning prototype

## Scope and authority
Phase 1 was confirmed by the user. Read AGENTS.md, ARCHITECTURE.md, LATHE_SPEC.md and IMPLEMENTATION_PLAN.md and inspected the existing data, assembly, scene and tests. No genuine conflict was found. LATHE_SPEC describes the complete MVP; the implementation plan assigns reclamping, chamfer and dial dragging to Phase 3 and measurement to Phase 4. Those features are not implemented here.

## Play in Unity
1. Open Assets/_Project/Scenes/Boot.unity and press Play.
2. If the mode is PAUSED after switching focus, click Resume.
3. Select x100 and Start spindle.
4. Click X - twice, then Z - several times. A reduced-diameter band and shoulder should appear.
5. Stop spindle and move X or Z: the tool should move without removing material.
6. Retract with X + and restart the spindle: removed material must stay removed.

X/Z keys select the mouse-wheel axis. 1/2/3 select x1/x10/x100. Mouse wheel and Up/Down move the selected axis; Space toggles the spindle. On-screen X/Z buttons address their own axis and all essential commands have buttons. Scrolling over the left control panel does not move the tool. Focus loss pauses and stops the spindle; Resume does not restart it. Restart Play Mode for a fresh test piece; there is no gameplay undo or refill button.

There is intentionally no exact-coordinate DRO or measurement answer. The HUD displays selected axis, scale, spindle, tool and mode only. The fixed inspection view is a prototype presentation, not 3D exploration.

## Implementation
- TurningProfile.RemoveMaterial splits intersected segments, only decreases radius, merges adjacent equal-radius intervals and notifies Changed only when material changes. The event is excluded from serialized state.
- LatheController is pure C#. Commands pass through LatheInputRouter, and only the controller authorizes cutting. No workpiece, stopped spindle, missing/noncutting tool, inactive context or finished state prevents removal. Spindle start at existing contact cuts the contacted band. X radial moves use the deepest endpoint; Z moves sweep the finite-width cutting band, avoiding missed material at coarse steps.
- A protected grip interval remains inaccessible; reversed mounting is rejected until Phase 3. Tool changes require a stopped spindle. X is radius internally; geometry/presentation use the existing diameter conversion when needed.
- TurningMeshBuilder renders the profile as a capped rotational mesh with shoulder faces. TurningWorkpieceView subscribes to profile changes and owns/disposes its generated mesh. Calculation segments and angular render resolution remain independent.
- RoughingToolDefinition and LathePrototypeSettings hold static configuration. The roughing tool's immutable runtime snapshot and workpiece state are separate from assets.
- LathePrototype composes the isolated test stock, primitive machine view, input actions and HUD. The existing Input System package is referenced by the runtime assembly. No package version changes are required. GameBootstrap uses the scene's settings reference to enter the prototype.

## Files
Modified: Scripts/Core/GameBootstrap.cs, Scripts/LastMachinist.Runtime.asmdef, Scripts/Machining/Turning/TurningProfile.cs, Scenes/Boot.unity.

Created under Assets/_Project: Scripts/Machining/Turning/{LatheCommands,LatheController,RoughingToolDefinition,TurningMeshBuilder,TurningWorkpieceView,LathePrototypeSettings,LathePrototype}.cs; Tests/Editor/LatheTests.cs; Data/PrototypeLatheSettings.asset; Data/Tools/PrototypeRoughingTool.asset; Art/Materials/Prototype{Stock,Tool,Machine}.mat; associated Unity metadata. Documentation: this file and test results.

## Placeholders and limitations
Stock dimensions, cutting width, grip length, movement step, wheel normalization and visual rotation speed are explicitly configurable prototype values. They are not final machining constants, wear values or tolerance decisions. Test stock is not quest inventory. Primitive machine geometry, faceted shading, an English prototype HUD and immediate-mode UI are temporary. There is no cutting sound, tool wear, coolant, quest, stock consumption, disk save, reclamp, chamfer, vernier or dial dragging. This is not MVP completion.

## Validation
Unity 6000.6.0f1 EditMode tests passed in the isolated validation copy: 18 total, 18 passed, 0 failed. This includes the six Phase 1 data tests and twelve Phase 2 turning tests covering command routing, scale/axis movement, cutting preconditions, persistent removal, protected grip, interval merging and mesh refresh. Results are preserved in `docs/PHASE_2_TEST_RESULTS.xml`.

The Windows player build completed successfully. A headless smoke run emitted `PHASE2_SMOKE_PASS: geometry changed, shoulder created, material retained.` and exited successfully. A second rendered run booted the prototype successfully; its automated screenshot was black because the player was launched without an interactive window, so visual appearance should still be checked in the open Unity Editor. The original repository's existing settings were not modified by validation.
