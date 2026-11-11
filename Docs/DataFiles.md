# Data Files

The game reads its data from the `Data` folder next to the executable. Every file listed here is required, except for files referenced by optional fields. Mods can change the data files (see [Mods.md](Mods.md)).

## General Rules

* Files are JSON. Comments and trailing commas are allowed.
* Property names are camelCase, but matching is case-insensitive. Unknown properties are ignored.
* Every property is required unless marked optional. A required property can't be missing or `null`, and list items can't be `null`.
* Text values described as "filled in" can't be empty or only whitespace.
* Ids are compared case-sensitively and must be unique within their list.
* Lists of identified items must not be empty, and each item needs a filled-in `id` and `title`.
* A **frequency** is how common an item is when chosen at random: `common`, `uncommon`, `rare`, or `veryRare`. It can be nudged up by adding `+` or `++`, or down by adding `-` or `--`, as in `common-` or `rare++`. Each step multiplies or divides the item's chance by the `stepMultiplier`, after the chances below are worked out, and then the chances are scaled to add up to 100. In a list, the uncommon items share the uncommon chance (see `frequencyChances` in GameInfo.json) equally, the rare and very rare items share theirs, and the common items share the rest. If a list has no items of a frequency, its chance goes to the common items, or is shared by the other frequencies in proportion to their chances if the list has no common items. These shares are worked out from the whole list. When some items can't be chosen (because their conditions aren't met, a limit has been reached, or they've already been used), they're left out, and the remaining items keep their chances compared to each other.
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

A **template** is text with placeholders. `{id}` is replaced with the visible value of the property with that id (the text of a string property, the number of an integer property, or the `title` of a value list property's value), or with the text of the template with that id. `{id.name}` is replaced with the `name` entry of the `text` of a value list property's value. Either form can end with a format after a colon, as in `{id.name:first_upper}`. The only format is `first_upper`, which capitalizes the first character. Literal braces are written `{{` and `}}`. Any other brace is an error. Every placeholder must name a property or a template, and a placeholder with a name must name a value list property whose values all have that `text` entry (leaving out values the variant's `when` condition rules out with its `values` or `notValues` for that property). A template must not use itself, directly or through other templates, in any of its variants. In the text a template creates, runs of spaces are collapsed to one, and spaces at the start and end, or before `.`, `,`, `;`, `:`, `!`, or `?`, are removed.

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
| `setByGame` | boolean | Optional (defaults to false). Whether the game sets the property's value itself. The property isn't shown on the new game screen. |
| `follows` | string | Optional. Id of another property, not this one. No two properties can follow the same property. This property comes right after that one (see below). |
| `values` | list of Character Value | Value list properties only. Required unless `valuesFrom` is given, and not allowed with it. Identified items. |
| `valuesFrom` | string | Optional. Value list properties only. `professions`: the property's values are the professions in Professions.json, each used as a Character Value. If Professions.json doesn't load, the property has no values, which is an error. |
| `shape` | Shape | Optional. Value list properties only. Weights the values by their order (leaving out values that aren't allowed) instead of by frequency. The values must then all be common and have no `frequencyWhen`. |
| `range` | Integer Range | Integer properties only. Optional if both `player` and `npc` have a `range`; otherwise required. |
| `groups` | list of Integer Group | Optional. Integer properties only. Must not be empty. Named parts of the numbers that conditions can use. |
| `defaults` | String Defaults | Optional. String properties only. |
| `player` | Role Settings | Optional. Replaces settings for the player. |
| `npc` | Role Settings | Optional. Replaces settings for non-player characters. |

Value list properties pick a random value, by frequency or by shape, from the values whose conditions are met, and pick a new one when their value's condition stops being met. Integer properties pick a random number in their range, by its shape. String properties pick a random default, or are blank if they have no defaults. On the new game screen, the player must fill in every string property that has a value. A property is hidden while it has no value or has only one possible value for the player.

A property **depends on** the properties its conditions, its values' conditions, and its values' `frequencyWhen` conditions use, and on the properties its defaults (for any role) are keyed by. Values are chosen, and properties are shown on the new game screen, in an order where each property comes after the properties it depends on, and a property with `follows` comes right after the property it follows. The followed property waits until its follower can be placed too, so it also comes after the properties its follower depends on. Otherwise, properties keep the order they're listed in. A property must not depend on itself, directly or through other properties, counting a property as depending on the property it follows, and a followed property as depending on what its follower depends on.

**Character Value**

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | |
| `frequency` | string | Optional (defaults to `common`). Frequency. |
| `text` | map of string to string | Optional (defaults to empty). Named text for templates. Names and text must be filled in. |
| `when` | Condition | Optional. A character can only have this value when the condition is met. |
| `prominence` | integer | Optional (defaults to 3). From 1 to 3: how hard the value is to hide. Obscurers hide it with a strength of at least this. |
| `frequencyWhen` | list of Frequency Override | Optional. Must not be empty. Frequencies that replace `frequency` when their conditions are met. The first override whose condition is met is used. Chances are worked out from the usual frequencies of all of the property's values. A value with an override then has the chance a value of its new frequency has there (values moved to a frequency no value usually has share that frequency's chance), and all the chances are scaled to add up to 100, so the other values keep their chances compared to each other. |

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
| `peak` | string | `low`, `middle`, or `high`: whether the first, middle, or last value is the most likely. It can be moved up by adding `+` or `++`, or down by adding `-` or `--`, as in `middle--`. Each step moves the peak by a tenth of the distance from the first value to the last. `low` can't be moved down, and `high` can't be moved up. |
| `mostWithin` | number | More than 0. About two-thirds of choices fall within this many steps of the peak (the curve's standard deviation). |

**String Defaults**

Has either `values`, or `keyedBy` with `valuesByKey`. When `keyedBy` names a property whose values come from the professions, `valuesByKey` is left out and comes from the professions instead: each profession's `defaults` entry for this property is its list. Only keyed defaults can have `otherValues`.

| Property | Type | Notes |
|---|---|---|
| `values` | list of Weighted Value | Optional. Must not be empty. |
| `keyedBy` | string | Optional. Id of a value list property. The defaults used depend on that property's value, and a new default is picked when it changes. |
| `valuesByKey` | map of string to list of Weighted Value | Optional. Each key must be a value id of the `keyedBy` property, and each list must not be empty. Unless there are `otherValues`, every value the `keyedBy` property can have, for each role these defaults are used by, must have a list. |
| `otherValues` | list of Weighted Value | Optional. Must not be empty. Other values, used for a key with no list of its own. They're also what the player can get on the new game screen: randomizing just this property picks one of them instead of a usual value. Randomizing everything, or changing the `keyedBy` property, picks a usual value again. |

**Weighted Value**

| Property | Type | Notes |
|---|---|---|
| `value` | string | Filled in. |
| `frequency` | string | Optional (defaults to `common`). Frequency. |

**Role Settings**

Each setting is only allowed on properties of the type it applies to, and replaces the property's setting of the same name.

| Property | Type | Notes |
|---|---|---|
| `values` | list of string | Optional. Value list properties only. Ids of the property's values allowed for the role. If empty, characters of the role never have the property. Otherwise, each id must be filled in, unique, and match a value. |
| `range` | Integer Range | Optional. Integer properties only. |
| `defaults` | String Defaults | Optional. String properties only. |

## Locations.json

The templates the town is built from when a new game starts.

| Property | Type | Notes |
|---|---|---|
| `quarterKinds` | list of Quarter Kind | Must not be empty. Each item needs a filled-in, unique `id`. |
| `landmarkCount` | Integer Range | The minimum is at least 0. How many landmark quarters the town has, counting landmarks of unique kinds. |
| `layouts` | list of Layout | Must not be empty. The shapes the town can be laid out in, chosen by frequency. |
| `features` | list of Map Feature | Optional (defaults to empty). Identified items, if any. The things on the map that aren't places to go, such as a river or a forest. |
| `featureCount` | Integer Range | Required if there are `features`. The minimum is at least 0. How many features a town has. |
| `quarters` | list of Quarter | Identified items. |
| `houseTitle` | string | Filled in. Template (see Characters.json) for the title of every house, filled in with the Characters.json properties of the head of the household living there. It can only name properties, not character templates. |
| `houses` | list of House | Identified items, except that houses have no `title`. |
| `businesses` | list of Business | Identified items. |

There must be at least one house, and at least one quarter with houses, for each of the `low`, `medium`, and `high` wealth levels. Every unique kind must have at least one quarter, and there must be at least one landmark quarter if `landmarkCount` can be more than 0. Ids must be unique across quarters, houses, and businesses.

**Quarter Kind**

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `area` | string | `village` or `outskirts`. The part of the map quarters of this kind are in. |
| `unique` | boolean | Optional (defaults to false). Whether every town has exactly one quarter of this kind. |
| `path` | string | Filled in. What a path to or from a quarter of this kind is called. |
| `borders` | string | Optional. Not allowed for unique kinds. The id of one of `features`. Every quarter of this kind borders that feature, and is only used in towns that have it. |

**Layout**

| Property | Type | Notes |
|---|---|---|
| `shape` | string | `radial`, `linear`, `crossroads`, or `warren` (see Laying out the town). |
| `center` | string | The id of a unique kind in the `village` area. The quarter the shape is built around. |
| `frequency` | string | Optional (defaults to `common`). Frequency. |

**Map Feature**

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | The name shown on the map. |
| `type` | string | `river`: a channel one cell wide running straight across the whole map. `area`: a rectangle placed in empty space next to the quarters that border it. |
| `frequency` | string | Optional (defaults to `common`). Frequency. How likely the feature is to be in a town. |
| `size` | Quarter Size | Required for an `area`, and not allowed for a `river`. Each part at least 1. |
| `crossings` | list of string | Required for a `river`, and not allowed for an `area`. Must not be empty, and each must be filled in and unique. What a path across the river can be called, such as `bridge` or `ford`. |
| `text` | list of string | Must not be empty. Sentences that can be added to the description of a quarter that borders the feature. Each is filled in, and is a template (see Characters.json) whose only placeholder is `{direction}`, optionally with a format, such as `{direction:first_upper}`. It's replaced with the direction the feature is in, such as `north-east`. |

**Location Template**

Quarters, houses, and businesses all have these properties.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Required for quarters and businesses. Not allowed for houses, which are titled by `houseTitle`. |
| `shortDescription` | string | Filled in. One sentence, shown when listed as a place to go. |
| `mediumDescription` | string | Filled in. One paragraph, shown on arrival. |
| `longDescription` | list of string | Must not be empty, and each paragraph must be filled in. Shown when examining the location. |
| `frequency` | string | Optional (defaults to `common`). Frequency. How likely the template is to be chosen. |

**Quarter**

A Location Template with these properties too. A quarter is a group of houses and businesses, or a landmark if it has neither. Quarters connect to each other, and each house and business connects only to its quarter.

| Property | Type | Notes |
|---|---|---|
| `kind` | string | Must be the id of one of `quarterKinds`. Decides where the quarter goes on the map, which businesses can be in it, and what its paths are called. |
| `wealth` | string | Required if the quarter has `houses`, and not allowed otherwise. `low`, `medium`, or `high`. The wealth level of the households living in the quarter. |
| `houses` | Count Range | Optional (no houses). How many houses the quarter holds. The minimum is at least 0, and the maximum is at least the minimum and at least 1. |
| `businesses` | integer | Optional (defaults to 0). At least 0. The most businesses the quarter holds. |
| `size` | Quarter Size | Optional (defaults to 1 by 1). The quarter's size on the map, which can be turned either way. |
| `borders` | string | Optional. Not allowed for quarters of unique kinds. The id of one of `features` the quarter borders, beyond any its kind borders. The quarter is only used in towns that have it. |
| `featureText` | map of string to string | Optional. Each key is the id of one of `features`, and each value is a sentence like the feature's `text`. Used instead of the feature's own text when the quarter borders it. |

**Quarter Size**

| Property | Type | Notes |
|---|---|---|
| `length` | integer | Optional (defaults to 1). At least 1. |
| `width` | integer | Optional (defaults to 1). At least 1. |

**Count Range**

| Property | Type | Notes |
|---|---|---|
| `minimum` | integer | |
| `maximum` | integer | |

**House**

A Location Template with this property too.

| Property | Type | Notes |
|---|---|---|
| `wealth` | string | `low`, `medium`, or `high`. The wealth level of the household living in the house. |

**Business**

A Location Template with these properties too. A place a business is run from.

| Property | Type | Notes |
|---|---|---|
| `profession` | string | Filled in. The id of a profession in Professions.json that doesn't work from home. The profession whose businesses can be run from this place. |
| `quarterKinds` | list of string | Must not be empty. Each kind must be filled in, unique, and one of `quarterKinds`. At least one of the kinds must have a quarter with room for businesses. The kinds of quarter the business can be in. |

## Professions.json

The professions: the values of the profession property, and how their businesses work. Every profession is a business except the `homemaker` and `infirm` professions named in Families.json.

| Property | Type | Notes |
|---|---|---|
| `professions` | list of Profession | Must not be empty. |

**Profession**

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in and unique. |
| `title` | string | Filled in. |
| `defaults` | map of string to list of Weighted Value | Optional. Each key must be a string property whose defaults are keyed by the profession property and take their lists from the professions (see String Defaults), and each list must not be empty. The profession's usual values for that property, such as its last names. |
| `frequency`, `frequencyWhen`, `text`, `when`, `prominence` | | The same as for a Character Value. The profession is a value of every Characters.json property with `valuesFrom` set to `professions`, and these settings are checked there. |
| `wealth` | string | Required for businesses (see below), and not allowed otherwise. Wealth level, optionally followed by `+`, `++`, `-`, or `--`. Each step moves a fifth of the way toward the next level up or down, so a step up adds a fifth of the level's amount and a step down takes away a tenth: `low++` is 2.8, `medium--` is 3.2, and `high-` is 7.2. How much a business in the profession earns. |
| `maxBusinesses` | integer | Optional (no limit). Businesses only. At least 1. The most businesses in the profession the town can have. |
| `helpers` | integer | Optional (no limit). Businesses only. At least 0. The most members of the owner's household that can help in each business. |
| `helperWhen` | Condition | Optional. Businesses only. Can't use `hidden`. A condition on Characters.json properties that a character must meet to help. Without it, a helper must be allowed to have the profession (by the profession value's `when`). |
| `worksFromHome` | boolean | Optional (defaults to false). Businesses only. Whether the business has no place of its own. If false, at least one business in Locations.json must be for the profession; if true, none can be. |
| `forHouseholdElders` | boolean | Optional (defaults to false). Businesses only. Only allowed if `worksFromHome` is true. Whether a household elder can take up the profession as their own business. |

## Families.json

How households are generated. A **chance** is a percentage from 0 to 100. A **wealth level** is `veryLow`, `low`, `medium`, or `high`, worth 1, 2, 4, and 8.

| Property | Type | Notes |
|---|---|---|
| `properties` | Family Properties | |
| `male` | string | Filled in. Id of the gender property's value for men. |
| `female` | string | Filled in. Id of the gender property's value for women. |
| `homemaker` | string | Filled in. Id of the profession property's value for a household elder who keeps house. It isn't a business. |
| `infirm` | string | Filled in. Id of the profession property's value for a household elder who is too frail to work. It isn't a business. |
| `independent` | string | Filled in. Id of the work role property's value for someone who doesn't help in a household business. |
| `helper` | string | Filled in. Id of the work role property's value for someone who helps in a household business. |
| `head` | string | Filled in. Id of the household role property's value for the head of a household. |
| `member` | string | Filled in. Id of the household role property's value for everyone else in a household. |
| `familyCount` | Integer Range | The minimum is at least 0. How many households the town has besides the player's. |
| `femaleHeadChance` | number | Chance. The chance that the head of a household is a woman. |
| `femaleHeadMinWealth` | string | Wealth level. The lowest wealth a woman at the head of a household can have from her profession. |
| `headAge` | Integer Range | Within the age property's range for non-player characters. The maximum must be at least the youngest age at which the player can have an adult child: the youngest age in that range plus `minParentAge`. The age of the head of a household. The player's age uses the part of this range from that youngest age up, with the same weights. |
| `wifeChance` | number | Chance. The chance that a man at the head of a household has a wife. |
| `wifeAgeDifference` | Integer Range | The wife's age minus her husband's age. |
| `householdElderChance` | number | Chance. The chance that a household elder lives with the household. |
| `femaleHouseholdElderChance` | number | Chance. The chance that a household elder is a woman, when a man is allowed. |
| `householdElderAgeGap` | Shape | Weights how much older a household elder is than their child, from `minParentAge` to the oldest age at which the elder could have had the child. |
| `householdElderMinAge` | Gender Ages | Each at least 0. The youngest age at which a man or a woman can be a household elder. |
| `infirmChance` | map of string to number | Optional (no chance). Each key must be the id of one of the age property's groups, and each value is a chance. The chance that a woman who is a household elder is infirm, by the group her age is in. |
| `householdElderHomemakerChance` | number | Chance. The chance that a woman who is a household elder, and doesn't help in a household business, keeps house rather than taking up a profession for household elders. |
| `minParentAge` | integer | At least 0. The youngest age at which someone can have a child. |
| `maxMotherAge` | integer | At least `minParentAge`. The oldest age at which a woman can have a child. |
| `maxFatherAge` | integer | At least `minParentAge`. The oldest age at which a man can have a child. |
| `childCount` | Integer Range | The minimum is at least 0. How many children live with the household, if their parents' ages allow it. |
| `childAge` | Shape | Weights the age of the first child chosen, over the ages the children could be, from youngest to oldest. |
| `siblingAgeSpread` | number | More than 0. About two-thirds of the other children are within this many years of the first child chosen. |
| `sonChance` | number | Chance. The chance that a child is a son, when a man is allowed. |
| `childHomemakerChance` | number | Chance. The chance that a daughter who doesn't help in a household business keeps house as a homemaker. |
| `helperChance` | number | Chance. The chance that a member of the household helps in a household business, when they're allowed to and there's room. |
| `helperWealth` | number | From 0 to 1. The part of a business's wealth that its first helper adds to their household's wealth. |
| `helperWealthFalloff` | number | From 0 to 1. Each helper after the first in a business adds this fraction of what the helper before them added. |
| `homemakerWealth` | number | What each homemaker adds to their household's wealth. |
| `infirmWealth` | number | What each infirm member adds to their household's wealth. Can be negative. |
| `householdFortune` | Integer Range | The minimum is at least 0. A percentage each household's wealth is multiplied by, for its good or bad fortune. |
| `headAgeWealth` | map of string to number | Optional (100 for every group). Each key must be the id of one of the age property's groups, and each value is at least 0. A percentage a household's wealth is multiplied by, by the group the head's age is in. Groups that aren't listed are 100. |
| `wealthThresholds` | Wealth Thresholds | |

**Family Properties**

The ids of the Characters.json properties that generating households depends on. Each must match a property of the given type.

| Property | Type | Notes |
|---|---|---|
| `gender` | string | Filled in. A `valueList` property. Must have the `male` and `female` values. |
| `age` | string | Filled in. An `integer` property. It can't have a `player` range, since the player's age comes from `headAge`. |
| `profession` | string | Filled in. A `valueList` property with `valuesFrom` set to `professions`. Must have the `homemaker` and `infirm` values. |
| `lastName` | string | Filled in. A `string` property. Its non-player defaults must have `otherValues`, which are the other last names households use. |
| `workRole` | string | Filled in. A `valueList` property with `setByGame`. Must have the `independent` and `helper` values. |
| `householdRole` | string | Filled in. A `valueList` property with `setByGame`. Must have the `head` and `member` values. |

**Gender Ages**

| Property | Type | Notes |
|---|---|---|
| `male` | integer | The age for men. |
| `female` | integer | The age for women. |

**Wealth Thresholds**

| Property | Type | Notes |
|---|---|---|
| `medium` | number | At least 0. The household wealth needed for the `medium` wealth level. |
| `high` | number | At least `medium`. The household wealth needed for the `high` wealth level. |

**Generating households.** The player's household is made first, with the player at its head. Then `familyCount` more households are made, one at a time. Every character other than the player uses the non-player settings of Characters.json, and gets the household's last name unless noted. Only professions in Professions.json are businesses; the `homemaker` and `infirm` values are only given to household elders. Each head, including the player, gets the `head` household role, and everyone else gets `member`.

* The head is a woman with `femaleHeadChance`, and otherwise a man. A woman at the head of a household lives with no men. A non-player head's age is chosen from `headAge`, and so is the player's (see `headAge`). The head's usual last name is their `lastName` property's value, but one of the other last names that no household uses yet is used instead if the usual name is blank or already used, or another household's head has the same profession.
* A man has a wife with `wifeChance`. The player always has one, if the ages allow a wife who could be the mother of an adult child.
* Then the children are added, from oldest to youngest. Their ages must be in the age property's range and differ from each other, and each parent's age minus the child's must be from `minParentAge` to that parent's maximum. The player always has at least one child if the ages allow it.
* Then a household elder is added with `householdElderChance`. They're the parent of the head or the wife. A wife's parent, and sometimes a widow's own parent, has one of the other last names. A household elder whose age would be outside the age property's range, or below their gender's `householdElderMinAge`, isn't added.
* A man who is a household elder is infirm. A woman is infirm with the `infirmChance` for her age's group. Otherwise, she helps in a household business if she would as a member (below). If not, she keeps house as a homemaker with `householdElderHomemakerChance`, and otherwise takes up a profession with `forHouseholdElders` that she's allowed to have and whose `maxBusinesses` hasn't been reached, chosen by frequency. If there's no such profession, she keeps house. Household elders never start any other business.
* Each member besides the head helps in a household business, with `helperChance`, if they're allowed to and it has room for another helper. Only the head and the wife have household businesses, and the head's comes first. Helpers get the `helper` work role, and everyone else, including the player, gets `independent`. A wife or child who doesn't help stays at home: a daughter keeps house as a homemaker with `childHomemakerChance`, and otherwise they start a business of their own among the professions that work from home (or any profession, if they aren't allowed to have any that do). The head always starts a business. A business is chosen among the professions the person is allowed to have that have a place in the town (see below) and whose `maxBusinesses` hasn't been reached. If none of the professions the person is allowed to have meets either rule, that rule is ignored. A woman at the head of a household only chooses among the professions whose wealth is at least `femaleHeadMinWealth`, unless she isn't allowed to have any of them. The player's profession is never limited.
* A household's wealth starts as the sum of the wealth of each business its members own, plus what each helper adds (the first helper in a business adds `helperWealth` times its wealth, and each later helper `helperWealthFalloff` times what the one before them added), plus `homemakerWealth` for each homemaker and `infirmWealth` for each infirm member. That sum is multiplied by the `headAgeWealth` percentage for the head's age, and by a percentage chosen from `householdFortune`. Its wealth level is `high` or `medium` if it reaches that threshold, and `low` otherwise.

**Map features.** Before any household is made, the town chooses a number of map features from `featureCount`, by frequency, with at most one river. If the player's profession only has places in a kind of quarter that borders a feature the town doesn't have, that feature is added. A profession has a place in the town if it works from home, or one of its places in Locations.json can go in a kind of quarter whose `borders` feature (if any) the town has.

**Laying out the town.** The quarters are chosen first, and then arranged on the map. Quarters that border a feature the town doesn't have, through their kind or themselves, are never chosen, and neither are business places that can only go in such quarters.

* The town gets one quarter of each unique kind.
* Each household gets a house of its wealth level, in a quarter of the same wealth that isn't of a unique kind. Quarters are added until there's room for every household, each planning a number of houses within its range, leaning toward fewer. Each quarter gets one house, then its minimum, as far as the households go, and the rest are spread among quarters below their planned number.
* Each business that doesn't work from home gets a place from the businesses for its profession, in a quarter of one of its kinds with room for another business. If none has room, a quarter that isn't of a unique kind is added, preferring one whose minimum houses is 0. If none can be added, the business goes in a full quarter of one of its kinds, and a warning is logged.
* Landmarks are added until the town has a number from `landmarkCount`, counting landmarks of unique kinds. Village and outskirts landmarks take turns.
* A layout is chosen by frequency, and its shape arranges the quarters on a grid, starting with the quarter of its `center` kind. Each quarter is a rectangle of its size that doesn't overlap any other. Quarters only connect where their rectangles share part of an edge, with at most two connections on each side of a quarter.
* Each connection between quarters gets a direction: the side of the quarter it leaves by, or one step to either side, toward the end of the side it's nearest. Several connections on one side point different ways, no quarter has two exits in the same direction, and every connection points the opposite way from its other end. A connection that can't get a direction of its own without a clash is left out, unless the town needs it to stay connected.
* With a river, the river runs along one side of the village once the village quarters are placed, touching the outermost of them or one cell beyond them. Quarters can't sit on it. A quarter growing into the river is placed on the far bank instead, facing the quarter it grew from, and a quarter of the outskirts can be connected to a quarter it faces directly across the river. Quarters that border the river are placed last, touching it if there's room anywhere, and otherwise as close to it as there's room for.
* Each connection is named by the `path` of the quarters' kinds: the outskirts quarter's if one quarter is in the outskirts, or else either quarter's, preferring kinds that aren't unique. A connection across the river is named by one of the river's `crossings` instead.
* Each area feature is placed in empty space touching a quarter that borders it, or else touching a quarter in the outskirts, starting with those farthest from the center. A warning is logged if a quarter that should border a feature doesn't.
* A quarter borders the river if it touches it, and an area if it shares part of an edge with it. For each feature a quarter borders, the quarter's `featureText` for it, or else one of the feature's `text` sentences at random, is added to the end of its `mediumDescription`, with the direction the feature is in: the side of the quarter it's on, or one step to either side, toward the end of the side it's nearest.

Each shape lays out the core of the village around the center in its own way:

* `radial`: four to six village quarters ring the center, some connected to their neighbors in the ring.
* `linear`: a main street of four to six quarters runs straight through the center, east to west or north to south. Half the time, a second street of three or four quarters starts beside it and runs alongside it, with lanes crossing between the two where they touch.
* `crossroads`: arms of one or two quarters lead straight out from the center to the north, east, south, and west.
* `warren`: there's no core. Quarters crowd against each other and the center wherever there's room, and more of the quarters that touch are connected, making many small loops.

The village quarters are taken for the core roughly richest first, with some taken out of order. The rest of the village then branches off close to the core. A quarter usually branches off one of the same wealth, and prefers spots touching quarters of the same wealth. Landmarks are placed first, and a landmark that doesn't lead anywhere yet gets the next quarter beyond it.

The outskirts leave the village in two or three groups. They start from the ends of the main street (`linear`) or the arms (`crossroads`), and otherwise from quarters at the edge of the village on different sides of it. Each group is a chain that sometimes forks, a loop of three, a hub with the other quarters around its first, or a ladder of two chains side by side with rungs between them. Landmarks at a dead end connect to a quarter touching them if they can. Templates that haven't been used yet are chosen first, by frequency. If every template that fits has been used, one is used again, and a warning is logged.

## Checks Between Files

Once each file has loaded on its own, the files are checked against each other. The professions are only checked against the profession property if Families.json is correct. The profession property must take its values from the professions. The `homemaker` and `infirm` professions must have no business settings, and every other profession must have a `wealth`. Each business in Locations.json must be for a profession in Professions.json that doesn't work from home.
