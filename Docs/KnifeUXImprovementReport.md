# Knife Tool UX Improvement Report

## Iteration 1 — Topology migration and two-point cut

**Implemented.** Knife now consumes the combined persistent Extrude-plus-Inset topology instead of rebuilding the original cube.

- A tap resolves through the MeshCollider triangle to a semantic and exact graph face.
- The first tap places a visible red Point A marker.
- The second tap must land on the same semantic topology face.
- Two sufficiently separated points define the cut direction.
- The selected polygons are split into two valid coplanar polygons along that line.
- The cut persists by semantic face ID when Knife is reopened.
- A red line visualizes the committed cut.
- `Reset Cut` removes only the selected face's Knife operation.
- Vertical screen dragging no longer controls Knife angle.
- Extrude and Inset state remain intact beneath the cut.

## Iteration 2 — History, snapping, and guided exercise

**Implemented.** Knife now includes modeling-oriented snap and history controls:

- Snap cycles through `Off`, nearest `Edge`, and nearest `Vertex` on the exact graph face.
- Point positions are snapped in object-local topology space, so cube rotation does not affect the result.
- Committing or resetting a cut creates an Undo snapshot.
- Undo/Redo restores the complete semantic-face cut dictionary.
- Starting a new object clears Knife state and history.

The Knife tutorial now:

1. Prepares and preserves a stable positive `+Y` Extrude cap.
2. Displays two green boundary targets on that cap.
3. Requires Point A at the left target.
4. Animates a ghost cue from A to B.
5. Requires Point B at the right target on the same semantic face.
6. Restarts safely after wrong-face input.
7. Validates both local-space endpoints and the target semantic face.
8. Explains that the operation inserts boundary vertices and divides the cap into two faces.
9. Retry clears only the target Knife cut and preserves prerequisite topology.

## Current scope

Knife currently supports one straight through-face cut per semantic face. The two taps define its direction, and the split extends to the selected polygon boundaries. Arbitrary multi-segment paths and interior-only endpoint topology remain future work.

## Recommended next iteration

Migrate Loop Cut onto the combined topology with explicit loop preview, valid-loop detection, segment count, slide controls, history, and tutorial guidance.
