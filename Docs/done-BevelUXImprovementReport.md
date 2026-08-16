# Bevel Tool UX Improvement Report

## Current interaction

The Bevel lesson already supports the essential learning loop:

1. Activate Bevel.
2. Tap one edge, or tap a face to select its four boundary edges.
3. See selected edges highlighted in orange.
4. Pull the outward lever to change width.
5. Read the width in millimetres.
6. Reset the selection or use Undo/Redo.

Widths are stored per stable cube edge. Editing one selection preserves bevels already applied to other edges. The existing mesh generation, scale-aware millimetre conversion, clamping, and validation should remain the foundation.

## Highest-priority improvements

### 1. Make selection mode explicit

**Implemented:** The Bevel lesson now provides an `Edge | Face` selector, mode-specific status text, four-edge face feedback, a short green confirmation pulse, orange active edges, blue inactive edges, and empty-space deselection.

Show a compact `Edge | Face` selector in the Bevel lesson. Automatic nearest-element selection is useful, but on a phone it can be unclear whether the learner selected one edge or four face edges.

Recommended feedback:

- Status text: `Edge selected` or `Face selected — 4 edges`.
- Orange highlight only for active edges.
- Brief green confirmation pulse after selection.
- Tap empty space to deselect.

### 2. Improve lever precision

**Implemented:** Normal and 25% Precision lever sensitivity, cycling `Off / 0.5 mm / 1 mm` snapping, exact numeric width entry, and a camera-facing millimetre label beside the lever are now available. Width entry and gesture changes remain undoable.

Keep the lever as the primary direct-manipulation control, but add precision behavior:

- Normal drag for regular adjustment.
- A `Precision` toggle that reduces sensitivity, for example to 25%.
- Snap options such as `Off`, `0.5 mm`, and `1 mm`.
- A numeric width field for entering an exact thesis exercise value.
- Display the value beside the lever so the learner does not need to look away from the cube.

### 3. Clarify limits and invalid input

**Implemented:** All input paths now clamp the stored width before rebuilding. At the maximum, the on-model value turns amber and displays `Maximum for this cube`; the workspace status shows the same message, and the lever stops at the effective clamped position.

The clamp currently protects the mesh, but the learner needs visible feedback when it is reached.

- Change the value label to amber at the maximum width.
- Add a small `Maximum for this cube` message.
- Stop the lever visually at the clamped position.
- Never allow the displayed requested value to disagree with the effective applied value.

### 4. Add tutorial guidance

**Implemented:** The Bevel lesson now guides the learner through Edge mode, validates a glowing stable target edge, animates a ghost drag cue from the real lever, requires a 5.0 mm result, explains Bevel Width on success, and offers an isolated retry that preserves unrelated bevels.

The lesson should teach Blender concepts, not only provide a working tool.

Suggested sequence:

1. Highlight the edge-selection control.
2. Ask the learner to select a specified edge.
3. Animate a ghost hand pulling the lever once.
4. Ask for a target such as `5 mm`.
5. Validate the selected edges and effective width.
6. Explain that Blender's Bevel Width changes the distance of the cut from the original edge.
7. Let the learner retry without resetting unrelated bevels.

### 5. Add clear transform context

**Implemented:** The workspace now visually separates `VIEW CONTROLS` from `BEVEL CONTROLS`, highlights only the active Move/Rotate/Scale/Spin state, prevents conflicting Spin modes, preserves lever rotation locking and all six orientation shortcuts, and adds `Reset View`.

Move, Rotate, Scale, and Spin are viewing aids during the Bevel tutorial, but they can be mistaken for modeling steps.

- Visually separate `View controls` from `Bevel controls`.
- Keep Rotate active only while its toggle is highlighted.
- Disable object rotation while the lever endpoint is held.
- Retain the six orientation shortcuts.
- Add a `Frame Object` or `Reset View` shortcut.

## Blender-parity features for later phases

These features would move the lesson closer to Blender, but should follow the core mobile interaction improvements:

- Bevel segments for rounded rather than single-flat chamfers.
- Profile control.
- Affect mode: Edges versus Vertices.
- Clamp Overlap toggle with a visible explanation.
- Angle, Weight, and Vertex Group limit methods.
- Harden Normals and shading comparison.
- Multiple selection with additive/remove selection gestures.
- A Blender-style operation panel showing Width, Segments, Profile, and Clamp.

## Recommended next implementation order

1. Edge/Face mode and unmistakable selection feedback.
2. Lever value label, snapping, and precision mode.
3. Clamp-limit feedback.
4. Guided target-width tutorial with validation.
5. Reset View and stronger active-tool indication.
6. Segments and Profile after the base lesson is validated with learners.

## Usability tests

Test each item on the actual Android phone:

- Select a front-face edge without accidentally selecting the face.
- Select the face center and confirm exactly four edges highlight.
- Reach 3 mm, 5 mm, and 8 mm without using `+1 mm` repeatedly.
- Rotate to every side and select an edge within two attempts.
- Hold the lever and verify rotation never starts.
- Bevel two different edges and confirm their individual widths persist.
- Undo and Redo both single-edge and four-edge face operations.
- Reach the maximum width and understand why the lever stops.
