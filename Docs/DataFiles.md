# Data Files

The game reads its data from the `Data` folder next to the executable. Every file listed here is required, except for files referenced by optional fields.

## General Rules

* Files are JSON. Comments and trailing commas are allowed.
* Property names are camelCase, but matching is case-insensitive. Unknown properties are ignored.
* Every property is required unless marked optional. A required property can't be missing or `null`, and list items can't be `null`.
* Text values described as "filled in" can't be empty or only whitespace.
* Ids are compared case-sensitively and must be unique within their list.
* Lists of identified items must not be empty, and each item needs a filled-in `id` and `title`.
* A **frequency** is how common an item is when chosen at random: `1` (rare), `2` (uncommon), or `3` (common). Each step is four times as likely as the one below it.
* Any problem stops the file from loading, and every problem found is reported.

## GameInfo.json

| Property | Type | Notes |
|---|---|---|
| `title` | string | Filled in. |
| `description` | string | Filled in. |
| `icon` | string | Optional. Path of the application icon image, relative to the `Data` folder. Must not be blank, rooted, or contain invalid path characters. |

## Professions.json

| Property | Type | Notes |
|---|---|---|
| `professions` | list of Profession | Identified items. |

**Profession**

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | |
| `description` | string | Filled in. Finishes the sentence "He works as ...". |
| `defaultLastName` | string | Filled in. |
| `frequency` | integer | Frequency. |

## FirstNames.json

| Property | Type | Notes |
|---|---|---|
| `male` | list of Name | Must not be empty. |
| `female` | list of Name | Must not be empty. |

**Name**

| Property | Type | Notes |
|---|---|---|
| `name` | string | Filled in. |
| `frequency` | integer | Frequency. |

## CharacterOptions.json

| Property | Type | Notes |
|---|---|---|
| `playerMinimumAge` | integer | At least 1. |
| `playerMaximumAge` | integer | At least `playerMinimumAge`. |
| `npcMinimumAge` | integer | At least 1. |
| `npcMaximumAge` | integer | At least `npcMinimumAge`. |
| `heights` | list of Character Option | Identified items. |
| `weights` | list of Character Option | Identified items. |

**Character Option**

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | |
| `description` | string | Filled in. Finishes the sentence "He is ...". |
| `frequency` | integer | Frequency. |

## Locations.json

| Property | Type | Notes |
|---|---|---|
| `startingLocation` | string | Filled in. Id of the location where the player starts. Must match a location. |
| `locations` | list of Location | Identified items. |

**Location**

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | |
| `shortDescription` | string | Filled in. One sentence, shown when listed as a place to go. |
| `mediumDescription` | string | Filled in. One paragraph, shown on arrival. |
| `longDescription` | list of string | Must not be empty, and each paragraph must be filled in. Shown when examining the location. |
| `connections` | list of string | Optional (defaults to empty). Ids of locations reachable from this one. Each must be filled in, match another location, and not be the location's own id. |
