# Inset Tool UX Improvement Report

## Iteration 1 — Persistent topology operation

**Implemented.** Inset now operates on the editable topology produced by Extrude and stores an independent value for each stable semantic face.

- Selecting an untouched topology face loads `0%`.
- Selecting an edited face restores its stored percentage.
- Caps, source faces, and Extrude-generated sides can be inset.
- Editing one semantic face preserves stored Inset values on other faces.
- Rebuilding starts with the persistent Extrude solid and applies every stored Inset operation deterministically.
- `Reset Face` removes only the selected face's Inset.
- Undo/Redo stores the complete per-face Inset dictionary; one continuous lever drag is one history step.
- Replacing the learning object clears Inset state and history.

## Precision and feedback

- The diagonal lever remains the primary direct-manipulation control.
- `Precision` reduces drag sensitivity to 25%.
- Snap cycles through `Off`, `0.5%`, and `1%`.
- Exact numeric percentage entry and `-1%` / `+1%` controls are available.
- Values clamp consistently from `0%` to `45%`.
- The value beside the lever and numeric field turn amber at the maximum.

## Iteration 2 — Guided Inset exercise

**Implemented.** Opening the Inset tutorial now provides a staged exercise:

1. A positive `+Y` Extrude cap is prepared when the prerequisite extrusion does not already exist.
2. The stable `+Y` cap is highlighted green at its effective extruded position.
3. The learner must select semantic topology face ID `3`; generated side selections produce retry guidance.
4. A ghost drag cue demonstrates the diagonal Inset lever.
5. The learner is asked to reach exactly `20.0%` by lever or numeric entry.
6. Completion validates both the semantic target face and effective stored Inset percentage.
7. The explanation identifies the smaller inner face and surrounding boundary ring.
8. Retry resets only the target Inset and preserves the prerequisite Extrude cap.

## Current limitation

Inset state persists when switching tools, but Bevel, Knife, and Loop Cut have not yet been migrated to consume the combined Extrude-plus-Inset topology.

## Recommended next iteration

Migrate Knife onto the combined Extrude-plus-Inset topology. Begin with stable face selection and an explicit two-point cut interaction rather than the current global vertical-drag angle control.
