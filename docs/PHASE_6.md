# PHASE 6 — Quest and Dialogue

## Scope
Adds the explicit `quest_pump_01` state model, gated progression flags, stock selection transition and data-driven dialogue nodes. Full machining/evaluation integration and save/load remain deferred.

## Implementation
- `QuestStateMachine` owns the ordered quest states and emits `StateChanged` events.
- Vernier acquisition and shape inference are explicit gated actions.
- Selecting material consumes one stock; completion sets `PumpRepaired`.
- `DialogueNode`, `DialogueChoice` and `DialogueCatalog` keep speaker/text/branch data outside NPC behaviours.

## Verification
`Phase6Tests` covers ordered transitions, event emission, vernier/inference gates, stock consumption, completion and dialogue lookup. Runtime source compiles against Unity assemblies with the existing serialized-field warning only.

## Boundaries
Dialogue text is represented by data objects; no NPC scene, quest UI, scene transition, save/load or Phase 7 subsystem integration was added.

The v0.3 follow-up adds `DialogueCatalogDefinition` and `Data/Dialogue/Day1Dialogue.asset` so the DAY 1 Korean dialogue is authorable data rather than NPC code.

Startup now presents title → DAY 1 story → workshop introduction before enabling the lathe prototype.
