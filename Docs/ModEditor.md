# Mod Editor

The mod editor (`Tools/ModEditor`) is a standalone tool for writing mods (see [Mods.md](Mods.md)). It shows a mod folder's files as a tree of their pieces, lets each piece be edited as JSON, moved, or copied between files, shows the game's own data to copy from, checks the mod as it's edited by loading the game data with it, and runs the game with the mod. Changes are kept in the editor until they're saved.

## Starting it

Run `ModEditor.exe` from the game's build folder, `OperaEtDies/bin/Debug/net9.0-windows`. The editor and the Portrait Editor are built into the same folder as the game, so the editor can find the game, and both use the game's `Data` folder there. It takes no options.

It doesn't open a mod on its own. The start page has **Open Mod Folder…** and the mod folders opened recently (✕ takes one off the list). Opening an empty folder starts a new mod.

## The window

* **Toolbar:** open another mod folder (Ctrl+O), save (Ctrl+S), reload the mod and the game data from disk (F5), undo (Ctrl+Z) and redo (Ctrl+Y), and run the game (Ctrl+R, see below). On the right, it says why the last action couldn't be done, such as a piece dropped where it can't go. The title starts with ● while there are unsaved changes, and opening another folder, reloading, or closing the editor asks whether to save them first. When the mod's files change on disk (such as when they're edited in another program), they're loaded again, keeping the selection, and a message says so. If there are unsaved changes, a bar at the bottom asks first: **Load and discard my changes**, or **Keep my changes**, after which saving only writes the files changed here, keeping the other changes on disk. The editor's own saves, and changes that leave the files as they were, are passed over.
* **Filter** (Ctrl+F): shows only the items in both trees whose names or details have the text, and the items they're in.
* **Mod tab:** the mod folder, its subfolders and files in the order the game applies them, each data file's sections with their items or settings, and each operations file's operations (each shown as its `op`, the ids of the items it works on, and its `path`; see below). Items show their `title` if they have one, and settings show short values. Files that can't be read, or that problems were found in, are red, and so are the folders they're in.
* **Game Data tab:** the game's data as the mod leaves it: its sections, with their items or settings. It can be looked at and copied, and the mod can replace or remove its pieces (see below), but it can't be changed here. What the mod does is marked (see **What the mod changes** below), and **Only what the mod changes** shows just those pieces and the sections they're in.
* **Editor (right):** the selected item, in a form, as JSON, or as the changes it makes or has made to it (the tab is kept between sessions). See **The form** and **What the mod changes** below. In the **JSON** tab, changes apply as you type, once the JSON is valid; until then, the line below the editor says what's wrong. Selecting a file edits its whole text, which is kept exactly as typed (comments included), even while it isn't valid. Changes to one item, in either tab, are undone all at once.
* **Problems (bottom):** what the game reports when it loads its data with the mod applied, exactly as it would on starting, checked a moment after each change (including unsaved ones). Double-click a problem in a mod file to select the file. Right-click (or Ctrl+C) copies the selected problems as text, and **Copy All** copies them all.

## Organizing

Files can be named and nested in folders however is convenient, since file names don't matter to the game; only the order of their paths does.

* **New files and folders:** the buttons above the tree, and the menu of any item, add a data file (`{}`), an operations file (`[]`), or a folder, in the selected folder (or the folder of the selected file). File names get `.json` added if they don't have it.
* **Adding pieces:** **+ Section** adds a section to the selected data file, offering the game data's section names, with the kind (list or settings) it has there. **+ Item** (Ins) adds, after the selected piece, an item with a new id to a list section, a setting (asking for its name) to a settings section, or a `set` operation to an operations file.
* **Moving:** drag an item onto another. A piece dropped on a file or section goes at the end of it, and one dropped on another piece goes before or after it, by which half of it the mouse is over (a line shows where). An item or setting goes into the section of the same name, which is added to a data file that doesn't have it; a section is joined with the file's section of the same name. Operations only go into operations files. Files and folders dropped on a folder move into it. Hold Ctrl to copy instead. **Move To** in an item's menu moves a piece to the end of another file, and Alt+Up and Alt+Down move it within its list.
* **Copying and reuse:** Ctrl+C, Ctrl+X, and Ctrl+V copy, cut, and paste pieces, which keep the section they belong to. Pasting goes after the selected piece, or at the end of the selected file or section. JSON copied from elsewhere is pasted as operations into an operations file, as items (an object, or a list of them) or settings (an object) into a section, or as sections (an object) into a data file. Ctrl+D duplicates a file, item, or operation. A copied item gets a new id (such as `hairCopy`) if the section it goes into already has its id.
* **From the game data:** right-click a piece of the game data (a section, an item with an id, or a setting) and choose **Replace** to add a `replace` operation for it to the mod, with its current value, so it can be changed while the old value is easy to see. The operation is selected in the Mod tab, which is shown. **Remove** adds a `remove` operation instead, without changing the selection, and a message in the bottom corner says where it went. Either goes after the operation last selected in the mod, or at the end of the operations file last selected, or else at the end of the first operations file (`Operations.json` is added if there isn't one). A piece of the game data can also be dragged onto the mod (it's always copied) or copied. **Copy Path** (Ctrl+Shift+C) copies the path that finds a piece from an operation, such as `properties[hairColor]`, for the game data's pieces and the mod's own.
* **Renaming and deleting:** F2 renames the selected file or folder, and Del deletes the selected item. A section left empty by moving or deleting its last item or setting is removed.

A piece changed in the editor is written back to its file in the style of the data files, so the rest of the file loses its comments and layout. Editing the file's whole text instead keeps them. Saving writes only the files that changed, and sends deleted files and folders to the recycle bin.

## The form

The **Form** tab edits the selected item, setting, or settings section, or an operation, with a field for each property the game reads there. The fields come from the classes the game reads the data into, so they follow the game as the data format changes (see [DataFiles.md](DataFiles.md) for what each one means). A whole file, a folder, or a whole list section has no form; edit those as JSON.

* Each field's name is on the left, and every editor starts at the same distance from the left, however deeply its field is nested (a nested field's name has less room). Required properties are bold. A name that lights up under the mouse can be right-clicked for **Copy Path**, which copies the path that finds the field's data from an operation, such as `houses[cottage].description.short`, and **Help** (see below); a map entry's key box has them in its own menu. A name's tooltip is what the data files say about it (its type and notes) and its path. It's there for the fields of items with ids, settings, and the fields inside an operation's value (not the value itself, whose path is the operation's), and list items are found by id if they have one, or else by index. Emptying an optional field leaves the property out. An optional true-or-false box has a third state (a square) that leaves it out.
* Text, numbers, true or false, and fixed choices (such as a property's `type`) have their own fields. A number that can't be read turns its box red, with the reason as its tooltip, and isn't applied.
* Objects are shown with their fields indented below their name. An optional one that's left out has **+ Add**, and ✕ leaves it out again (only a property that isn't required can be left out).
* Lists have **+ Add** to add an item at the end, and each item is numbered, with ▲ and ▼ at the end of its row to move it up or down and ✕ to remove it. A single text written for a list of one is edited as a list.
* Maps (objects whose names are keys, such as a condition's `values`) have **+ Add** and ✕, and a box for each key (where a value's name would be), which is applied when it loses focus.
* Properties the editor doesn't know the shape of are edited as JSON. Properties the game doesn't read there are shown in red, as JSON, so they can be seen and fixed.
* A field that refers to something by id offers the ids it can be, from the game data and the mod's data files (and items its operations insert), completing them as you type. A ⚠ beside it says when its text isn't one of them. These marks are the form's own hints; the Problems list only has what the game reports. These are: properties, quarter kinds, map features, professions, needs, days, weeks, seasons, portrait scales, offsets, and colors (a color can also be written out, such as `#FFAA00`), the values of a property (in a condition, the values of the property named by the key; for an inherited property's `unblended` and a default's `valuesByKey`, the property named beside it; and for a property's own role settings, that property), the roles the game knows, frequencies, peaks, and wealth levels.
* An operation's form has its `op` and `path` (offering what can come next after the path typed so far: the sections, then the items of a list by id, the properties of an object, or the keys of a map; and marked with ⚠ only if a step of it isn't something the data has there, such as a misspelled property), and the properties that op takes. A `replace` or `set` value, and an `insert` value or values, get fields for the data the path leads to, when that's known; otherwise they're edited as JSON. `before` and `after` offer the ids of the list in the game data, without marking other ids. While the path is changed, the operation keeps the value it had before: each new path starts from that value (until the value itself is changed), so typing a path doesn't lose it. When the path of a `replace` or `set` goes further into the data that value was of (such as from `houses[cottage]` to `houses[cottage].description`), the value is the part of it the new path leads to, so it stays the data the path changes. This only happens in the form, not when the path is typed as JSON.
* The game data's pieces are shown in the form too, but they can't be changed (their paths can still be copied).

There is no visual editor for portrait layers; they're edited in the form or as JSON like everything else.

## Operations in the tree

Each operation is shown as its `op`, then the ids of the items it works on, then its `path`. The ids are those of the items an `insert` or `replace` value (or values) has, the new id a `copy` sets with a change to `id`, or else the id at the end of the path, as in `properties[hair].values[red]`.

## What the mod changes

A moment after each change (including unsaved ones), along with the problems, the editor works out what the mod changes in the game data. It applies the mod's files one at a time, in the order the game does: a data file as a whole, and an operations file one operation at a time, noting what each changes. Then it compares the game data with and without the whole mod. Lists are compared by id where every item has one (or else by position), so items that only move aren't changes.

* **In the Game Data tab,** what the mod adds is in the tree, marked ✚ in green; what it changes, or changes something in, is marked ✎ in orange; and what it removes stays in the tree, marked ✖ and struck through. A section says how many of its items or settings are changed. Each piece shows its value with the mod (or without it, if the mod removes it). **Go to Change in Mod** in a piece's menu selects the part of the mod that last changed it, in the Mod tab.
* **In the Form tab,** for a piece of the game data, each field the mod adds is marked ✚ and its name is green, and each field it changes, or changes something in, is marked ✎ and its name is orange. Its tooltip says so. **Go to Change in Mod** in the field's menu selects the part of the mod that last changed it (or something in it). What the mod removes has no field, so only the field it was in is marked; the Changes tab lists it.
* **The Changes tab,** for a piece of the game data, lists each path the mod changes in it, whether it's added, removed, or changed, its value before and after, and the parts of the mod that changed it, in the order they're applied (the last is the one whose value it has). Each of those goes to it in the Mod tab. For a data file (or a section, item, or setting in one) or an operation, it lists what that changes when it's applied.
* **In the Mod tab,** an operation is marked ⚠ when it changes nothing (such as a `set` to the value already there), when it fails (see Problems), or when what it changes is changed back later, so the mod doesn't change it in the end.

## Help

The help shows this document, [DataFiles.md](DataFiles.md), and [Mods.md](Mods.md), as they were when the editor was built, in a window of its own beside the editor. It lists the documents and the headings of the one shown (choosing either goes there), and follows the links between them. The box above the document (Ctrl+F) finds text in it: Enter, F3, or **Find Next** selects the next place it appears, starting again from the top after the end. Closing it only hides it, so it keeps its place.

* **?** on the toolbar opens it at the start of this document.
* **Help** (F1) in a field's menu goes to the row of the data files' table for that property, picked out in yellow, or to the heading for its kind of data if there's no row. A list item or map value goes to its list's or map's row. An operation's own properties go to the operation table in Mods.md.
* F1 goes to the help about the form field with the focus, or else about the selected item: the heading for an item's kind of data, a setting's row, a section's row in the list of sections, the operations in Mods.md, or, for a file or folder, how the editor organizes them. **Help** in an item's menu in either tree does the same.

The help is drawn from the Markdown with [Markdig](https://github.com/xoofx/markdig) (see [Dependencies.md](Dependencies.md)). It covers what the documentation uses: headings, paragraphs, lists, tables, code, emphasis, and links. A paragraph that's only bold text (such as **Operation** in Mods.md) counts as a heading. Links to documents that aren't in the help can't be followed in it.

## Running the game

**▶ Run Game** (Ctrl+R) saves the mod, then starts the game from the editor's folder with `--mod` and the mod folder, so only the mod being edited is loaded, whatever the game's mod list holds (see [CommandLine.md](CommandLine.md)). **Developer mode** adds `--developer`.

The recently opened folders, the developer mode check box, the editor's tab (form or JSON), the window's position and size, and the tree's width are kept between sessions in the registry, under `HKEY_CURRENT_USER\Software\Golden Anvil\Opera Et Dies Mod Editor`.

## The icon

`Tools/ModEditor/Icon.ico` is drawn from `Tools/ModEditor/Art/Icon.xaml`, an anvil in the style of Sub Corona's icon, at 16, 20, 24, 32, 40, 48, 64, 128, and 256 px. To make it again after changing the drawing, run (from the repository root):

```bash
powershell -STA -NoProfile -ExecutionPolicy Bypass -File Tools/ModEditor/Art/Render.ps1 -PreviewDir <scratch folder>
```

Then look at the previews, especially the 16 and 32 px ones, and build again so the new icon is used.
