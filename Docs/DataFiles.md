# Data Files

The game reads its data from the `Data` folder next to the executable. Every file listed here is required, except for files referenced by optional fields and files marked optional. Mods can change the data files (see [Mods.md](Mods.md)).

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

**Condition checks.** For each role, conditions are checked in every combination of values they can depend on. Each check only combines the values of the properties it depends on (through conditions, and the properties those conditions depend on). Only `when` conditions of properties and their values count here: the conditions of Frequency Overrides can't change which values are allowed, so they're left out of these checks. Every value list property must have at least one value whose condition is met in every combination of the properties its conditions use where its own condition is met. Every template used is checked too: `fullName`, `description`, templates no other template uses, and templates used by the chosen variant of a template being checked. A template is checked in every combination of the properties its conditions use and the properties its placeholders and `hidden` lists name depend on, together with the values assumed by the template that uses it. Each property a template uses that the obscurers could hide (the strongest obscurer of it is at least the lowest prominence of its possible values) is checked both visible and hidden. In each case, the template must have a variant that's used, and every property that variant's placeholders name must have a visible value. Each check can have at most 10000 combinations, and a template can use at most 10 properties that the obscurers could hide.

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
| `sortByTitle` | boolean | Optional (defaults to false). Value list properties only. Whether the values are listed in alphabetical order of their titles when choosing one, rather than in the order they're defined in. It doesn't change how values are chosen at random. |
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
| `buildingTravelTime` | integer | At least 0. How many minutes it takes to go into or out of a house or business from its quarter. |
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
| `optionalWith` | string | Optional. Unique kinds only. The id of a map feature in `features`. A town with the feature only has a quarter of this kind `optionalChance` percent of the time, unless none of the quarters for its houses and businesses border the feature, when it always has one. The layouts can't be built around a kind with it. |
| `optionalChance` | number | Optional (defaults to 50). From 0 to 100. |
| `path` | string | Filled in. What a path to or from a quarter of this kind is called. |
| `borders` | string | Optional. Not allowed for unique kinds. The id of one of `features`. Every quarter of this kind borders that feature, and is only used in towns that have it. |
| `travelTime` | integer | At least 0. How many minutes it takes to walk a path to or from a quarter of this kind. A path between two quarters takes the longer of their kinds' times. |

**Layout**

| Property | Type | Notes |
|---|---|---|
| `shape` | string | `radial`, `linear`, `crossroads`, or `warren` (see Laying out the town). |
| `center` | string | The id of a unique kind in the `village` area without `optionalWith`. The quarter the shape is built around. |
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
| `workHours` | Work Hours | Optional. Businesses only. The hours people in the profession work, used in place of the `start` and `end` of each need met at the workplace in Schedules.json. Without it, those needs' hours are used. |

**Work Hours**

| Property | Type | Notes |
|---|---|---|
| `start` | integer | From 0 to 23. The hour work starts. |
| `end` | integer | From 1 to 24 (midnight), and different from `start`. The hour work ends. If it's before `start`, work runs past midnight, as two stretches of each day: from midnight to `end`, and from `start` to midnight. |

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
| `headRelations` | Head Relations | |
| `atWork` | string | Filled in. Id of the at work property's value for someone who is working. |
| `notAtWork` | string | Filled in. Id of the at work property's value for someone who isn't working. |
| `married` | string | Filled in. Id of the married property's value for someone who is married. |
| `unmarried` | string | Filled in. Id of the married property's value for someone who isn't married. |
| `familyCount` | Integer Range | The minimum is at least 0. How many households the town has besides the player's. |
| `playerRelations` | list of Player Relation | Optional (defaults to empty). Can be empty. How each member of the player's household is related to the player, who is its head: the first relation whose `when` the member meets is shown. |
| `relationships` | Relationships | How the relationship between every two characters (including the player) is chosen when a game starts, and what relationship scores mean. A relationship has a score from 0 to 100, the same both ways. |
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
| `inheritedProperties` | array of Inherited Property | Optional (none). The properties family members share. |

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
| `headRelation` | string | Filled in. A `valueList` property with `setByGame`. Must have each of the `headRelations` values. |
| `headName` | string | Filled in. A `string` property with `setByGame`. Each character's value is the full name of the head of their household, so the head's value is their own full name. |
| `atWork` | string | Filled in. A `valueList` property with `setByGame`. Must have the `atWork` and `notAtWork` values. During a game, each character has the `atWork` value while they're in the place of the business they own or help in, and the `notAtWork` value anywhere else. A character with a profession who works from home has an even chance of `atWork` or `notAtWork` each time they arrive home, including when the game starts with them there. A character without a profession always has `notAtWork`. |
| `married` | string | Filled in. A `valueList` property with `setByGame`. Must have the `married` and `unmarried` values. |

**Head Relations**

Ids of the head relation property's values, each saying what the head of a household is to a member of it. Each is filled in.

| Property | Type | Notes |
|---|---|---|
| `self` | string | For the head. |
| `husband` | string | For the head's wife. |
| `father` | string | For a child of a man at the head of a household. |
| `mother` | string | For a child of a woman at the head of a household. |
| `son` | string | For a household elder who is the parent of a man at the head of a household. |
| `daughter` | string | For a household elder who is the parent of a woman at the head of a household. |
| `sonInLaw` | string | For a household elder who is the parent of the head's wife. |
| `daughterInLaw` | string | For a household elder who is the parent of the late husband of a woman at the head of a household. |

**Player Relation**

| Property | Type | Notes |
|---|---|---|
| `when` | Condition | Can't use `hidden`. |
| `title` | string | Filled in. Shown to the player, such as in "This is your {title}". |

**Relationships**

A relationship's score is chosen from `score`, plus a bonus for two members of the same household and `neighborBonus` for two characters in different households whose houses are on the same quarter, up to 100. Two members of a household are related as worked out from their head relation values (named in `headRelations`): the head and the head's wife, or two household elders who are both parents of the head or both parents of the head's wife, are a couple (`spouseBonus`); a household elder and their own child or any child of the head are an elder and their family (`elderBonus`); a household elder and their child's spouse are an elder and an in-law (`elderInLawBonus`); and any other two are family (`familyBonus`).

| Property | Type | Notes |
|---|---|---|
| `score` | Integer Range | The minimum is at least 0, and the maximum is at most 100. |
| `familyBonus` | integer | At least 0. |
| `spouseBonus` | integer | At least 0. |
| `elderBonus` | integer | At least 0. |
| `elderInLawBonus` | integer | At least 0. |
| `neighborBonus` | integer | At least 0. |
| `friendScore` | integer | From 0 to 100. Two characters whose score is at least this are friends. |
| `levels` | list of Relationship Level | At least one. In order of `minimum`: the first's is 0, and each one after it is more than the one before it and at most 100. A score is shown to the player as the title of the last level whose minimum it reaches. |

**Relationship Level**

| Property | Type | Notes |
|---|---|---|
| `minimum` | integer | The lowest score with this title. |
| `title` | string | Filled in. Shown to the player, such as "Friend". |

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

**Inherited Property**

| Property | Type | Notes |
|---|---|---|
| `property` | string | Filled in. Id of a `valueList` property that isn't one of the Family Properties. |
| `blend` | Shape | Optional. Weights the values a child of two parents can have when neither parent's value is `unblended`: the values from one parent's to the other's, in the order the property lists them, leaving out `unblended` values and values the child isn't allowed to have (except the parents' own). Without it, the child has one parent's value or the other's, with equal chances. |
| `unblended` | array of string | Optional (none). Each must be the id of one of the property's values. Values that don't blend. |
| `unblendedChance` | number | Optional (50). Chance. The chance that a child with one parent whose value is `unblended` and one whose value isn't has the `unblended` value. Otherwise the child has the other parent's value. A child of two parents with `unblended` values gets a random value as usual. |

**Generating households.** The player's household is made first, with the player at its head. Then `familyCount` more households are made, one at a time. Every character other than the player uses the non-player settings of Characters.json, and gets the household's last name unless noted. Only professions in Professions.json are businesses; the `homemaker` and `infirm` values are only given to household elders. Each head, including the player, gets the `head` household role, and everyone else gets `member`. Each character also gets the head relation value from `headRelations` that fits how they're related to the head, and the head's full name as their head name.

* The head is a woman with `femaleHeadChance`, and otherwise a man. A woman at the head of a household lives with no men. A non-player head's age is chosen from `headAge`, and so is the player's (see `headAge`). The head's usual last name is their `lastName` property's value, but one of the other last names that no household uses yet is used instead if the usual name is blank or already used, or another household's head has the same profession.
* A man has a wife with `wifeChance`. The player always has one, if the ages allow a wife who could be the mother of an adult child. Whether the head has a wife, and her age, are chosen before the head's other values.
* A head who has a wife, and the wife, get the `married` value, and everyone else gets `unmarried`. The married value is set before the rest of a non-player character's values are chosen, so their conditions (such as a profession's `when` or `frequencyWhen`) can use it. While the player's values are chosen, such as on the new game screen, the player has `married` if their gender is `male`, and `unmarried` otherwise; it's then set again to fit whether the player has a wife.
* Then the children are added, from oldest to youngest. Their ages must be in the age property's range and differ from each other, and each parent's age minus the child's must be from `minParentAge` to that parent's maximum. The player always has at least one child if the ages allow it.
* Then a household elder is added with `householdElderChance`. They're the parent of the head or the wife; a widow's household elder is her own parent or her late husband's, with even chances. A wife's parent, and a widow's own parent, has one of the other last names. A household elder whose age would be outside the age property's range, or below their gender's `householdElderMinAge`, isn't added.
* Each of the `inheritedProperties` is set before the rest of a character's values are chosen. A household elder has their child's value. A child with only the head as a parent has the head's value, and a child of the head and the wife takes after both (see Inherited Property). A relative's value that the character isn't allowed to have is ignored, and a character with no relatives' values left gets a random value as usual.
* A man who is a household elder is infirm. A woman is infirm with the `infirmChance` for her age's group. Otherwise, she helps in a household business if she would as a member (below). If not, she keeps house as a homemaker with `householdElderHomemakerChance`, and otherwise takes up a profession with `forHouseholdElders` that she's allowed to have and whose `maxBusinesses` hasn't been reached, chosen by frequency. If there's no such profession, she keeps house. Household elders never start any other business.
* Each member besides the head helps in a household business, with `helperChance`, if they're allowed to and it has room for another helper. Only the head and the wife have household businesses, and the head's comes first. Helpers get the `helper` work role, and everyone else, including the player, gets `independent`. A wife or child who doesn't help stays at home: a daughter keeps house as a homemaker with `childHomemakerChance`, and otherwise they start a business of their own among the professions that work from home (or any profession, if they aren't allowed to have any that do). The head always starts a business. A business is chosen among the professions the person is allowed to have that have a place in the town (see below) and whose `maxBusinesses` hasn't been reached. If none of the professions the person is allowed to have meets either rule, that rule is ignored. A woman at the head of a household only chooses among the professions whose wealth is at least `femaleHeadMinWealth`, unless she isn't allowed to have any of them. The player's profession is never limited.
* A household's wealth starts as the sum of the wealth of each business its members own, plus what each helper adds (the first helper in a business adds `helperWealth` times its wealth, and each later helper `helperWealthFalloff` times what the one before them added), plus `homemakerWealth` for each homemaker and `infirmWealth` for each infirm member. That sum is multiplied by the `headAgeWealth` percentage for the head's age, and by a percentage chosen from `householdFortune`. Its wealth level is `high` or `medium` if it reaches that threshold, and `low` otherwise.

**Map features.** Before any household is made, the town chooses a number of map features from `featureCount`, by frequency, with at most one river. If the player's profession only has places in a kind of quarter that borders a feature the town doesn't have, that feature is added. A profession has a place in the town if it works from home, or one of its places in Locations.json can go in a kind of quarter whose `borders` feature (if any) the town has.

**Laying out the town.** The quarters are chosen first, and then arranged on the map. Quarters that border a feature the town doesn't have, through their kind or themselves, are never chosen, and neither are business places that can only go in such quarters.

* The town gets one quarter of each unique kind, except an optional kind (one with `optionalWith`) in a town with its feature. Once the houses and businesses have their quarters, the town gets one of those if none of their quarters border the feature, or else `optionalChance` percent of the time.
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

## Calendar.json

The game's calendar. A year is made of seasons, each season of weeks, and each week of days, in the order listed. A day has 24 hours of 60 minutes. Times are shown with the day's title, the week's title followed by the season's title, the hour from 0 to 23, and the minute, and with the year (shown with `yearFormat`) when shown in full.

| Property | Type | Notes |
|---|---|---|
| `days` | list of Calendar Item | The days of each week. |
| `weeks` | list of Calendar Item | The weeks of each season. |
| `seasons` | list of Calendar Item | The seasons of each year. |
| `dayParts` | list of Day Part | The parts of each day, in order of when they start. Each must start after the one before it. |
| `specialDays` | list of Special Day | Optional (defaults to empty). Can be empty. Days of the year with their own title, which fall on the same day every year. |
| `startYear` | integer | At least 1. The year a new game starts in. Years count from 1. |
| `yearFormat` | string | How a year is shown. Must include `{year}`, which is replaced by the year's number. |

**Calendar Item**

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in. |
| `title` | string | Filled in. |

**Day Part**

A part of the day lasts from its start until the next part starts. The last part lasts until the first part starts the next day, so it goes past midnight unless the first part starts at 0.

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in. |
| `title` | string | Filled in. |
| `start` | integer | The hour it starts at, from 0 to 23. |
| `announcement` | string | Optional. Filled in. What the player notices when the part of the day starts while they wait, such as a bell ringing. Without it, they're told which part of the day has begun. |

**Special Day**

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in. |
| `title` | string | Filled in. |
| `season` | string | Id of a season in `seasons`. |
| `week` | string | Id of a week in `weeks`. |
| `day` | string | Id of a day in `days`. |

## Schedules.json

How people decide what to do. Everyone works through a list of tasks toward a goal: walking to a place one path at a time, starting work if the goal needs it, spending time there, and stopping work. When someone other than the player runs out of tasks, they work out how urgent each of their needs is, and choose one of the needs with an urgency of at least `minimumUrgency` at random, with each need's chance in proportion to its urgency raised to `urgencyPower`. A need that can't be met (it has no place, or no time is left to spend on it) leaves them idle. Urgency runs from 0 to 100, and is then multiplied by every urgency factor for the need whose condition the person meets, so it can be more than 100.

| Property | Type | Notes |
|---|---|---|
| `minimumUrgency` | number | At least 0. Needs less urgent than this aren't chosen. |
| `urgencyPower` | number | More than 0. |
| `sightingHours` | integer | At least 1. How many hours people remember the last time they saw each other person. |
| `sleep` | Sleep | Naps, and going to bed in time for work. |
| `talking` | Talking | When people refuse to talk, and what waking someone to talk costs them. |
| `socializing` | Socializing | How people stop to chat with friends they meet, and seek them out when they're lonely. |
| `needs` | list of Need | Each need needs a filled-in `id` and `title`. |
| `idle` | Idle | What people do when no need is chosen. |
| `start` | Start | How everyone starts a new game. |
| `travelTimeFactors` | list of Travel Time Factor | Optional (defaults to empty). Can be empty. |
| `urgencyFactors` | list of Urgency Factor | Optional (defaults to empty). Can be empty. |

**Talking**

Talking with someone interrupts what they were doing: it's paused (with no progress toward their goal) until the talk is over, and then carries on with the time it had left. If their goal is a quota need that's outside its hours or already done by then, they choose a new goal instead.

| Property | Type | Notes |
|---|---|---|
| `refuseUrgency` | number | At least 0. Someone whose goal is at least this urgent refuses to talk. |
| `wakeLevel` | number | From 0 to 100. How much someone woken up to talk has the level of the `start` need rise. |
| `askAboutMinutes` | integer | At least 1. How long it takes to ask someone whether they've seen a person. |
| `askDoingMinutes` | integer | At least 1. How long it takes to ask someone what they're doing. |
| `socializeMinutes` | integer | At least 1. How long socializing with someone takes. |
| `socializeNeed` | string | Optional. The id of a level need. While two people socialize, it's met (at its `satisfyPerHour`) for each of them whose relationship with the other is at least `friendScore` in Families.json. That's the only way it's met: looking for a friend doesn't meet it. It's chosen as a goal like other needs, but with its urgency multiplied by `seekFactor` in `socializing`, and only by someone with at least one friend. |

**Socializing**

When someone starts a task, they and each friend where they are (someone whose relationship with them is at least `friendScore` in Families.json) may stop to socialize, for the `socializeMinutes` in `talking`. Someone is willing to start a chat by their urgency for the socializing need, times their relationship score out of 100, less their goal's urgency times `chatGoalWeight` (unless their goal is the socializing need). The most willing of the two, if at least `chatUrgency`, starts it. Neither can already be talking, the one starting it can't be asleep, and the other can't be too busy to talk (see Talking), or asleep unless the socializing need is at least `wakeUrgency` for the one starting it. Unless they were told what the friend is doing since the friend chose their goal, they first ask, which adds `askDoingMinutes` in `talking`. Someone who chooses to chat with the player asks them first, and time stops, along with whatever the player was doing, until the player answers. They don't ask the player while someone else is waiting for an answer, or for `askPlayerAgainHours` after the player turns them down.

When the socializing need is chosen as a goal, the person chooses a friend to look for and where, at random from clues to where each friend might be, each weighing the clue's weight in `clues`: the friend's workplace while one of their needs met at work is within its hours, or their home otherwise; where they were last seen, or said they were going, weighing less as the sighting gets older (nothing once it's `sightingHours` old); and for each need met at a quarter only on some `days`, within its hours and not yet done by the friend, a place for it. Each clue's weight is divided by 1 plus the walk there in minutes over `nearMinutes`, and multiplied by how long ago they last talked in hours over that plus `recentHours` (1 if they haven't talked). The place the person is in is never chosen. They walk there and look around for `seekMinutes`, then choose a goal again. As they set off along a path, if the friend they're looking for is in the place the path leads to and is walking away from it, they call out: the friend waits there for as long as the walk takes and a minute more, and they chat once the seeker arrives. If no friend is there when they start looking around, they ask someone else there (other than a friend or the player, and anyone asleep, talking, or too busy to talk) about their friends, for the `askAboutMinutes` in `talking`, and learn what that person has seen of them.

| Property | Type | Notes |
|---|---|---|
| `chatUrgency` | number | At least 0. |
| `chatGoalWeight` | number | At least 0. |
| `wakeUrgency` | number | At least 0. |
| `seekFactor` | number | More than 0. |
| `seekMinutes` | integer | At least 1. |
| `recentHours` | number | More than 0. |
| `nearMinutes` | number | More than 0. |
| `askPlayerAgainHours` | number | More than 0. |
| `clues` | Seek Clues | |

**Seek Clues**

| Property | Type | Notes |
|---|---|---|
| `workplace` | number | At least 0. |
| `home` | number | At least 0. |
| `sighting` | number | At least 0. |
| `gathering` | number | At least 0. |

**Need**

There are two kinds of need. A **level** need has a level from 0 to 100, which grows by `perHour` each hour, except while it's being met (when it drops by `satisfyPerHour` each hour) or while a need in its `pausedBy` list is being met. Time spent meeting a level need lasts until its level reaches 0. A level need's urgency rises from 0 at `urgentFrom` to 100 at level 100, and it's 0 outside its hours.

A **quota** need has a quota to meet each day or each week (its `period`), between the hours `start` and `end` on the days it can be met. The quota is measured either in time or in trips:

* **Time:** `minutes`, plus `minutesPerMember` for each member of the family, spent in stretches of at most `shift` minutes.
* **Trips:** `visits`, plus `visitsPerMember` for each member of the family, rounded up. Each trip spends `visitMinutes`, plus `visitMinutesPerMember` for each member of the family, at the place, and then, for a need that returns home, `homeMinutes`, plus `homeMinutesPerMember` for each member of the family, at home afterward.

Each stretch or trip is shorter if less of the quota is left, or if less of today's hours are left once the person gets to the place. Only time spent at the place (and, for a trip, at home afterward) counts toward the quota, which starts again from 0 at the start of each day or week (the first day in Calendar.json). For a need shared by the family, the time every member has spent counts, and so does the time other members already plan to spend on it. A quota need's urgency is 0 outside its hours, on days it can't be met, and once its quota is met. Otherwise it's 0 while the spare time left (the minutes left in its hours from now to the end of its day or week, minus the minutes left of its quota) is at least `lead`, rises to 100 as the spare time drops to 0, and stays at 100 after that. A weekly quota need with `days` and `overdueUrgency` is overdue once the time left in its hours on its days this week has run out with its quota unmet: it can then be met during its hours on any day for the rest of the week (other than special days it skips), with an urgency of `overdueUrgency`.

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in. |
| `title` | string | Filled in. Shown to the player with the level's title, for a need with `levels`. |
| `activity` | string | Optional (defaults to `title`). Filled in. What someone meeting the need is shown to be doing. |
| `kind` | string | `level` or `quota`. |
| `when` | Condition | Optional. Who has the need. Without it, everyone does. Can't use `hidden`. |
| `place` | string | Where the need is met: `home`, `workplace`, or `quarter`. Only people with a business have a need met at their `workplace`: their business's place, or their home if it's run from home. |
| `quarterKinds` | list of Quarter Choice | Required for the `quarter` place, and not allowed otherwise. Must not be empty. With `nearest`, the nearest quarter of any of the kinds is chosen. Otherwise the kind is chosen by frequency among the kinds the town has, then a quarter of that kind, with nearer quarters more likely: each quarter's weight is 1 divided by 1 plus the minutes it takes to walk there. |
| `nearest` | boolean | Optional (defaults to false). Only allowed for the `quarter` place. |
| `atWork` | boolean | Optional (defaults to false). Whether people are at work while meeting the need. |
| `returnHome` | boolean | Optional (defaults to false). Not allowed for the `home` place. Whether people walk home after meeting the need. |
| `perHour` | number | Level needs only, and required. At least 0. |
| `satisfyPerHour` | number | Level needs only, and required. More than 0. |
| `pausedBy` | list of string | Optional. Level needs only. Ids of needs in `needs`. |
| `urgentFrom` | number | Optional (defaults to 0). Level needs only. At least 0 and less than 100. |
| `wakeUrgency` | number | Optional. Level needs only. At least 0. Someone asleep (meeting the `start` need, and decided for by the game rather than the player) wakes once this need's urgency is at least this, and more than the sleep need's urgency is by then, unless the sleep need pauses it. When that happens is worked out as they start sleeping, from how their needs will change, five minutes at a time. Without it, the need never wakes anyone. |
| `startLevel` | Integer Range | Optional (defaults to 0). Level needs only. From 0 to 100. The level at the start of a new game. |
| `levels` | list of Level | Optional. Level needs only. Must not be empty. The words for how high the need is. Needs with `levels` are shown to the player. |
| `period` | string | Optional (defaults to `day`). Quota needs only. `day` or `week`. |
| `days` | list of string | Optional (defaults to every day). Quota needs only. Ids of days in Calendar.json, each filled in and listed once. The days of the week the need can be met on. |
| `onSpecialDays` | boolean | Optional (defaults to true). Quota needs only. Whether the need can be met on special days. |
| `start` | integer | Required for quota needs, optional for level needs. The hour the need can be met from. |
| `end` | integer | Required for quota needs, and for level needs with a `start`. The hour the need can be met until. For a quota need, `start` must be from 0 to 23 and `end` from 1 to 24, and they must be different. If `end` is before `start`, the hours run past midnight, as two stretches of each day: from midnight to `end`, and from `start` to midnight. The quota for one person must fit in the hours of its period (one day, or each of its days in a week). For a level need, both are from 0 to 23 and must be different, and the hours run past midnight if `start` is after `end`. |
| `minutes` | integer | Quota needs measured in time only. At least 0. Required unless there's `minutesPerMember`. |
| `minutesPerMember` | integer | Optional. Quota needs measured in time and shared by the family only. At least 0. |
| `shift` | integer | Quota needs measured in time only, and required for them. More than 0. |
| `visits` | number | Quota needs measured in trips only. At least 0. Required unless there's `visitsPerMember`. |
| `visitsPerMember` | number | Optional. Quota needs measured in trips and shared by the family only. At least 0. |
| `visitMinutes` | integer | Quota needs measured in trips only. At least 0. Required unless there's `visitMinutesPerMember`. With one family member, a trip must spend at least a minute at the place. |
| `visitMinutesPerMember` | integer | Optional. Quota needs measured in trips and shared by the family only. At least 0. |
| `homeMinutes` | integer | Optional. Quota needs measured in trips that return home only. At least 0. |
| `homeMinutesPerMember` | integer | Optional. Quota needs measured in trips that return home and are shared by the family only. At least 0. |
| `lead` | integer | Quota needs only, and required. More than 0. |
| `overdueUrgency` | number | Optional. Quota needs with a `period` of `week` and `days` only. From 0 to 100. |
| `homeActivity` | string | Optional (defaults to `activity`). Quota needs with time at home after their trips only. Filled in. What someone is shown to be doing during that time. |
| `sharedByFamily` | boolean | Optional (defaults to false). Quota needs only. Whether the need is met for the whole family by anyone in it. |
| `playerAction` | Player Action | Optional. Lets the player spend time on the need where they can meet it: their home or workplace, or a quarter of one of `quarterKinds`. Not offered for a need met at a workplace when the player has no business, or whose `when` the player doesn't meet. |

A quota need is measured in trips if it has any of `visits`, `visitsPerMember`, `visitMinutes`, `visitMinutesPerMember`, `homeMinutes`, or `homeMinutesPerMember`, and can't have them along with `minutes`, `minutesPerMember`, or `shift`.

**Player Action**

For a need whose quota isn't measured in trips, the player chooses one of the options, and spends that long meeting the need, starting and stopping work around it for a need with `atWork`. For a quota need, the options can't be chosen on a day it can't be met (one not in its `days`, or a special day when `onSpecialDays` is false), unless it's overdue, or with `onlyInHours`, outside its hours (`start` to `end`) on such a day. While a need with `onlyInHours` is closed, the player is told the days and hours it's open in place of the action.

For a quota need measured in trips, the player spends one trip's time at the place (`visitMinutes` and `visitMinutesPerMember`), if the quota isn't met and can be met today (and now, with `onlyInHours`). For a trip that returns home, they're then told `return`, and the trip is in progress until they finish it at home with `home`, which takes the trip's time at home (`homeMinutes` and `homeMinutesPerMember`, which can be 0). While it's in progress, they can't start the trip again, and the time at home counts as planned for the rest of the family.

After each action, the player is told `result` (or `home.result`) followed by how long it took, if it took any time.

| Property | Type | Notes |
|---|---|---|
| `title` | string | Filled in. The name of the action. |
| `result` | string | Filled in. |
| `options` | list of Player Action Option | Required for a need whose quota isn't measured in trips, and must not be empty. Not allowed for a quota need measured in trips. |
| `return` | string | Required for a quota need measured in trips that returns home, and not allowed otherwise. Filled in. |
| `home` | Player Action Home | Required for a quota need measured in trips that returns home, and not allowed otherwise. |
| `onlyInHours` | boolean | Optional, defaults to false. Only allowed for a quota need. |

**Player Action Home**

| Property | Type | Notes |
|---|---|---|
| `title` | string | Filled in. The name of the action that finishes the trip. |
| `result` | string | Filled in. |

**Player Action Option**

Each option has exactly one of `untilMet`, `untilHour`, and `minutes`. An option that would take no time can't be chosen.

| Property | Type | Notes |
|---|---|---|
| `title` | string | Filled in. Optional for an option with `minutes`, which is otherwise called by how long it takes. Shown with how long the option takes. |
| `untilMet` | boolean | Optional (defaults to false). Until a level need's level is 0, or a quota need's quota for the player (or the family, for a need shared by the family) is met this day or week. |
| `untilHour` | integer | Optional. From 0 to 23. Until the next time it's this hour, which is a day away at that hour. |
| `minutes` | integer | Optional. More than 0. |

**Quarter Choice**

| Property | Type | Notes |
|---|---|---|
| `quarterKind` | string | Filled in. The id of a quarter kind in Locations.json. |
| `frequency` | frequency | Optional (defaults to `common`). |

**Level**

The title of the last level whose `minimum` is at or below the need's level is shown.

| Property | Type | Notes |
|---|---|---|
| `minimum` | number | From 0 to 100, and more than the level before it. |
| `title` | string | Filled in. |

**Idle**

Someone who doesn't choose a need goes to the place and waits there for a number of minutes, then decides again.

| Property | Type | Notes |
|---|---|---|
| `place` | string | As for a need. |
| `quarterKinds` | list of Quarter Choice | As for a need. |
| `minutes` | Integer Range | The minimum is at least 1. |
| `activity` | string | Optional. Filled in. What someone idle is shown to be doing. Without it, nothing is shown. |

**Sleep**

The sleep need is the `start` need. Choosing it while its level is below `napBelow` is a nap, lasting `napMinutes` rather than until the level reaches 0, unless it's bedtime (below). Someone only naps once between sleeps that aren't naps, so they can't choose sleep again as a nap until then.

The sleep need's urgency is the higher of its usual urgency and its bedtime urgency. Bedtime urgency applies to someone with a quota need met at their workplace (see `place`) whose level is above the sleep need's `urgentFrom`: it's worked out like a quota need's urgency, from the minutes until their work hours next start on a day they can work, less the minutes of sleep they need to reach 0. It's 0 while that leaves at least `bedtimeLead` minutes to spare, and rises to 100 as the time to spare runs out.

| Property | Type | Notes |
|---|---|---|
| `napBelow` | number | From 0 to 100. |
| `napMinutes` | Integer Range | The minimum is at least 1. |
| `bedtimeLead` | integer | At least 1. |

**Start**

A new game starts at midnight on the first day of a random week of the start year. Everyone is at home, meeting `need`, with each level need at its `startLevel`, except anyone other than the player whose work hours (a quota need met at their workplace) include midnight on a day they work: they start awake at their workplace instead. Each person asleep wakes `wakeMinutes` from `wakeHour`, at least a minute after midnight, and the level of `need` is set to what's met by then. Time runs until the player wakes.

| Property | Type | Notes |
|---|---|---|
| `need` | string | The id of a level need in `needs`. |
| `wakeHour` | integer | From 0 to 23. |
| `wakeMinutes` | Integer Range | How many minutes before or after `wakeHour` each person wakes. |

**Urgency Factor**

| Property | Type | Notes |
|---|---|---|
| `when` | Condition | Can't use `hidden`. |
| `needs` | list of string | Must not be empty. Ids of needs in `needs`, each filled in and listed once. |
| `factor` | number | More than 0. |

**Travel Time Factor**

Walking a path takes its travel time (see Locations.json) multiplied by every factor whose condition the person meets, rounded up to a whole minute.

| Property | Type | Notes |
|---|---|---|
| `when` | Condition | Can't use `hidden`. |
| `factor` | number | More than 0. |

## Portraits.json

Optional. Without it, characters have no portraits. A character's portrait is drawn from its layers, bottom to top, and cut off at its edges.

| Property | Type | Notes |
|---|---|---|
| `width` | number | More than 0. The width of a portrait, in the units the paths use. |
| `height` | number | More than 0. The height of a portrait, in the units the paths use. |
| `colors` | list of Portrait Color | Optional (defaults to empty). Each needs a filled-in, unique `id`. |
| `scales` | list of Portrait Scale | Optional (defaults to empty). Each needs a filled-in, unique `id`. |
| `offsets` | list of Portrait Offset | Optional (defaults to empty). Each needs a filled-in, unique `id`. |
| `layers` | list of Portrait Layer | Must not be empty. Each needs a filled-in, unique `id`. |

A **color** is either a hex color, written `#RRGGBB` or `#AARRGGBB`, or the id of a Portrait Color. A color in `colors` can only use the colors before it.

**Portrait Color**

A named color, worked out for each character in the order of the list.

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in and unique. |
| `color` | string | Color. Used when no variant applies. |
| `variants` | list of Portrait Color Variant | Optional. Must not be empty. The first variant whose `when` condition is met replaces `color`. |
| `mix` | string | Optional. Color. Blended into the color by `amount`. Must be given together with `amount`. |
| `amount` | number | Optional. From 0 (none of `mix`) to 1 (all of it). Must be given together with `mix`. |

**Portrait Color Variant**

| Property | Type | Notes |
|---|---|---|
| `when` | Condition | Can't use `hidden`. |
| `color` | string | Color. |

**Portrait Scale**

A named stretch, worked out for each character. Stretching a shape doesn't change the width of its outline.

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in and unique. |
| `originX` | number | Optional (defaults to 0). The horizontal position that stays in place. |
| `originY` | number | Optional (defaults to 0). The vertical position that stays in place. |
| `x` | number | Optional (defaults to 1). More than 0. How much shapes are stretched horizontally when no variant applies. |
| `y` | number | Optional (defaults to 1). More than 0. How much shapes are stretched vertically when no variant applies. |
| `variants` | list of Portrait Scale Variant | Optional. Must not be empty. The first variant whose `when` condition is met replaces `x` and `y`. |

**Portrait Scale Variant**

| Property | Type | Notes |
|---|---|---|
| `when` | Condition | Can't use `hidden`. |
| `x` | number | Optional (defaults to 1). More than 0. |
| `y` | number | Optional (defaults to 1). More than 0. |

**Portrait Offset**

A named shift, worked out for each character. Shapes are shifted after they're stretched.

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in and unique. |
| `x` | number | Optional (defaults to 0). How far shapes are moved right when no variant applies. |
| `y` | number | Optional (defaults to 0). How far shapes are moved down when no variant applies. |
| `variants` | list of Portrait Offset Variant | Optional. Must not be empty. The first variant whose `when` condition is met replaces `x` and `y`. |

**Portrait Offset Variant**

| Property | Type | Notes |
|---|---|---|
| `when` | Condition | Can't use `hidden`. |
| `x` | number | Optional (defaults to 0). |
| `y` | number | Optional (defaults to 0). |

**Portrait Layer**

| Property | Type | Notes |
|---|---|---|
| `id` | string | Filled in and unique. |
| `when` | Condition | Optional. Can't use `hidden`. The layer is only drawn for characters who meet it. |
| `at` | Portrait Place | Optional. The layer is only drawn for characters in one of these places. Without it, the layer is drawn anywhere. |
| `scale` | string | Optional. The id of a Portrait Scale. Stretches the layer's shapes, except those with their own `scale`. |
| `offset` | string | Optional. The id of a Portrait Offset. Moves all of the layer's shapes. |
| `shapes` | list of Portrait Shape | Must not be empty. Drawn bottom to top. |

**Portrait Place**

Has `home` set to true, `quarterKinds`, `workplaces`, or more than one of them. A character is in a workplace when they're in a business's place, or in their own home while they have the at work property's `atWork` value (see Family Properties). They're in a home when they're in any other house, and otherwise in the kind of quarter they're in. On the new game screen, the player is in a home and has the `notAtWork` value.

| Property | Type | Notes |
|---|---|---|
| `home` | boolean | Optional (defaults to false). Whether the layer is drawn in a home. |
| `quarterKinds` | list of string | Optional. Must not be empty. Each is filled in, unique, and the id of a quarter kind in Locations.json. |
| `workplaces` | list of string | Optional. Must not be empty. Each is filled in, unique, and a value of the profession property. The layer is drawn in a business's place for one of these professions, or in the home of a character with one of them while they're working from home. |

**Portrait Shape**

Has `fill`, `stroke`, or both.

| Property | Type | Notes |
|---|---|---|
| `path` | string | Filled in. The outline, in WPF path markup (SVG path data, optionally starting with `F0` for the even-odd fill rule, the default, or `F1` for the nonzero fill rule). |
| `when` | Condition | Optional. Can't use `hidden`. The shape is only drawn for characters who meet it. |
| `fill` | string | Optional. Color. Fills the shape. |
| `gradient` | string | Optional. Requires `fill`. `vertical` (top to bottom), `horizontal` (left to right), or `radial` (center out). Fills the shape with a gradient from `fill` to `fillTo` across the shape's bounds. |
| `fillTo` | string | Optional. Requires `gradient`. Color. The color the gradient ends with. Without it, the gradient fades `fill` out to transparent. |
| `stroke` | string | Optional. Color. Outlines the shape, with rounded ends and corners. |
| `strokeWidth` | number | Optional (defaults to 1). More than 0. |
| `opacity` | number | Optional (defaults to 1). From 0 to 1. |
| `scale` | string | Optional. The id of a Portrait Scale. Stretches the shape, in place of the layer's `scale`. |

## Checks Between Files

Once each file has loaded on its own, the files are checked against each other. The professions are only checked against the profession property if Families.json is correct. The profession property must take its values from the professions. The `homemaker` and `infirm` professions must have no business settings, and every other profession must have a `wealth`. Each business in Locations.json must be for a profession in Professions.json that doesn't work from home. The conditions in Portraits.json must use Characters.json properties and values, as described for Condition, and the places in Portraits.json must use Locations.json quarter kinds and profession property values. The conditions in Schedules.json must use Characters.json properties and values in the same way, its quarter kinds must be quarter kinds in Locations.json, and its days must be days in Calendar.json.

# Adding to the Data

The data for one thing is often spread across several files, so this section lists everything that needs to change to add something new. Most mistakes stop the data from loading with an error, but some don't: those are marked **not checked**, and only show up as something missing in the game. After a change, generate some villages with the village report (see `Docs/CommandLine.md`) to make sure the data loads and the new item turns up, and check its `log` records for warnings.

Portraits.json is generated, so portrait art is never added to it directly. It's added to SubCorona's `Art/Portraits/Portraits.sh`, as described in SubCorona's `Docs/Portraits.md`, which also explains how to generate the file and preview the results.

Saved games refer to properties, values, location and map feature templates, and needs by id, so renaming or removing one of those stops older saves from loading. Adding new ones is safe.

## Profession

* **Professions.json:** an entry in `professions`, with a `text` entry for every name the templates use. In Sub Corona, that's `description` and `helperDescription`. Set `when` for who can have it (such as only men), `helperWhen` for who can help in it, and `defaults` for its usual last names (optional, since households fall back on the last name property's `otherValues`).
* For a business, give it a `wealth`, and decide on `maxBusinesses`, `helpers`, `worksFromHome`, and `forHouseholdElders`.
* **Locations.json:** for a business that doesn't work from home, at least one entry in `businesses` for it. Each of its `quarterKinds` should have quarters with room for businesses (a `businesses` count), and there should be enough places for `maxBusinesses` businesses, or businesses end up in full quarters and a warning is logged.
* **Characters.json:** for a business, add its id to the profession property's `player` values, so the player can choose it (**not checked**). For a profession that isn't a business (like the homemaker), add its id to the `notValues` of the `work` template's helper variant, since it has no `helperDescription`.
* **Families.json:** only if it replaces the `homemaker` or `infirm` profession.
* **Portrait art** (**not checked**): a workplace background (a layer with `inworkplace`), and work clothes that show while working (layers with `working` conditions). A profession that isn't a business only needs clothes, if any.

## Character Property

* **Characters.json:** an entry in `properties`. If it should appear in a character's description, use it in a template that `description` uses (directly or through other templates), and give every value the `text` entries those templates use (**not checked** if no template uses it).
* **Characters.json:** if it can be seen, add it to the `darkness` obscurer, and to any other obscurers that should hide it (**not checked**).
* **Families.json:** an entry in `inheritedProperties` if family members should share it (**not checked**). The Family Properties are the ones the game itself depends on, so a new one there needs code.
* **Portrait art** (**not checked**): if it changes how a character looks, layers, shapes, colors, scales, or offsets whose conditions use it, with art for every value.
* The village report gets a column for it automatically.

## Character Value

* **Characters.json:** an entry in the property's `values`. Give it every `text` entry the templates use for the property. For a property with a `shape`, its place in the list sets how likely it is; for a property in Families.json `inheritedProperties` with a `blend`, its place in the list sets which values it blends with.
* **Characters.json:** if the property's `player` or `npc` settings list `values`, add it there for each role that can have it (**not checked**).
* **Characters.json and Portraits.json:** look over the conditions that list other values of the property (`when`, `frequencyWhen`, and template variants). A `notValues` list now lets the new value through, and a `values` list leaves it out (**not checked**).
* **Families.json:** if it's a value Families.json names (such as a gender or head relation), update the id there. For an inherited property, add it to `unblended` if it shouldn't blend.
* **Portrait art** (**not checked**): if the property shows in portraits, art for the value, checked in the contact sheets.
* A rare value may trip the `lowChanceWarning`.

## Character Template

* **Characters.json:** an entry in `templates`, and a placeholder for it (`{id}`) in the template that should show it, such as `description` or one of the templates it uses (**not checked**: a template nothing uses is still checked, but never shown).

## Name

* **First names:** a Weighted Value in the first name property's `defaults`, under the right key (such as a gender).
* **Last names:** a Weighted Value in the last name property's `otherValues` for a general last name, or in a profession's `defaults` in Professions.json for a last name that comes from a trade.

## Obscurer

* **Characters.json:** an entry in `obscurers`, with the properties it hides. Nothing in the game puts obscurers in effect yet, so a new one has no effect until code does.

## Quarter Kind

* **Locations.json:** an entry in `quarterKinds`, and at least one entry in `quarters` of the kind (required for a unique kind; otherwise the kind is just never used). A unique kind that's only needed without a feature (such as a well where there's no river) can be made optional with `optionalWith`. If its quarters should hold businesses, add it to the `quarterKinds` of those entries in `businesses`. For a unique kind in the village, it can be the `center` of new `layouts`. If it borders a map feature, set `borders`.
* **Portrait art** (**not checked**): a background for the kind (a layer with `inquarter`).

## Quarter

* **Locations.json:** an entry in `quarters`, with an id that isn't used by any quarter, house, or business. Choose its `kind`, and either `wealth` and `houses`, `businesses`, or neither for a landmark. Set `borders` and `featureText` if it's by a map feature.
* Quarters are reused, with a warning, when the town needs more of a kind than there are, so make sure there are enough quarters for each kind and wealth level. The village report's `reusedTemplates` column and `log` records show reuse.

## House

* **Locations.json:** an entry in `houses`, with an id that isn't used by any quarter, house, or business, and its `wealth`. There must be quarters of that wealth with houses for it to be used. As with quarters, too few houses of a wealth level leads to reuse.

## Business Place

* **Locations.json:** an entry in `businesses`, with an id that isn't used by any quarter, house, or business. Its `profession` must be a profession in Professions.json that doesn't work from home, and at least one of its `quarterKinds` must have a quarter with room for businesses.

## Map Feature

* **Locations.json:** an entry in `features` (and `featureCount`, if it's the first one). A river needs `crossings`. Its `text` describes it in the descriptions of the quarters it borders.
* **Locations.json:** set `borders` on the quarter kinds or quarters that belong beside it, such as a riverside kind for a river (**not checked**: without them, an area feature is placed next to a quarter in the outskirts). Add `featureText` to quarters whose description of it should differ from the feature's own `text`.

## Layout

* **Locations.json:** an entry in `layouts`, whose `center` is a unique quarter kind in the village.

## Calendar

* **Day, week, or season:** an entry in `days`, `weeks`, or `seasons` in Calendar.json. Adding one changes the length of the year, so check the `specialDays` still fall where they should (**not checked**). For a day, add it to the `days` of each quota need in Schedules.json that should be met on it (**not checked**).
* **Part of the day:** an entry in `dayParts`, in order of its start.
* **Special day:** an entry in `specialDays`, naming its season, week, and day.

## Need

* **Schedules.json:** an entry in `needs`, choosing its `kind` and `place`, and the settings for its kind. Give a level need `levels` if the player should see it. For a need met in a quarter, list the kinds of quarter in `quarterKinds`.
* **Schedules.json:** add it to the `pausedBy` of any level need that shouldn't grow while it's being met (**not checked**).
* **Schedules.json:** add it to the `needs` of any urgency factor that should apply to it, such as the homemaker's for a household chore (**not checked**).
* The village report gets a column for it automatically, and `--simulate-days` shows when people meet it.

## Portrait Art

* Colors, scales, offsets, and layers are added to SubCorona's `Art/Portraits/Portraits.sh`, never to Portraits.json (see SubCorona's `Docs/Portraits.md`). Layers are drawn in the order they're written, so put a new layer where it belongs among the others.
