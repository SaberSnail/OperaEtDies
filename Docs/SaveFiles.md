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
| `version` | number | The save format version. Must be 5. |
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
| `kind` | string | `feature`, `location`, `person`, `family`, `business`, `relationship`, `clothing`, or `outfit`. Must come first. |
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
| `goal` | string | Optional. The id of the need in `needs` the person is trying to meet, or `null` if they're idle or have no goal. |
| `tasks` | list of Task | The tasks left toward the person's goal, in order. Can be empty. |
| `taskEnd` | string | Optional. When the first task ends, written the same way as `time`, if the person is doing it. `null` if they aren't doing anything, as for the player while they decide what to do. Ignored when there are no tasks. |
| `needs` | object | The level of each level need, keyed by need id, as of `time`. Each key must be the id of a level need in `needs`, and each level must be from 0 to 100. A level need that isn't listed is at 0. |
| `progress` | object | The minutes spent at the place on quota needs this day or week (as each need's period says), keyed by need id, as of `time`. Each key must be the id of a quota need in `needs`, and each value must be a whole number of at least 0. A quota need that isn't listed has had no time spent on it. |
| `pendingTrip` | string | Optional. The id of a quota need in `needs` measured in trips that return home, whose trip the person has been to the place for but hasn't finished at home yet. Missing or `null` if there isn't one. Only the player has one. |
| `goalChoice` | Goal Choice | Optional. How the person last chose their goal from their needs. Missing or `null` if their plan was last set some other way, as for the player. |
| `sightings` | list of Sighting | Optional. The last time the person saw each other person, or heard about them, in the last `sightingHours` hours (from `schedules`). Missing or `null` if there are none. At most one for each other person, and none of the person themselves. |
| `talkRequest` | world id | Optional. For the player, someone who asked them to chat and is waiting for an answer, or `null` if no one is. Must be a person. |
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

**Clothing** (`clothing`)

A piece of clothing, which is an item. Where it is comes from the location or person that has it.

| Property | Type | Notes |
|---|---|---|
| `clothing` | string | The id of an article of clothing in `clothing`. |
| `colors` | object | The id of a color in `clothingColors` for each of the clothing's colors, keyed by color name. Must have exactly the clothing's color names. |
| `owner` | world id | Optional. Who the piece belongs to, or `null` if nobody. Must be a person. |
| `inWash` | boolean | Optional (`false`). Whether the piece has been taken to be washed and hasn't been hung out to dry yet. |
| `dryAt` | string | Optional. When the piece, hung out to dry, is dry, written the same way as `time`, or `null` if it hasn't been hung out since it was last taken to be washed. |

**Outfit** (`outfit`)

Clothing a person owns that's worn together.

| Property | Type | Notes |
|---|---|---|
| `owner` | world id | Must be a person. |
| `outfitKind` | string | When it's worn: `everyday`, `work`, or `night`. |
| `template` | string | Optional. The id of the outfit in `outfits` it was made from, or `null` if none. |
| `pieces` | list of world id | Each must be clothing. No piece can be listed twice, and no two can take the same clothing slot in the same layer. |

**Goal Choice**

| Property | Type | Notes |
|---|---|---|
| `time` | string | When the goal was chosen, written the same way as `time`. |
| `urgencies` | list of Need Urgency | Each need that was at all urgent then, with how urgent it was. Can be empty. |
| `need` | string | Optional. The id of the need in `needs` chosen from the ones urgent enough, or `null` if none were. |
| `result` | string | `chosen` if the need became the goal, or why the person went idle instead: `nothingUrgent`, `noPlace` (nowhere to meet the need), or `noTime` (no time left for it). |
| `seeking` | Seek Choice | Optional. For the socializing need, which friend the person chose to look for, where, and why, or `null` otherwise. |

**Seek Choice**

| Property | Type | Notes |
|---|---|---|
| `friend` | world id | The friend being looked for. Must be a person. |
| `place` | world id | Where they're looking. Must be a location. |
| `clue` | string | Why they're looking there: `workplace` (the friend's workplace, during their work hours), `home` (the friend's home), `sighting` (where the friend was last seen or said they were going), or `gathering` (where people gather today). |

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

**Sighting Plan**

| Property | Type | Notes |
|---|---|---|
| `goal` | string | The id of a need in `needs`, or `null` if they were idle. |
| `destination` | world id | Where their walk ended, or `null` if they weren't walking. Must be a location. |

**Task**

One step toward a person's goal. Each task's `kind` property says which kind it is, and must come first. A talk or wait task can come before the person's other tasks, since it interrupts them.

| Kind | Other properties | Notes |
|---|---|---|
| `travel` | `destination` (world id) | Walks to a location next to the person's. Must be a location. |
| `busy` | `minutes` (number, optional) | Spends time where the person is. Without `minutes` (or with `null`), it lasts until the goal's level need reaches 0. |
| `atWork` | `isAtWork` (boolean), `clothes` (world id, optional) | Starts or stops work, which takes no time. With `clothes`, the person changes into that outfit as they start, or out of it as they stop. `clothes` must be an outfit. |
| `talk` | `with` (world id), `minutes` (number), `need` (string, optional) | Talks with someone for that many minutes, interrupting what the person was doing. `with` must be a person. `need`, if given, is the id of a level need in `needs` met while they talk, such as when socializing with a friend. |
| `wait` | `for` (world id), `minutes` (number) | Waits where the person is for someone who called out to them, for that many minutes, interrupting what the person was doing. `for` must be a person. |
| `fetchClothes` | `outfit` (world id) | Picks up the pieces of the outfit kept at home to carry them, which takes no time. Must be an outfit. |
| `changeClothes` | `outfit` (world id), `isTakingOff` (boolean) | Changes into the outfit, or out of it, such as out of work clothes after work, which takes the `changeMinutes` in the `schedules` settings. Must be an outfit. |

A `travel`, `busy`, `talk`, `wait`, or `changeClothes` task that was interrupted has `resumeMinutes` (number), the minutes it had left, which it takes when it starts again. It's missing or `null` for a task that wasn't interrupted.
**Rect**

| Property | Type | Notes |
|---|---|---|
| `x` | number | |
| `y` | number | |
| `width` | number | |
| `height` | number | |
