# To Do

## Long Term

* Village names: generate a name for each village (from the data), keep it in the game state so it's saved, and show it in the game.
* Save titles: show the village name and the in-game date and time in each save's title, alongside the main character's name. The in-game date and time need the passage of time first.
* Look for things that need to move to data
* Improve data organization
* Show more of the game's status
* Character needs: flesh them out beyond the basic needs added with schedules.
* Negative effects of high fatigue and restlessness.
* Negative effects of skipped chores, different for each chore.
* Church: the seventh day of the week is for church, as a need that brings villagers to the churchyard.
* Personality traits that change how urgent each need is for a person (such as loving the market), so villagers fall out of step with each other.
* Traveling as a group: following someone, being followed, or escorting someone slower.
* Locks on the paths into houses and businesses, with ways around them.
* What the player knows about when businesses are usually open, and a screen showing it.
* A curfew set by the knight, making it risky to be out after dark.
* Seasonal and day/night variants of quarter descriptions and quarter kind portrait backgrounds.
* Weather: chosen each day, saved, and shown in the game, and maybe affecting descriptions, schedules, and events.
* Personality traits.
* Relationships between characters
* Talking to villagers.
* Customize the family and villagers at the start of the game.
* Customize some game configuration parameters when starting a game.
* Choose the starting date when starting a game, since some seasons may be harder to start in.
* Church bells at noon (and maybe other hours) that the player notices, marking the time of day.
* Tune how fast the knight converts villagers: uncontested, about 4 picks a week takes close to 4 years to convert everyone, which seems long. Converts helping him will speed it up.
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
* Portrait editor: add and remove path points (and change a segment's command) with the mouse, instead of only by typing the path.
* Portrait editor: copying edits as mod operations, and a way to carry an edited shape back to `Art/Portraits/Portraits.sh`, since Portraits.json is generated.

## Current Work

Schedules: villagers move around the town for work, their needs, and free time, instead of staying at home.

- [x] Set the at work property from schedules, instead of from location alone. People who work from home are currently at work half the time they arrive home, by chance.
- [x] Actions that take time, with the cost shown before acting, including actions that pass time in blocks: working, waiting until a part of the day, and sleeping.
- [x] Basic character needs that drive which goal a person picks next.
- [x] Simulation loop: time passes through an event loop where people pick goals from their needs and work through task lists (travel path by path, be busy, set or clear at work). Report option to simulate days and write out what people did.
- [x] Needs: sleep, work, meals, home upkeep, market shopping on market day (the sixth day of the week), and recreation, with urgency that grows steadily or as a deadline approaches.
- [x] Fatigue and restlessness properties, shown for the main character.
- [x] Start a new game at midnight on the first day of the week, with everyone asleep at home and waking at slightly different times. The game starts when the player wakes.
- [x] Save goals, task lists, and needs, with round-trip tests.
- [x] Player actions that take time: waiting (interruptible), sleeping, and working, with the cost shown first.
- [x] Developer view of why a person chose their goal.
- [x] The people shown at a location are a snapshot from when the player arrived. Keep them up to date as people come and go, once the player can wait.
- [x] The player is never at work, since only tasks start work. Working as a player action should set it.
- [x] A Wait button at the player's location that waits until someone arrives or leaves, or until the next part of the day.
- [x] In the list of villagers, label the player, and for developers, a button to go straight to where anyone else is.
- [x] Shared daily needs overshoot: family members who decide at the same moment both start them, so upkeep averages 86 minutes a day per household instead of 60, and about one household in eight sends two people to market.
- [x] Weekly needs, so shared chores can be done toward a weekly total, which limits overshoot to one day a week. Family members in the same place should also work on a shared chore together, not separately.
- [x] Split upkeep into separate chores (such as cooking, fetching water, firewood, laundry, cleaning, mending and other small repairs, the garden and animals), each daily or weekly, with sensible times that grow with household size where it makes sense.
- [x] A missed wash day becomes an overdue chore, done in the next day or two.
- [x] Show what each person is doing next to their name.
- [x] Follow someone: with a person followed, waiting goes with them when they leave. Waiting also stops each time they start something new.
- [x] Water, laundry, and firewood go to the nearest place they can be done.
- [x] Homemakers find household chores three times as urgent, so they do most of them.
- [x] A small portrait beside each person's name, hidden while the full portrait is shown.
- [x] A status page showing the player's needs.
- [x] Sleeping: until rested, until dawn, or for 1, 2, 4, or 8 hours. No cost for going without sleep yet.
- [x] Working: time at the workplace that meets the work need, and nothing else for now.
- [x] The player can do the household chores done at home.
- [x] Chores done elsewhere (water, firewood, laundry, market): the part at the place, then a button at home to finish trips that return home. The trip is marked in progress so the family leaves it to the player.
- [x] Mark the player's home and workplace on the map and mini-map.
- [x] In the list of villagers, show how members of the player's household are related to them.
- [x] The status page counts trip chores in visits, with the time each takes, and marks chores that are done.
- [x] Mark the buttons that lead to the player's home and workplace.
- [x] Hide chore options longer than the time left, and chores that are done.
- [x] Locations described as wells that weren't of kind well (and other overlaps with specialized quarter kinds) rewritten, so it's clear where to get water.
- [x] Show when the market is open in place of its action while it's closed.
- [x] Show how members of the player's household are related to them on the main screen too.
- [x] Clicking the sidebar button for an open Villagers, Status, or Map view closes it and returns to the main screen.
- [x] For developers, show where each person is ultimately going and why, while everyone else only sees the next place on their way.
- [x] When waiting ends because the part of the day changed, say so (the church bell rings) instead of "No one came or went".
- [x] While following someone, waiting only stops when they start spending time on a different goal or leave, not each time they carry on with the same one (such as resting again).
- [ ] Sleep: sleep isn't forced, but going without it has consequences.
- [ ] Discuss and decide which weekly special days exist besides market day.
- [ ] In-game log of what happens, with links to places (tokens written and read by WorldId), saved with a maximum length.
- [ ] Tune schedules with `--simulate-days`: most villagers go to bed right at 21:00 when the sleep hours open, and wake around 5:00.
- [ ] Jobs marked as working from home all work at home for now. Split them into categories with their own behavior (working away at a site, gathering in the forest, visiting other homes).
- [ ] Interruptions: pause the task list for an interruption, or replace the goal, and always choose the goal again afterward.
- [ ] Rename the days of the week to something more interesting than the real-world names.
- [ ] More needs and things to do, so every hour is accounted for by a goal. Idle time in the report means there isn't enough to do, so resting should be its own goal. Ideally the day isn't long enough to do everything.
- [ ] Leisure at home: time with the family at home, the household's version of meeting at the well, and a daily chance for the player to spend time with their family.
- [ ] Errands to businesses (such as bread from the baker or ale from the brewer), and chores that take people to landmarks and the outskirts.
- [ ] Socializing need: visit someone, chosen at random for now (as if everyone were close), and find them without knowing where they are: try their home or workplace depending on the time of day, and the gathering places.
- [ ] Sightings: people remember who they've seen today and where, so they (and the player) can ask others whether they've seen someone. Lookout points could add sightings too. Old sightings go stale.
- [ ] Finding mode for the player and NPCs: when the seeker is about to step into a place the person they're looking for is just leaving, the person is stopped and waits for them, as if called by name.
- [ ] Tune the chore times and leads with `--simulate-days`. Weekly chores (firewood, mending) mostly get done midweek, and Sunday is still mostly idle.
- [ ] Whether the player is following someone isn't saved, and NPCs can't follow anyone yet. Following will need to move into the world once group travel exists.
- [ ] Player actions that take time aren't interrupted by anything yet, such as someone coming in while the player sleeps.
- [ ] Relations to the player only cover their own household. Relatives in other households (such as married children) will need relationships between characters.
- [ ] Make it clear where firewood can be gathered (any wayside or fields quarter). Nothing tells the player that, so they don't know where to go.
- [ ] Needs whose hours are at night may never be met, because people sleep through the hours when they could meet them.
