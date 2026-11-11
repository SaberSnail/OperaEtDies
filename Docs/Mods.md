# Mods

A mod is a folder of files that change the data files described in [DataFiles.md](DataFiles.md). The game applies the mods in its mod list, in order.

* A mod folder can have a file with the same name as any data file (`GameInfo.json`, `Characters.json`, `Locations.json`, `Professions.json`, or `Families.json`). Each one is optional. Other JSON files are ignored.
* Each data file is read, the file with the same name in each mod folder is applied to it in order, and then the result is read and checked as described in DataFiles.md. Problems found in the result are reported as coming from the mods.
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

Add a house to Locations.json, let the `square` quarter hold one more business, and remove the last paragraph of the `lane` quarter's long description:

```json
[
	{
		"op": "insert",
		"path": "houses",
		"value": {
			"id": "cottage",
			"title": "Cottage",
			"shortDescription": "A small cottage.",
			"mediumDescription": "The cottage has a single room with a hearth.",
			"longDescription": [ "The cottage has a single room with a hearth." ],
			"wealth": "low"
		}
	},
	{ "op": "set", "path": "quarters[square].businesses", "value": 4 },
	{ "op": "remove", "path": "quarters[lane].longDescription[2]" }
]
```

Allow the `smith` profession of Professions.json to have a second business and a helper of any gender:

```json
[
	{ "op": "set", "path": "professions[smith].maxBusinesses", "value": 2 },
	{ "op": "set", "path": "professions[smith].helperWhen", "value": { "values": { "gender": [ "male", "female" ] } } }
]
```

Make rare items more common, in GameInfo.json. This only works if the file has `frequencyChances`, since every step but the last must exist; otherwise, set `frequencyChances` to a whole object instead:

```json
[
	{ "op": "set", "path": "frequencyChances.rare", "value": 8 }
]
```
