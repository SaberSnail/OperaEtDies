# To Do

## Long Term

### Story and the Knight

* Create todo items for the story
* Scripted introduction
* Random events
* Tune how fast the knight converts villagers: uncontested, about 4 picks a week takes close to 4 years to convert everyone, which seems long. Converts helping him will speed it up.
* A curfew set by the knight, making it risky to be out after dark.

### Village Life

* Character needs: flesh them out beyond the basic needs added with schedules.
* More needs and things to do, so every hour is accounted for by a goal. Idle time in the report means there isn't enough to do, so resting should be its own goal. Ideally the day isn't long enough to do everything.
* Leisure at home: time with the family at home, the household's version of meeting at the well, and a daily chance for the player to spend time with their family.
* Errands to businesses (such as bread from the baker or ale from the brewer), and chores that take people to landmarks and the outskirts.
* Jobs marked as working from home all work at home for now. Split them into categories with their own behavior (working away at a site, gathering in the forest, visiting other homes).
* Church: the seventh day of the week is for church, as a need that brings villagers to the churchyard.
* Church bells at noon (and maybe other hours) that the player notices, marking the time of day.
* Landmark purposes: lookout points that show who is in nearby quarters, gathering places (the well, a crier's spot), a wandering peddler. Most need NPC movement or events first.
* Player actions that take time are only interrupted by someone asking to chat, not by anything else yet, such as someone coming in while the player sleeps.
* Life events over time: aging, births, marriages, deaths, and households changing.

### Consequences

* Sleep: sleep isn't forced, but going without it has consequences.
* Negative effects of high fatigue and restlessness.
* Negative effects of skipped chores, different for each chore.
* More consequences for waking someone than a little fatigue, such as annoying them.

### People and Relationships

* Personality traits.
* Personality traits that change how urgent each need is for a person (such as loving the market), so villagers fall out of step with each other.
* Relations to the player only cover their own household. Relatives in other households (such as married children) will need relationships between characters.
* Gossip: trust secondhand sightings less than firsthand ones, and let gossip be wrong.
* Traveling as a group: following someone, being followed, or escorting someone slower.
* Whether the player is following someone isn't saved, and NPCs can't follow anyone yet. Following will need to move into the world once group travel exists.

### Economy and Possessions

* Inventory
* Clothing
* Money and trade: buying, selling, wages, and what the knight takes.
* The player's own trade: working, earning, and helpers.
* What the player knows about when businesses are usually open, and a screen showing it.

### The Village and Its Surroundings

* Village names: generate a name for each village (from the data), keep it in the game state so it's saved, and show it in the game.
* Change map generation so rivers can run through towns, not just past them.
* Locks on the paths into houses and businesses, with ways around them.
* Seasonal and day/night variants of quarter descriptions and quarter kind portrait backgrounds.
* Weather: chosen each day, saved, and shown in the game, and maybe affecting descriptions, schedules, and events.

### Starting a Game

* Customize the family and villagers at the start of the game.
* Customize some game configuration parameters when starting a game.
* Choose the starting date when starting a game, since some seasons may be harder to start in.

### Telling the Player What's Going On

* Show more of the game's status
* In-game log of what happens, with links to places (tokens written and read by WorldId), saved with a maximum length.
* A journal or notes recording what has happened and what the player has learned about people.
* Make it clear where firewood can be gathered (any wayside or fields quarter). Nothing tells the player that, so they don't know where to go.
* Make it clear to the player when a rest day cuts a night shift in half.

### Interface and Saves

* Settings screen: text size, a dark theme, and similar options.
* Keyboard navigation for choosing exits and actions.
* Hotkeys for common commands, including quick save and quick load.
* Pictures and simple animation, such as a clearer map style, or small illustrations for places.
* Naming saves, so the player can tell them apart.
* Save titles: show the village name and the in-game date and time in each save's title, alongside the main character's name. The in-game date and time need the passage of time first.

### Tuning

* Tune schedules with `--simulate-days` once most systems are in place: most villagers go to bed right at 21:00 when the sleep hours open, and wake around 5:00.
* Work only grows urgent once its hours are open, and isn't urgent enough to start at once when the hours are long, so people start a couple of hours after their work opens (bakers around 4:40 for 4:00, fishers 6:00 for 4:00, everyone else 8:30 for 6:00). Part of tuning work, once most systems are in place.
* Tune the chore times and leads with `--simulate-days`. Weekly chores (firewood, mending) mostly get done midweek, and Crownday is still mostly idle.
* Tune socializing with `--simulate-days`. After three days, people without friends stay fully lonely (as expected), and about one in twenty with friends does too.

### Portraits

* Portrait variation: people with the same visual properties look identical. Vary clothing color and face proportions from a stored per-person seed, probably alongside the Clothing work and once games can be saved.
* Portrait editor: add and remove path points (and change a segment's command) with the mouse, instead of only by typing the path.
* Portrait editor: copying edits as mod operations, and a way to carry an edited shape back to `Art/Portraits/Portraits.sh`, since Portraits.json is generated.

### Data and Mods

* Look for things that need to move to data
* Add `any` and `not` to Condition, so conditions can express things like "not (barmaid and at work)" directly instead of needing copies of shapes or extra colors. Conditions are used throughout the data, and the combination checks and dependency ordering assume a simple AND of property values, so this is a large change.
* Mod support beyond the basics: validating a mod on its own, showing which mod changed what, and example mods.
* Mods that copy a value from the data go stale: a mod that writes in a value it read from the base data (such as a portrait shape's stroke color, used as a new color's default) keeps the old value if the base data changes, silently undoing the change. One fix is a `from` path on `insert`, `replace`, and `set`, used in place of `value`, that copies the data at that path when the operation runs.
* Mods have to find list items without ids (such as portrait shapes) by index, which points at the wrong item if the base data adds, removes, or reorders items in the list. One fix is optional ids on those items (such as portrait shapes), ignored by the game, so mods can find them by id.

### Engine

* GameWorld's lists (locations, families, people, and so on) are built once when the world is created, so they'll need updating once objects can be added or removed during play (births, deaths, new businesses).
* Automatic checks on generation: turn the statistics checked by hand in the village report into tests with limits.

## Current Work

Improve data organization: reshape the data format so related data is grouped, concepts aren't split across files, and any data can go in any file.

### Loading

- [x] Read every JSON file in the data folder and merge them: each top-level section is a list of identified items (joined across files; a duplicate id is an error) or a settings object (merged key by key; the same key in two files is an error).
- [x] Lists without ids that need to merge (layouts, urgency factors, travel time factors, calendar items, portrait lists) become top-level sections of identified items.
- [x] Mod files can have any name, and hold either a list of operations or data merged in like a data file. Operation paths start from the merged data. Protected properties become `game.title` and `game.icon`.
- [x] Errors name the file an item came from.
- [x] Unknown properties are errors.
- [x] The portrait editor and the portrait art scripts work with the new layout.

### Roles and Professions

- [x] `role` tags on properties and values replace the id mapping in Families.json.
- [x] `role` tags on needs replace `start.need` and `talking.socializeNeed`.
- [x] Business settings grouped under `business` on a profession. A profession without one isn't a business.
- [x] An optional `player` flag on a profession (defaulting to true) replaces the profession property's list of player values.
- [x] `helperWhen` defaults to anyone being able to help.

### Characters and Families

- [x] Value groups: values can belong to groups, and conditions can name a group.
- [x] A single string allowed wherever a list of ids is expected.
- [x] `lowChanceWarning` moves to `game`.
- [x] Families.json fields grouped into `head`, `wife`, `children`, `elder`, `parentAge`, and `wealth`.
- [x] `relationships` and `playerRelations` move to their own `relationships` section.

### Village

- [x] A quarter's houses grouped with their wealth.
- [x] Count Range merged into Integer Range, with an optional shape.
- [x] Location descriptions grouped into `description` with `short`, `medium`, and `long`.
- [x] A quarter kind's optional settings grouped into `optional`.
- [x] Village-wide settings (landmark and feature counts, layouts, building travel time, house title, family count) gathered into a `village` section.

### Schedules

- [x] Needs hold a `level` or `quota` object instead of `kind`, and a quota holds `time` or `trips`.
- [x] One amount type for the per-member pairs (`minutes`, `visits`, `visitMinutes`, `homeMinutes`).
- [x] One Hours type (`start`, `end`, `days`, `onSpecialDays`) for needs, profession work hours, and `onlyInHours`.
- [x] A Place type for needs and idle in place of `place`, `quarterKinds`, and `nearest`.
- [x] `socializeMinutes` moves from `talking` to `socializing`.

### Long Text

- [x] Every long text field (a full sentence or more) is an object holding the text and a required flag saying whether AI wrote it. Existing text is marked as written by AI.

### Docs

- [x] DataFiles.md: general rules carry the defaults, shared types documented once, one-line property notes, behavior in short "How it works" sections, and "Adding to the Data" cut to what isn't checked.
- [x] Mods.md updated for the new loading, with its examples fixed.
- [x] Fix the inaccuracies found in the docs (house titles in the mod example, "array of" against "list of", Quarter Choice's type, Portraits.sh names used as format names).

### Cleanup
