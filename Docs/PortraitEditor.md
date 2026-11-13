# Portrait Editor

The portrait editor (`Tools/PortraitEditor`) is a standalone tool for working on the portrait art in `Portraits.json`. It loads the data the same way the game does, shows the portrait data, lets it be changed with the mouse or with fields, draws the changes on a set of preview characters as they're made, and copies any item as JSON to paste into a data file or a mod file. It never writes the data files.

## Starting it

Run `PortraitEditor.exe` from `Tools/PortraitEditor/bin/Debug/net9.0-windows`. Options are matched ignoring case, and anything else is ignored.

| Option | Does |
|---|---|
| `--no-mods` | Loads the data without the mods in the game's mod list. |
| `--data <folder>` | Loads the data files from the folder. |
| `--save-icon <file>` | Saves the editor's icon to the file and exits without showing the editor (see below). Problems are written to standard error, and the exit code is 1 if there were any. |
| `--icon-previews <folder>` | With `--save-icon`, also saves each size of the icon to the folder as a PNG file. |

Without `--data`, it loads Sub Corona's `Data` folder as found when the editor was built (not the copy next to the game, so it sees the files as soon as they change), or the `Data` folder next to the editor if that doesn't exist. Mods come from the game's mod list, as in the game.

## The window

* **Tree (left):** the colors, scales, offsets, and layers, with each layer's shapes. Edited items are bold with a dot. Layers and shapes that aren't drawn for the focused character are grey, and each color and shape has a swatch of its color for that character. The filter matches ids, and the check box shows only the layers and shapes drawn for the focused character (or forced, below). The buttons add a shape to the selected layer, duplicate, delete, or move the selected item.
  * **Canvas states:** the box beside each layer and shape sets whether it's drawn on the canvas. It only affects the canvas, never the previews or the data, and lasts until the editor closes (kept by layer id when the data is loaded again).
    * **Normal** (blank): drawn when the focused character meets its conditions.
    * **Forced** (📌, and the row is tinted): drawn even when the character doesn't meet them. A forced layer ignores its own condition and place, but its shapes still need their own conditions, so force a shape to see one that the character wouldn't have.
    * **Hidden** (⊘): never drawn.
    * A shape's own state wins over its layer's. Clicking the box goes to the next state, and Alt+click shows only that layer or shape (forcing it and hiding everything else).
    * Right-clicking a layer or shape sets its state, its layer's and every shape in it, or the layers the tree is showing (with the filter) or all layers. Setting several layers at once puts their shapes back to normal, so they follow their layers. The layers heading has the last two as well.
    * While anything is forced or hidden, a bar below the tree counts them, and its ✕ puts everything back to normal.
* **Canvas (left of center):** the focused character's portrait, with the selected shape's outline (dashed red when it isn't drawn on the canvas), its bounds, and its points. When the selected shape isn't drawn, a line below the canvas says why (which conditions the character doesn't meet, or that it's hidden), and **Show anyway** forces it.
  * Click a shape to select it. Mostly see-through shapes (such as lighting over the whole portrait) are passed over in favor of what can be seen. Ctrl+click selects the shape under the selected one, cycling through everything at that point, see-through shapes last.
  * Drag inside the bounds to move the shape, and drag the handles on the bounds to stretch it. Shift keeps it level, upright, or in proportion.
  * Drag a point to move it. Moving the end of a segment also moves the curve handles attached to it, and the start and end of a closed figure move together. Shift keeps it level or upright.
  * The zoom buttons and Ctrl+wheel zoom out by 20% at a time down to 20%, and in by 25% at a time up to 400%. Fit zooms so the whole portrait fits without scroll bars, which is also how the editor starts. Shift+wheel scrolls sideways. Grid shows a line every 20 units (darker every 100).
  * The fields above the canvas give the shape's bounds and the selected point, before the shape is stretched and moved, and changing them changes the path.
  * Shapes are edited in their own units, before their layer's scale and offset for the focused character, which the canvas applies so the shape is shown where it's drawn.
* **Editor (right of center):** the selected item. Shapes have fields for every property, colors for their color, mix, and amount, and every item can be edited as JSON. Color fields take a hex color or a portrait color's id; the swatch opens the portrait colors (as they are for the focused character) and a color picker. **Copy JSON** copies the item in the style of the data files, and **Revert** puts an edited item back as it was loaded.
* **Preview characters (right):** characters drawn with the data as it's edited, in as many columns as fit. Beside each portrait, ✏️ shows the character's place and properties (closing any other character's), 🎲 randomizes them, 👥 adds a copy of the character right after it (to make several similar characters), and 🗑️ removes the character. The place is one of: anywhere (only the layers drawn without a place), at home, at work in the workplace of their profession, or in a kind of quarter. The properties can be changed like on the new game screen. The selected character is shown on the canvas. The characters are kept between sessions, in `%LOCALAPPDATA%\OperaEtDies\PortraitEditor\Previews.json`; three random characters are only made the first time.

Changes are drawn a moment after they're made. If the data has problems, they're listed at the bottom; if it can't be read at all, the portraits keep showing the last version that could. Ctrl+Z and Ctrl+Y undo and redo changes (in a text box, they undo its typing instead).

When the data files or mod files change on disk (such as after running `Generate.sh`), they're loaded again. If there are edits, a bar asks first, since loading again discards them. F5 or **Reload** loads them again at any time.

The **?** button (or F1) lists the keyboard and mouse shortcuts. The window's position, whether it's maximized, the widths of the side panels, the tree's "only drawn" check box, the canvas grid, and the size of the preview portraits are kept between sessions in the registry, under `HKEY_CURRENT_USER\Software\Golden Anvil\Opera Et Dies Portrait Editor`. A window that would be off every screen opens in the middle of the screen instead.

## Paths

Paths are edited as text, so they keep their commands, whether each segment is relative, and their spacing. Only the segments whose numbers change are written again, with numbers rounded to one decimal place. Paths the editor can't take apart (but WPF can draw) can still be edited as text.

## The icon

`Tools/PortraitEditor/Icon.ico` is drawn by the editor from the portrait data, so it can be made again when the art changes. It's a close-up of one character's face (the character is set in `Model/IconRenderer.cs`), cropped around the drawn shapes of the `head` layer, in a rounded square with a frame, at 16, 20, 24, 32, 40, 48, 64, 128, and 256 px. It always uses the data without mods, and comes out the same each time. To make it again, close the editor, build it, and run (in Git Bash, which waits for it and shows its errors):

```bash
Tools/PortraitEditor/bin/Debug/net9.0-windows/PortraitEditor.exe --no-mods --save-icon Tools/PortraitEditor/Icon.ico --icon-previews <scratch folder>
```

Then look at the previews, especially the 16 and 32 px ones, and build again so the new icon is used.
