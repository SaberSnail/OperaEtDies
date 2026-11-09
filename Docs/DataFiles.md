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

## Characters.json

| Property | Type | Notes |
|---|---|---|
| `templates` | list of Character Template | Must include the ids `fullName` (a character's full name) and `description` (a description of a character). |
| `properties` | list of Character Property | Identified items. In the order shown on the new game screen. |

**Character Template**

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in. Unique within `templates`, and must not match a property id. |
| `template` | string | Filled in. Template. |

A **template** is text with placeholders. `{id}` is replaced with the value of the property with that id (the text of a string property, the number of an integer property, or the `title` of a value list property's value), or with the text of the template with that id. `{id.name}` is replaced with the `name` entry of the `text` of a value list property's value. Either form can end with a format after a colon, as in `{id.name:first_upper}`. The only format is `first_upper`, which capitalizes the first character. Literal braces are written `{{` and `}}`. Any other brace is an error. Every placeholder must name a property or a template, and a placeholder with a name must name a value list property whose values all have that `text` entry. A template must not use itself, directly or through other templates.

**Character Property**

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Label on the new game screen. |
| `type` | string | `string`, `integer`, or `valueList`. |
| `values` | list of Character Value | Required for `valueList` properties, and not allowed for other types. Identified items. |
| `range` | Integer Range | Integer properties only. Optional if both `player` and `npc` have a `range`; otherwise required. |
| `defaults` | String Defaults | Optional. String properties only. |
| `player` | Role Settings | Optional. Replaces settings for the player. |
| `npc` | Role Settings | Optional. Replaces settings for non-player characters. |

Value list properties pick a random value by frequency. Integer properties pick a random number in their range. String properties pick a random default, or are blank if they have no defaults. On the new game screen, the player must fill in every string property. A property with only one possible value for the player isn't shown.

**Character Value**

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | |
| `frequency` | integer | Frequency. |
| `text` | map of string to string | Optional (defaults to empty). Named text for templates. Names and text must be filled in. |

**Integer Range**

| Property | Type | Notes |
|---|---|---|
| `minimum` | integer | |
| `maximum` | integer | At least `minimum`. |
| `shape` | string | `low`, `middle`, or `high`: the part of the range random numbers favor. Numbers farther from it are increasingly rare. |

**String Defaults**

Has either `values`, or both `keyedBy` and `valuesByKey`.

| Property | Type | Notes |
|---|---|---|
| `values` | list of Weighted Value | Optional. Must not be empty. |
| `keyedBy` | string | Optional. Id of a value list property defined earlier in `properties`. The defaults used depend on that property's value, and a new default is picked when it changes. |
| `valuesByKey` | map of string to list of Weighted Value | Optional. Each key must be a value id of the `keyedBy` property, and each list must not be empty. Every value the `keyedBy` property can have, for each role these defaults are used by, must have a list. |

**Weighted Value**

| Property | Type | Notes |
|---|---|---|
| `value` | string | Filled in. |
| `frequency` | integer | Frequency. |

**Role Settings**

Each setting is only allowed on properties of the type it applies to, and replaces the property's setting of the same name.

| Property | Type | Notes |
|---|---|---|
| `values` | list of string | Optional. Value list properties only. Ids of the property's values allowed for the role. Must not be empty, and each id must be filled in, unique, and match a value. |
| `range` | Integer Range | Optional. Integer properties only. |
| `defaults` | String Defaults | Optional. String properties only. |

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
| `connections` | list of string | Optional (defaults to empty). Ids of locations reachable from this one. Connections go one way. Each must be filled in, match another location, and not be the location's own id. |
