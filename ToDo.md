# To Do

## Long Term

* Passage of time: day and night cycles, maybe seasons, and weather. Player actions take time: decide what counts as an action (traveling, talking, working, examining) and what each costs.
* Village names: generate a name for each village (from the data), keep it in the game state so it's saved, and show it in the game.
* Save titles: show the village name and the in-game date and time in each save's title, alongside the main character's name. The in-game date and time need the passage of time first.
* Look for things that need to move to data
* Improve data organization
* Show more of the game's status
* Schedules: villagers move around the town for work, their needs, and free time, instead of staying at home.
* Set the at work property from schedules once they exist, instead of from location alone. People who work from home are currently at work half the time they arrive home, by chance.
* Character needs
* Personality traits.
* Relationships between characters
* Talking to villagers.
* Customize the family and villagers at the start of the game.
* Customize some game configuration parameters when starting a game.
* Inventory
* Clothing
* Money and trade: buying, selling, wages, and what the knight takes.
* The player's own trade: working, earning, and helpers.
* Random events
* Landmark purposes: lookout points that show who is in nearby quarters, gathering places (the well, a crier's spot), a wandering peddler. Most need NPC movement or events first.
* Life events over time: aging, births, marriages, deaths, and households changing.
* Scripted introduction
* Create todo items for the story
* A journal or notes recording what has happened and what the player has learned about people.
* Settings screen: text size, a dark theme, and similar options.
* Keyboard navigation for choosing exits and actions.
* Naming saves, so the player can tell them apart.
* Hotkeys for common commands, including quick save and quick load.
* Pictures and simple animation, such as a clearer map style, or small illustrations for places.
* Mod support beyond the basics: validating a mod on its own, showing which mod changed what, and example mods.
* Automatic checks on generation: turn the statistics checked by hand in the village report into tests with limits.
* Portrait variation: people with the same visual properties look identical. Vary clothing color and face proportions from a stored per-person seed, probably alongside the Clothing work and once games can be saved.
* Mods that copy a value from the data go stale: a mod that writes in a value it read from the base data (such as a portrait shape's stroke color, used as a new color's default) keeps the old value if the base data changes, silently undoing the change. One fix is a `from` path on `insert`, `replace`, and `set`, used in place of `value`, that copies the data at that path when the operation runs.
* Mods have to find list items without ids (such as portrait shapes) by index, which points at the wrong item if the base data adds, removes, or reorders items in the list. One fix is optional ids on those items (such as portrait shapes), ignored by the game, so mods can find them by id.
* Add `any` and `not` to Condition, so conditions can express things like "not (barmaid and at work)" directly instead of needing copies of shapes or extra colors. Conditions are used throughout the data, and the combination checks and dependency ordering assume a simple AND of property values, so this is a large change.
* Change map generation so rivers can run through towns, not just past them.
* GameWorld's lists (locations, families, people, and so on) are built once when the world is created, so they'll need updating once objects can be added or removed during play (births, deaths, new businesses).

## Current Work
