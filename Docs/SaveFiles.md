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
| `version` | number | The save format version. Must be 2. |
| `gameTitle` | string | The title of the game it was saved from. Must match the loaded game's title. |
| `dataHash` | string | A hash of every file in the data folder, including their paths within it. |
| `modsHash` | string | Optional (defaults to empty). A hash of every file in each mod folder, in order, including their paths within their folders. Empty if the game was saved without mods. |
| `mods` | list of string | The mod folders that were loaded, in order. Not used when loading. |
| `savedAt` | string | When the game was saved, as an ISO 8601 date and time with its offset. Only used to show the save. |
| `playerName` | string | The player's full name. Only used to show the save. |
| `nextId` | number | The world id the next new object will get. Must be greater than every world id in the save. |
| `time` | string | The current time in the game, as written by the game's calendar. Must be a time the calendar in Calendar.json can read: a whole number of seconds since the start of year 1, at least 0. |
| `objects` | list of World Object | Every object in the world. Each object's `kind` property says which kind it is. Objects can refer to objects anywhere in the list. |

If either hash doesn't match the loaded data or mods, the player is asked whether to load the save anyway.

There must be exactly one player, everyone must be in exactly one family, and no one can own or help in more than one business.

## World Objects

Every world object has these properties:

| Property | Type | Notes |
|---|---|---|
| `kind` | string | `feature`, `location`, `person`, `family`, or `business`. Must come first. |
| `id` | world id | |

**Feature** (`feature`)

A map feature, such as a river or a forest.

| Property | Type | Notes |
|---|---|---|
| `template` | string | The id of a map feature in Locations.json. |
| `bounds` | Rect | Where it is on the map. |

**Location** (`location`)

A quarter, house, or business's place.

| Property | Type | Notes |
|---|---|---|
| `type` | string | `quarter`, `house`, or `business`: which list in Locations.json `template` is from. |
| `template` | string | The id of a quarter, house, or business template in Locations.json. |
| `bounds` | Rect | Optional. For a quarter, where it is on the map. |
| `exits` | list of Exit | The ways out of the location. |
| `surroundings` | list of Surrounding | For a quarter, the map features it borders. |

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
| `traits` | object | The person's character property values, keyed by character property id. A string property's value is a string, an integer property's value is a whole number, and a value list property's value is the id of one of its values. Each key must be the id of a character property in Characters.json. The at work property set in Families.json must have a value. |

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
| `profession` | string | The id of a profession in Professions.json. |
| `owner` | world id | Must be a person. |
| `helpers` | list of world id | Each must be a person. |
| `location` | world id | Optional. The place the business is run from, or `null` if it's run from the owner's home. Must be a location. |

**Rect**

| Property | Type | Notes |
|---|---|---|
| `x` | number | |
| `y` | number | |
| `width` | number | |
| `height` | number | |
