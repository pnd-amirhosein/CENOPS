# Glasswork 2.8.1 — Note block editor cleanup

Based on Glasswork 2.8. No database changes.

- Removed the block kind chips (عنوان/متن/کد) and ellipsis/action menu.
- One inline delete icon per block.
- Drag handle appears outside the left edge on row hover/focus.
- Native HTML5 drag/drop is installed via delegated JavaScript, including `dataTransfer.setData` to make the gesture work across browsers.
- Dragging above or below the midpoint of a block inserts before or after the target.
- The actual model list is reordered in the Blazor component and persisted with the existing Notes Save action.

## Test
1. Create a note containing three distinct blocks, hover a block, and drag it above/below another one.
2. Save and reopen the note to confirm the reordered blocks persisted.
3. Delete a block using its icon; confirm only that block disappears.
4. Check that the block controls do not obscure the content on narrower screens.

No EF migration required.
