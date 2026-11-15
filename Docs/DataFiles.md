# Data Files

The game reads its data from the `Data` folder next to the executable. Mods can change it (see [Mods.md](Mods.md)).

## Files and Sections

Every `.json` file in the data folder and its subfolders is read, in order of their paths. File names don't matter: each file is an object whose properties are **sections**, and any section can be in any file.

* A **list section** is a list of items, each with an `id`. Lists with the same name are joined, in file order. An id already used in the list is an error.
* A **settings section** is an object. Objects with the same name are joined property by property. A property already set by another file is an error.

Problems are reported against the file holding the item or setting they're about.

| Section | Kind | Holds |
|---|---|---|
| `game` | settings | [Game](#game) |
| `properties` | list | [Character Property](#character-property) |
| `templates` | list | [Character Template](#character-template) |
| `obscurers` | list | [Obscurer](#obscurer). Optional. |
| `professions` | list | [Profession](#profession) |
| `families` | settings | [Families](#families) |
| `relationships` | settings | [Relationships](#relationships) |
| `village` | settings | [Village](#village-settings) |
| `quarterKinds` | list | [Quarter Kind](#quarter-kind) |
| `layouts` | list | [Layout](#layout) |
| `features` | list | [Map Feature](#map-feature). Optional. |
| `quarters` | list | [Quarter](#quarter) |
| `houses` | list | [House](#house) |
| `businesses` | list | [Business Place](#business-place) |
| `calendar` | settings | [Calendar](#calendar-settings) |
| `days`, `weeks`, `seasons` | list | [Calendar Item](#calendar-item) |
| `dayParts` | list | [Day Part](#day-part) |
| `specialDays` | list | [Special Day](#special-day). Optional. |
| `schedules` | settings | [Schedules](#schedules-settings). Optional; without it, people stay at home. |
| `needs` | list | [Need](#need). Optional. |
| `urgencyFactors` | list | [Urgency Factor](#urgency-factor). Optional. |
| `travelTimeFactors` | list | [Travel Time Factor](#travel-time-factor). Optional. |
| `portraits` | settings | [Portraits](#portraits-settings). Optional; without it, characters have no portraits. |
| `portraitColors` | list | [Portrait Color](#portrait-color). Optional. |
| `portraitScales` | list | [Portrait Scale](#portrait-scale). Optional. |
| `portraitOffsets` | list | [Portrait Offset](#portrait-offset). Optional. |
| `portraitLayers` | list | [Portrait Layer](#portrait-layer). Required with `portraits`. |

## General Rules

* Files are JSON. Comments and trailing commas are allowed.
* Property names are camelCase, but matching ignores case. An unknown property is an error.
* A property is required unless marked optional. A required property can't be `null`, and list items can't be `null`.
* Text must be filled in (not empty or only whitespace), and lists and maps must not be empty, unless marked otherwise.
* Ids are case-sensitive and unique within their list.
* Wherever a list of strings is expected, a single string can be written for a list of one.
* Any problem stops the data from loading, and every problem found is reported.

A **chance** is a percentage from 0 to 100. A **wealth level** is `veryLow`, `low`, `medium`, or `high`, worth 1, 2, 4, and 8.

## Shared Types

#### Frequency

A string saying how common an item is when chosen at random: `common`, `uncommon`, `rare`, or `veryRare`, optionally followed by `+`, `++`, `-`, or `--`, as in `rare++`. In a list, the uncommon items share the uncommon chance (see [Frequency Chances](#frequency-chances)) equally, the rare and very rare items share theirs, and the common items share the rest. A frequency with no items gives its chance to the common items, or, if there are none, to the other frequencies in proportion. Each step then multiplies or divides an item's chance by the `stepMultiplier`, and the chances are scaled to add up to 100. Shares are worked out from the whole list; items that can't be chosen (conditions not met, a limit reached, already used) are left out, and the rest keep their chances compared to each other.

#### Long Text

Text of a sentence or more.

| Property | Type | Notes |
|---|---|---|
| `text` | string | |
| `ai` | boolean | Whether AI wrote the text. Not used by the game. |

#### Integer Range

| Property | Type | Notes |
|---|---|---|
| `minimum` | integer | |
| `maximum` | integer | At least `minimum`. |
| `shape` | [Shape](#shape) | Optional. Weights the numbers by their order. Without it, every number is equally likely. |

#### Shape

Weights an ordered list by a bell curve.

| Property | Type | Notes |
|---|---|---|
| `peak` | string | `low`, `middle`, or `high`: the first, middle, or last item is the most likely. Each `+` or `-` (up to two) moves the peak a tenth of the way along the list. `low` can't move down, and `high` can't move up. |
| `mostWithin` | number | More than 0. About two-thirds of choices fall within this many steps of the peak. |

#### Weighted Value

| Property | Type | Notes |
|---|---|---|
| `value` | string | |
| `frequency` | [Frequency](#frequency) | Optional (`common`). |

#### Condition

Has at least one of `values`, `notValues`, and `hidden`, and is met when every part it has is met. A condition that "can't use `hidden`" is any condition outside templates.

| Property | Type | Notes |
|---|---|---|
| `values` | map of string to list of string | Optional. Each key is a value list property, and each list holds its value ids or value group ids (see [Character Value](#character-value)). Outside templates, a key can also be an integer property with `groups`, with group ids. Met when every listed property has one of its listed values. A property with no value never meets it. |
| `notValues` | map of string to list of string | Optional. Like `values`, but met when every listed property has a value that isn't listed. |
| `hidden` | list of string | Optional. Templates only. Property ids that must have no visible value. In templates, a property whose value is obscured counts as having no value. |

#### Frequency Override

| Property | Type | Notes |
|---|---|---|
| `when` | [Condition](#condition) | Can't use `hidden`. |
| `frequency` | [Frequency](#frequency) | Used in place of the usual frequency when `when` is met. |

In a list of overrides, the first whose condition is met is used. The item then has the chance an item of the new frequency has among the list's usual frequencies, and the chances are scaled to add up to 100.

#### Template

A string with placeholders. `{id}` is replaced by the visible value of the property with that id (its text, number, or value `title`), or by the text of the template with that id. `{id.name}` is replaced by the `name` entry of the `text` of a value list property's value. Either can end with `:first_upper`, which capitalizes the first character. Literal braces are written `{{` and `}}`. Every placeholder must name a property or template, and a named entry must exist on every value the variant's condition allows. A template can't use itself, directly or through other templates. In the result, runs of spaces become one, and spaces at the ends or before `.`, `,`, `;`, `:`, `!`, or `?` are removed.

#### Hours

The hours of the day something happens in.

| Property | Type | Notes |
|---|---|---|
| `start` | integer | From 0 to 23. |
| `end` | integer | From 1 to 24 (midnight), and different from `start`. An end before the start runs past midnight. |
| `days` | list of string | Optional (every day). Only where noted. Day ids, each listed once. |
| `onSpecialDays` | boolean | Optional (true). Only where noted. |

## Game

#### Game

The `game` settings section.

| Property | Type | Notes |
|---|---|---|
| `title` | string | Mods can't change it. |
| `description` | [Long Text](#long-text) | |
| `icon` | string | Optional. Path of the application icon, relative to the data folder. Not rooted, and no invalid path characters. Mods can't change it. |
| `frequencyChances` | [Frequency Chances](#frequency-chances) | Optional. |
| `lowChanceWarning` | number | Optional. At least 0 and less than 100. A warning is logged when the data loads for every character value with this chance or less of being chosen, for each role. |

#### Frequency Chances

Percentages. `uncommon`, `rare`, and `veryRare` together must be less than 100.

| Property | Type | Notes |
|---|---|---|
| `uncommon` | number | Optional (20). At least 0. |
| `rare` | number | Optional (5). At least 0. |
| `veryRare` | number | Optional (0.5). At least 0. |
| `stepMultiplier` | number | Optional (1.5). At least 1. How much each `+` multiplies a chance by, and each `-` divides it by. |

## Characters

#### Character Property

| Property | Type | Notes |
|---|---|---|
| `id` | string | Unique within `properties`. |
| `title` | string | Shown on the new game screen. |
| `type` | string | `string`, `integer`, or `valueList`. |
| `role` | string | Optional. See [Roles](#roles). |
| `when` | [Condition](#condition) | Optional. A character only has the property when it's met. |
| `setByGame` | boolean | Optional (false). The game sets the value, and it isn't shown on the new game screen. |
| `follows` | string | Optional. Another property's id. This property comes right after it (see Order). No two properties can follow the same one. |
| `values` | list of [Character Value](#character-value) | Value lists only, and required for them unless the property has the `profession` role, whose values are the professions. |
| `shape` | [Shape](#shape) | Optional. Value lists only. Weights the allowed values by their order instead of by frequency. The values must then all be common, without `frequencyWhen`. |
| `sortByTitle` | boolean | Optional (false). Value lists only. Lists the values alphabetically when choosing one. |
| `range` | [Integer Range](#integer-range) | Integers only. Optional if both `player` and `npc` have one. |
| `groups` | list of [Integer Group](#integer-group) | Optional. Integers only. Named parts of the numbers that conditions can use. Groups can't overlap; a number in no group counts as no value. |
| `defaults` | [String Defaults](#string-defaults) | Optional. Strings only. |
| `player` | [Role Settings](#role-settings) | Optional. Replaces settings for the player. |
| `npc` | [Role Settings](#role-settings) | Optional. Replaces settings for everyone else. |

A value list property picks a value, by frequency or shape, from those whose conditions are met, and picks again when its value's condition stops being met. An integer picks a number in its range. A string picks a default, or is blank. On the new game screen, the player must fill in every string property they have; a property is hidden while it has no value or only one possible value.

**Order.** A property **depends on** the properties its conditions, its values' conditions and `frequencyWhen` conditions, and its defaults' `keyedBy` use. Properties are chosen and shown in an order where each comes after what it depends on, a property with `follows` comes right after the one it follows (which waits for its follower's dependencies too), and otherwise in the order listed. A property can't depend on itself, directly or indirectly.

#### Character Value

| Property | Type | Notes |
|---|---|---|
| `id` | string | Unique within the property. |
| `title` | string | |
| `role` | string | Optional. See [Roles](#roles). |
| `groups` | list of string | Optional. Groups the value is in. A condition listing a group id means every value in the group. A group can't be the id of one of the property's values. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). |
| `frequencyWhen` | list of [Frequency Override](#frequency-override) | Optional. Frequencies that replace `frequency` when their conditions are met. |
| `text` | map of string to string | Optional. Named text for templates. |
| `when` | [Condition](#condition) | Optional. A character can only have the value when it's met. |
| `prominence` | integer | Optional (3). From 1 to 3: how hard the value is to hide from obscurers. |

#### Integer Group

| Property | Type | Notes |
|---|---|---|
| `id` | string | Unique within `groups`. |
| `minimum` | integer | Optional (no lower limit). |
| `maximum` | integer | Optional (no upper limit). At least `minimum`. |

#### String Defaults

Has `values`, or `keyedBy` with `valuesByKey`. A list whose key is a value of the `profession` property can come from that profession's `defaults` instead of `valuesByKey`.

| Property | Type | Notes |
|---|---|---|
| `values` | list of [Weighted Value](#weighted-value) | Optional. |
| `keyedBy` | string | Optional. A value list property. Its value picks the list, and a new default is picked when it changes. |
| `valuesByKey` | map of string to list of [Weighted Value](#weighted-value) | Optional. Each key is a value of `keyedBy`. Unless there are `otherValues`, every value `keyedBy` can have (for each role using these defaults) needs a list. |
| `otherValues` | list of [Weighted Value](#weighted-value) | Optional. Keyed defaults only. Used for keys without a list, and what randomizing just this property on the new game screen picks from. |

#### Role Settings

Each setting is only allowed for the property type it applies to, and replaces the property's setting of the same name.

| Property | Type | Notes |
|---|---|---|
| `values` | list of string | Optional. Value lists only. The value ids allowed. Can be empty, in which case the role never has the property. |
| `range` | [Integer Range](#integer-range) | Optional. Integers only. |
| `defaults` | [String Defaults](#string-defaults) | Optional. Strings only. |

#### Character Template

Has `template` or `variants`. The ids `fullName` and `description` must exist.

| Property | Type | Notes |
|---|---|---|
| `id` | string | Unique within `templates`, and not a property id. |
| `template` | string | Optional. [Template](#template). |
| `variants` | list of [Template Variant](#template-variant) | Optional. The first variant whose `when` is met is used. |

#### Template Variant

| Property | Type | Notes |
|---|---|---|
| `when` | [Condition](#condition) | Optional. Without it, the variant is always used. |
| `template` | string | [Template](#template). Can be empty. |

#### Obscurer

Something that keeps some properties from being seen or heard, depending on the situation in the game.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `hides` | map of string to integer | Each key is a property id, or `*` for every property, and each strength is from 1 to 3. A value is hidden by a strength at least its prominence. String and integer values have a prominence of 3. |

**Checks.** For each role, conditions are checked in every combination of the values they depend on (leaving out Frequency Overrides). Every value list property must have an allowed value in every combination where its own condition is met. Every template in use (`fullName`, `description`, templates no other template uses, and those their variants use) must, in every combination of what its conditions and placeholders depend on, have a variant that's used and whose placeholders all have visible values, with each property the obscurers could hide checked both visible and hidden. A check can have at most 10000 combinations, and a template can use at most 10 properties the obscurers could hide.

### Roles

The game finds the properties and values it depends on by their `role`. Each role is used by exactly one property, of the given type, whose values must include one of each value role. Values can only have roles in these properties.

| Role | Type | Value roles |
|---|---|---|
| `gender` | `valueList` | `male`, `female` |
| `age` | `integer` | |
| `profession` | `valueList` | (the professions' roles) |
| `lastName` | `string` | |
| `workRole` | `valueList`, set by game | `independent`, `helper` |
| `householdRole` | `valueList`, set by game | `head`, `member` |
| `headRelation` | `valueList`, set by game | `self` (for the head), `husband` (for the head's wife), `father` and `mother` (for the head's children), `son` and `daughter` (for a parent of the head), `sonInLaw` (for a parent of the head's wife), `daughterInLaw` (for a parent of a widow's late husband) |
| `headName` | `string`, set by game | |
| `atWork` | `valueList`, set by game | `atWork`, `notAtWork` |
| `married` | `valueList`, set by game | `married`, `unmarried` |

The `lastName` property's non-player defaults need `otherValues`. The `age` property can't have a `player` range, since the player's age comes from the families' [Head](#head) `age`. The game sets `headName` to the full name of the head of each character's household, and `atWork` to `atWork` while a character is in the place of the business they own or help in (or, working from home, half the time they arrive home) and `notAtWork` otherwise.

## Professions

#### Profession

A value of the `profession` property, and how its businesses work. The roles `homemaker` (a household elder who keeps house) and `infirm` (a household elder too frail to work) must each be on one profession, without `business`.

| Property | Type | Notes |
|---|---|---|
| `id` | string | Unique within `professions`. |
| `title` | string | |
| `role` | string | Optional. `homemaker` or `infirm`. |
| `groups` | list of string | Optional. As for a [Character Value](#character-value). |
| `player` | boolean | Optional (true). Whether the player can choose it. A profession without `business` must have it false. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). |
| `frequencyWhen` | list of [Frequency Override](#frequency-override) | Optional. Frequencies that replace `frequency` when their conditions are met. |
| `text` | map of string to string | Optional. Named text for templates. |
| `when` | [Condition](#condition) | Optional. A character can only have the profession when it's met. |
| `prominence` | integer | Optional (3). From 1 to 3. |
| `defaults` | map of string to list of [Weighted Value](#weighted-value) | Optional. Each key is a string property whose defaults are keyed by the `profession` property without their own `valuesByKey`. The profession's usual values, such as its last names. |
| `business` | [Business](#business) | Optional. Without it, the profession isn't a business. |

#### Business

| Property | Type | Notes |
|---|---|---|
| `wealth` | string | A wealth level, optionally followed by `+`, `++`, `-`, or `--`. Each step moves a fifth of the way to the next level: a step up adds a fifth of the level's amount and a step down takes away a tenth, so `low++` is 2.8 and `high-` is 7.2. What a business earns. |
| `maxBusinesses` | integer | Optional (no limit). At least 1. The most businesses in the village. |
| `helpers` | integer | Optional (no limit). At least 0. The most members of the owner's household who can help. |
| `helperWhen` | [Condition](#condition) | Optional. Can't use `hidden`. Who can help. Without it, anyone can. |
| `worksFromHome` | boolean | Optional (false). The business has no place of its own. Otherwise it needs at least one [Business Place](#business-place); if so, it can have none. |
| `forHouseholdElders` | boolean | Optional (false). Only with `worksFromHome`. A household elder can take it up. |
| `hours` | [Hours](#hours) | Optional. Without `days` or `onSpecialDays`. Used in place of the hours of the needs met at the workplace. |

## Families and Relationships

#### Families

The `families` settings section.

| Property | Type | Notes |
|---|---|---|
| `head` | [Head](#head) | |
| `wife` | [Wife](#wife) | |
| `children` | [Children](#children) | |
| `elder` | [Elder](#elder) | |
| `parentAge` | [Parent Age](#parent-age) | |
| `helperChance` | number | Chance. That a member helps in a household business, when allowed and there's room. |
| `wealth` | [Family Wealth](#family-wealth) | |
| `inheritedProperties` | list of [Inherited Property](#inherited-property) | Optional (none). Can be empty. |

#### Head

| Property | Type | Notes |
|---|---|---|
| `femaleChance` | number | Chance. That the head is a woman. |
| `femaleMinWealth` | string | Wealth level. The lowest wealth a woman at the head of a household can have from her profession. |
| `age` | [Integer Range](#integer-range) | Within the `age` property's non-player range, with a maximum of at least that range's minimum plus the [Parent Age](#parent-age) `minimum` (the youngest age at which the player can have an adult child). The player's age is the part of this range from that age up. |

#### Wife

| Property | Type | Notes |
|---|---|---|
| `chance` | number | Chance. That a man at the head of a household has a wife. |
| `ageDifference` | [Integer Range](#integer-range) | The wife's age minus her husband's. |

#### Children

| Property | Type | Notes |
|---|---|---|
| `count` | [Integer Range](#integer-range) | Minimum at least 0. How many children live with the household, if their parents' ages allow it. |
| `age` | [Shape](#shape) | Weights the age of the first child chosen, over the ages they could be. |
| `siblingAgeSpread` | number | More than 0. About two-thirds of the other children are within this many years of the first. |
| `sonChance` | number | Chance. That a child is a son, when a man is allowed. |
| `homemakerChance` | number | Chance. That a daughter who doesn't help in a household business keeps house. |

#### Elder

A household elder: a parent of the head or the wife.

| Property | Type | Notes |
|---|---|---|
| `chance` | number | Chance. That a household elder lives with the household. |
| `femaleChance` | number | Chance. That the elder is a woman, when a man is allowed. |
| `ageGap` | [Shape](#shape) | Weights how much older the elder is than their child, from the [Parent Age](#parent-age) `minimum` to the oldest possible. |
| `minAge` | [Gender Ages](#gender-ages) | Each at least 0. The youngest age at which a man or a woman can be an elder. |
| `infirmChance` | map of string to number | Optional (none). Each key is an `age` group, and each value a chance: that a woman who is an elder is infirm. |
| `homemakerChance` | number | Chance. That a woman who is an elder, and doesn't help in a household business, keeps house rather than taking up a profession for elders. |

#### Gender Ages

| Property | Type | Notes |
|---|---|---|
| `male` | integer | The age for men. |
| `female` | integer | The age for women. |

#### Parent Age

| Property | Type | Notes |
|---|---|---|
| `minimum` | integer | At least 0. The youngest age at which someone can have a child. |
| `maxMother` | integer | At least `minimum`. The oldest age at which a woman can have a child. |
| `maxFather` | integer | At least `minimum`. The oldest age at which a man can have a child. |

#### Family Wealth

| Property | Type | Notes |
|---|---|---|
| `helper` | number | From 0 to 1. The part of a business's wealth its first helper adds. |
| `helperFalloff` | number | From 0 to 1. Each later helper adds this fraction of what the one before added. |
| `homemaker` | number | What each homemaker adds. |
| `infirm` | number | What each infirm member adds. Can be negative. |
| `fortune` | [Integer Range](#integer-range) | Minimum at least 0. A percentage each household's wealth is multiplied by. |
| `headAge` | map of string to number | Optional (100 for every group). Each key is an `age` group, and each value a percentage of at least 0 that a household's wealth is multiplied by, by the head's age. |
| `thresholds` | [Wealth Thresholds](#wealth-thresholds) | |

#### Wealth Thresholds

| Property | Type | Notes |
|---|---|---|
| `medium` | number | At least 0. The household wealth needed for `medium`. |
| `high` | number | At least `medium`. The household wealth needed for `high`. |

#### Inherited Property

| Property | Type | Notes |
|---|---|---|
| `property` | string | A value list property without a role. |
| `blend` | [Shape](#shape) | Optional. Weights the values a child of two parents can have, from one parent's to the other's in list order, leaving out `unblended` values and values the child can't have (except the parents' own). Without it, the child has one parent's value or the other's. |
| `unblended` | list of string | Optional (none). Can be empty. Values of the property that don't blend. |
| `unblendedChance` | number | Optional (50). Chance. That a child of one parent with an `unblended` value and one without has that value; otherwise the other parent's. Two parents with `unblended` values give a random value. |

#### Relationships

The `relationships` settings section. Every two characters share a score from 0 to 100.

| Property | Type | Notes |
|---|---|---|
| `score` | [Integer Range](#integer-range) | From 0 to 100. The starting score. |
| `familyBonus` | integer | At least 0. |
| `spouseBonus` | integer | At least 0. |
| `elderBonus` | integer | At least 0. |
| `elderInLawBonus` | integer | At least 0. |
| `neighborBonus` | integer | At least 0. For characters in different households whose houses share a quarter. |
| `friendScore` | integer | From 0 to 100. Characters with at least this score are friends. |
| `levels` | list of [Relationship Level](#relationship-level) | In order of `minimum`: the first's is 0, and each after is higher, up to 100. A score is shown as the title of the last level it reaches. |
| `playerRelations` | list of [Player Relation](#player-relation) | Optional (none). Can be empty. How members of the player's household are related to the player; the first whose `when` is met is shown. |

Within a household, the bonus comes from the head relation values: the head and wife, or two elders who are both parents of the head or of the wife, get `spouseBonus`; an elder and their own child or any child of the head get `elderBonus`; an elder and their child's spouse get `elderInLawBonus`; anyone else gets `familyBonus`. Scores are capped at 100.

#### Relationship Level

| Property | Type | Notes |
|---|---|---|
| `minimum` | integer | The lowest score with this title. |
| `title` | string | |

#### Player Relation

| Property | Type | Notes |
|---|---|---|
| `when` | [Condition](#condition) | Can't use `hidden`. |
| `title` | string | Shown as in "This is your {title}". |

### How Households Are Generated

The player's household comes first, with the player at its head, then the [Village](#village-settings) `familyCount` more. Everyone else uses the non-player settings and the household's last name, unless noted. Each head gets the `head` household role and everyone else `member`, along with the fitting head relation and the head's full name.

* The head is a woman with `head.femaleChance`; a woman at the head lives with no men. The head's age comes from `head.age`. Their usual last name is their `lastName` value, but an unused other last name is used if that's blank or taken, or another head has the same profession.
* A man has a wife with `wife.chance`; the player always does if the ages allow a wife who could have an adult child. The head and wife get `married` and everyone else `unmarried`, before other values are chosen. On the new game screen the player is `married` if male.
* Children are added from oldest to youngest, with different ages within the `age` range, each parent's age minus the child's from `parentAge.minimum` to their maximum. The player always has one if possible.
* A household elder is added with `elder.chance`: a parent of the head or the wife (a widow's own parent or her late husband's, with even chances). A wife's parent and a widow's own parent have an other last name. An elder outside the age range or under `elder.minAge` isn't added.
* Each inherited property is set first: an elder has their child's value, a child of the head alone the head's, and a child of the head and wife takes after both. A relative's value the character can't have is ignored.
* A man who is an elder is infirm; a woman is infirm with `elder.infirmChance` for her age group, and otherwise helps as a member would, keeps house with `elder.homemakerChance`, or takes up a profession with `forHouseholdElders` (by frequency, within `maxBusinesses`), or keeps house if there's none.
* Each member besides the head helps with `helperChance` in the head's or else the wife's business, if allowed and there's room, and gets the `helper` work role; everyone else gets `independent`. A wife or child who doesn't help keeps house (a daughter, with `children.homemakerChance`) or starts a business that works from home (or any business, if they can't have one that does). The head always starts a business. Businesses are chosen among the professions the person can have that have a place in the village and haven't reached `maxBusinesses`, dropping either rule if it leaves nothing. A woman at the head chooses among professions with at least `head.femaleMinWealth`, if she can have any. The player's choice is never limited.
* A household's wealth is the sum of its businesses' wealth, what helpers add, and `wealth.homemaker` and `wealth.infirm` for each, multiplied by the `wealth.headAge` percentage and a percentage from `wealth.fortune`. Its level is `high` or `medium` if it reaches that threshold, and `low` otherwise.

## Village

#### Village Settings

The `village` settings section.

| Property | Type | Notes |
|---|---|---|
| `familyCount` | [Integer Range](#integer-range) | Minimum at least 0. Households besides the player's. |
| `landmarkCount` | [Integer Range](#integer-range) | Minimum at least 0. Landmark quarters, counting landmarks of unique kinds. |
| `featureCount` | [Integer Range](#integer-range) | Required if there are `features`. Minimum at least 0. |
| `buildingTravelTime` | integer | At least 0. Minutes to go into or out of a house or business. |
| `houseTitle` | string | [Template](#template) filled in with the properties (not templates) of the household's head. Every house's title. |

#### Quarter Kind

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `area` | string | `village` or `outskirts`. |
| `unique` | boolean | Optional (false). Every village has exactly one quarter of this kind. |
| `optional` | [Quarter Kind Optional](#quarter-kind-optional) | Optional. Unique kinds only. Can't be a layout's center. |
| `path` | string | What a path to or from the kind is called. |
| `borders` | string | Optional. Not for unique kinds. A feature id. Quarters of the kind border it, and are only used in villages that have it. |
| `travelTime` | integer | At least 0. Minutes to walk a path to or from the kind. A path takes the longer of its ends' times. |

#### Quarter Kind Optional

A village with the feature only has a quarter of the kind `chance` percent of the time, unless none of its houses' and businesses' quarters border the feature.

| Property | Type | Notes |
|---|---|---|
| `with` | string | A feature id. |
| `chance` | number | Optional (50). Chance. |

#### Layout

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `shape` | string | `radial`, `linear`, `crossroads`, or `warren`. |
| `center` | string | A unique village kind without `optional`, the shape is built around. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). |

#### Map Feature

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Shown on the map. |
| `type` | string | `river`: a channel one cell wide across the map. `area`: a rectangle next to the quarters that border it. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). |
| `size` | [Quarter Size](#quarter-size) | Areas only, and required for them. |
| `crossings` | list of string | Rivers only, and required for them. Each unique. What a path across can be called. |
| `text` | list of [Long Text](#long-text) | Sentences that can be added to a bordering quarter's description. Each is a [Template](#template) whose only placeholder is `{direction}` (optionally `{direction:first_upper}`), the direction the feature is in. |

#### Location Description

| Property | Type | Notes |
|---|---|---|
| `short` | [Long Text](#long-text) | One sentence, shown when the location is listed as a place to go. |
| `medium` | [Long Text](#long-text) | One paragraph, shown on arrival. |
| `long` | list of [Long Text](#long-text) | Paragraphs shown when examining the location. |

#### Quarter

A group of houses and businesses, or a landmark if it has neither. Quarter, house, and business ids are unique across all three lists.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | |
| `description` | [Location Description](#location-description) | |
| `frequency` | [Frequency](#frequency) | Optional (`common`). |
| `kind` | string | A quarter kind. |
| `houses` | [Quarter Houses](#quarter-houses) | Optional (no houses). |
| `businesses` | integer | Optional (0). At least 0. The most businesses. |
| `size` | [Quarter Size](#quarter-size) | Optional (1 by 1). Can be turned either way. |
| `borders` | string | Optional. Not for unique kinds. A feature it borders beyond its kind's. Only used in villages with it. |
| `featureText` | map of string to [Long Text](#long-text) | Optional. Each key is a feature id, and each value a sentence like a feature's `text`, used instead of it. |

#### Quarter Houses

| Property | Type | Notes |
|---|---|---|
| `minimum` | integer | At least 0. The fewest houses, if there are enough households. |
| `maximum` | integer | At least `minimum` and at least 1. |
| `shape` | [Shape](#shape) | Optional. Weights the planned number of houses. Without it, the plan leans toward fewer. |
| `wealth` | string | `low`, `medium`, or `high`. The wealth of the households living there. |

#### Quarter Size

| Property | Type | Notes |
|---|---|---|
| `length` | integer | Optional (1). At least 1. |
| `width` | integer | Optional (1). At least 1. |

#### House

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `description` | [Location Description](#location-description) | |
| `frequency` | [Frequency](#frequency) | Optional (`common`). |
| `wealth` | string | `low`, `medium`, or `high`. |

A house has no `title`; it's titled by the village's `houseTitle`.

#### Business Place

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | |
| `description` | [Location Description](#location-description) | |
| `frequency` | [Frequency](#frequency) | Optional (`common`). |
| `profession` | string | A business profession that doesn't work from home. |
| `quarterKinds` | list of string | Each unique and a quarter kind. At least one must have a quarter with room for businesses. |

There must be a house, and a quarter with houses, for each of `low`, `medium`, and `high`. Every unique kind needs a quarter, and there must be a landmark quarter if `landmarkCount` can be more than 0.

### How the Village Is Laid Out

* **Features.** First the village chooses `featureCount` features by frequency, with at most one river, adding any the player's profession needs for a place. A profession has a place if it works from home, or one of its places can go in a kind whose `borders` feature (if any) the village has. Quarters and places needing a missing feature are never used.
* **Quarters.** The village gets one quarter of each unique kind (an optional kind is decided once houses and businesses are placed). Each household gets a house of its wealth in a quarter of that wealth, adding quarters until there's room: each plans a number of houses within its range, gets one, then its minimum, and the rest are spread. Each business that doesn't work from home gets a place of its profession in a quarter of one of its kinds with room, adding a non-unique quarter (preferring one with a minimum of 0 houses) if needed, or using a full one with a warning. Landmarks are added up to `landmarkCount`, alternating between the village and the outskirts. Unused templates are chosen first, by frequency; reusing one logs a warning.
* **Shape.** A layout is chosen by frequency and arranges quarters as non-overlapping rectangles on a grid from its `center`. `radial` rings the center with four to six quarters; `linear` runs a main street of four to six through it, half the time with a second street alongside; `crossroads` sends arms of one or two quarters out in four directions; `warren` crowds quarters together with many small loops. The core is taken roughly richest first, the rest of the village branches off it near quarters of the same wealth, and landmarks come first. The outskirts leave in two or three groups (from the ends of the street or arms, or different sides) as chains, loops of three, hubs, or ladders.
* **Connections.** Quarters only connect where their edges meet, at most two per side. Each connection gets a direction (its side, or one step toward the nearer end), with no quarter having two exits in one direction, dropping connections that can't unless needed. A connection is named by the outskirts kind's `path` if one end is in the outskirts, or else either kind's (preferring non-unique), or a river's `crossings` across a river.
* **River.** The river runs along one side of the village, touching the outermost quarters or one cell beyond. A quarter growing into it goes on the far bank, an outskirts quarter can connect across it, and quarters that border it are placed last, as close as there's room.
* **Areas.** Each area feature is placed touching a quarter that borders it, or else an outskirts quarter far from the center. A quarter that should border a feature but doesn't logs a warning.
* **Descriptions.** For each feature a quarter borders (touching a river, or sharing an edge with an area), its `featureText` or a random feature `text` is added to its medium description, with the direction.

## Calendar

A year is made of seasons, each of weeks, each of days, in list order. A day has 24 hours. Times are shown with the day, the week and season, and the time, and the year when shown in full.

#### Calendar Settings

The `calendar` settings section.

| Property | Type | Notes |
|---|---|---|
| `startYear` | integer | At least 1. The year a new game starts in. |
| `yearFormat` | string | How a year is shown. Must include `{year}`. |

#### Calendar Item

A day of the week, a week of each season, or a season of the year.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | |

#### Day Part

Each lasts until the next starts, and the last until the first starts the next day.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | |
| `start` | integer | From 0 to 23, after the one before. |
| `announcement` | [Long Text](#long-text) | Optional. What the player notices when it starts while they wait. Without it, they're told which part has begun. |

#### Special Day

A day of the year with its own title, the same day every year.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | |
| `season` | string | A season id. |
| `week` | string | A week id. |
| `day` | string | A day id. |

## Schedules

Everyone works through tasks toward a goal: walking to a place, starting work if needed, spending time there, and stopping work. Someone other than the player who runs out of tasks chooses one of their needs with an urgency of at least `minimumUrgency`, with chances in proportion to urgency raised to `urgencyPower`. A need that can't be met leaves them idle. Urgency runs from 0 to 100, then is multiplied by every urgency factor that applies.

#### Schedules Settings

The `schedules` settings section.

| Property | Type | Notes |
|---|---|---|
| `minimumUrgency` | number | At least 0. |
| `urgencyPower` | number | More than 0. |
| `sightingHours` | integer | At least 1. How long people remember the last time they saw someone. |
| `sleep` | [Sleep](#sleep) | |
| `talking` | [Talking](#talking) | |
| `socializing` | [Socializing](#socializing) | |
| `idle` | [Idle](#idle) | |
| `start` | [Start](#start) | |

#### Sleep

About the need with the `sleep` role. Choosing it below `napBelow` is a nap of `napMinutes` (once between full sleeps), unless it's bedtime. For someone with a quota need met at their workplace whose sleep level is above its `urgentFrom`, sleep is also as urgent as a quota need with `bedtimeLead` would be, counting the minutes until work next starts less the sleep they need.

| Property | Type | Notes |
|---|---|---|
| `napBelow` | number | From 0 to 100. |
| `napMinutes` | [Integer Range](#integer-range) | Minimum at least 1. |
| `bedtimeLead` | integer | At least 1. |

#### Talking

Talking pauses what someone was doing until it's over. If their goal is a quota need that's then closed or done, they choose again.

| Property | Type | Notes |
|---|---|---|
| `refuseUrgency` | number | At least 0. Someone whose goal is at least this urgent refuses to talk. |
| `wakeLevel` | number | From 0 to 100. How much the sleep need rises for someone woken to talk. |
| `askAboutMinutes` | integer | At least 1. Asking whether someone has seen a person. |
| `askDoingMinutes` | integer | At least 1. Asking what someone is doing. |

#### Socializing

About the need with the `socializing` role, met (at its `satisfyPerHour`) only while chatting with a friend (a [Relationships](#relationships) score of at least `friendScore`). It's only chosen by someone with a friend, with its urgency times `seekFactor`.

When someone starts a task, they and each friend there may chat: each is willing by their socializing urgency times their score out of 100, less their goal's urgency times `chatGoalWeight` (unless their goal is socializing), and the more willing starts if at least `chatUrgency`. Neither can be talking, the starter can't be asleep, and the other can't be too busy or asleep (unless the starter's socializing urgency is at least `wakeUrgency`). Unless told what the friend is doing since their goal was chosen, they ask first. Someone asking the player stops time until the player answers, and waits `askPlayerAgainHours` after being turned down.

Seeking out a friend, the person picks a friend and place from clues, each weighing its weight in `clues`: the friend's workplace during their work hours, or home otherwise; their last sighting (or where they said they were going), fading to nothing after `sightingHours`; and places for needs met in a quarter on some days, within hours and not done. Each weight is divided by 1 plus the walk in minutes over `nearMinutes`, and multiplied by the hours since they last talked over that plus `recentHours`. The current place is never chosen. They look around for `seekMinutes`, calling out to a friend leaving the place they're heading to, and otherwise ask someone there what they've seen of their friends.

| Property | Type | Notes |
|---|---|---|
| `chatMinutes` | integer | At least 1. How long a chat takes. |
| `chatUrgency` | number | At least 0. |
| `chatGoalWeight` | number | At least 0. |
| `wakeUrgency` | number | At least 0. |
| `seekFactor` | number | More than 0. |
| `seekMinutes` | integer | At least 1. |
| `recentHours` | number | More than 0. |
| `nearMinutes` | number | More than 0. |
| `askPlayerAgainHours` | number | More than 0. |
| `clues` | [Seek Clues](#seek-clues) | |

#### Seek Clues

| Property | Type | Notes |
|---|---|---|
| `workplace` | number | At least 0. |
| `home` | number | At least 0. |
| `sighting` | number | At least 0. |
| `gathering` | number | At least 0. |

#### Idle

Someone who doesn't choose a need waits at a place, then decides again.

| Property | Type | Notes |
|---|---|---|
| `place` | [Place](#place) | |
| `minutes` | [Integer Range](#integer-range) | Minimum at least 1. |
| `activity` | string | Optional. What they're shown to be doing. |

#### Start

A new game starts at midnight on the first day of a random week of the calendar's `startYear`. Everyone is at home asleep, with each level need at its `startLevel`, except anyone other than the player whose work hours include midnight on a working day, who starts awake at work. Each sleeper wakes `wakeMinutes` from `wakeHour` (at least a minute after midnight), and time runs until the player wakes.

| Property | Type | Notes |
|---|---|---|
| `wakeHour` | integer | From 0 to 23. |
| `wakeMinutes` | [Integer Range](#integer-range) | |

#### Place

`"home"`, `"workplace"` (the business's place, or home if it's run from home; only people with a business have such needs), or a quarter, written as an object:

| Property | Type | Notes |
|---|---|---|
| `quarterKinds` | list of [Quarter Choice](#quarter-choice) | The kind is chosen by frequency among the village's kinds, then a quarter of it, weighted by 1 over 1 plus the walk in minutes. |
| `nearest` | boolean | Optional (false). The nearest quarter of any of the kinds instead. |

#### Quarter Choice

| Property | Type | Notes |
|---|---|---|
| `quarterKind` | string | A quarter kind id. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). |

#### Need

Has exactly one of `level` and `quota`.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Shown to the player with its level's title, for a need with `levels`. |
| `role` | string | Optional. `sleep` (required on one need when there are needs) or `socializing`, each on at most one level need. |
| `activity` | string | Optional (the title). What someone meeting it is shown doing. |
| `when` | [Condition](#condition) | Optional. Can't use `hidden`. Who has the need. Without it, everyone does. |
| `place` | [Place](#place) | Where it's met. |
| `atWork` | boolean | Optional (false). People are at work while meeting it. |
| `returnHome` | boolean | Optional (false). Not for `home`. People walk home afterward. |
| `hours` | [Hours](#hours) | Required for quota needs, with `days` and `onSpecialDays` allowed. Optional for level needs, without them. |
| `level` | [Level Need](#level-need) | Optional. |
| `quota` | [Quota Need](#quota-need) | Optional. |
| `playerAction` | [Player Action](#player-action) | Optional. |

#### Level Need

A level from 0 to 100 that grows by `perHour` each hour, except while it's met (dropping by `satisfyPerHour`) or a `pausedBy` need is met. Meeting it lasts until it's 0. Its urgency rises from 0 at `urgentFrom` to 100 at 100, and is 0 outside its hours.

| Property | Type | Notes |
|---|---|---|
| `perHour` | number | At least 0. |
| `satisfyPerHour` | number | More than 0. |
| `pausedBy` | list of string | Optional. Need ids. |
| `urgentFrom` | number | Optional (0). At least 0 and less than 100. |
| `wakeUrgency` | number | Optional. At least 0. A sleeper (not the player) wakes once this need's urgency reaches it and passes sleep's, unless sleep pauses it. Without it, the need never wakes anyone. |
| `startLevel` | [Integer Range](#integer-range) | Optional (0). From 0 to 100. |
| `levels` | list of [Level](#level) | Optional. The words for how high it is. Needs with them are shown to the player. |

#### Level

The title of the last level at or below the need's level is shown.

| Property | Type | Notes |
|---|---|---|
| `minimum` | number | From 0 to 100, higher than the one before. |
| `title` | string | |

#### Quota Need

A quota for each day or week, measured in time or trips, met within its hours. Only time at the place (and at home after a trip) counts, and it starts again each day or week. For a need shared by the family, everyone's time counts, including time others plan to spend. Its urgency is 0 outside its hours, on other days, or once met; otherwise it's 0 while the spare time left (minutes left in its hours this period, less the quota left) is at least `lead`, rising to 100 as it runs out. Each stretch or trip is shortened if less of the quota or the day's hours are left. Has exactly one of `time` and `trips`, and the quota for one person must fit in its hours for the period.

| Property | Type | Notes |
|---|---|---|
| `period` | string | Optional (`day`). `day` or `week`. |
| `lead` | integer | More than 0. |
| `overdueUrgency` | number | Optional. From 0 to 100. Weekly needs with `hours.days` only. Once its days have passed this week with the quota unmet, it can be met on any day of the week (other than special days it skips) with this urgency. |
| `sharedByFamily` | boolean | Optional (false). Met for the whole family by anyone in it. |
| `time` | [Quota Time](#quota-time) | Optional. |
| `trips` | [Quota Trips](#quota-trips) | Optional. |

#### Quota Time

| Property | Type | Notes |
|---|---|---|
| `minutes` | [Family Amount](#family-amount) | Whole numbers. The minutes it takes each period. |
| `shift` | integer | More than 0. The most minutes spent at once. |

#### Quota Trips

| Property | Type | Notes |
|---|---|---|
| `visits` | [Family Amount](#family-amount) | Trips each period, rounded up. |
| `minutes` | [Family Amount](#family-amount) | Whole numbers. Minutes at the place each trip; at least 1 for one person. |
| `home` | [Quota Trip Home](#quota-trip-home) | Optional. Only for needs that return home. |

#### Quota Trip Home

The time each trip takes at home afterward.

| Property | Type | Notes |
|---|---|---|
| `minutes` | [Family Amount](#family-amount) | Whole numbers. |
| `activity` | string | Optional (the need's activity). What someone is shown doing during that time. |

#### Family Amount

A number, or an object:

| Property | Type | Notes |
|---|---|---|
| `base` | number | Optional (0). At least 0. |
| `perMember` | number | Optional. At least 0. Added for each family member. Only for needs shared by the family. |

The object needs at least one of them.

#### Player Action

Lets the player meet the need where it's met (their home or workplace, or a quarter of one of its kinds), unless it's met at a workplace and they have no business, or they don't meet its `when`. For a need not measured in trips, they choose an option and spend that long. For a trip, they spend a trip's time at the place, are told `return`, and finish at home with `home`. Quota actions can't be taken on a day the need can't be met (unless overdue), or with `onlyInHours`, outside its hours, when the player is told when it's open instead. After each action the player is told `result` (or the home `result`) and how long it took.

| Property | Type | Notes |
|---|---|---|
| `title` | string | |
| `result` | string | |
| `options` | list of [Player Action Option](#player-action-option) | Required unless measured in trips, and not allowed then. |
| `return` | [Long Text](#long-text) | Required for a trip that returns home, and not allowed otherwise. |
| `home` | [Player Action Home](#player-action-home) | Required for a trip that returns home, and not allowed otherwise. |
| `onlyInHours` | boolean | Optional (false). Quota needs only. |

#### Player Action Home

The action at home that finishes a trip.

| Property | Type | Notes |
|---|---|---|
| `title` | string | |
| `result` | string | |

#### Player Action Option

Exactly one of `untilMet`, `untilHour`, and `minutes`. An option that takes no time can't be chosen.

| Property | Type | Notes |
|---|---|---|
| `title` | string | Optional with `minutes`, which is otherwise called by its length. |
| `untilMet` | boolean | Optional (false). Until a level need is 0, or the quota is met. |
| `untilHour` | integer | Optional. From 0 to 23. Until the next time it's this hour. |
| `minutes` | integer | Optional. More than 0. |

#### Urgency Factor

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `when` | [Condition](#condition) | Can't use `hidden`. |
| `needs` | list of string | Need ids, each listed once. |
| `factor` | number | More than 0. Multiplies those needs' urgency. |

#### Travel Time Factor

Walking takes its travel time times every factor that applies, rounded up.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `when` | [Condition](#condition) | Can't use `hidden`. |
| `factor` | number | More than 0. |

## Portraits

A portrait is drawn from its layers, bottom to top, and cut off at its edges. A **color** is `#RRGGBB`, `#AARRGGBB`, or the id of a Portrait Color.

#### Portraits Settings

The `portraits` settings section.

| Property | Type | Notes |
|---|---|---|
| `width` | number | More than 0, in the units the paths use. |
| `height` | number | More than 0, in the units the paths use. |

#### Portrait Color

Worked out for each character in list order, using only colors before it.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `color` | string | Color, unless a variant applies. |
| `variants` | list of [Portrait Color Variant](#portrait-color-variant) | Optional. The first whose `when` is met replaces `color`. |
| `mix` | string | Optional, with `amount`. Color blended in. |
| `amount` | number | Optional, with `mix`. From 0 to 1. |

#### Portrait Color Variant

| Property | Type | Notes |
|---|---|---|
| `when` | [Condition](#condition) | Can't use `hidden`. |
| `color` | string | Color. |

#### Portrait Scale

A stretch that doesn't change outline widths.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `originX` | number | Optional (0). The horizontal position that stays in place. |
| `originY` | number | Optional (0). The vertical position that stays in place. |
| `x` | number | Optional (1). More than 0. |
| `y` | number | Optional (1). More than 0. |
| `variants` | list of [Portrait Scale Variant](#portrait-scale-variant) | Optional. The first whose `when` is met replaces `x` and `y`. |

#### Portrait Scale Variant

| Property | Type | Notes |
|---|---|---|
| `when` | [Condition](#condition) | Can't use `hidden`. |
| `x` | number | Optional (1). More than 0. |
| `y` | number | Optional (1). More than 0. |

#### Portrait Offset

A shift, applied after stretching.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `x` | number | Optional (0). How far right. |
| `y` | number | Optional (0). How far down. |
| `variants` | list of [Portrait Offset Variant](#portrait-offset-variant) | Optional. The first whose `when` is met replaces `x` and `y`. |

#### Portrait Offset Variant

| Property | Type | Notes |
|---|---|---|
| `when` | [Condition](#condition) | Can't use `hidden`. |
| `x` | number | Optional (0). |
| `y` | number | Optional (0). |

#### Portrait Layer

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `when` | [Condition](#condition) | Optional. Can't use `hidden`. |
| `at` | [Portrait Place](#portrait-place) | Optional. Without it, drawn anywhere. |
| `scale` | string | Optional. A Portrait Scale id, for shapes without their own. |
| `offset` | string | Optional. A Portrait Offset id, for all shapes. |
| `shapes` | list of [Portrait Shape](#portrait-shape) | Drawn bottom to top. |

#### Portrait Place

Has `home` set to true, `quarterKinds`, `workplaces`, or several. A character is in a workplace in a business's place, or at home while `atWork`; in a home in any other house; and otherwise in their quarter's kind. On the new game screen the player is at home and not at work.

| Property | Type | Notes |
|---|---|---|
| `home` | boolean | Optional (false). |
| `quarterKinds` | list of string | Optional. Unique quarter kind ids. |
| `workplaces` | list of string | Optional. Unique professions. Drawn in a place of the profession, or at home for someone working from home in it. |

#### Portrait Shape

Has `fill`, `stroke`, or both.

| Property | Type | Notes |
|---|---|---|
| `path` | string | WPF path markup (SVG path data, optionally starting with `F0` for even-odd fill, the default, or `F1` for nonzero). |
| `when` | [Condition](#condition) | Optional. Can't use `hidden`. |
| `fill` | string | Optional. Color. |
| `gradient` | string | Optional, with `fill`. `vertical`, `horizontal`, or `radial`, from `fill` to `fillTo` across the shape's bounds. |
| `fillTo` | string | Optional, with `gradient`. Color. Without it, `fill` fades to transparent. |
| `stroke` | string | Optional. Color, with rounded ends and corners. |
| `strokeWidth` | number | Optional (1). More than 0. |
| `opacity` | number | Optional (1). From 0 to 1. |
| `scale` | string | Optional. A Portrait Scale id, used in place of the layer's. |

# Adding to the Data

Most mistakes stop the data from loading. These steps are **not checked**, and only show up as something missing in the game. Afterward, generate villages with the village report (see `Docs/CommandLine.md`) and check its `log` records.

Portraits.json is generated, so portrait art is added to SubCorona's `Art/Portraits/Portraits.sh`, as described in SubCorona's `Docs/Portraits.md`.

Saved games refer to properties, values, location and map feature templates, and needs by id, so renaming or removing one stops older saves from loading.

* **Profession:** give it the `text` entries the templates use, and `player: false` if the player shouldn't have it. Make sure there are enough business places for `maxBusinesses`, or businesses end up in full quarters with a warning. Add it to the value groups that fit, and to the conditions that list professions it should be in. Add portrait art: a workplace background and work clothes.
* **Character property:** use it in a template `description` uses, with the `text` entries those templates need. Add it to the obscurers that should hide it, and to `families.inheritedProperties` if family members share it. Add portrait art if it changes how a character looks.
* **Character value:** add it to any `player` or `npc` `values` lists and value groups it belongs in, and check the conditions that list other values of the property: a `notValues` list lets it through, and a `values` list leaves it out. Add portrait art if the property shows in portraits. Its place in the list matters for a property with a `shape`, and for an inherited property with a `blend`.
* **Template:** use it from `description` or a template it uses, or it's never shown.
* **Quarter kind:** add quarters of the kind, add it to the `quarterKinds` of the business places that can be in it, and add a portrait background.
* **Quarter or house:** make sure there are enough for each kind and wealth level, or templates are reused with a warning.
* **Map feature:** set `borders` on the quarter kinds or quarters that belong beside it; otherwise an area is placed next to an outskirts quarter.
* **Day:** add it to the `hours.days` of the needs met on it, and check the special days still fall where they should.
* **Need:** add it to the `pausedBy` of level needs that shouldn't grow while it's met, and to the urgency factors that apply to it.
