# Extrude Tool UX Improvement Report

## Iteration 1 — True extrusion topology

**Implemented.** Extrude no longer changes the learning cube by moving its outer boundary like a resized cuboid.

The mesh rebuild now:

1. Identifies the selected face from its stable local axis and sign.
2. Preserves the selected face's original four-edge boundary loop.
3. Removes the original cap polygon.
4. Creates a translated duplicate cap along the face normal.
5. Connects the base and cap loops with four new side polygons.
6. Keeps the five unaffected cube faces intact.

At zero distance, the controller returns the original cube mesh and avoids zero-area side triangles.

## Why this matters

The result is now technically a face extrusion rather than a face move or object resize. The generated cap and four connecting sides provide the topology needed for later teaching overlays and eventual cross-tool editing.

## Iteration 2 — Persistent topology visualization

**Implemented.** Extrusion topology now has object-attached teaching overlays that remain aligned while the cube rotates:

- A blue four-edge loop identifies the preserved original boundary.
- An orange cap and orange cap loop identify the duplicated face.
- Four green-gray overlays identify the newly generated connecting sides.
- All overlays update continuously with the lever distance.
- Zero-distance extrusion avoids zero-thickness side overlays.

## Iteration 3 — Precise distance controls

**Implemented.** Extrude now presents scale-aware world millimetres while retaining the existing normalized internal mesh parameter.

- A signed value follows the lever: positive is `Outward`, negative is `Inward`, and zero is `On face`.
- `Precision` reduces lever sensitivity to 25%.
- Snap cycles through `Off`, `0.5 mm`, and `1 mm`.
- Exact signed numeric entry and `-1 mm` / `+1 mm` controls are available.
- Numeric entry is exact; snap applies to lever and step adjustments.
- Every input path uses the same effective clamp, keeping the mesh, lever, status, and field synchronized.
- At either limit, the value turns amber and the lever stops at the applied position.

## Iteration 4 — Guided target-distance exercise

**Implemented.** Opening the Extrude tutorial now starts a staged lesson:

1. The cube's stable local `+Y` face is highlighted green and the learner is asked to select it.
2. Selecting a different face produces retry guidance without changing the target.
3. A ghost drag cue demonstrates pulling the face-normal lever outward.
4. The learner is asked to reach exactly `+20.0 mm` by lever or numeric entry.
5. Completion validates both the selected face and signed effective distance.
6. The completion explanation identifies the duplicated cap and four generated side faces.
7. Retry resets only the active exercise extrusion and returns to face selection.

## Iteration 5 — Per-face state and history

**Implemented.** Each of the six stable cube faces (`-X`, `+X`, `-Y`, `+Y`, `-Z`, `+Z`) now owns an independent signed extrusion distance.

- Selecting a never-edited face loads `0 mm` without changing geometry.
- Selecting an edited face restores its stored distance.
- Editing one face preserves every other face's extrusion.
- The mesh rebuild starts from the base cube and applies all six stored face operations in stable face-ID order.
- `Reset Face` clears only the selected face.
- Undo/Redo stores complete six-face snapshots; a continuous lever drag creates one history step.
- Scaling clamps and rebuilds the stored millimetre values without clearing them.
- Replacing the placed learning object clears extrusion state and history for the new object.

## Iteration 6 — Merged extrusion boundary

**Implemented.** The six stored face operations are now evaluated as one axis-aligned solid instead of six independent polygon shells.

- Positive extrusion prisms are unioned with the base cube.
- Negative distances subtract the pushed-in face region.
- A coordinate grid derived from the six effective cap positions classifies solid cells.
- Only exposed cell boundaries are emitted into the rendered mesh.
- Internal faces between adjacent extrusion regions are removed.
- Adjacent extrusions now meet as one clean boundary rather than overlapping shells.
- Stable face state, signed millimetres, selection, Reset Face, Undo, and Redo are unchanged.

## Iteration 7 — Stable generated-face identity and picking

**Implemented.** The merged Extrude mesh now records semantic topology identity alongside its rendered triangles.

- The six source/moved-cap faces retain stable IDs for `-X`, `+X`, `-Y`, `+Y`, `-Z`, and `+Z`.
- Each generated side ID encodes its source face and one of that face's four tangent boundaries.
- Grid fragments created by merged adjacent extrusions share the same semantic side ID.
- Mesh-collider triangle indices map taps back to these topology IDs.
- Selecting a generated side highlights all fragments belonging to that semantic face.
- The status identifies the selected cap/source face or generated side.
- Extrude distance controls are deliberately disabled for generated sides until cross-tool operations can safely modify those polygons.

## Iteration 8 — Editable topology graph and first Inset migration

**Implemented.** Extrude boundary generation now also rebuilds a persistent in-memory topology graph:

- Coincident polygon positions become shared logical vertices with deterministic IDs.
- Undirected edges reference their two vertex IDs and all adjacent face IDs.
- Faces retain both a semantic face ID and a stable per-fragment graph ID.
- Collider triangles map to both the semantic face and exact graph face.
- Public topology counts expose the current vertex, edge, and face totals for validation.

Inset is the first consumer of this graph:

- Entering Inset preserves and rebuilds the current merged Extrude boundary.
- Tapping an Extrude cap or generated side resolves through the topology mapping.
- The selected semantic face is inset directly instead of recreating the original cube.
- All fragments of the selected semantic face receive consistent inset rings.
- The diagonal Inset handle anchors at the tapped generated geometry.
- Selection feedback highlights the affected topology.

Current limitation: the Inset result is still a live tool preview. Switching away rebuilds the persistent Extrude state, so Inset amount/history persistence is the next required migration.

## Recommended next iteration

**Completed in [InsetUXImprovementReport.md](InsetUXImprovementReport.md):** per-topology-face Inset state, Reset/Undo/Redo, deterministic Extrude-then-Inset rebuilding, percentage precision controls, snapping, exact entry, and lever feedback.

The next iteration is the guided Inset target exercise described in that report.

## Later iterations

1. Guided Inset target exercise.
2. Bevel, Knife, and Loop Cut migrations.
