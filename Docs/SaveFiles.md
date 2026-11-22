# Save Files

Saved games are kept in `%LocalAppData%\Golden Anvil\<game title>\Saves`, where any characters in the title that can't be used in a folder name are replaced with `_`. Each save is a separate file, named after the player and the time it was saved, with the extension `.json`. Every `.json` file in the folder is listed when loading a game.

## General Rules

* Files are JSON, written indented. Property names and enum values are camelCase.
* Every property is required unless marked optional, and can't be `null` unless marked optional.
* A **world id** is a whole number of at least 1 that identifies an object in the world. Every object in a save has a different world id. World ids mean nothing on their own.
* Data ids refer to items in the game data, and are compared case-sensitively.
* Saves hold only the source of truth. Anything that can be worked out from the rest (a location's people, a person's home and workplace, house titles, descriptions) is rebuilt from the save and the current game data when it's loaded.
* Any problem stops the save from loading, and the first problem found is reported.

## Save File

| Property | Type | Notes |
|---|---|---|
| `version` | number | The save format version. Must be 8. |
| `gameTitle` | string | The title of the game it was saved from. Must match the loaded game's title. |
| `dataHash` | string | A hash of every file in the data folder, including their paths within it. |
| `modsHash` | string | Optional (defaults to empty). A hash of every file in each mod folder, in order, including their paths within their folders. Empty if the game was saved without mods. |
| `mods` | list of string | The mod folders that were loaded, in order. Not used when loading. |
| `savedAt` | string | When the game was saved, as an ISO 8601 date and time with its offset. Only used to show the save. |
| `playerName` | string | The player's full name. Only used to show the save. |
| `nextId` | number | The world id the next new object will get. Must be greater than every world id in the save. |
| `time` | string | The current time in the game, as written by the game's calendar. Must be a time the calendar can read: a whole number of seconds since the start of year 1, at least 0. |
| `objects` | list of World Object | Every object in the world. Each object's `kind` property says which kind it is. Objects can refer to objects anywhere in the list. |

If either hash doesn't match the loaded data or mods, the player is asked whether to load the save anyway.

There must be exactly one player, everyone must be in exactly one family, and no one can own or help in more than one business. Every two people must have exactly one relationship. An item can be in at most one place: one location's or person's items, or worn.

## World Objects

Every world object has these properties:

| Property | Type | Notes |
|---|---|---|
| `kind` | string | `feature`, `location`, `person`, `family`, `business`, `relationship`, `clothing`, `outfit`, `appointment`, `bundle`, or `job`. Must come first. |
| `id` | world id | |

**Feature** (`feature`)

A map feature, such as a river or a forest.

| Property | Type | Notes |
|---|---|---|
| `template` | string | The id of a map feature in `features`. |
| `bounds` | Rect | Where it is on the map. |

**Location** (`location`)

A quarter, house, or business's place.

| Property | Type | Notes |
|---|---|---|
| `type` | string | `quarter`, `house`, or `business`: which list (`quarters`, `houses`, or `businesses`) `template` is from. |
| `template` | string | The id of a quarter, house, or business template. |
| `bounds` | Rect | Optional. For a quarter, where it is on the map. |
| `exits` | list of Exit | The ways out of the location. |
| `surroundings` | list of Surrounding | For a quarter, the map features it borders. |
| `items` | list of world id | Optional. The items kept there, such as what a household keeps at home. Missing or `null` if there are none. Each must be an item. |

**Exit**

| Property | Type | Notes |
|---|---|---|
| `location` | world id | The location it leads to. |
| `direction` | string | Optional. For a path between quarters, the direction it leads: `north`, `northEast`, `east`, `southEast`, `south`, `southWest`, `west`, or `northWest`. |
| `path` | string | Optional. For a path between quarters, its name. |
| `isCrossing` | boolean | Whether the path crosses the river. |

**Surrounding**

| Property | Type | Notes |
|---|---|---|
| `feature` | world id | The map feature. Must be a feature. |
| `direction` | string | The direction the feature is in from the quarter, as for an exit. |
| `text` | string | The sentence describing it, added to the end of the quarter's medium description. |

**Person** (`person`)

| Property | Type | Notes |
|---|---|---|
| `isPlayer` | boolean | Whether the person is the player. |
| `role` | string | How the person is related to the head of their family: `head`, `wife`, `child`, or `householdElder`. |
| `location` | world id | Where the person is. Must be a location. |
| `traits` | object | The person's character property values, keyed by character property id. A string property's value is a string, an integer property's value is a whole number, and a value list property's value is the id of one of its values. Each key must be the id of a character property. The property with the `atWork` role must have a value. |
| `tasks` | list of Task | The person's tasks, in order: those left for the step of their current sequence they're on (or for being idle), and any change of clothes before it. Can be empty. |
| `taskEnd` | string | Optional. When the first task ends, written the same way as `time`, if the person is doing it. `null` if they aren't doing anything, as for the player while they decide what to do. Ignored when there are no tasks. |
| `needs` | object | The level of each level need, keyed by need id, as of `time`. Each key must be the id of a level need in `needs`, and each level must be from 0 to 100. A level need that isn't listed is at 0. |
| `progress` | object | What's been done of quota needs this day or week (as each need's period says), keyed by need id, as of `time`: the minutes spent on a quota measured in time, or the runs finished of one measured in runs. Each key must be the id of a quota need in `needs`, and each value must be a whole number of at least 0. A quota need that isn't listed has had nothing done of it. |
| `sequences` | list of Sequence | Optional. The sequences of steps the person is in the middle of, in the order they were last current, so the last that has set off is the one they're carrying out (whose need is their goal), and the others are on hold, or for appointments, waiting for them to set off. Missing or `null` if there are none. |
| `replacedClothes` | list of Replaced Clothes | Optional. For each outfit the person changed into that replaced clothes they were wearing, the pieces it replaced, which they change back into when they change out of it. |
| `goalChoice` | Goal Choice | Optional. How the person last chose their goal from their needs. Missing or `null` if their plan was last set some other way, as for the player. |
| `sightings` | list of Sighting | Optional. The last time the person saw each other person, or heard about them, in the last `sightingHours` hours (from `schedules`). Missing or `null` if there are none. At most one for each other person, and none of the person themselves. |
| `talkRequest` | world id | Optional. For the player, someone who asked them to chat and is waiting for an answer, or `null` if no one is. Must be a person. |
| `askRequest` | world id | Optional. For the player, someone who asked them the question of the `ask` step they're on, while they're waiting for an answer, or `null` if no one is. Must be a person. |
| `hasNapped` | boolean | Optional (defaults to false). Whether the person has napped since they last slept until rested. |
| `inventory` | list of world id | Optional. The items the person is carrying. Missing or `null` if there are none. Each must be an item. |
| `clothing` | list of world id | Optional. The clothing the person is wearing. Missing or `null` if there is none. Each must be clothing, and no two can take the same clothing slot in the same layer. |

**Family** (`family`)

| Property | Type | Notes |
|---|---|---|
| `lastName` | string | The last name of the head of the family. |
| `members` | list of world id | The members, starting with the head. Each must be a person. Must not be empty. |
| `home` | world id | The house the family lives in. Must be a location. |
| `wealthLevel` | string | `veryLow`, `low`, `medium`, or `high`. |

**Business** (`business`)

| Property | Type | Notes |
|---|---|---|
| `profession` | string | The id of a profession in `professions`. |
| `owner` | world id | Must be a person. |
| `helpers` | list of world id | Each must be a person. |
| `location` | world id | Optional. The place the business is run from, or `null` if it's run from the owner's home. Must be a location. |

**Relationship** (`relationship`)

How two people get along, which is the same both ways.

| Property | Type | Notes |
|---|---|---|
| `first` | world id | Must be a person. |
| `second` | world id | Must be a person other than `first`. |
| `score` | number | A whole number from 0 to 100. |
| `lastTalked` | string | Optional. When the two last talked, written the same way as `time`, or `null` if they haven't talked since the game started. |
| `lastDeclined` | string | Optional. When the player last turned down a request to chat from the other one, written the same way as `time`, or `null` if they haven't. |

**Appointment** (`appointment`)

A time and place two people agreed to meet, made by an `ask` step with an `appointment`, until it's kept or called off. Whether each has set off for it is in their sequence for it.

| Property | Type | Notes |
|---|---|---|
| `worker` | world id | The one who asked, who carries out the appointment's steps. Must be a person. |
| `client` | world id | The one asked. Must be a person other than `worker`. |
| `need` | string | The id of the need in `needs` whose `ask` step made it. |
| `step` | number | The index of that `ask` step in the need's steps. It must make appointments. |
| `place` | world id | Where they meet. Must be a location. |
| `time` | string | When they meet, written the same way as `time`. |
| `isStarted` | boolean | Optional (false). Whether both arrived, and the worker is carrying out its steps. |

**Job** (`job`)

Work someone took on for someone else, made by an `ask` step with a `job`, until it's done. While it isn't, the worker's steps to `"jobSite"` go to its place, and the time they spend there meeting its need's quota counts toward it. The worker's jobs are worked out from these.

| Property | Type | Notes |
|---|---|---|
| `worker` | world id | The one who asked, who does the work. Must be a person. |
| `client` | world id | The one asked, who it's done for. Must be a person other than `worker`. |
| `need` | string | The id of the need in `needs` it's done for. |
| `place` | world id | Where the work is done. Must be a location. |
| `minutes` | number | At least 1. How many minutes the work takes in all. |
| `minutesDone` | number | At least 0. How many minutes of the work are done. |

**Clothing** (`clothing`)

A piece of clothing, which is an item. Where it is comes from the location or person that has it.

| Property | Type | Notes |
|---|---|---|
| `clothing` | string | The id of an article of clothing in `clothing`. |
| `colors` | object | The id of a color in `clothingColors` for each of the clothing's colors, keyed by color name. Must have exactly the clothing's color names. |
| `owner` | world id | Optional. Who the piece belongs to, or `null` if nobody. Must be a person. |
| `readyAt` | string | Optional. When the piece can be worn again, such as once washing hung out to dry is dry, written the same way as `time`, or `null` if it was never put away to be ready later. |
| `delayActivity` | string | Optional. What the piece is shown doing until `readyAt`, such as "Drying", from the step that put it away, or `null` if it was never put away to be ready later. |

**Bundle** (`bundle`)

Something a step has a person carry until a later step puts it down, which is an item. Where it is comes from the person that has it.

| Property | Type | Notes |
|---|---|---|
| `owner` | world id | The person carrying it. Must be a person. |
| `title` | string | What it's called. |
| `items` | list of world id | Optional. The items it stands for, which are carried with it. Missing or `null` if there are none. Each must be an item. |

**Outfit** (`outfit`)

Clothing a person owns that's worn together.

| Property | Type | Notes |
|---|---|---|
| `owner` | world id | Must be a person. |
| `outfitKind` | string | When it's worn: `everyday`, `work`, `night`, or `nothing`. |
| `template` | string | Optional. The id of the outfit in `outfits` it was made from, or `null` if none. |
| `pieces` | list of world id | Each must be clothing. No piece can be listed twice, and no two can take the same clothing slot in the same layer. |

**Goal Choice**

| Property | Type | Notes |
|---|---|---|
| `time` | string | When the goal was chosen, written the same way as `time`. |
| `urgencies` | list of Need Urgency | Each need that was at all urgent then, with how urgent it was. Can be empty. |
| `need` | string | Optional. The id of the need in `needs` chosen from the ones urgent enough, or `null` if none were. |
| `result` | string | `chosen` if the need became the goal, or why the person went idle instead: `nothingUrgent`, `noPlace` (nowhere to meet the need), or `noTime` (no time left for it). |

**Seek Choice**

| Property | Type | Notes |
|---|---|---|
| `friend` | world id | The friend being looked for, or `null` when looking for anyone who meets a condition. Must be a person. |
| `place` | world id | Where they're looking. Must be a location. |
| `clue` | string | Why they're looking there: `workplace` (the friend's workplace, during their work hours), `home` (the friend's home), `sighting` (where the friend was last seen or said they were going), `gathering` (where people gather today), or `people` (one of the places to look for anyone who meets a condition). |

**Need Urgency**

| Property | Type | Notes |
|---|---|---|
| `need` | string | The id of a need in `needs`. |
| `urgency` | number | At least 0. |

**Sighting**

| Property | Type | Notes |
|---|---|---|
| `person` | world id | The person seen. Must be a person. |
| `location` | world id | Where they were seen. Must be a location. |
| `time` | string | When they were last seen, written the same way as `time`. |
| `activity` | string | What they were doing: `none` (nothing in particular), `idle`, `busy` (spending time on `need`), `walking` (setting off along a path to `destination`), `left` (leaving for `destination`), or `talking`. |
| `need` | string | The id of a need in `needs` for `busy`, and `null` otherwise. |
| `destination` | world id | The next place on their way for `walking` and `left`, and `null` otherwise. Must be a location. |
| `source` | world id | Optional. Who told the person about the sighting, or `null` if they saw it themselves. Must be a person. |
| `plan` | Sighting Plan | Optional. What the person seen said they were doing, if they were asked, or `null` if they weren't. |
| `doing` | string | Optional. For `busy`, what they were shown doing, such as "Washing clothes" (the activity of the step they were on), or `null`. |

**Sighting Plan**

| Property | Type | Notes |
|---|---|---|
| `goal` | string | The id of a need in `needs`, or `null` if they were idle. |
| `destination` | world id | Where their walk ended, or `null` if they weren't walking. Must be a location. |
| `doing` | string | Optional. What they said they were doing for `goal` or were about to, such as "Washing clothes", or `null` if they were idle. |

**Sequence**

One run through a need's steps by a person.

| Property | Type | Notes |
|---|---|---|
| `need` | string | The id of a need in `needs`. |
| `step` | number | The index in the need's steps of the step it's on, or the number of steps once it's done. |
| `minutesLeft` | number | Optional. At least 0. What's left of the minutes chosen for it, which `do` steps that take from it use up, or `null` for a level need met until it's met, or a quota measured in runs. |
| `isCarriedOut` | boolean | Optional (false). Whether the step it's on has been carried out by the game, with the tasks it gave, so it's only checked once they're done. |
| `isCut` | boolean | Optional (false). Whether carrying out the step it's on was cut short by the end of the step's hours. |
| `until` | string | Optional. When the `wait` or `wander` step it's on is over, written the same way as `time`, once it's been reached, or for following someone who leads them, when their following is over. |
| `place` | world id | Optional. Where the person was once they got to where the steps it's on are done, or `null` on the way somewhere. Must be a location. |
| `takeover` | string | Optional (`none`). For the player, how much of it the game carries out for them: `none`, `step` (the step it's on), or `all`. |
| `appointment` | world id | Optional. The appointment it's for: its steps, for its worker, or the client's part in them. Must be an appointment. |
| `setOff` | string | Optional (`gone`). For an appointment's sequence, whether the person has set off for it: `gone` (they have, as for any other sequence), `waiting` (it isn't time yet), or `due` (it's time for the player to set off, and they're being asked whether to). Only `gone` without an `appointment`. |
| `partner` | world id | Optional. The person its steps last talked with or asked. Must be a person. |
| `seeking` | Seek Choice | Optional. Which friend its `seek` step chose to look for (or that it's looking for anyone who meets a condition), where, and why. |
| `answers` | object | Optional. The answers to the questions its steps asked, keyed by name, as booleans (true for yes). |
| `rememberedPlaces` | object | Optional. The places its steps remembered, keyed by name, as world ids. Each must be a location. |
| `bundles` | object | Optional. The bundles its steps took or made, keyed by the step's bundle id, as world ids. Each must be a bundle. |
| `role` | string | Optional (`steps`). `steps` for a run through the need's steps, or for taking part in someone else's: `client` (keeping an appointment as its client, whose `need` is the worker's) or `follower` (following someone who leads them, as part of the leader's `need`). |
| `leader` | world id | Optional. For taking part in someone else's steps, who the person follows, once they're following. Must be a person. |

**Replaced Clothes**

| Property | Type | Notes |
|---|---|---|
| `outfit` | world id | The outfit changed into. Must be an outfit. |
| `pieces` | list of world id | The pieces it replaced. Each must be a piece of clothing. |

**Task**

One step toward a person's goal. Each task's `kind` property says which kind it is, and must come first. A talk or wait task can come before the person's other tasks, since it interrupts them.

| Kind | Other properties | Notes |
|---|---|---|
| `travel` | `destination` (world id) | Walks to a location next to the person's. Must be a location. |
| `busy` | `minutes` (number, optional), `step` (number, optional), `isAside` (boolean, optional) | Spends time where the person is, for the step of the goal's steps with the index `step`, if any. Without `minutes` (or with `null`), it lasts until the goal's level need reaches 0. With `isAside` true, it's time aside from the goal, such as the player changing clothes, which meets no need. |
| `atWork` | `isAtWork` (boolean) | Starts or stops work, which takes no time. |
| `talk` | `with` (world id), `minutes` (number), `need` (string, optional) | Talks with someone for that many minutes, interrupting what the person was doing. `with` must be a person. `need`, if given, is the id of a need in `needs` met while they talk, such as socializing with a friend, or the quota of a `talk` step that meets it. |
| `wait` | `for` (world id), `minutes` (number) | Waits where the person is for someone who called out to them, for that many minutes, interrupting what the person was doing. `for` must be a person. |
| `follow` | `leader` (world id), `minutes` (number) | Follows someone wherever they go, for at most that many minutes, interrupting what the person was doing, until they're let go. `leader` must be a person. |
| `meet` | `appointment` (world id), `minutes` (number) | Waits where the person is for an appointment, for that many minutes: until its time, or a while longer for the other to arrive. `appointment` must be an appointment. |
| `changeClothes` | `outfit` (world id), `isTakingOff` (boolean) | Changes into the outfit, or out of it, such as out of work clothes after work, which takes the `changeMinutes` in the `schedules` settings. Must be an outfit. |

A `travel`, `busy`, `talk`, `wait`, `follow`, `meet`, or `changeClothes` task that was interrupted has `resumeMinutes` (number), the minutes it had left, which it takes when it starts again. It's missing or `null` for a task that wasn't interrupted.
**Rect**

| Property | Type | Notes |
|---|---|---|
| `x` | number | |
| `y` | number | |
| `width` | number | |
| `height` | number | |
