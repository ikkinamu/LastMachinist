# PHASE 0 — Project foundation

## Authority and scope
AGENTS.md > MVP_SPEC.md > ARCHITECTURE.md > relevant domain specs > IMPLEMENTATION_PLAN.md > preserved source. MASTER_SPEC.md is the index and repeats this ordering. No conflict was found in the Phase 0 requirements. Genuine refined-document conflicts require stopping and reporting.

Phase 0 has no prior-phase prerequisite. The existing Unity project was inspected before implementation. Preserve its pinned Editor version (6000.6.0f1), packages, render pipeline, template assets and settings. This does not certify the installed version's LTS status; changing the editor baseline is outside this skeleton change.

## Folder and namespace policy
Project-owned assets live in Assets/_Project. Scripts/Core contains the startup composition point. Namespaces start with LastMachinist and follow the relevant script directory, beginning with LastMachinist.Core. No asmdef is needed for one runtime component: use Assembly-CSharp initially, introducing assembly boundaries only when a concrete dependency or test need warrants them.

Data/Items, Materials, Tools, Quests and Dialogue reserve locations for future static definition assets. These are empty locations, not implemented definitions. Prefabs, Scenes, Art, Audio and Tests reserve shared asset locations. Create other recommended script directories only when their phase needs them. Empty folders are represented by Unity folder metadata.

No inventory, item/material definitions, workpiece geometry, orientation, save DTOs, quest, measurement or machining logic is implemented.

## Startup
Open Assets/_Project/Scenes/Boot.unity and enter Play Mode. The scene copies the existing template camera, lighting and volume setup and adds one scene-local GameBootstrap. The Console should show `Last Machinist: PHASE 0 boot ready.` once per scene startup, with no blocking errors. The empty sky/background is an intentional Phase 0 placeholder; it is not a title screen or gameplay UI.

Boot is first in the default build scene list. The original SampleScene entry and asset remain intact. No global singleton, service container, scene transition framework or persistent state is added.

## Acceptance checks
- Inspect the scene for a valid GameBootstrap reference and camera.
- Open with the pinned Unity editor and check import/compilation for errors.
- Run Boot in Play Mode and confirm the startup message without exceptions.
- Check the final diff remains limited to the skeleton, its documentation and startup scene registration.

Unity execution must be verified before declaring Phase 0 fully accepted. Do not begin Phase 1 automatically.
## Verification result (2026-09-12)
Unity 6000.6.0f1 imported and compiled the project and completed a Windows x64 player build successfully. The built player ran in batch mode with graphics disabled and emitted the expected PHASE 0 boot message without a runtime exception. Rendering and interactive Editor Play Mode were not visually checked. Unity-generated changes to existing rendering settings were restored after verification; the final settings change is only Boot scene registration. Phase 1 remains unstarted.

