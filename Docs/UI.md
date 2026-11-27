# User Interface Design

This is the design of the game's interface: the principles behind it and the decisions that follow from them. Keep to it when adding or changing UI, and update it when a decision changes.

## Guiding Principles

* **Help the player know how to play.** The UI shows where they need to go, what's coming up, and the information they need to make decisions.
* **Options are available but not in the player's face.** Each choice has a reasonable default, with an easy way to reach the alternatives.
* **Free room goes to information.** Spare space on the screen shows information rather than more buttons.
* **Moderate density.** Neither cramped nor airy.
* **Icons always have tooltips.** Icon-only buttons are fine, but every one has a tooltip, and tooltips show the hotkey.
* **Look before deciding.** The player is never locked into a decision without being able to look things up first.
* **Clean, not busy, but not bare.** Some decoration and appeal, kept restrained.
* **The engine lives in the center.** Anything that blocks, passes time, or involves other people happens only in the center area. Setting things up, which passes no time and changes nothing in the world (such as choosing a destination, or starting work or a chore), can be done from the side columns and sheets too. Detailed information goes in dismissible sheets.
* **The theme comes from data where practical.** Colors, fonts, and factions are data; layout and control templates are XAML.
* **The layout adapts.** The UI handles window resizing and font size changes.

## Window

The game is one window. The start screen (the main menu), new game, load game, and mod management are all content of the main window rather than separate windows.

* The main menu has no side columns. Its content sits in the center area.
* New game has a left column listing the setup steps (such as family, character, appearance, and village), which the player can move between freely, the current step's editor in the center, and a right column with persistent information about what the player has chosen, such as a live portrait.
* The title bar is custom (using `WindowChrome`) and drawn in the theme: the game's icon and title, the toolbar during play, and the minimize, maximize, and close buttons. Dragging, resizing, snapping, double-clicking to maximize, and the system menu work as in a standard title bar. A maximized window is kept inside the screen, and loses the thin border it has otherwise.
* Borderless full screen is an option, switched by F11 or Alt+Enter anywhere, or in settings. The game starts in a window, unless it was full screen when it was last closed. Full screen covers the whole of the screen the window is mostly on, taskbar included, and follows the window if it's moved to another screen. The title bar stays, since the toolbar is in it, but loses the icon, the title, and the minimize and maximize buttons; a button for leaving full screen takes their place beside the close button. The window can't be dragged, resized, or snapped while it's full screen. Leaving full screen puts the window back where it was before, even in a later session.
* During play, Esc stops time if it's passing. Otherwise it closes the top sheet, or with no sheet open, opens the main menu, which takes over the window. Pressing Esc again quickly (within the system's double-click time) after it closed a sheet closes all the others, and never opens the main menu. Resume returns to the game exactly as it was. Settings are reached from the main menu.
* The window opens where it was when it was last closed (its position, size, and whether it was maximized), kept in the registry with the mod list and the settings. A window closed while full screen keeps where it was before it filled the screen.

### Screens

The window shows the game being played, if there is one, with a screen over it that takes over the whole window: the main menu, or a screen reached from it. The game stays underneath, hidden rather than removed, so resuming finds it exactly as it was. Sheets belong to the game and sit over its center, below any screen.

* **Main menu.** Before a game: continue the new game being set up (there even when there isn't one, so nothing moves), start a new game, load a game, settings, manage mods, and exit. During a game: resume, save, load a game, settings, quit the game and manage mods (changing mods loads the data again, which a game can't survive), quit to the main menu, and exit. Any errors loading the data, and the data and mods hashes, sit below the choices.
* **New game.** Its steps are listed on the left. For now there's one step, customizing the player's character, and it's set up for more (such as family and village). The right column shows the portrait, name, and profession as chosen so far; they're only edited in the center. Back (or Esc) returns to the main menu and keeps the new game, so the player can continue it or start over.
* **Load game**, **settings** (see Settings), and **mod management** use only the center. Back (or Esc) returns to the main menu.
* **Saving before leaving.** Quitting, loading another game, managing mods, exiting, or closing the window while the game has changed since it was last saved (or since it was started or loaded) asks whether to save first: Save, Don't Save, or Cancel to stay. If saving fails, the question stays with the reason.

The side columns of new game and play share one layout control (`ColumnLayout`), so they match and later resizing rules apply to both.

## Layout During Play

The window has three areas below the title bar. The side columns only show what the game has: an entry is added once the game has the state behind it, never as a placeholder.

* **Left: the character's state.** From the top:
  * The portrait in its frame, the name, and the profession.
  * The date (and any festival), and the time of day beside a small day dial. The dial is an arc over a horizon. The sun moves along it from the start of the first part of the day to the start of the last, and the moon moves along it through the night (the last part of the day). Marks on the arc show where the parts of the day in between start.
  * The next appointment, on a line that stays when there's none, so nothing below it moves.
  * **Needs**: an icon for each need shown to the player (its icon from the data), over a short bar that's full when the need is fully met. They wrap onto as many rows as the column's width needs. The tooltip says the need and the word for its level. A need at its worst level is drawn in the `Threat` color.
  * The mini-map, at most 10 ems wide, and as wide as the column when that's narrower.
  * The column scrolls when it doesn't all fit. The purse will go here once there's money.
* **Center: what the player is dealing with.** The location, a conversation, a prompt, and so on. All blocking prompts and every interaction with the engine happen here. Stopping time is a button at the top of the center, always there, and only enabled while time passes. When time passes at once (see Settings), there's never anything to stop, so it's hidden, keeping its place. In the location, a "More" link right after the description (on its last line, if there's room) examines it, showing the longer description, and goes once it's shown. Waiting and choosing something to start (such as work or a chore) are a row of buttons below, with how the last wait or action went on the line under them, which is always there, so a message of a line moves nothing.
  * **Exits** (the location's paths and places) are split buttons, 200 pixels wide. Icons at the left of the button mark the player's home, where they work, or their next appointment's place, then the way to their destination (in the accent color), and the button's tooltip says what each icon means.
* **Right: the state of the game.** A Concerns panel at the top, with the log below it (see Log). Concerns shows, from the top:
  * **Work** (the player's own quotas, such as their trade): its icon, its title, how much is done this day or week, and a button that starts it (or carries on with it, if the player is in the middle of it), as choosing it from what they can start would. The button is disabled when it can't be started now, with why in its tooltip. Its icon is in the accent color once it's done. It only appears for a player with work.
  * **Household** (the chores the family shares, such as cooking), as icons over short bars of how much is done, like the needs. Each is a button that starts the chore, or carries on with it. Its tooltip says how much is done and what clicking it does, or why it can't be started now. A chore that's done for the day or week is drawn in the accent color with a check mark, and one that can't be done today is muted.
  * Where the player is heading (see Destination).
  * Today's appointments still to come.
  * Work and the chores are always there, so they come first, and the parts that come and go are below them. Once the game has them, Concerns will also show the tribute due and how much is saved, who the knight has taken to the keep this week, villagers who are wavering, and later quests or goals.
  * Starting work or a chore passes no time, so it can be done while sheets are open, which stay open. If time is passing, it stops first. The player carries on with the steps once no sheets are open, as after choosing a destination. Starting it puts anything else the player is in the middle of on hold.

The toolbar runs along the top in the title bar during play: character, villagers, map, wardrobe (only at home), inventory, and status, with the main menu at the right end, apart from the window buttons. Its buttons are icons only, with tooltips that include their hotkeys (see Actions and Input).

* Character, villagers, map, and status open sheets (see Sheets). A toolbar button replaces any sheets with its own, and the button of the sheet on top closes that sheet. The buttons of open sheets look pressed.
* The wardrobe and the inventory aren't sheets but places the player visits: they use the engine (taking clothes passes time, and changing takes time once the player is done), so they open in the center, in place of the location, closing any sheets.
* A button beside them returns to the center, closing every sheet. It's only enabled while a sheet is open.
* Using them while time passes stops time first, and what they open opens once it has stopped.

### Resizing

* The side columns' widths are in ems (15 for the left, 19 for the right), so they scale with the font size, and the center gives up space as the window narrows. Icons, the day dial, the portrait, and the title bar scale with the font size too.
* The center's text is capped at about 90 to 100 characters per line (53 ems). Extra width becomes margin. The map uses the center's whole width.
* **Compact mode.** When the center's text would be narrower than about 45 characters (25 ems), the left column switches to a compact width of 8 ems, and if that isn't enough, the right column switches to 12 ems too. It only depends on the window's width and the text size. A compact column switches back only once the center would have an em to spare, so it doesn't flicker while the window is resized. In compact mode, the portrait is smaller (4.5 ems wide rather than 8.5), and the mini-map, the needs, and the chores fit the narrower column. Nothing else changes, so nothing moves within a column when it switches.
* There's no minimum window size, but the layout isn't designed for windows smaller than 1100 by 700, where things may overflow, vanish, or be too small to be useful.
* These numbers are starting points, to be tuned by trying different window and font sizes.

## Sheets

Detailed information (a person, the villagers, the map, the player's status, the full log, and so on) opens in a dismissible sheet drawn over the center, with the side columns still visible on either side. Sheets are not separate windows.

* A sheet covers the whole center, between the side columns' double rules. Its title and a close button run along its top, over a double rule with a lozenge at its center. Its text keeps the center's line length, except the map, which uses the whole width.
* Sheets only open while time is paused. Opening one while time is passing (by hotkey, the toolbar, or a link in the side columns) stops time first, rather than the sheet being unavailable, and the sheet opens once time has stopped (at the end of the moment passing, or when the player arrives if they're walking). Time doesn't pass while a sheet is open.
* While a sheet is open the engine can't be used, since the center is covered, but the toolbar, the side columns, and other sheets can.
* Opening a sheet from another (the villagers, then a villager, then for developers their needs), or from the center or a side column (a person at the location, a person or place in the log), stacks it on any open ones. The close button and Esc close the top sheet, showing the one under it. Esc pressed twice quickly, or the toolbar's button for returning to the center, closes them all.
* A toolbar button replaces the sheets with its own, rather than stacking it.
* Sheets don't repeat the pending prompt. The player closes the sheet to see it again.
* Sheets don't use the engine. Outside the center the player can only set things up, such as choosing a destination (see Destination) or starting work or a chore from Concerns, which doesn't close the sheet; the engine acts once they're back in the center. The one exception is the developer's shortcuts for moving straight to a place, which take no time, close the sheets, and move the player at once.

## Destination

The player can have a destination: a place they're heading for.

* The player chooses it from a sheet or a side column: clicking a quarter on the map, a family's "Head there" button in the villagers sheet, or the button beside an appointment in Concerns. People can't be destinations, since they move. (For developers, right-clicking a quarter on the map goes straight there.)
* The player's steps choose it too, when a step needs the player somewhere they aren't (such as taking the water home), or when it's time to set off for an appointment.
* The last one chosen wins. Choosing a different destination while the current one belongs to the player's steps asks under Concerns whether to give them up, and only goes ahead once the player confirms. The steps are given up then, and carry on (such as putting away what they carried) once the sheets close. When the steps move on from the place they chose, it stops being the destination. Reaching the destination clears it, and a button beside it under Concerns clears it too.
* The exit in the center that leads there quickest is marked with an icon (an arrow, or a pin if it leads straight there), the map and mini-map mark its quarter, and Concerns shows it, with how long it takes to walk there.
* The menu of the exit marked with the arrow has "Walk all the way to …", which walks the player all the way there, a path at a time, as ordinary walking, so anything that interrupts a walk interrupts it, as do anything the player has to answer, stopping time, and a path they aren't dressed enough to take. The exit that leads straight there has no menu, since one path is all the way.
* It's part of the game, so it's saved.

## Log

* The log fills the bottom of the right column, under Concerns, divided from it by a splitter, and starts with three fifths of the column. It scrolls back through earlier entries. A button on its heading collapses it down to the heading, giving Concerns the room, and opens it again where it was. There's no one-line collapsed mode, since several things often happen at once.
* The splitter is a double rule with a crimson ribbon bookmark hanging from its right end, and the log's "What you saw" heading sits right under the rule so they read as one piece. On hover, the rules turn crimson, the ribbon brightens, the band gets a slight highlight, and the cursor becomes a resize cursor. It's styled so it doesn't look like a standard window splitter. Neither part can be dragged smaller than a few lines. While the log is collapsed, the splitter is only the double rule.
* A button on the log's heading opens the log in a sheet, for reading at a larger size. Clicking a person in either opens their sheet, and clicking a place opens the map.

## Actions and Input

* **Split buttons** handle actions with a reasonable default and alternatives (`SplitButton`). The main part does the obvious thing, and the chevron beside it opens a themed menu of the other ways, below it. The default never changes with what the player chose before.
  * Split buttons keep their own height, centered in a taller row, like the other buttons.
  * While the main part is disabled, so is the chevron: the other ways are never available when the obvious one isn't.
  * The menu is only shown when there are other ways. On a button whose text changes (such as the step the player is on), it's only shown while one of them can be chosen now; on one whose text stays the same, it's shown even when none can, so it doesn't come and go. Ways that can't be chosen now are drawn disabled, with why in their tooltips.
  * The main part's number badge does the default, and Shift with the number opens the menu. Alt+Down opens the menu of the split button with keyboard focus. While the menu is open, its items are numbered 1 to 9, which their keys choose, the arrow keys move through it, and Esc closes it.
  * The main part's tooltip says what it does, and the chevron's says "Other ways", with Shift and the number if it has one.
  * **Where they're used.** The time it takes is always shown, with when it would end, such as "Work until 15:00 (1 hour)".
    * **The player's steps**: the step's own button, or for a step without one, having it done for them ("Do it for me"), with having it done for them and having all of it done ("Do it all for me") in the menu. Showing all of the steps and stopping are icon buttons before it.
    * **Waiting**: until someone arrives or leaves, or the next part of the day, with other times in the menu: those the data offers (such as an hour, or until the next part of the day, by its name), until it's time to set off for the next appointment, and until the step the player is on opens, if it's closed now.
    * **Exits**: going there (one path), and for the exit that's the way to the destination, walking all the way there in the menu (see Destination). The exit's number takes the path, and Shift with it opens the menu.
    * **What the player can start**: starting it, with the shift anyone else would choose ("Until done (8 hours)", or until when it would end), or carrying on with it, with the shorter shifts the data offers and having it all done for them in the menu.
  * Choosing between a step's options, conversations, prompts, and Concerns stay plain buttons: they're different actions, or a decision the player should see all the answers to, rather than ways of doing the same thing.
* **Keyboard navigation** reaches the exits and actions: Tab moves between controls, the arrow keys move within a group, and Space or Enter presses the control with keyboard focus. Space and Enter are kept for that, so they're never hotkeys. Keyboard focus is a strong ring in the accent color (`KeyboardFocusVisualStyle`), shown only once the keyboard has been used, not after a click.
* **Hotkeys** are shown in tooltips (such as "Villagers (V)"), and as small number badges at the right end of the center's numbered buttons. The keys and the text that shows them come from one map in code (`Hotkeys`), so they always agree. Keys that type (letters and numbers) are off while a text box has focus; Esc and the function keys still work there. A key held down only counts once.
* **Where keys work.** Full screen and Esc work everywhere. The rest only work during play, with nothing over the game: on the main menu and the screens reached from it, and while the save question is up, only Esc and full screen do anything. While a sheet is open, the number keys (with Shift or without) do nothing (the center is covered), but the letters, quick save, and quick load still work. While time passes, the sheets' letters stop it first and then open their sheet, quick save stops it and then saves, and the number keys do nothing.

| Key | Does |
|---|---|
| 1 to 9, 0 | The center's numbered choices (0 is the tenth) |
| Shift+1 to 9, 0 | Opens the menu of a numbered split button |
| C | Character sheet |
| V | Villagers sheet |
| M | Map sheet |
| W | Wardrobe (in the center, only at home) |
| I | Inventory (in the center) |
| T | Status sheet |
| S | What the player can start (in the center) |
| J | Moves keyboard focus to Concerns, to use its buttons |
| Esc | Stop time, or close the top sheet, or open the main menu if no sheet is open. Twice quickly closes every sheet |
| F5 | Quick save |
| F9 | Quick load |
| F11, Alt+Enter | Full screen |

The letters open or close: pressed again, a sheet's letter closes its sheet if it's on top, and W, I, and S close what they opened in the center.

### Numbered Choices

* **The location.** Its paths and places come first, numbered in the order they're listed, so each location keeps the same numbers whenever the player is there. The buttons of the player's steps take the numbers left after them: the split button for the step, then its options. The "More" link and waiting have no number, and starting something has S. Anything past the tenth has no number, and is reached by keyboard navigation.
* **Prompts and conversations** number their buttons from 1 in the order they're shown. A button that's only there some of the time keeps its number, so the others never change: a conversation always has asking what they're doing as 1 and chatting as 2, and its question for the player's step, calling off, asking to start, and asking about someone are 3 to 6 whether or not the ones before them are there.
* **What the player can start** numbers its buttons in the order they're listed.
* The wardrobe and the inventory have too many buttons to number, and are used with the mouse or keyboard navigation.

### Quick Save and Quick Load

* F5 saves the game to the quick save, a single file in the save folder that each quick save replaces. It's listed with the other saves, marked as the quick save. If time is passing, it stops first.
* F9 loads the quick save in place of the game, without asking whether to save the game first, since going back is the point. A quick save made with different data or mods opens the list of saved games, asking to confirm it as usual. If time is passing, it stops first.
* What happened (saved, failed and why, or that there's no quick save yet) shows at the top of the center, beside the button for stopping time, until the player does something.

### Tooltips

Tooltips are drawn in the theme: `TextOnDark` text on a `Text` background, in the interface font at the small size, with rounded corners, wrapping at about 26 ems.

## Animation

* Sheets slide in quickly (150 ms, easing out) as they open, and close at once.
* Any input during an animation (a key or a mouse button) finishes it at once, so the player never waits on one.
* All animations go through one helper (`Motion`), so the "reduce motion" setting turns them off in one place.
* Other small, snappy animations are welcome where they help.

## Theme

The style is a manuscript: warm parchment surfaces, brown-black ink, double rules, crimson drop caps and small-caps labels, and restrained ornament. (A woodcut style was considered and not chosen.) Text is set in EB Garamond, which is under the SIL Open Font License. Its license is next to its font files, and in `ThirdPartyNotices.txt`.

* Theme resources are named by the role they play, not by their color: surfaces (`Surface`, `Panel`, `Chrome`, `Inset`), text (`Text`, `TextMuted`, `TextDisabled`, `TextOnDark`, `TextOnAccent`), rules (`Rule`, `RuleStrong`), `Accent`, `Threat`, bars (`Track`, `Meter`), the day dial's `Sun`, control states (`Control`, `ControlHover`, `ControlPressed`, `ControlDisabled`), and the map's colors. Each color has a `…Color` and a `…Brush` resource, defined in `Themes/Default/Default.xaml`.
* The `theme` section of the data holds the colors, the fonts (by role: `narrative` for the story, and `interface` for everything else), and the text sizes. `ThemeResources` turns it into resources that replace the ones in `Default.xaml`, whose values are only the built-in theme used when the data has none.
* The theme loads with the data. Until the game data has loaded, or when it fails to load, the theme from the data without mods is used, so the main menu and any load errors are always drawn in the usual theme.
* XAML uses theme resources only as `DynamicResource`, and control states are triggers that set theme brushes rather than color animations, so a new theme changes everything at once.
* Text sizes come from one base size, `DefaultFontSize`, with the others (`SmallFontSize`, `LargeFontSize`, `TitleFontSize`, `CaptionFontSize`) worked out from it by the theme's scales. The text size setting multiplies them all: they're worked out again in their own resources, merged after the theme, whenever the theme or the setting changes. Lengths that should grow with the text are in ems (`EmConverter`).
* EB Garamond's font files are in the data folder's `Fonts` folder.
* The tools (the mod editor and the portrait editor) share the theme resources and control styles, but draw them in a plain palette and font from `Themes/Tools/Tools.xaml`, not the game's theme.
* All icons and decoration are vector paths filled from theme brushes, so another palette needs no new art. The icons are line drawings on a 24 by 24 grid, stroked in the text color by the icon button styles, and in the accent color on hover. The interface's own icons (the toolbar, closing, the destination) are in `Themes/Shared/Icons.xaml`. Icons for things in the data, such as needs, are in the data's `icons` section, referred to by id, in the same style.
* Group labels (such as "Needs" and "Concerns") are small caps in the accent color (`CaptionTextStyle`).
* Titles (the location's name, a conversation, each sheet and screen) are large small caps (`TitleTextStyle`).
* **Double rules** (`DoubleRule`: two thin lines with a gap) run under the title bar, between each side column and the center, under each sheet's title, under the main menu's title, and in the log's splitter. Control borders, the mini-map, and the portrait frame keep single rules.
* **Drop cap.** The location's description opens with a drop cap (`DropCapText`): its first letter in the accent color, sunk two lines deep, with those lines indented around it and the rest wrapping at the full width. It's the only drop cap, so there's at most one on a screen. Conversations (a record of what was said), sheets (information), and the main menu's centered description have none.
* **Ornament** is restrained, about one to a screen, filled in the accent color: a fleuron in the double rule under the main menu's title, a lozenge in each sheet's title rule, and an ivy-leaf tailpiece ending the location's description. Their paths are filled shapes on the icons' 24 by 24 grid, in `Themes/Shared/Icons.xaml` with the interface's icons, since nothing in the data refers to them.
* Portraits sit in a frame, and stay as they are. The portrait art draws its own background (its `background` layer covers the whole portrait), so a new palette never changes what the portraits are drawn on, and the frame's thin mat uses the palette's `Inset` color. A woodcut look made by changing only the portrait renderer was tried and rejected; a woodcut style would mean redrawing the portrait art.
* The light (parchment) theme ships first. It's structured so a dark palette is only a change to the colors in the data.

### Factions

Factions are data, each with an id, a name, a main color, a second color, a tint, and an emblem. Color can carry meaning, but each faction's emblem always goes with its color, so color is never the only cue.

| Faction | Main color | Second color | Tint | Emblem |
|---|---|---|---|---|
| The crown | Crimson `#8E1B22` | Black `#1A1416` | `#E9CFC9` | A crown |
| The resistance | Woad blue `#2F4F7A` | Linen `#E2D6BA` | `#CFD9E6` | Still to choose (the mockups used a pitchfork) |

* Unaligned villagers get no color or emblem.
* Minor side factions will come later with their own colors. Candidates were teal and pewter, and forest green and bronze. Avoid purple and anything near red.
* How firmly a villager is aligned is shown too, such as the faction emblem going from an outline (leaning) to filled (committed), or a small tilt marker between the two emblems. Which one is still to be decided.

## Settings

The settings screen is reached from the main menu, before and during a game. It uses only the center, and Back (or Esc) returns to the main menu. Each setting is a row of choices, and choosing one applies it at once and keeps it.

* **Text size**: 90% to 150% in steps of 10%, multiplying every text size in the theme. A line of the story's text below the choices shows the size.
* **Display**: a window, or full screen (see Window). F11 and Alt+Enter switch it too, and the choices follow.
* **Reduce motion**: follow Windows' own setting for showing animations (the default), on, or off.
* **How fast time passes**: slow, normal (the default), fast, very fast, or instant. The faster speeds shorten the time it takes to watch time go by (normally from half a second for a minute up to five seconds for twelve hours), and slow lengthens it: twice as long for slow, half for fast, and a quarter for very fast. At instant, time passes at once and the button for stopping it is hidden. A new speed applies from the next thing the player does.
* A dark theme, later.

The settings are the player's preferences, not game state, so they aren't saved with games. They're kept in the registry, under the game's key with the mod list and where the window was. `--reset-settings` forgets them (see `CommandLine.md`).
