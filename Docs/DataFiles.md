# Data Files

The game reads its data from the `Data` folder next to the executable. Every file listed here is required, except for files referenced by optional fields. Mods can change the data files (see [Mods](#mods)).

## General Rules

* Files are JSON. Comments and trailing commas are allowed.
* Property names are camelCase, but matching is case-insensitive. Unknown properties are ignored.
* Every property is required unless marked optional. A required property can't be missing or `null`, and list items can't be `null`.
* Text values described as "filled in" can't be empty or only whitespace.
* Ids are compared case-sensitively and must be unique within their list.
* Lists of identified items must not be empty, and each item needs a filled-in `id` and `title`.
* A **frequency** is how common an item is when chosen at random: `common`, `uncommon`, `rare`, or `veryRare`. It can be nudged up by adding `+` or `++`, or down by adding `-` or `--`, as in `common-` or `rare++`. Each step multiplies or divides the item's chance by the `stepMultiplier`, after the chances below are worked out, and then the chances are scaled to add up to 100. When an item is chosen from a list, the uncommon items share the uncommon chance (see `frequencyChances` in GameInfo.json) equally, the rare and very rare items share theirs, and the common items share the rest. If a list has no items of a frequency, its chance goes to the common items, or is shared by the other frequencies in proportion to their chances if the list has no common items.
* Any problem stops the file from loading, and every problem found is reported.

## GameInfo.json

| Property | Type | Notes |
|---|---|---|
| `title` | string | Filled in. |
| `description` | string | Filled in. |
| `icon` | string | Optional. Path of the application icon image, relative to the `Data` folder. Must not be blank, rooted, or contain invalid path characters. |
| `frequencyChances` | Frequency Chances | Optional (defaults to 20 uncommon, 5 rare, and 0.5 very rare, with steps of 1.5). |

**Frequency Chances**

The percentage chances used when choosing items by frequency. The uncommon, rare, and very rare chances together must be less than 100.

| Property | Type | Notes |
|---|---|---|
| `uncommon` | number | Optional (defaults to 20). At least 0. |
| `rare` | number | Optional (defaults to 5). At least 0. |
| `veryRare` | number | Optional (defaults to 0.5). At least 0. |
| `stepMultiplier` | number | Optional (defaults to 1.5). At least 1. How much each `+` multiplies an item's chance by, and each `-` divides it by. |

## Characters.json

| Property | Type | Notes |
|---|---|---|
| `templates` | list of Character Template | Must include the ids `fullName` (a character's full name) and `description` (a description of a character). |
| `properties` | list of Character Property | Identified items. |
| `obscurers` | list of Obscurer | Optional (defaults to empty). |
| `lowChanceWarning` | number | Optional. A percentage chance, at least 0 and less than 100. When the game data loads, a warning is logged for every value with this chance or less of being chosen, for each role: value list values (using their lowest chance across the values their conditions depend on), integer values, and string defaults. |

**Character Template**

Has either `template` or `variants`.

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in. Unique within `templates`, and must not match a property id. |
| `template` | string | Optional. Filled in. Template. |
| `variants` | list of Template Variant | Optional. Must not be empty. The first variant whose `when` condition is met is used. |

**Template Variant**

| Property | Type | Notes |
|---|---|---|
| `when` | Condition | Optional. Without it, the variant is always used if no earlier variant was. |
| `template` | string | Template. Can be empty. |

A **template** is text with placeholders. `{id}` is replaced with the visible value of the property with that id (the text of a string property, the number of an integer property, or the `title` of a value list property's value), or with the text of the template with that id. `{id.name}` is replaced with the `name` entry of the `text` of a value list property's value. Either form can end with a format after a colon, as in `{id.name:first_upper}`. The only format is `first_upper`, which capitalizes the first character. Literal braces are written `{{` and `}}`. Any other brace is an error. Every placeholder must name a property or a template, and a placeholder with a name must name a value list property whose values all have that `text` entry. A template must not use itself, directly or through other templates, in any of its variants. In the text a template creates, runs of spaces are collapsed to one, and spaces at the start and end, or before `.`, `,`, `;`, `:`, `!`, or `?`, are removed.

**Condition**

Has at least one of `values`, `notValues`, and `hidden`. It's met when every part it has is met. In templates, conditions only see visible values, so a property whose value is obscured counts as having no value.

| Property | Type | Notes |
|---|---|---|
| `values` | map of string to list of string | Optional. Must not be empty. Each key is the id of a value list property, and each list holds ids of that property's values. Except in template conditions, a key can also be an integer property with `groups`, and its list holds group ids; the property's value is the group its number is in. Each list must not be empty, and each id must be filled in, unique, and match a value (or group). Met when every listed property has one of its listed values. A property with no value never meets it. |
| `notValues` | map of string to list of string | Optional. The same as `values`, but met when every listed property has a value that isn't listed. A property with no value never meets it. |
| `hidden` | list of string | Optional. Template conditions only. Ids of properties that must have no visible value. Must not be empty, and each id must be filled in, unique, and match a property. |

**Visible values.** When a character is described, some obscurers may be in effect. A property has no visible value if the character has no value for it, or if an obscurer in effect hides it with a strength at least the value's prominence. String and integer values have a prominence of 3.

**Condition checks.** For each role, conditions are checked in every combination of values they can depend on. Each check only combines the values of the properties it depends on (through conditions, and the properties those conditions depend on). Every value list property must have at least one value whose condition is met in every combination of the properties its conditions use where its own condition is met. Every template used is checked too: `fullName`, `description`, templates no other template uses, and templates used by the chosen variant of a template being checked. A template is checked in every combination of the properties its conditions use and the properties its placeholders and `hidden` lists name depend on, together with the values assumed by the template that uses it. Each property a template uses that the obscurers could hide (the strongest obscurer of it is at least the lowest prominence of its possible values) is checked both visible and hidden. In each case, the template must have a variant that's used, and every property that variant's placeholders name must have a visible value. Each check can have at most 10000 combinations, and a template can use at most 10 properties that the obscurers could hide.

**Obscurer**

Something that keeps some properties from being seen or heard. Which obscurers are in effect depends on the situation in the game.

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in. Unique within `obscurers`. |
| `hides` | map of string to integer | Must not be empty. Each key is a property id, or `*` for every property. Each number is a strength from 1 to 3. |

**Character Property**

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Label on the new game screen. |
| `type` | string | `string`, `integer`, or `valueList`. |
| `when` | Condition | Optional. A character only has a value for the property when the condition is met. |
| `values` | list of Character Value | Required for `valueList` properties, and not allowed for other types. Identified items. |
| `shape` | Shape | Optional. Value list properties only. Weights the values by their order (leaving out values that aren't allowed) instead of by frequency. The values must then all be common and have no `frequencyWhen`. |
| `range` | Integer Range | Integer properties only. Optional if both `player` and `npc` have a `range`; otherwise required. |
| `groups` | list of Integer Group | Optional. Integer properties only. Must not be empty. Named parts of the numbers that conditions can use. |
| `defaults` | String Defaults | Optional. String properties only. |
| `player` | Role Settings | Optional. Replaces settings for the player. |
| `npc` | Role Settings | Optional. Replaces settings for non-player characters. |

Value list properties pick a random value, by frequency or by shape, from the values whose conditions are met, and pick a new one when their value's condition stops being met. Integer properties pick a random number in their range, by its shape. String properties pick a random default, or are blank if they have no defaults. On the new game screen, the player must fill in every string property that has a value. A property is hidden while it has no value or has only one possible value for the player.

A property **depends on** the properties its conditions, its values' conditions, and its values' `frequencyWhen` conditions use, and on the properties its defaults (for any role) are keyed by. Values are chosen, and properties are shown on the new game screen, in an order where each property comes after the properties it depends on. Otherwise, properties keep the order they're listed in. A property must not depend on itself, directly or through other properties.

**Character Value**

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | |
| `frequency` | string | Optional (defaults to `common`). Frequency. |
| `text` | map of string to string | Optional (defaults to empty). Named text for templates. Names and text must be filled in. |
| `when` | Condition | Optional. A character can only have this value when the condition is met. |
| `prominence` | integer | Optional (defaults to 3). From 1 to 3: how hard the value is to hide. Obscurers hide it with a strength of at least this. |
| `frequencyWhen` | list of Frequency Override | Optional. Must not be empty. Frequencies that replace `frequency` when their conditions are met. The first override whose condition is met is used. Chances are worked out from the usual frequencies of the values that are allowed. A value with an override then has the chance a value of its new frequency has there (values moved to a frequency no value usually has share that frequency's chance), and all the chances are scaled to add up to 100, so the other values keep their chances compared to each other. |

**Frequency Override**

| Property | Type | Notes |
|---|---|---|
| `when` | Condition | Can't use `hidden`. |
| `frequency` | string | Frequency. |

**Integer Group**

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in. Unique within `groups`. |
| `minimum` | integer | Optional (no lower limit if left out). |
| `maximum` | integer | Optional (no upper limit if left out). At least `minimum`. |

Groups must not overlap. A number in no group counts as having no value in conditions.

**Integer Range**

| Property | Type | Notes |
|---|---|---|
| `minimum` | integer | |
| `maximum` | integer | At least `minimum`. |
| `shape` | Shape | Weights the numbers in the range by their order. |

**Shape**

Weights an ordered list of values by a bell curve. The value at the peak is the most likely, and values grow less likely the farther they are from it.

| Property | Type | Notes |
|---|---|---|
| `peak` | string | `low`, `middle`, or `high`: whether the first, middle, or last value is the most likely. |
| `mostWithin` | number | More than 0. About two-thirds of choices fall within this many steps of the peak (the curve's standard deviation). |

**String Defaults**

Has either `values`, or both `keyedBy` and `valuesByKey`.

| Property | Type | Notes |
|---|---|---|
| `values` | list of Weighted Value | Optional. Must not be empty. |
| `keyedBy` | string | Optional. Id of a value list property. The defaults used depend on that property's value, and a new default is picked when it changes. |
| `valuesByKey` | map of string to list of Weighted Value | Optional. Each key must be a value id of the `keyedBy` property, and each list must not be empty. Every value the `keyedBy` property can have, for each role these defaults are used by, must have a list. |

**Weighted Value**

| Property | Type | Notes |
|---|---|---|
| `value` | string | Filled in. |
| `frequency` | string | Optional (defaults to `common`). Frequency. |

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

## Mods

A mod is a folder of files that change the data files. The game applies the mods in its mod list, in order.

* A mod folder can have a file with the same name as any data file (`GameInfo.json`, `Characters.json`, or `Locations.json`). Each one is optional. Other JSON files are ignored.
* Each data file is read, the file with the same name in each mod folder is applied to it in order, and then the result is read and checked as described above. Problems found in the result are reported as coming from the mods.
* A mod folder that doesn't exist, or any problem in a mod file, stops the game data from loading.
* Mods can't change `title` or `icon` in GameInfo.json.

A mod file is JSON, with comments and trailing commas allowed, holding a list of Operations. They're applied in order, and every problem is reported.

**Operation**

| Property | Type | Notes |
|---|---|---|
| `op` | string | `insert`, `replace`, `set`, `remove`, or `move`. Case doesn't matter. |
| `path` | string | Path to the data the operation changes. |
| `value` | any | Required for `insert`, `replace`, and `set`. The data to add. Can be `null`. |
| `index` | integer | Optional. `insert` and `move` only. A position in the list, counting from 0. |
| `before` | string | Optional. `insert` and `move` only. Filled in. The id of the list item to go before. |
| `after` | string | Optional. `insert` and `move` only. Filled in. The id of the list item to go after. |

At most one of `index`, `before`, and `after` can be given.

| Op | Does |
|---|---|
| `insert` | Adds `value` to the list at `path`: at `index` (from 0 to the list's length), before or after the item with the given id, or else at the end. If `path` is a property that an object doesn't have, it's added as an empty list first. |
| `replace` | Replaces the property or list item at `path`, which must exist, with `value`. |
| `set` | Sets the object property at `path` to `value`, adding it if it doesn't exist. |
| `remove` | Removes the property or list item at `path`, which must exist. |
| `move` | Moves the list item at `path` to `index` (its position in the list once it's taken out), or before or after the item with the given id. One of them is required, and the id can't be the item's own. |

**Path**

A path is a series of steps that starts from the top of the file, such as `properties[hair].values[red].frequencyWhen[0]`. Every step but the last must lead to data that exists.

* A name, at the start of the path or after a `.`, picks a property of an object.
* `[text]` picks a property of an object, or an item of a list. In a list, a whole number picks the item at that index, counting from 0, and anything else picks the first item whose `id` is that text. It can't contain `]`.
* `['text']` is the same, except that the text is always a property name or an id, and can contain `.`, `[`, and `]`, but not `']`.
* Property names match exactly if possible, or else ignoring case. Ids are case-sensitive.

**Examples**

The ids in these examples are made up. Each example is a whole mod file, but a mod file can hold any number of operations.

Add a new property to Characters.json, after the `hairColor` property:

```json
[
	{
		"op": "insert",
		"path": "properties",
		"after": "hairColor",
		"value": {
			"id": "eyeColor",
			"title": "Eye Color",
			"type": "valueList",
			"values": [
				{ "id": "brown", "title": "Brown" },
				{ "id": "blue", "title": "Blue", "frequency": "uncommon" },
				{ "id": "green", "title": "Green", "frequency": "rare" }
			]
		}
	}
]
```

Change the frequency of the `red` value of the `hairColor` property (this adds `frequency` if the value didn't have one):

```json
[
	{ "op": "set", "path": "properties[hairColor].values[red].frequency", "value": "veryRare" }
]
```

Remove the `when` condition from the `red` value of the `hairColor` property:

```json
[
	{ "op": "remove", "path": "properties[hairColor].values[red].when" }
]
```

Add a frequency override to the start of the `red` value's `frequencyWhen` list (the list is created if the value didn't have one). Leave out `index` to add it at the end instead:

```json
[
	{
		"op": "insert",
		"path": "properties[hairColor].values[red].frequencyWhen",
		"index": 0,
		"value": { "when": { "values": { "region": [ "north" ] } }, "frequency": "common" }
	}
]
```

Replace the `red` value's whole `frequencyWhen` list, and change the frequency of the second override of the `blond` value:

```json
[
	{
		"op": "replace",
		"path": "properties[hairColor].values[red].frequencyWhen",
		"value": [
			{ "when": { "values": { "region": [ "north" ] } }, "frequency": "common" }
		]
	},
	{ "op": "set", "path": "properties[hairColor].values[blond].frequencyWhen[1].frequency", "value": "rare" }
]
```

Replace the whole `blond` value of the `hairColor` property, keeping its place in the list:

```json
[
	{
		"op": "replace",
		"path": "properties[hairColor].values[blond]",
		"value": { "id": "blond", "title": "Golden", "frequency": "uncommon" }
	}
]
```

Remove the `blue` value of the `hairColor` property, and the whole `scar` property:

```json
[
	{ "op": "remove", "path": "properties[hairColor].values[blue]" },
	{ "op": "remove", "path": "properties[scar]" }
]
```

Move the `hairColor` property to the start of the list, and its `black` value to just after its `brown` value:

```json
[
	{ "op": "move", "path": "properties[hairColor]", "index": 0 },
	{ "op": "move", "path": "properties[hairColor].values[black]", "after": "brown" }
]
```

Make the `hood` obscurer hide every property more strongly, using a quoted step for the `*` key:

```json
[
	{ "op": "set", "path": "obscurers[hood].hides['*']", "value": 2 }
]
```

Add a location to Locations.json, connect an existing location to it, and remove the last paragraph of another location's long description:

```json
[
	{
		"op": "insert",
		"path": "locations",
		"value": {
			"id": "mill",
			"title": "Mill",
			"shortDescription": "The mill by the river.",
			"mediumDescription": "The mill wheel turns slowly in the current.",
			"longDescription": [ "The mill wheel turns slowly in the current." ],
			"connections": [ "square" ]
		}
	},
	{ "op": "insert", "path": "locations[square].connections", "value": "mill" },
	{ "op": "remove", "path": "locations[church].longDescription[2]" }
]
```

Make rare items more common, in GameInfo.json. This only works if the file has `frequencyChances`, since every step but the last must exist; otherwise, set `frequencyChances` to a whole object instead:

```json
[
	{ "op": "set", "path": "frequencyChances.rare", "value": 8 }
]
```
