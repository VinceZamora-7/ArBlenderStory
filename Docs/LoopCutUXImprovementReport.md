# Loop Cut Tool UX Improvement Report

## Iteration 1 — Combined-topology loop cuts

**Implemented.** Loop Cut now consumes the combined Extrude, Inset, and Knife topology instead of rebuilding the original cube.

- Tapping the object creates an explicit loop preview.
- Axis cycles through local `X`, `Y`, and `Z` and remains correct when the object rotates.
- Segment count supports one to three evenly spaced cuts.
- Slide moves the cut planes from `-45%` to `+45%` between neighboring boundaries.
- Each plane splits every intersected topology polygon and preserves its semantic face identity.
- A valid loop requires at least three intersected faces for every requested segment.
- Preview edges are green when valid and red when invalid.
- Vertical whole-screen dragging no longer controls Loop Cut.
- Reset, Undo, and Redo preserve the complete loop configuration.
- New learning objects start with clean Loop Cut state and history.

## Guided exercise

The Loop Cut tutorial now asks the learner to:

1. Tap the model to create a preview.
2. Find a green valid-loop axis.
3. Select local `Y`.
4. Create two segments.
5. Keep Slide at `0%`.
6. Learn that Loop Cut propagates edge rings through connected topology and that Slide moves them between neighboring boundaries.

## Iteration 2 — Edge discovery and quad-ring traversal

**Implemented.** Loop Cut discovery now begins from the topology edge nearest the learner's tap on the exact graph face.

- The selected edge direction chooses the initial local cut axis.
- Traversal crosses each quad through its opposite edge.
- Edge-to-face adjacency advances the traversal into the next connected face.
- A ring is valid only when traversal returns to the starting edge.
- The discovered ring is drawn in blue; incomplete traversal is orange.
- Red endpoints show where traversal terminates.
- The panel reports the exact result: closed quad ring, open boundary, pole, missing adjacency, or non-quad face.
- The planar cut validity check now also requires a closed discovered ring.
- Ring discovery remains object-local and follows cube rotation.

This uses tap-near-edge interaction on mobile rather than desktop hover, while retaining Blender's topology-driven opposite-edge traversal concept.

## Iteration 3 — Span-based cut application

**Implemented.** Loop Cut application no longer uses a global axis-aligned plane.

- Each traversed quad stores its exact graph face ID, entry edge, and opposite edge.
- Opposite-edge endpoint orientation is aligned to prevent twisted interpolation.
- Segment positions are interpolated directly along both edges of every quad.
- Each interpolated pair defines the local coplanar split for that quad.
- Segment count applies multiple ordered splits per traversed face.
- Slide changes the interpolation parameter between the neighboring edges.
- Preview lines use the same interpolated points as the mesh operation.
- Rotated and non-axis-aligned quad rings now receive cuts that follow their own topology.
- Cycling X/Y/Z re-discovers a matching edge direction on the selected graph face; it no longer changes an unrelated global plane.

## Iteration 4 — Direct slide lever and precision controls

**Implemented.** A Loop Cut slide lever is now attached to the first discovered quad span.

- The lever anchor follows the effective first loop segment.
- Its drag axis follows the average direction of the neighboring quad edges.
- Pulling the endpoint changes the same interpolation value used by every ring span.
- The lever moves continuously with the applied slide and segment configuration.
- Holding the endpoint turns the lever blue and locks Move/Rotate/Scale/Spin interaction.
- `Precision` reduces drag sensitivity to 25%.
- Snap cycles through `Off`, `1%`, and `5%`.
- Signed numeric entry supports exact slide targets.
- All input paths clamp consistently to `-45%` through `+45%`.
- The value follows the lever and turns amber at either limit.
- Axis rediscovery rebuilds the pre-LoopCut graph first, preserving valid face lookup after a preview has split polygons.

## Iteration 5 — Preview, confirm, slide, and commit

**Implemented.** Loop Cut now has an explicit Blender-inspired state machine:

1. `Preview` — tapping near an edge discovers and displays a temporary ring without splitting the mesh.
2. `Confirm Cut` — validates the ring, creates one Undo entry, applies the topology, and enters Slide mode.
3. `Sliding` — enables the on-model lever, snap, precision, buttons, and numeric slide entry.
4. `Finish Slide` — commits the final topology and removes the lever.
5. `Cancel` — removes an uncommitted preview, or returns Sliding to Preview with the cut removed.

Additional behavior:

- Axis and segment changes are accepted only during Preview.
- Slide changes are accepted only during Sliding.
- Preview adjustments do not create Undo history noise.
- The first confirmation creates the history boundary between no cut and committed topology.
- The panel displays the active phase and changes the confirmation button label contextually.
- The tutorial now teaches preview, two segments, confirmation, `+10%` sliding, and final commitment.

## Recommended next iteration

Add multi-operation topology history so several independently committed Loop Cuts can coexist, be selected again, and be edited or removed individually.
