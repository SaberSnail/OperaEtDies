# Mods

A mod is a folder of JSON files that change the data described in [DataFiles.md](DataFiles.md). The game applies the mods in its mod list, in order.

* The data folder's files are merged first (see Files and Sections in DataFiles.md). Then each mod folder's `.json` files, including those in subfolders, are applied in order of their paths. File names don't matter.
* A mod file holds either **data**, an object of sections merged in the same way as a data file, or a list of **operations**, applied in order to the merged data.
* The result is then read and checked as described in DataFiles.md. Problems found in it are reported as coming from the mods.
* A mod folder that doesn't exist, or any problem in a mod file, stops the data from loading, and every problem is reported.
* Mods can't change `game.title` or `game.icon`.

**Operation**

| Property | Type | Notes |
|---|---|---|
| `op` | string | `insert`, `replace`, `set`, `remove`, `move`, or `copy`. Case doesn't matter. |
| `path` | string | Path to the data the operation changes. |
| `value` | any | Required for `replace` and `set`. For `insert`, one of `value` and `values` is required. Can be `null`. |
| `values` | list | Optional. `insert` only, not with `value`. The items to add, in order. Can be empty. |
| `changes` | list | Optional. `copy` only. Operations applied to the copy before it's added, with paths starting from the copy. Can be empty. |
| `index` | integer | Optional. `insert`, `move`, and `copy` only. A position in the list, from 0. |
| `before` | string | Optional. `insert`, `move`, and `copy` only. The id of the item to go before. |
| `after` | string | Optional. `insert`, `move`, and `copy` only. The id of the item to go after. |

At most one of `index`, `before`, and `after` can be given.

| Op | Does |
|---|---|
| `insert` | Adds `value`, or each of `values`, to the list at `path`: at `index` (from 0 to the list's length), before or after the given item, or else at the end. A missing object property is added as an empty list first. |
| `replace` | Replaces the existing property or list item at `path` with `value`. |
| `set` | Sets the object property at `path` to `value`, adding it if needed. |
| `remove` | Removes the existing property or list item at `path`. |
| `move` | Moves the list item at `path` to `index` (its position once taken out), or before or after the given item (not itself). One is required. |
| `copy` | Copies the list item at `path`, applies `changes` to the copy, and adds it to the same list: at `index` (counting the original), before or after the given item, or else right after the original. If a change fails, the copy isn't added. Problems in the changes are numbered after the operation, as in 3.2. A copy of an item with an `id` needs a new one. |

**Path**

A path is a series of steps from the top of the merged data, such as `properties[hair].values[red].frequencyWhen[0]`. Every step but the last must lead to data that exists.

* A name, at the start or after a `.`, picks an object property.
* `[text]` picks an object property or a list item. In a list, a whole number picks the item at that index, from 0, and anything else picks the item with that `id`. It can't contain `]`.
* `['text']` is the same, except the text is always a name or id, and can contain `.`, `[`, and `]`, but not `']`.
* Names match exactly if possible, or else ignoring case. Ids are case-sensitive.

**Examples**

The ids are made up. Each example is a whole mod file.

Add a property, and a value to another property, as data:

```json
{
	"properties": [
		{
			"id": "eyeColor",
			"title": "Eye Color",
			"type": "valueList",
			"values": [
				{ "id": "brown", "title": "Brown" },
				{ "id": "blue", "title": "Blue", "frequency": "uncommon" }
			]
		}
	],
	"professions": [
		{ "id": "cobbler", "title": "Cobbler", "business": { "wealth": "low", "worksFromHome": true } }
	]
}
```

Add a property after the `hairColor` property:

```json
[
	{
		"op": "insert",
		"path": "properties",
		"after": "hairColor",
		"value": { "id": "eyeColor", "title": "Eye Color", "type": "valueList", "values": [ { "id": "brown", "title": "Brown" } ] }
	}
]
```

Change a value's frequency (adding `frequency` if it's missing), and remove another value's condition:

```json
[
	{ "op": "set", "path": "properties[hairColor].values[red].frequency", "value": "veryRare" },
	{ "op": "remove", "path": "properties[hairColor].values[blond].when" }
]
```

Add a frequency override to the start of a value's `frequencyWhen` (creating the list if needed):

```json
[
	{
		"op": "insert",
		"path": "properties[hairColor].values[red].frequencyWhen",
		"index": 0,
		"value": { "when": { "values": { "region": "north" } }, "frequency": "common" }
	}
]
```

Add two values just before the `red` value:

```json
[
	{
		"op": "insert",
		"path": "properties[hairColor].values",
		"before": "red",
		"values": [
			{ "id": "auburn", "title": "Auburn", "frequency": "uncommon" },
			{ "id": "white", "title": "White", "frequency": "rare" }
		]
	}
]
```

Replace a whole value in place, remove another value and a whole property, and move a property to the start:

```json
[
	{ "op": "replace", "path": "properties[hairColor].values[blond]", "value": { "id": "blond", "title": "Golden" } },
	{ "op": "remove", "path": "properties[hairColor].values[blue]" },
	{ "op": "remove", "path": "properties[scar]" },
	{ "op": "move", "path": "properties[hairColor]", "index": 0 }
]
```

Add a `copper` value right after `red` by copying it:

```json
[
	{
		"op": "copy",
		"path": "properties[hairColor].values[red]",
		"changes": [
			{ "op": "set", "path": "id", "value": "copper" },
			{ "op": "set", "path": "title", "value": "Copper" }
		]
	}
]
```

Draw the sixth shape of the `bosom` portrait layer with a different outline for barmaids. The copy goes right after the original, which is then left out for barmaids. This only works if the shape has a `when` with `values`:

```json
[
	{
		"op": "copy",
		"path": "portraitLayers[bosom].shapes[5]",
		"changes": [
			{ "op": "set", "path": "stroke", "value": "skinShade" },
			{ "op": "set", "path": "when.values.profession", "value": [ "barmaid" ] }
		]
	},
	{ "op": "set", "path": "portraitLayers[bosom].shapes[5].when.notValues", "value": { "profession": [ "barmaid" ] } }
]
```

Make an obscurer hide every property more strongly, using a quoted step for the `*` key:

```json
[
	{ "op": "set", "path": "obscurers[hood].hides['*']", "value": 2 }
]
```

Let a quarter hold one more business, remove a paragraph of another's long description, and let smiths have a second business with helpers of any gender:

```json
[
	{ "op": "set", "path": "quarters[square].businesses", "value": 4 },
	{ "op": "remove", "path": "quarters[lane].description.long[2]" },
	{ "op": "set", "path": "professions[smith].business.maxBusinesses", "value": 2 },
	{ "op": "remove", "path": "professions[smith].business.helperWhen" }
]
```

Make rare items more common. This only works if the data sets `game.frequencyChances`; otherwise, set it to a whole object:

```json
[
	{ "op": "set", "path": "game.frequencyChances.rare", "value": 8 }
]
```
