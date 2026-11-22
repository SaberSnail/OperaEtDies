# Data Files

The game reads its data from the `Data` folder next to the executable. Mods can change it (see [Mods.md](Mods.md)).

## Files and Sections

Every `.json` file in the data folder and its subfolders is read, in order of their paths. File names don't matter: each file is an object whose properties are **sections**, and any section can be in any file.

* A **list section** is a list of items, each with an `id`. Lists with the same name are joined, in file order. An id already used in the list is an error.
* A **settings section** is an object. Objects with the same name are joined property by property. A property already set by another file is an error.
* `comments` isn't a section. It's an optional string saying what the file is for, for the people editing the data, and the game ignores it. Objects in the data can have comments too (see General Rules).

Problems are reported against the file holding the item or setting they're about.

| Section | Kind | Holds |
|---|---|---|
| `game` | settings | [Game](#game) |
| `properties` | list | [Character Property](#character-property) |
| `templates` | list | [Character Template](#character-template) |
| `clothingSlots` | list | [Clothing Slot](#clothing-slot). Optional. |
| `clothingBulks` | list | [Clothing Bulk](#clothing-bulk). Optional. |
| `clothingSettings` | settings | [Clothing Settings](#clothing-settings). Optional. |
| `clothingLayers` | list | [Clothing Layer](#clothing-layer). Optional. |
| `clothingColors` | list | [Clothing Color](#clothing-color). Optional, unless a clothing item has `colors`. |
| `clothing` | list | [Clothing Item](#clothing-item). Optional. |
| `outfits` | list | [Outfit](#outfit). Optional. |
| `modestyRules` | list | [Modesty Rule](#modesty-rule). Optional. |
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
| `stepLists` | list | [Step List](#step-list). Optional. |
| `urgencyFactors` | list | [Urgency Factor](#urgency-factor). Optional. |
| `travelTimeFactors` | list | [Travel Time Factor](#travel-time-factor). Optional. |
| `portraits` | settings | [Portraits](#portraits-settings). Optional; without it, characters have no portraits. |
| `portraitColors` | list | [Portrait Color](#portrait-color). Optional. |
| `portraitScales` | list | [Portrait Scale](#portrait-scale). Optional. |
| `portraitOffsets` | list | [Portrait Offset](#portrait-offset). Optional. |
| `portraitShapeSets` | list | [Portrait Shape Set](#portrait-shape-set). Optional. |
| `portraitLayers` | list | [Portrait Layer](#portrait-layer). Required with `portraits`. |

## General Rules

* Files are JSON. Comments and trailing commas are allowed.
* Property names are camelCase, but matching ignores case. An unknown property is an error.
* Every object described by a table below (an item, a settings section, or an object inside one) can also have an optional `comments` string, which can be empty. It's for the people editing the data, and the game ignores it. Maps and lists can't have comments. A settings section's `comments` is joined like its other properties, so only one file can set it.
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
| `text` | string | The text. |
| `ai` | boolean | Whether AI wrote the text. Not used by the game. |

#### Integer Range

| Property | Type | Notes |
|---|---|---|
| `minimum` | integer | The lowest number. |
| `maximum` | integer | At least `minimum`. |
| `shape` | [Shape](#shape) | Optional. Weights the numbers by their order. Without it, every number is equally likely. |

#### Duration Range

A range of lengths of time, chosen as a whole number of minutes.

| Property | Type | Notes |
|---|---|---|
| `minimum` | [Duration](#duration) | The shortest. |
| `maximum` | [Duration](#duration) | At least `minimum`. |
| `shape` | [Shape](#shape) | Optional. Weights the numbers of minutes by their order. Without it, every number is equally likely. |

#### Shape

Weights an ordered list by a bell curve.

| Property | Type | Notes |
|---|---|---|
| `peak` | string | `low`, `middle`, or `high`: the first, middle, or last item is the most likely. Each `+` or `-` (up to two) moves the peak a tenth of the way along the list. `low` can't move down, and `high` can't move up. |
| `mostWithin` | number | More than 0. About two-thirds of choices fall within this many steps of the peak. |

#### Weighted Value

| Property | Type | Notes |
|---|---|---|
| `value` | string | The value. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often it's chosen. |

#### Condition

Has at least one of `values`, `notValues`, and `hidden`, and is met when every part it has is met. A condition that "can't use `hidden`" is any condition outside templates.

| Property | Type | Notes |
|---|---|---|
| `values` | map of string to list of string | Optional. Each key is a value list property, and each list holds its value ids or value group ids (see [Character Value](#character-value)). Outside templates, a key can also be an integer property with `groups`, with group ids. Met when every listed property has one of its listed values. A property with no value never meets it. |
| `notValues` | map of string to list of string | Optional. Like `values`, but met when every listed property has a value that isn't listed. |
| `hidden` | list of string | Optional. Templates only. Property ids that must have no visible value. Character templates can also list `clothing`. In templates, a property whose value is obscured counts as having no value. |

#### Frequency Override

| Property | Type | Notes |
|---|---|---|
| `when` | [Condition](#condition) | Can't use `hidden`. When the override applies. |
| `frequency` | [Frequency](#frequency) | Used in place of the usual frequency when `when` is met. |

In a list of overrides, the first whose condition is met is used. The item then has the chance an item of the new frequency has among the list's usual frequencies, and the chances are scaled to add up to 100.

#### Template

A string with placeholders. `{id}` is replaced by the visible value of the property with that id (its text, number, or value `title`), or by the text of the template with that id. `{id.name}` is replaced by the `name` entry of the `text` of a value list property's value. Either can end with `:first_upper`, which capitalizes the first character. Literal braces are written `{{` and `}}`. Every placeholder must name a property or template, and a named entry must exist on every value the variant's condition allows. A template can't use itself, directly or through other templates. In the result, runs of spaces become one, and spaces at the ends or before `.`, `,`, `;`, `:`, `!`, or `?` are removed.

#### Hours

The hours of the day something happens in.

| Property | Type | Notes |
|---|---|---|
| `start` | integer | From 0 to 23. The hour it starts. |
| `end` | integer | From 1 to 24 (midnight), and different from `start`. An end before the start runs past midnight. |
| `days` | list of string | Optional (every day). Only where noted. Day ids, each listed once. |
| `onSpecialDays` | boolean | Optional (true). Only where noted. |

## Game

#### Game

The `game` settings section.

| Property | Type | Notes |
|---|---|---|
| `title` | string | Mods can't change it. The game's name. |
| `description` | [Long Text](#long-text) | Shown when choosing a game to play. |
| `icon` | string | Optional. Path of the application icon, relative to the data folder. Not rooted, and no invalid path characters. Mods can't change it. |
| `frequencyChances` | [Frequency Chances](#frequency-chances) | Optional. The chance of each frequency. |
| `lowChanceWarning` | number | Optional. At least 0 and less than 100. A warning is logged when the data loads for every character value with this chance or less of being chosen, for each role. |

#### Frequency Chances

Percentages. `uncommon`, `rare`, and `veryRare` together must be less than 100.

| Property | Type | Notes |
|---|---|---|
| `uncommon` | number | Optional (20). At least 0. The chance shared by the uncommon items in a list. |
| `rare` | number | Optional (5). At least 0. The chance shared by the rare items in a list. |
| `veryRare` | number | Optional (0.5). At least 0. The chance shared by the very rare items in a list. |
| `stepMultiplier` | number | Optional (1.5). At least 1. How much each `+` multiplies a chance by, and each `-` divides it by. |

## Characters

#### Character Property

| Property | Type | Notes |
|---|---|---|
| `id` | string | Unique within `properties`, and not `clothing`. |
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
| `regions` | list of string | Optional (none). Unique clothing slot ids, when there are clothing slots: the regions of the body the property shows on. Worn clothing hides a value when it covers every one of them at least as much as the value's prominence (string and integer values have a prominence of 10). How much a region is covered is the most `coverage` any worn piece gives it. Without regions, clothing never hides the property. |
| `form` | boolean | Optional (false). Whether the property is about the form of the body, such as a paunch or broad shoulders: only worn clothing whose `bulk` has `hidesForm` counts toward hiding it. |

A value list property picks a value, by frequency or shape, from those whose conditions are met, and picks again when its value's condition stops being met. An integer picks a number in its range. A string picks a default, or is blank. On the new game screen, the player must fill in every string property they have; a property is hidden while it has no value or only one possible value.

**Order.** A property **depends on** the properties its conditions, its values' conditions and `frequencyWhen` conditions, and its defaults' `keyedBy` use. Properties are chosen and shown in an order where each comes after what it depends on, a property with `follows` comes right after the one it follows (which waits for its follower's dependencies too), and otherwise in the order listed. A property can't depend on itself, directly or indirectly.

#### Character Value

| Property | Type | Notes |
|---|---|---|
| `id` | string | Unique within the property. |
| `title` | string | Shown on the new game screen, and in templates by `{id}`. |
| `role` | string | Optional. See [Roles](#roles). |
| `groups` | list of string | Optional. Groups the value is in. A condition listing a group id means every value in the group. A group can't be the id of one of the property's values. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often it's chosen. |
| `frequencyWhen` | list of [Frequency Override](#frequency-override) | Optional. Frequencies that replace `frequency` when their conditions are met. |
| `text` | map of string to string | Optional. Named text for templates. For a value with the `male` or `female` role, the game also uses its `possessive`, if it has one, such as "her", to describe things that are theirs, such as "her home". |
| `when` | [Condition](#condition) | Optional. A character can only have the value when it's met. |
| `prominence` | integer | Optional (10). From 1 to 10: how much clothing must cover the property's `regions` to hide the value. Only allowed when the property has `regions`. |

#### Integer Group

| Property | Type | Notes |
|---|---|---|
| `id` | string | Unique within `groups`. |
| `minimum` | integer | Optional (no lower limit). The lowest number in the group. |
| `maximum` | integer | Optional (no upper limit). At least `minimum`. |

#### String Defaults

Has `values`, or `keyedBy` with `valuesByKey`. A list whose key is a value of the `profession` property can come from that profession's `defaults` instead of `valuesByKey`.

| Property | Type | Notes |
|---|---|---|
| `values` | list of [Weighted Value](#weighted-value) | Optional. The defaults to pick from. |
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

Has `template` or `variants`. The ids `fullName` and `description` must exist. Their templates can also use `{clothing}`, without a named entry: in `description` and the templates it uses, the `description` of each piece of clothing worn that can be seen (see [Clothing Layer](#clothing-layer)), in the order of the first slot each takes in `clothingSlots` and then from the outermost layer in, joined into a list such as "a, b, and c". It has no visible value when nothing is worn, or in `fullName`.

| Property | Type | Notes |
|---|---|---|
| `id` | string | Unique within `templates`, not a property id, and not `clothing`. |
| `template` | string | Optional. [Template](#template). The text, for a template without variants. |
| `variants` | list of [Template Variant](#template-variant) | Optional. The first variant whose `when` is met is used. |

#### Template Variant

| Property | Type | Notes |
|---|---|---|
| `when` | [Condition](#condition) | Optional. Without it, the variant is always used. |
| `template` | string | [Template](#template). Can be empty. The text. |

**Checks.** For each role, conditions are checked in every combination of the values they depend on (leaving out Frequency Overrides). Every value list property must have an allowed value in every combination where its own condition is met. Every template in use (`fullName`, `description`, templates no other template uses, and those their variants use) must, in every combination of what its conditions and placeholders depend on, have a variant that's used and whose placeholders all have visible values, with each property clothing can hide (with every piece of clothing worn at once), and `{clothing}`, checked both visible and hidden. A check can have at most 10000 combinations, and a template can use at most 10 of these.

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
| `title` | string | Shown on the new game screen, and in templates by `{profession}`. |
| `role` | string | Optional. `homemaker` or `infirm`. |
| `groups` | list of string | Optional. As for a [Character Value](#character-value). |
| `player` | boolean | Optional (true). Whether the player can choose it. A profession without `business` must have it false. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often it's chosen. |
| `frequencyWhen` | list of [Frequency Override](#frequency-override) | Optional. Frequencies that replace `frequency` when their conditions are met. |
| `text` | map of string to string | Optional. Named text for templates. |
| `when` | [Condition](#condition) | Optional. A character can only have the profession when it's met. |
| `defaults` | map of string to list of [Weighted Value](#weighted-value) | Optional. Each key is a string property whose defaults are keyed by the `profession` property without their own `valuesByKey`. The profession's usual values, such as its last names. |
| `business` | [Business](#business) | Optional. Without it, the profession isn't a business. |
| `modesty` | map of string to map of string to integer | Optional (none). Can be empty. Each key is a modesty rule id, and each value a map whose keys are clothing slot ids and whose values are from -10 to 10: how much more (or less, when negative) that [Modesty Rule](#modesty-rule)'s minimums are for that slot while working, for people following it. The player counts as working from when they start work's steps until they're done with them, so they can change for work and go out in their work clothes. |

#### Business

| Property | Type | Notes |
|---|---|---|
| `wealth` | string | A wealth level, optionally followed by `+`, `++`, `-`, or `--`. Each step moves a fifth of the way to the next level: a step up adds a fifth of the level's amount and a step down takes away a tenth, so `low++` is 2.8 and `high-` is 7.2. What a business earns. |
| `maxBusinesses` | integer | Optional (no limit). At least 1. The most businesses in the village. |
| `helpers` | integer | Optional (no limit). At least 0. The most members of the owner's household who can help. |
| `helperWhen` | [Condition](#condition) | Optional. Can't use `hidden`. Who can help. Without it, anyone can. |
| `forHouseholdElders` | boolean | Optional (false). Only for a business that works from home (see below). A household elder can take it up. |
| `activity` | string | Optional. What the people in a business in this profession are shown doing while they work (on a step of a [need](#need) that's work without an `activity` of its own), instead of the need's `activity`. |
| `steps` | list of [Step](#step) | Optional. At least one item, with at least one `do` step, and for a need measured in time, a `do` or `wander` step with `meetsQuota`. What the people in a business in this profession do to work, replacing the steps of each [need](#need) that's work. Checked as the steps of each need that's work, which there must be. |
| `hours` | [Hours](#hours) | Optional. Without `days` or `onSpecialDays`. Used in place of the hours of the needs met at the workplace. |

A business works from home, with no place of its own, when no [Business Place](#business-place) is for its profession. Its work is done at home or wherever its steps take its people.

## Clothing

#### Clothing Slot

A region of the body. Clothing is anchored in its `slots`, and someone wears at most one piece of clothing in each slot in each layer. How much of each region clothing covers, whether it's anchored there or not, is its `coverage`. Worn clothing is described in the order of the first slot each piece takes.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Shown when the player must cover it more. |

#### Clothing Layer

How close to the body clothing is worn, listed from the innermost out. A worn piece is left out of the description of what someone is wearing when each of its slots is covered at least as much as the [Clothing Settings](#clothing-settings) `hidesClothingAt` by worn pieces in layers outside its own.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Its name. |

#### Clothing Bulk

How loose or heavy clothing is.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Its name. |
| `modesty` | integer | Optional (0). From -10 to 10. How much more (or less, when negative) modesty clothing of the bulk gives than its `coverage`, in each region it covers. |
| `hidesForm` | boolean | Optional (false). Whether clothing of the bulk hides the form of the body under it, for character properties with `form`. |

#### Clothing Settings

The `clothingSettings` settings section. Optional.

| Property | Type | Notes |
|---|---|---|
| `hidesClothingAt` | integer | Optional (10). From 0 to 10. How much each slot of a worn piece must be covered, by pieces in layers outside it, to leave it out of the description of what someone is wearing. |

#### Clothing Color

Any clothing can be any color.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `name` | string | The color in descriptions. |
| `color` | string | `#RRGGBB`. How it's drawn in portraits. |
| `article` | string | Optional (`a`). Goes before `name` where a description asks for it. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often a piece is given it. |

#### Clothing Item

An article of clothing. Each piece of it has a clothing color for each of its `colors`.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Its name, such as "Tunic". |
| `slots` | list of string | Unique clothing slot ids. Where it's anchored, which decides what it can be worn with. |
| `layer` | string | A clothing layer id. How close to the body it's worn. |
| `coverage` | map of string to integer | Optional (none). Can be empty. Each key is a clothing slot id, and each value from 0 to 10: how much of that region a worn piece covers, from 0 (none of it) to 10 (all of it), whether it takes the slot or not. A region that isn't listed is 0. |
| `bulk` | string | Optional. A clothing bulk id. Without it, the piece's coverage counts as it is for modesty, and it doesn't hide form. |
| `accessory` | boolean | Optional (`false`). Whether it's an accessory, such as shoes or a belt. Accessories aren't washed, and someone given several outfits with the same accessory gets one piece of it, which the outfits share. |
| `colors` | list of string | Optional (none). Unique names for the clothing's colors. Each piece has a color for each, which `description` and portrait layers use by name. |
| `description` | string | A few words describing a piece. Its placeholders are names in `colors`, replaced with the color's `name`, or with `article` and `name` when written `{name.a}`. The only format is `first_upper`. |
| `text` | [Long Text](#long-text) | What it looks like. |

#### Outfit

Clothing worn together, given to people when they're created.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `kind` | string | `everyday`, `work`, `night`, or `nothing` (what people have on when they'd otherwise wear nothing, such as for swimming, which is always complete). |
| `when` | [Condition](#condition) | Optional. Can't use `hidden`. Who can be given it. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often it's given. |
| `clothing` | list of string | Unique clothing item ids, no two of which take the same slot in the same layer. Can be empty when `complete`, or for a `nothing` outfit. The clothing in the outfit. |
| `complete` | boolean | Optional (false). Work outfits only. Whether it's a complete set of clothes worn on its own, such as a uniform: everything else is taken off when it's put on. Otherwise work clothes go over what's being worn. |

#### Modesty Rule

How well covered people must be. Each person follows the first rule whose `when` they meet, and one who meets none can wear anything. In each slot a rule lists, the modesty of the worn clothing must be at least the minimum: the most any worn piece covering the region gives, which is its `coverage` plus its `bulk`'s `modesty` (kept from 0 to 10), plus the profession's `modesty` for the slot while working (the minimum is kept from 0 to 10). The player can't close the inventory or wardrobe while they aren't covered enough (with the `home` minimums at home, and the `outside` ones elsewhere), and is warned and stopped when they aren't covered enough to leave home or follow someone out of it (`outside`), or start spending time on a need, in the clothes it changes them into, such as work clothes when starting work or night clothes when going to sleep at home (`home` or `outside`, by where they are). The `formal` minimums aren't used yet.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `when` | [Condition](#condition) | Optional. Can't use `hidden`. Who follows the rule. Without it, everyone does. |
| `home` | map of string to integer | Optional (none). Can be empty. Each key is a clothing slot id, and each value from 0 to 10. The minimums at home. |
| `outside` | map of string to integer | Optional (none). Can be empty. As for `home`. The minimums out of the house. |
| `formal` | map of string to integer | Optional (none). Can be empty. As for `home`. The minimums at formal occasions, such as church. |

### How Clothing Is Given

Everyone in a new game gets two `everyday` outfits, one `work` outfit, one `night` outfit, and one `nothing` outfit, each chosen by frequency from the outfits of that kind whose `when` they meet, with the shares worked out among those outfits alone. A kind with no outfit they can have is skipped, except `nothing`: someone with no `nothing` outfit they can have gets one that's nothing at all (as does everyone in a game saved before there were any). Each piece's colors are chosen by frequency from all the clothing colors. Everyone starts in their night outfit (or, without one, their first everyday outfit), and everything else is kept at home. Someone who starts at work wears their first everyday outfit with their work outfit over it, carrying the everyday pieces it replaces (those in the same slot and layer), or for a `complete` work outfit, wears just that, carrying their first everyday outfit.

## Families and Relationships

#### Families

The `families` settings section.

| Property | Type | Notes |
|---|---|---|
| `head` | [Head](#head) | The head of each household. |
| `wife` | [Wife](#wife) | The wife of a man at the head of a household. |
| `children` | [Children](#children) | The children living with a household. |
| `elder` | [Elder](#elder) | A parent of the head or the wife living with the household. |
| `parentAge` | [Parent Age](#parent-age) | The ages at which people can have children. |
| `helperChance` | number | Chance. That a member helps in a household business, when allowed and there's room. |
| `wealth` | [Family Wealth](#family-wealth) | How wealthy each household is. |
| `inheritedProperties` | list of [Inherited Property](#inherited-property) | Optional (none). Can be empty. Properties children get from their parents. |

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
| `thresholds` | [Wealth Thresholds](#wealth-thresholds) | The household wealth needed for each wealth level. |

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
| `familyBonus` | integer | At least 0. Added to the starting score of two members of a household not covered by another bonus. |
| `spouseBonus` | integer | At least 0. Added for a married couple in a household. |
| `elderBonus` | integer | At least 0. Added for an elder and their child, or a child of the head. |
| `elderInLawBonus` | integer | At least 0. Added for an elder and their child's spouse. |
| `neighborBonus` | integer | At least 0. For characters in different households whose houses share a quarter. |
| `friendScore` | integer | From 0 to 100. Characters with at least this score are friends. |
| `levels` | list of [Relationship Level](#relationship-level) | In order of `minimum`: the first's is 0, and each after is higher, up to 100. A score is shown as the title of the last level it reaches. |
| `playerRelations` | list of [Player Relation](#player-relation) | Optional (none). Can be empty. How members of the player's household are related to the player; the first whose `when` is met is shown. |

Within a household, the bonus comes from the head relation values: the head and wife, or two elders who are both parents of the head or of the wife, get `spouseBonus`; an elder and their own child or any child of the head get `elderBonus`; an elder and their child's spouse get `elderInLawBonus`; anyone else gets `familyBonus`. Scores are capped at 100.

#### Relationship Level

| Property | Type | Notes |
|---|---|---|
| `minimum` | integer | The lowest score with this title. |
| `title` | string | Shown for a score in the level. |

#### Player Relation

| Property | Type | Notes |
|---|---|---|
| `when` | [Condition](#condition) | Can't use `hidden`. Who the relation is for. |
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
| `landmarkCount` | [Integer Range](#integer-range) | Minimum at least 0. Landmark quarters, counting landmarks the village must have for their kinds' `count`. |
| `featureCount` | [Integer Range](#integer-range) | Required if there are `features`. Minimum at least 0. |
| `buildingTravelTime` | integer | At least 0. Minutes to go into or out of a house or business. |
| `houseTitle` | string | [Template](#template) filled in with the properties (not templates) of the household's head. Every house's title. |

#### Quarter Kind

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `area` | string | `village` or `outskirts`. |
| `count` | [Quarter Count](#quarter-count) | Optional (no limits). How many quarters of the kind a village has. |
| `optional` | [Quarter Kind Optional](#quarter-kind-optional) | Optional. Only for kinds with a `count` `min`. Can't be a layout's center. |
| `path` | string | What a path to or from the kind is called. |
| `borders` | string | Optional. Not for kinds with a `count` `min`. A feature id. Quarters of the kind border it, and are only used in villages that have it. |
| `travelTime` | integer | At least 0. Minutes to walk a path to or from the kind. A path takes the longer of its ends' times. |

#### Quarter Count

| Property | Type | Notes |
|---|---|---|
| `min` | integer | Optional (0). At least 0. Every village has at least this many quarters of the kind. The kind needs a quarter that doesn't have `borders`. |
| `max` | integer | Optional (no limit). At least 1 and at least `min`. No village has more quarters of the kind. |

#### Quarter Kind Optional

A village with the feature only has the kind's `count` `min` quarters `chance` percent of the time, unless none of its houses' and businesses' quarters border the feature.

| Property | Type | Notes |
|---|---|---|
| `with` | string | A feature id. |
| `chance` | number | Optional (50). Chance. |

#### Layout

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `shape` | string | `radial`, `linear`, `crossroads`, or `warren`. |
| `center` | string | A village kind with a `count` `min` and `max` of 1 and without `optional`, the shape is built around. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often it's chosen. |

#### Map Feature

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Shown on the map. |
| `type` | string | `river`: a channel one cell wide across the map. `area`: a rectangle next to the quarters that border it. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often it's chosen. |
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
| `title` | string | Its name, shown in the game and on the map. |
| `description` | [Location Description](#location-description) | What the player reads about it. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often it's chosen. |
| `kind` | string | A quarter kind. |
| `houses` | [Quarter Houses](#quarter-houses) | Optional (no houses). How many houses it has, and their wealth. |
| `businesses` | integer | Optional (0). At least 0. The most businesses. |
| `size` | [Quarter Size](#quarter-size) | Optional (1 by 1). Can be turned either way. |
| `borders` | string | Optional. A feature it borders beyond its kind's. Only used in villages with it. |
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
| `length` | integer | Optional (1). At least 1. How many map cells long it is. |
| `width` | integer | Optional (1). At least 1. How many map cells wide it is. |

#### House

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `description` | [Location Description](#location-description) | What the player reads about it. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often it's chosen. |
| `wealth` | string | `low`, `medium`, or `high`. |

A house has no `title`; it's titled by the village's `houseTitle`.

#### Business Place

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Its name, shown in the game. |
| `description` | [Location Description](#location-description) | What the player reads about it. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often it's chosen. |
| `profession` | string | A business profession that doesn't work from home. |
| `quarterKinds` | list of string | Each unique and a quarter kind. At least one must have a quarter with room for businesses. |

There must be a house, and a quarter with houses, for each of `low`, `medium`, and `high`. Every kind with a `count` `min` needs a quarter without `borders`, and there must be a landmark quarter if `landmarkCount` can be more than 0.

### How the Village Is Laid Out

* **Features.** First the village chooses `featureCount` features by frequency, with at most one river, adding any the player's profession needs for a place. A profession has a place if it works from home, or one of its places can go in a kind whose `borders` feature (if any) the village has. Quarters and places needing a missing feature are never used.
* **Quarters.** The village gets each kind's `count` `min` quarters (an optional kind's are decided once houses and businesses are placed), and never more than its `max`; quarters of an optional kind are only added that way. Each household gets a house of its wealth in a quarter of that wealth, adding quarters until there's room: each plans a number of houses within its range, gets one, then its minimum, and the rest are spread. Each business that doesn't work from home gets a place of its profession in a quarter of one of its kinds with room, adding a quarter (preferring one with a minimum of 0 houses) if needed and its kind allows, or using a full one with a warning. Landmarks are added up to `landmarkCount`, alternating between the village and the outskirts. Unused templates are chosen first, by frequency; reusing one logs a warning.
* **Shape.** A layout is chosen by frequency and arranges quarters as non-overlapping rectangles on a grid from its `center`. `radial` rings the center with four to six quarters; `linear` runs a main street of four to six through it, half the time with a second street alongside; `crossroads` sends arms of one or two quarters out in four directions; `warren` crowds quarters together with many small loops. The core is taken roughly richest first, the rest of the village branches off it near quarters of the same wealth, and landmarks come first. The outskirts leave in two or three groups (from the ends of the street or arms, or different sides) as chains, loops of three, hubs, or ladders.
* **Connections.** Quarters only connect where their edges meet, at most two per side. Each connection gets a direction (its side, or one step toward the nearer end), with no quarter having two exits in one direction, dropping connections that can't unless needed. A connection is named by the outskirts kind's `path` if one end is in the outskirts, or else either kind's (preferring one without a `count` `max` of 1), or a river's `crossings` across a river.
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
| `title` | string | Shown in dates. |

#### Day Part

Each lasts until the next starts, and the last until the first starts the next day.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Shown with the time of day. |
| `start` | integer | From 0 to 23, after the one before. |
| `announcement` | [Long Text](#long-text) | Optional. What the player notices when it starts while they wait. Without it, they're told which part has begun. |

#### Special Day

A day of the year with its own title, the same day every year.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Shown with the date. |
| `season` | string | A season id. |
| `week` | string | A week id. |
| `day` | string | A day id. |

## Schedules

Everyone works through their goal's [steps](#step) in order, each worked out into tasks when it's reached. Someone other than the player who has no tasks or steps left chooses one of their needs with an urgency of at least `minimumUrgency`, with chances in proportion to urgency raised to `urgencyPower`, among those whose steps can be started from where they are: every `goTo` step that isn't in a `choose` option, other than one going to a remembered place or the partner's home, can reach a place like its own, and for a need that looks for friends, there's somewhere to look. If none can be, or the need chosen has no time left to spend on it, they're idle. Reconsidering a `repeat` loop only gives way to a need whose steps can be started the same way. Urgency runs from 0 to 100, then is multiplied by every urgency factor that applies. Someone at home puts away the household's clothes they're carrying (as on getting home) before choosing.

#### Schedules Settings

The `schedules` settings section.

| Property | Type | Notes |
|---|---|---|
| `minimumUrgency` | number | At least 0. Needs less urgent than this aren't chosen as goals. |
| `urgencyPower` | number | More than 0. Urgency is raised to this power to weigh the choice of goal. |
| `sightingHours` | integer | At least 1. How long people remember the last time they saw someone. |
| `sleep` | [Sleep](#sleep) | How sleep works. |
| `clothing` | [Clothing Schedule](#clothing-schedule) | How people change their clothes. |
| `talking` | [Talking](#talking) | How talking works. |
| `socializing` | [Socializing](#socializing) | How people chat with and seek out friends. |
| `idle` | [Idle](#idle) | What people do without a goal. |
| `start` | [Start](#start) | How a new game starts. |

#### Sleep

About the need with the `sleep` role. Choosing it below `napBelow` is a nap of `napMinutes` (once between full sleeps), unless it's bedtime. For someone with a work need whose sleep level is above its `urgentFrom`, sleep is also as urgent as a quota need with `bedtimeLead` would be, counting the minutes until work next starts less the sleep they need.

| Property | Type | Notes |
|---|---|---|
| `napBelow` | number | From 0 to 100. Sleep chosen while the sleep need is below this is a nap. |
| `napMinutes` | [Duration Range](#duration-range) | Minimum at least 1. How long a nap lasts. |
| `bedtimeLead` | integer | At least 1. Bedtime becomes urgent once there are fewer spare minutes than this before work. |
| `dayClothesFactor` | number | More than 0, and at most 1. How fast sleep meets the need, as a share of its `satisfyPerHour`, while wearing any of the sleeper's everyday or work clothes. |

#### Clothing Schedule

How people handle their [clothing](#clothing-item). A piece is at hand when it's worn or carried, or kept at home while its wearer is there, and it isn't still drying. Needs change clothes and handle them through their [steps](#step). Night clothes put on at other times stay on.

Someone choosing a goal (or being idle) first changes out of their `nothing` outfit if they changed into it and not back out, then out of their `work` outfit if they're wearing any of it once they're outside its hours or have no work left that day, unless the goal is work or sleep, and out of their `night` outfit if they're wearing any of it, unless the goal is sleep, each taking `changeMinutes`. Changing out of an outfit puts back on the pieces it replaced, if they're at hand, then the rest of the other day clothes they're partly wearing (the outfit with the most pieces on), or failing that, the `everyday` outfit with the most pieces at hand, from the pieces at hand. Changing into an outfit takes off everything else first, for a `complete` one, or otherwise what's in the same slots in the same layers. What's taken off is carried, even at home, and the household's clothes they carry are put away on getting home, except the pieces of outfits they're partly wearing (other than through accessories), the pieces a later `undress` step of one of their sequences will put back on, and the clothes in their bundles. Someone whose sleep stops without its steps (such as the player woken to talk) keeps their night clothes on until they choose something other than sleep (or, for the player, change out of them). The player brings their own work clothes.

| Property | Type | Notes |
|---|---|---|
| `changeMinutes` | [Duration](#duration) | At least 1. How long changing clothes takes. Also how long the player takes to change clothes in the inventory or wardrobe, taken once as they close it, if they're wearing something different from when they opened it. |

#### Talking

Talking pauses what someone was doing until it's over. If their goal is a quota need that's then closed or done, they give up on it and choose again.

| Property | Type | Notes |
|---|---|---|
| `refuseUrgency` | number | At least 0. Someone whose goal is at least this urgent refuses to talk. |
| `agreeUrgency` | number | At least 0. Someone asked by an `ask` step with `forMinutes` or an `appointment` only says yes if none of their needs would be at least this urgent by the end of that time. |
| `wakeLevel` | number | From 0 to 100. How much the sleep need rises for someone woken to talk. |
| `askAboutMinutes` | [Duration](#duration) | At least 1. Asking whether someone has seen a person. |
| `askDoingMinutes` | [Duration](#duration) | At least 1. Asking what someone is doing. |

#### Socializing

About the need with the `socializing` role, met (at its `satisfyPerHour`) only while chatting with a friend (a [Relationships](#relationships) score of at least `friendScore`). It's chosen with its urgency times `seekFactor`. A need with a `seek` step looking for friends is only chosen by someone with a friend, when there's somewhere to look for one.

When someone starts a task, they and each friend there may chat: each is willing by their socializing urgency times their score out of 100, less their goal's urgency times `chatGoalWeight` (unless their goal is socializing), and the more willing starts if at least `chatUrgency`. Neither can be talking, the starter can't be asleep, and the other can't be asleep (unless the starter's socializing urgency is at least `wakeUrgency`). The other agrees unless their goal is at least `refuseUrgency`, and if they don't, the next most willing pair is tried. The player decides for themselves: someone asking the player stops time until the player answers, and waits `askPlayerAgainHours` after being turned down. Unless told what the friend is doing since their goal was chosen, they ask what it is first.

Seeking out a friend with a `seek` step, the person picks a friend and place from clues, each weighing its weight in `clues`: the friend's workplace during their work hours, or home otherwise; their last sighting (or where they said they were going), fading to nothing after `sightingHours`; and places for needs met in a quarter on some days, within hours and not done. Each weight is divided by 1 plus the walk in minutes over `nearMinutes`, and multiplied by the hours since they last talked over that plus `recentHours`. The current place is never chosen. They look around for the step's `minutes`, calling out to a friend leaving the place they're heading to, and otherwise ask someone there what they've seen of their friends.

| Property | Type | Notes |
|---|---|---|
| `chatMinutes` | [Duration](#duration) | At least 1. How long a chat takes. |
| `chatUrgency` | number | At least 0. How willing someone must be to start a chat. |
| `chatGoalWeight` | number | At least 0. How much a goal's urgency counts against stopping to chat. |
| `wakeUrgency` | number | At least 0. How urgent socializing must be to wake a sleeping friend. |
| `seekFactor` | number | More than 0. Multiplies socializing's urgency when choosing a goal. |
| `recentHours` | number | More than 0. How quickly a friend not talked with lately becomes the likelier one to seek. |
| `nearMinutes` | number | More than 0. How quickly places farther away become less likely to be searched. |
| `askPlayerAgainHours` | number | More than 0. How long someone the player turned down waits before asking again. |
| `clues` | [Seek Clues](#seek-clues) | How much each kind of clue weighs. |

#### Seek Clues

| Property | Type | Notes |
|---|---|---|
| `workplace` | number | At least 0. The friend's workplace during their work hours. |
| `home` | number | At least 0. The friend's home outside their work hours. |
| `sighting` | number | At least 0. Where the friend was last seen, or said they were going. |
| `gathering` | number | At least 0. Places where people gather for a need met only on some days. |

#### Idle

Someone who doesn't choose a need waits at a place, then decides again.

| Property | Type | Notes |
|---|---|---|
| `place` | [Place](#place) | Where they wait. |
| `minutes` | [Duration Range](#duration-range) | Minimum at least 1. How long they wait. |
| `activity` | string | Optional. What they're shown to be doing. |

#### Start

A new game starts at midnight on the first day of a random week of the calendar's `startYear`. Everyone is at home asleep, with each level need at its `startLevel`, except anyone other than the player whose work hours include midnight on a working day, who starts awake at work. Each sleeper wakes `wakeMinutes` from `wakeHour` (at least a minute after midnight), and time runs until the player has woken and finished their sleep's steps.

| Property | Type | Notes |
|---|---|---|
| `wakeHour` | integer | From 0 to 23. The hour people wake around. |
| `wakeMinutes` | [Integer Range](#integer-range) | How many minutes from `wakeHour` each sleeper wakes. |

#### Place

`"home"`, `"workplace"` (the business's place, or home if it's run from home), `"otherHome"` (the home of another household, weighted by 1 over 1 plus the walk in minutes, or the nearest for an estimate; any house but their own counts as one for the player and for `at`), in steps only `"partnerHome"` (the home of the person's partner, the one these steps last talked with or asked) and `"jobSite"` (the place of the person's job for the need, taken on by an `ask` step with a `job`, while it isn't done; nowhere without one), or an object: a quarter, with `quarterKinds`, or in steps only, a place a `remember` step before it remembered, with `remembered`.

| Property | Type | Notes |
|---|---|---|
| `quarterKinds` | list of [Quarter Choice](#quarter-choice) | Optional. Not with `remembered`. The kind is chosen by frequency among the village's kinds, then a quarter of it, weighted by 1 over 1 plus the walk in minutes. |
| `nearest` | boolean | Optional (false). Not with `remembered`. The nearest quarter of any of the kinds instead. |
| `remembered` | string | Optional. Not with `quarterKinds`. The name of a place a `remember` step remembered. |

#### Quarter Choice

| Property | Type | Notes |
|---|---|---|
| `quarterKind` | string | A quarter kind id. |
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often the kind is chosen. |

#### Need

Has exactly one of `level` and `quota`, and exactly one of `place` and `steps`. A need with a `place` has two steps: going there, then a `do` step without minutes of its own (which meets the quota, for a quota measured in time). A need is work if any of its own steps goes to `"workplace"`. Only people with a business have it, and their profession's business `hours`, if it has them, replace its own. For people in a business whose profession has `steps`, those replace the need's own steps. The need's main place is where the first `do` step of its own steps is done (the place of the last `goTo` step before it).

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `title` | string | Shown to the player with its level's title, for a need with `levels`. |
| `role` | string | Optional. `sleep` (required on one need when there are needs) or `socializing`, each on at most one level need. |
| `activity` | string | Optional (the title). What someone meeting it is shown doing, unless their step has its own. For a need that's work, the activity of the profession of the person's business comes before it. Someone seen meeting it is described with the activity of the step they were on, and someone asked what they're doing for it tells the activity of the step they're on, or else of their next `do` step. |
| `when` | [Condition](#condition) | Optional. Can't use `hidden`. Who has the need. Without it, everyone does. |
| `place` | [Place](#place) | Optional. Where it's met, for a need without `steps`. |
| `steps` | list of [Step](#step) | Optional. At least one item, with at least one `do`, `wander`, or `seek` step. What people do to meet it, in order, for a need without a `place`. |
| `onFail` | [Step Failure](#step-failure) | Optional (`stop`). What happens when one of its steps fails, for causes nothing closer to the step gives a result for. |
| `hours` | [Hours](#hours) | Required for quota needs, with `days` and `onSpecialDays` allowed. Optional for level needs, without them. |
| `level` | [Level Need](#level-need) | Optional. Makes it a level need. |
| `quota` | [Quota Need](#quota-need) | Optional. Makes it a quota need. |

#### Step

One step of what someone does to meet a need. Each kind of step allows only some of the settings. The steps are carried out in order, each checked when it's reached: a step whose `when` isn't met is passed, a step that's already done (such as being where a `goTo` step goes) is passed, and otherwise it's carried out. Steps that take time can be interrupted by talking, after which they carry on.

A step fails when it can't be done, and then what happens is its `onFail` for the cause, or that of the `repeat` or `choose` step, option, step list, or need it's in, the closest first (see [Step Failure](#step-failure)). The causes are `no` (an `ask` step answered no), `nobody` (no one to talk with, ask, lead, or find), `unreachable` (nowhere to go, or no way there), `missing` (what the step needs isn't at hand), `left` (the person left where the steps after the last step that goes somewhere were being done, other than through a step that goes somewhere), `interrupted` (an appointment for another need took over), `hours` (it was reached outside its `hours`, or they ended while it was being carried out, or the need's hours ended while time spent on it was interrupted), `noShow` (no one came to an appointment), and `noOption` (none of a `choose` step's options can be chosen).

* `goTo`: done once the person is at a place like `place` (required). Otherwise they walk there the quickest way, and it fails as `unreachable` if there's nowhere to go or they don't get there. For the need with the `socializing` role, it goes where the person chose to look for a friend instead.
* `do`: spends time where the person is, meeting a level need, or for a quota need with `meetsQuota`, counting toward it. Without `minutes`, it takes the rest of the time chosen for the goal: for a level need, until it's met (or for a nap, the nap's length), and for a quota need, what's left of the shift chosen when the goal was chosen. With `minutes` and `meetsQuota`, in a need measured in time, it takes at most what's left of the shift. Either way, it uses up that much of the shift, and is passed once none is left. With `atWork`, they're at work while doing it. With `hours`, it stops at their end, and fails as `hours`. With `partnerMeets`, the person's partner has that need met while they follow the person through it (such as an appointment's client), if they have it.
* `take`: done once the person carries its `bundle`. Otherwise they pick up things of `kind` (required) from `from` (required), if they're at home (or it fails as `missing`), and carry them in the bundle: for `clothing` from the `wardrobe`, the clothes kept at home that can be worn now, their own, or their household's with `owner` `household`; only the pieces of their own outfits of the kind `outfit`, if given; and only those that aren't accessories, unless `accessories`.
* `put`: done once the person no longer carries its `bundle` (or never took it). Otherwise they put it down: with `kind`, its things go to `to` (required with `kind`), if they're at home (or it fails as `missing`), where with `afterDelay` they can't be used until that long after, and are shown doing `delayActivity` until then; without `kind` (for a bundle a `make` step made), it's done with. Only a `bundle` taken or made by a step before it can be put down, with the same `kind` it was taken with, or without one, for one that was made.
* `make`: makes `bundle` for the person to carry, in place of the bundle `replaces`, if given, which must be one taken or made before it.
* `dress`: done once the person has on all of one of their outfits of the kind `outfit` (required) (for one that's nothing at all, once they're wearing nothing). Otherwise they change into their first outfit of the kind with every piece at hand, taking `changeMinutes`, or it fails as `missing`. The person remembers the pieces it replaced (everything else they had on, for a complete outfit, or what its pieces overlap), however long they wear it.
* `undress`: done once the person has none of their outfits of the kind `outfit` (required) changed into (for one that's nothing at all, that they changed into from other clothes). Otherwise they change out of it, taking `changeMinutes`, putting back on the pieces it replaced that are at hand, and then filling any gaps from the rest of the day clothes they're partly wearing, or after a complete outfit, from the everyday outfit with the most pieces at hand. Changing out of night or work clothes when choosing a goal works the same way.
* `countRun`: counts a run of a quota measured in runs.
* `choose`: carries on with the steps of one of its `options` (required, with at least one item), or past the `choose` step for one without steps. With `first`, it's the first whose `when` is met, or if none is, the step is passed. Otherwise, it's one chosen by frequency among those whose `when` is met and whose places (other than remembered ones) can be reached, with each frequency's chance shared as it would be among all the options, and it fails as `noOption` if there's none. For a quota need, someone the game carries the steps out for leaves out options whose steps from there to the end could take longer, with the most minutes of their `do` steps and the walking, than is left of the need's hours today, unless none would fit.
* `call`: carries out the steps of the step list whose id is `list` (required), as though they were in its place. A step list can't call itself, directly or through other step lists.
* `stop`: skips the rest of the steps.
* `remember`: remembers where the person is by `name` (required), for a later step to go to (or check they're at) with a remembered place.
* `wait`: done once `minutes` have passed since it was reached, or the next time it's `untilHour` (at once, if it's that hour exactly), meanwhile without meeting the need. It has exactly one of them.
* `wander`: done once `minutes` (required) have passed since it was reached. Until then, they walk to another quarter of one of `place`'s kinds (required, and a quarter), with the kind chosen by frequency and nearer quarters more likely, and stay `stopMinutes` (required, minimum at least 1) there, again and again, staying less to finish on time. The time staying meets the need, and counts toward its quota with `meetsQuota`, which also takes the minutes from the shift. With `atWork`, they're at work while staying.
* `seek`: looks for someone for `minutes` (required), walks there, and looks around, which doesn't meet the need. With `who`'s `friends`, it looks for a friend who meets `who`: it picks a friend and a place they might be from clues (see [Socializing](#socializing)). Otherwise it looks for anyone (other than the person's household, unless `who` says whether they're family) who meets `who` (anyone at all, without it), at a place like `place`, or without one, in any quarter: for a quarter, a kind chosen by frequency, then a quarter of it weighted by 1 plus how many such people the person has seen there within `sightingHours`, over 1 plus the walk in minutes over `nearMinutes` (see [Socializing](#socializing)), and for `"otherHome"`, another household's home weighted by 0.25 plus how many such people live there and work from home, plus how many the person has seen there, over the same. Without a place, or for `"otherHome"`, the place they're in isn't chosen. With `meetsQuota`, looking around takes from the time chosen for the goal and counts toward the quota, as with `do`, so looking for clients can be work. It fails as `nobody` if there's nowhere to look.
* `talk`: talks for `minutes` (required) with someone where the person is, as a chat, which meets the socializing need of each of them who counts the other as a friend. With `meetsQuota`, the time talking counts toward the quota and takes from the shift instead, as with `do`, and doesn't meet the person's socializing need. With `partnerMeets`, the one talked with has that need met while they talk, if they have it, instead of socializing. It's someone who isn't asleep, talking, or too busy to talk, meets `who`, isn't in the person's household (unless `who` says whether they're family, or it's the partner or the friend looked for), and is the one `with` names, or the player (unless the world is headless) if they aren't doing anything or being asked something else, and haven't turned the one asking down within `askPlayerAgainHours`, see [Socializing](#socializing). They become the person's partner. It fails as `nobody` if there's no one. The player is asked whether they'll talk, and stops time until they answer. If they say no, it fails as `no`.
* `ask`: asks such a person (or the player, if they aren't doing anything or being asked something else, and haven't turned the one asking down within `askPlayerAgainHours`, see [Socializing](#socializing)) a question, which takes `minutes` (required), and remembers the answer by `name` (required) for the `answeredYes` and `answeredNo` conditions of later steps. The answer is yes by `chance` (required). With `forMinutes`, it's also no unless the one asked can spare that many minutes: none of their needs would be at least `agreeUrgency` (see [Talking](#talking)) by the end of them, with their level needs rising meanwhile and that much less time left for their quotas. The one asked becomes the person's partner. With `appointment`, a yes also makes an appointment (see [Step Appointment](#step-appointment)), and the answer is no if there's no time for one. With `job`, a yes also takes on a job (see [Step Job](#step-job)), and the answer is no if there's no place for it. Asking meets the goal's quota and the partner's need with `meetsQuota` and `partnerMeets`, as for `talk`. The player answers yes or no themselves, with time stopped until they do (no `forMinutes` check applies to them), and saying no counts as turning the one asking down. A no fails it as `no`, and having no one to ask, as `nobody`. It requires a `player`, with which the player asks it in conversation (see [Step Player](#step-player)).
* `lead`: has the person's partner, if they're there, awake, and not talking or already following someone, follow the person for at most `minutes` (required), going wherever they go, or else it fails as `nobody`. The player follows them as part of their steps instead (putting their own on hold), until they're let go, `minutes` are up, they stop, or they call it off in conversation. It interrupts whatever the partner was doing, and someone following is too busy to talk.
* `release`: lets go of anyone following the person, who then choose what to do next. Anyone still following is also let go when the person is done with the steps (or gives up on them).
* `meet`: only directly among an appointment's `steps` (see [Step Appointment](#step-appointment)). Done once the appointment starts. Otherwise the person walks to the appointment's place, and waits there until its time, and then for up to `minutes` (required) past it for the other. It fails as `noShow` once that's over, or as `unreachable` if there's no way there. With `meetsQuota`, the time the worker spends waiting there counts toward the quota. It can't have an `onFail`: failing for any cause gives up the steps, which calls off the appointment.
* `repeat`: carries out its `steps` (required, with at least one item, and including a `do` step with `meetsQuota`), then carries them out again for as long as some of the shift is left and the need is open and not done. With `reconsider`, after a pass, if another of their needs would be considered as a goal and weighs more than the goal does, someone other than the player stops. The player is asked whether to keep going after every pass of one that reconsiders, with time stopped until they answer. The steps after it are carried out once it stops. Only for needs measured in time.

Steps other than `goTo`, `do`, `dress`, `undress`, `wait`, `wander`, `seek`, `talk`, `ask`, `meet`, and `repeat` take no time.

| Property | Type | Notes |
|---|---|---|
| `step` | string | `goTo`, `do`, `take`, `put`, `make`, `dress`, `undress`, `countRun`, `repeat`, `choose`, `call`, `stop`, `remember`, `wait`, `wander`, `seek`, `talk`, `ask`, `lead`, `release`, or `meet`. |
| `id` | string | Optional. Unique among the steps of its need, step list, appointment, or profession (including those of its `repeat` steps and options). Names it for an `onFail` to skip to. |
| `when` | [Step Condition](#step-condition) | Optional. When it's carried out. |
| `hours` | [Hours](#hours) | Optional (any hour). Without `days` or `onSpecialDays`. The hours it can be carried out in. |
| `onFail` | [Step Failure](#step-failure) | Optional. What happens when it fails, and for a `repeat` or `choose` step, when any step in it fails, for causes nothing closer gives a result for. |
| `place` | [Place](#place) | `goTo`, `wander`, and `seek` only. Required for `goTo` and `wander`. Not allowed for `seek` looking for friends. Where to go, or for `seek`, where to look (anywhere, without it). |
| `minutes` | [Family Duration](#family-duration) | `do`, `wait`, `wander`, `seek`, `talk`, `ask`, `lead`, and `meet` only. Optional, but required for a `do` step of a need measured in runs, for a `wait` step without `untilHour`, and for `wander`, `seek`, `talk`, `ask`, `lead`, and `meet` steps. Whole numbers. How long it takes, for `lead`, the longest the partner follows, or for `meet`, how long past the appointment's time to wait for the other. |
| `early` | [Duration](#duration) | `meet` only. Optional (0). How long before the appointment's time to arrive. |
| `untilHour` | integer | `wait` only. Optional. From 0 to 23. The hour it waits until. |
| `stopMinutes` | [Duration Range](#duration-range) | `wander` only. How long they stay at each quarter. |
| `activity` | string | `do`, `wait`, and `wander` only. Optional (the need's activity). What someone doing it is shown doing. |
| `meetsQuota` | boolean | `do`, `wander`, `seek`, `talk`, `ask`, and `meet` only. Optional (false). Only for a quota measured in time. The time spent on it counts toward the quota (for `meet`, the worker's time waiting). |
| `partnerMeets` | string | `do`, `talk`, and `ask` only. Optional. A need id. The need the step meets for the partner while it lasts, if they have it. |
| `atWork` | boolean | `do`, `wander`, and `seek` only. Optional (false). The person is at work while doing it. |
| `kind` | string | `take` and `put` only. Required for `take`. `clothing`. The kind of things it takes or puts down. |
| `from` | string | `take` only. `wardrobe` (where the household keeps its clothes at home). Where it takes things from. |
| `to` | string | `put` only. Required with `kind`, and not allowed without it. `wardrobe`. Where it puts things. |
| `owner` | string | `take` only. Optional (`self`). `self` or `household`. Whose clothes it takes. |
| `outfit` | string | `take`, `dress`, and `undress` only. Optional for `take`. `everyday`, `work`, `night`, or `nothing`. |
| `withPartner` | boolean | `dress` and `undress` only. Optional (false). The partner (the one these steps last talked with or asked) changes the same way at the same time, if they're there, awake, and not talking, such as a friend going swimming with them. It interrupts what they were doing, such as following. The player's clothes are changed at once. |
| `accessories` | boolean | `take` only. Optional (true). Whether it takes accessories too. |
| `bundle` | [Step Bundle](#step-bundle) | `take`, `put`, and `make` only. The bundle it takes things into or makes (with its `title`), or for `put`, the id of the bundle it puts down (without one). |
| `replaces` | string | `make` only. Optional. The id of a bundle the one it makes replaces. |
| `afterDelay` | [Duration](#duration) | `put` only, with `kind`. Optional. How long until its things can be used. |
| `delayActivity` | string | `put` only. Required with `afterDelay`, and not allowed without it. Not blank. What its things are shown doing until they can be used, such as "Drying". |
| `steps` | list of [Step](#step) | `repeat` only. The steps it repeats. |
| `reconsider` | boolean | `repeat` only. Optional (false). Whether someone stops repeating (or the player is asked) when something else is more urgent. |
| `options` | list of [Step Option](#step-option) | `choose` only. The options it chooses from. |
| `first` | boolean | `choose` only. Optional (false). Whether it takes the first option whose `when` is met. |
| `list` | string | `call` only. A step list id. |
| `name` | string | `remember` and `ask` only. The name it remembers the place or the answer by. |
| `with` | string | `talk` and `ask` only. `sought` (the friend a `seek` step chose to look for, or after a `seek` step with `who`, anyone there), `partner` (the one these steps last talked with or asked), or `anyone`. Who to talk with. |
| `who` | [Step Who](#step-who) | `talk`, `ask`, and `seek` only. Optional. Without it, anyone can. Only people who meet it can be talked with or are looked for. |
| `chance` | [Step Chance](#step-chance) | `ask` only. The chance the answer is yes. |
| `forMinutes` | [Family Duration](#family-duration) | `ask` only. Optional. Whole numbers. How long saying yes takes the one asked, such as going with the person to their home, which they must be able to spare. |
| `appointment` | [Step Appointment](#step-appointment) | `ask` only. Optional. An appointment a yes makes, to meet later at a time that suits the one asked. |
| `job` | [Step Job](#step-job) | `ask` only. Optional. Not with `appointment`. A job a yes takes on. |
| `player` | [Step Player](#step-player) | `goTo`, `do`, `take`, `put`, `dress`, `undress`, `wait`, and `ask` only. Required for `ask`. How the player takes the step. |

A person can be in the middle of several needs' steps: their current ones, others on hold, and steps for appointments waiting for them to set off. Each keeps its own bundles, remembered places, partner, and answers, and forgets them once it's done or given up. A bundle is carried in the person's inventory, standing for the things taken into it, which are carried with it, and only its step can put it down. When steps are given up, the things in their bundles stay carried, and the bundles are done with. Someone other than the player only has others on hold while an appointment for the same need is under way (see [Step Appointment](#step-appointment)). When the current ones end, the last ones put on hold carry on.

Estimates of walking follow every way the person could go from where they are, with conditions as they are now: every option of a `choose` step they could choose (with `first`, only the first whose `when` is met), up to 64 ways, past which only the first option is followed, and the steps a `repeat` step repeats count once. Each estimate is the fewest and most minutes of the ways. How urgent a quota is counts the most walking, so people set out in time whichever way they go, while whether there's time today for a shift or run counts the least, so people don't turn down what they could do. Setting off for an appointment allows for the most. A `wander` step counts the walk to the nearest quarter of its kinds. Choosing a need as a goal only needs the places of steps outside `choose` options and appointments to be reachable (other than remembered places, partners' homes, and job sites). A run counts toward its quota as the most minutes of `do` steps any way through the need's steps could take: every option of a `choose` step (and none, for one with `first` whose options all have a `when`), and steps with a `when` both carried out and passed by. Whether a run fits in today's hours counts the fewest.

#### Step Failure

What happens when a step fails: `"next"` (carry on with the step after it), `"stop"` (give up the steps), an object with `skipTo` (carry on from the step with that `id`, among the steps of the same need, step list, appointment, or profession as where the `onFail` is), or an object with a result for each cause, whose property names are causes (see [Step](#step)) and whose values are one of the others. A cause it has no result for is left to what the step is in. Skipping to a step that isn't found gives up the steps.

| Property | Type | Notes |
|---|---|---|
| `skipTo` | string | Optional. The `id` of the step to carry on from. Not with causes. |

#### Step Bundle

The id of a bundle, or an object:

| Property | Type | Notes |
|---|---|---|
| `id` | string | Not blank. |
| `title` | string | Required for `take` and `make`, and not allowed for `put`. What the bundle is called, such as "Washing". |

#### Duration

A whole number of minutes, at least 0, or text with hours and minutes (`"1:30"`), or days, hours (up to 23), and minutes (`"2:15:35"`), with two digits of minutes up to 59.

#### Step Who

Who a `talk`, `ask`, or `seek` step talks with, asks, or looks for: a [Condition](#condition) on their character (which can't use `hidden`), and optionally only the person's friends. Its `values` and `notValues` can also list `family`, which isn't a character property, with `member` for those in the household of the person taking the step, or `other` for those who aren't, such as `"notValues": { "family": [ "member" ] }` to leave out the person's own household. A `who` with `friends`, or only `family`, needs no other property.

| Property | Type | Notes |
|---|---|---|
| `values` | map of string to list of string | Optional. As for a [Condition](#condition), and `family`, with `member`, `other`, or both. |
| `notValues` | map of string to list of string | Optional. As for a [Condition](#condition), and `family`, with `member`, `other`, or both. |
| `hidden` | list of string | Not allowed. |
| `friends` | boolean | Optional (false). Only the person's friends (a [Relationships](#relationships) score of at least `friendScore`), among those who meet the rest. A `seek` step looking for friends picks a friend and where they might be from clues (see [Socializing](#socializing)), so it can't have a `place`. |


#### Step Appointment

An appointment an `ask` step makes when the answer is yes, for the one asking (the worker) and the one asked (the client), either of whom can be the player. In the same conversation, the worker first asks to meet right away: as soon as they're done talking and both could set off and get to `place` (see below). If that doesn't fit, they ask for the earliest later time on the hour or half hour. Either way, it must be up to `withinHours` ahead, all within `hours`, that's at least `gap` (beyond how long it takes) from either one's other appointments, and when the client could spare the time it takes them: the walk to it and the minutes of the `do` and `wait` steps after its `meet` step (or the `ask` step's `forMinutes`, if it has them). Like `forMinutes`, they can spare it if none of their needs would be at least `agreeUrgency` (see [Talking](#talking)) by the end of it: level needs rise for that long, from where they are now if it's within the hour, or else from nothing (they'd see to them in the meantime), and quota needs have that much less of their hours left from then, with as much still to do as now, or all of it in a later day or week. If there's no such time, the answer is no.

Its `steps` have exactly one `meet` step, directly among them. When it's made, each of them has steps for it, waiting until it's time to set off: the worker its `steps`, and the client their part in them, from its `meet` step. Waiting steps don't change what they're doing. Each sets off when there's just time to change out of night or work clothes they won't need, then, for the worker, carry out the steps before its `meet` step, and walk there to arrive its `meet` step's `early` before its time, as estimated from where they are (following the steps before it as other estimates of walking do, and counting `changeMinutes` for each change of clothes they'd make). Those steps can only be `goTo`, `take`, `put`, `make`, `dress`, `undress`, `remember`, `choose`, and `stop` steps, such as putting on work clothes. Setting off, the worker puts the steps they were in the middle of on hold if they're for the same need, or otherwise fails their current step as `interrupted`. The client's current step fails as `interrupted`, they let go of anyone following them, and they take part in the worker's steps instead: going there, meeting the worker, and following them. Both go there and wait with its `meet` step. Once both are there at its time, the worker carries out the steps after it, with the client as their partner, while the client follows them (staying with them, or going wherever the steps go) until they're done, or for at most as long as those steps take and `patience` more. Calling it off, or a `meet` step that fails (such as when the other hasn't come by the end of its wait), gives up the worker's steps for it and the client's part in them, and anyone who had set off chooses what to do next. Either of them giving up their steps for it calls it off.

The player's appointments are shown in the sidebar (the next one) and on their status page, with the quarter a house or business is off, and the quarter of the next one is marked on the maps, as is the way to its place. When it's time for them to set off, time stops, and they're asked whether to set off or call it off, rather than changing clothes for them. Calling it off lets the other go on with their day. Setting off starts their part, as for anyone else: as the worker, the steps before its `meet` step first, then going there, and meeting the other once both are there, which they can each have done for them (meeting waits there for the other). As the worker, they then take its `steps`, while the client follows them, and is let go once they're done or give them up. As the client, they stay with the worker, going where they go, until they're let go. The other waits for the player as for anyone else, and the player is told if the other gave up waiting, or never came.

| Property | Type | Notes |
|---|---|---|
| `place` | [Place](#place) | Where they meet, worked out for the worker when it's made, with the client as their partner (so `partnerHome` is the client's home). |
| `withinHours` | integer | Optional (24). At least 1. How many hours ahead to look for a time. |
| `hours` | [Hours](#hours) | Optional (any hour). Without `days` or `onSpecialDays`. The hours the whole appointment must be within. |
| `gap` | [Duration](#duration) | How far apart, beyond how long it takes, someone's appointments must be. |
| `patience` | [Duration](#duration) | How much longer than its steps take the client stays with the worker once it starts. |
| `steps` | list of [Step](#step) | At least one item, with exactly one `meet` step directly among them (and none in their `repeat` steps, options, or called step lists), only the kinds of steps above before it, at least one `do` step, each `do` step with `minutes`, and no step making another appointment. What the worker does once they set off (before the `meet` step), going there and meeting (the `meet` step), and once both are there (after it). They start with nothing taken, remembered, or asked, and the steps are done once they are. Only the need's `onFail` applies to them, beyond their own. |

#### Step Job

A job an `ask` step takes on when the answer is yes: work for the one asked, for the need whose steps it's in. Someone has at most one job for each need, and taking on another replaces it. Until it's done, a `"jobSite"` place is where the job is, and the `hasJob` condition is met. The time they spend there meeting the need's quota (on steps with `meetsQuota`, or talking for one) counts toward the job, and once it reaches `minutes`, the job is done.

| Property | Type | Notes |
|---|---|---|
| `place` | [Place](#place) | Not `"jobSite"`. Where the work is done, worked out for the one asking when it's taken on, with the one asked as their partner (so `partnerHome` is their home). |
| `minutes` | [Duration](#duration) | At least 1. How long the work takes in all, over as many days as it takes. |

#### Step Chance

The chance, from 0 to 100, that someone asked by an `ask` step says yes: `base`, plus `perScore` times their relationship score with the one asking over 100, multiplied by the `factor` of each of `factors` whose condition they meet, less `busy` times the urgency of the goal they're busy with, if any.

| Property | Type | Notes |
|---|---|---|
| `base` | number | From 0 to 100. |
| `perScore` | number | Optional (0). Added for the highest relationship score, in proportion for less. |
| `factors` | list of [Chance Factor](#chance-factor) | Optional (none). |
| `busy` | number | Optional (0). At least 0. Taken off for each point of the urgency of the goal they're busy with. |

#### Chance Factor

| Property | Type | Notes |
|---|---|---|
| `when` | [Condition](#condition) | Can't use `hidden`. Who it applies to. |
| `factor` | number | At least 0. Multiplies the chance. |

#### Step Option

| Property | Type | Notes |
|---|---|---|
| `frequency` | [Frequency](#frequency) | Optional (`common`). How often it's chosen. |
| `when` | [Step Condition](#step-condition) | Optional. When it can be chosen. |
| `player` | [Step Option Player](#step-option-player) | Required for the options of a `choose` step without `first`. How the player sees it. |
| `onFail` | [Step Failure](#step-failure) | Optional. What happens when one of its steps fails, for causes nothing closer gives a result for. |
| `steps` | list of [Step](#step) | Optional (none). The steps carried out when it's chosen. |

#### Step Option Player

| Property | Type | Notes |
|---|---|---|
| `title` | string | Not blank. What the option is called, such as "Gather herbs". |

#### Step List

Steps that `call` steps can carry out, so needs can share them. Its steps are checked as part of each need that calls it. A step list that no need or profession calls, directly or through other step lists, is checked on its own, leaving out what depends on the need calling it or on the bundles, places, and answers of the steps before it, and a warning is logged, since it's never carried out.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `onFail` | [Step Failure](#step-failure) | Optional. What happens when one of its steps fails, for causes nothing closer gives a result for (and then what the `call` step is in). |
| `steps` | list of [Step](#step) | Optional (none). |

#### Step Condition

Has at least one of its settings, and every setting it has must be met.

| Property | Type | Notes |
|---|---|---|
| `outfitAtHome` | string | Optional. `everyday`, `work`, `night`, or `nothing`. Met when the person's first outfit of the kind isn't one they're dressed in, every piece of it is worn, carried, or kept at home ready to wear, and some of it is at home. |
| `doneForToday` | boolean | Optional. Quota needs only. Met when this is whether the quota for the day or week is done (counting time others in the family plan to spend on a shared one), or the need's hours are over for now. For the player, it's always done, since what they started ends when they say so. |
| `at` | [Place](#place) | Optional. Met when the person is at a place like it: their home or workplace, a quarter of one of its kinds, or the remembered place. |
| `hours` | [Hours](#hours) | Optional. Only a start and an end. Met within those hours. |
| `shiftLeft` | integer | Optional. More than 0. Met when at least this many minutes are left of the time chosen for the goal. |
| `businessProfession` | string | Optional. A profession id, of a profession with a business. Met when the person owns or helps in a business in that profession. |
| `answeredYes` | string | Optional. The `name` of an `ask` step before it. Met when the answer was yes. |
| `answeredNo` | string | Optional. The `name` of an `ask` step before it. Met when the answer was no, or nobody was asked. |
| `hasJob` | boolean | Optional. Met when this is whether the person has a job for the need (see [Step Job](#step-job)) that isn't done. |
| `not` | [Step Condition](#step-condition) | Optional. Met when this condition isn't. |
| `any` | list of [Step Condition](#step-condition) | Optional. At least one item. Met when at least one of these conditions is. |

#### Step Player

How the player takes a step. The player starts a need's steps, as anyone else choosing it as a goal would, with the time anyone else would choose for it, when it's one they have, except a quota need once its quota is met or on a day it can't be met (unless overdue), and a need with somewhere its steps go that can't be reached, or with a `seek` step looking for friends when they have none. Starting a need they're in the middle of carries on with it. Starting one puts any others they're in the middle of on hold, and they can switch between them. Each step is checked as for anyone else, and the player's steps are checked again after anything they do, all of them, so a step done for one counts, such as putting down a bundle at home.

The player takes `goTo`, `take` (gathering all of its `outfit`, or with a `title`), `dress`, `undress`, and `seek` steps themselves: walking where it goes, taking along, putting on, or taking off clothes in their inventory or wardrobe, or being where someone it looks for is. They talk with or ask someone for `talk` and `ask` steps in conversation. A `wait` or `wander` step is done once its time is up, whatever they do meanwhile. A step with a `title` (other than `ask`) has a button with that title that carries it out, as anyone else would, and they're told its `result` and how long it took, and the `text` of the next `goTo` step with one. They choose the option of a `choose` step without `first` themselves, from those that can be chosen, and are asked whether to keep going after each pass of a `repeat` step with `reconsider`. Every other step is carried out as they go. The step they're on, or all of their steps, can instead be done for them, as anyone else would carry them out, other than asking whether to keep going. Anything that stops time for them, such as someone asking to chat, stops what's being done for them.

| Property | Type | Notes |
|---|---|---|
| `title` | string | Required for any step but `goTo`, and not allowed for `goTo`. What its button is called, or for an `ask` step, the button in conversation that asks it, such as "Offer to help with a birth". |
| `result` | string | Required for any step but `goTo` and `ask`, and not allowed for them. What the player is told after doing it. |
| `text` | [Long Text](#long-text) | Required for a `goTo` step, optional for an `ask` step, and not allowed otherwise. For `goTo`, what to tell the player to do while it's next. For `ask`, what the player says when they ask (the `title`, without it). |
| `asked` | [Long Text](#long-text) | Optional. Only for an `ask` step. What the player is told when someone else asks them its question (without it, the `title`). |

In conversation with someone, the player can ask the question of an `ask` step one of their sequences is on (once they've met anyone they set off to meet), if the one they're talking with is someone it asks: they meet its `who` (and aren't in the player's household, unless it says whether they're family), and with `with` `partner`, they're the player's partner, or with `sought`, they're the friend the player looked for, if any. It takes the step's `minutes`, and is answered as for anyone else, appointments included. The player is told the answer, and with an appointment, where and when they'll meet. Talking with someone in conversation in any way does a `talk` step one of their sequences is on, if it talks with them. The player can also tell someone they're calling off what they're doing together: an appointment between them that hasn't started (or as its worker, letting the client go), or following them as part of their steps. The other carries on without them.

#### Level Need

A level from 0 to 100 that grows by `perHour` each hour, except while it's met (dropping by `satisfyPerHour`) or a `pausedBy` need is met. It's met while someone does a `do` step of it, and a `do` step without `minutes` lasts until it's 0. Its urgency rises from 0 at `urgentFrom` to 100 at 100, and is 0 outside its hours.

| Property | Type | Notes |
|---|---|---|
| `perHour` | number | At least 0. How much the level grows each hour. |
| `satisfyPerHour` | number | More than 0. How much the level drops each hour while it's met. |
| `pausedBy` | list of string | Optional. Need ids. Needs that stop this one growing while they're met. |
| `urgentFrom` | number | Optional (0). At least 0 and less than 100. The level at which it starts to be urgent. |
| `wakeUrgency` | number | Optional. At least 0. A sleeper (not the player) wakes once this need's urgency reaches it and passes sleep's, unless sleep pauses it. Without it, the need never wakes anyone. |
| `startLevel` | [Integer Range](#integer-range) | Optional (0). From 0 to 100. The level at the start of a new game. |
| `levels` | list of [Level](#level) | Optional. The words for how high it is. Needs with them are shown to the player. |

#### Level

The title of the last level at or below the need's level is shown.

| Property | Type | Notes |
|---|---|---|
| `minimum` | number | From 0 to 100, higher than the one before. |
| `title` | string | Shown to the player. |

#### Quota Need

A quota for each day or week, measured in time or runs, met within its hours, which starts again each day or week. Measured in time, only time spent on `do` steps with `meetsQuota` counts, and steps must include at least one. Measured in runs, a run counts when its `countRun` step is carried out, steps must include one (so the need can't have a `place`), and each run counts as the minutes its `do` steps take, which must be at least 1 for one person. For a need shared by the family, everyone's progress counts, including what others plan to do: the time left in their `do` steps that meet it, or a whole run while they're partway through one (before its `countRun` step, or with it pending, for the player). Its urgency is 0 outside its hours, on other days, or once met; otherwise it's 0 while the spare time left (minutes left in its hours this period, less the quota left, and less the walking estimated for each shift or run left: every `goTo` step whose condition is met now, from where the person is, counting the steps a `repeat` step repeats once) is at least `lead`, rising to 100 as it runs out. Choosing it chooses a shift: `shift` minutes, shortened if less of the quota is left, or less of the day's hours once the person has walked to its first `do` step. A run is only started if its `do` steps fit in the day's hours with the walking up to the last of them. With no time left, they're idle instead. Talking can interrupt it, after which someone whose need is then closed or done gives up on it. Has exactly one of `time` and `runs`, and the quota for one person must fit in its hours for the period.

| Property | Type | Notes |
|---|---|---|
| `period` | string | Optional (`day`). `day` or `week`. |
| `lead` | integer | More than 0. It starts to become urgent once fewer spare minutes than this are left. |
| `overdueUrgency` | number | Optional. From 0 to 100. Weekly needs with `hours.days` only. Once its days have passed this week with the quota unmet, it can be met on any day of the week (other than special days it skips) with this urgency. |
| `sharedByFamily` | boolean | Optional (false). Met for the whole family by anyone in it. |
| `time` | [Quota Time](#quota-time) | Optional. Measures the quota in time. |
| `runs` | [Family Amount](#family-amount) | Optional. Measures the quota in runs: how many each period, rounded up. |

#### Quota Time

| Property | Type | Notes |
|---|---|---|
| `minutes` | [Family Duration](#family-duration) | Whole numbers. The minutes it takes each period. |
| `shift` | integer | More than 0. The most minutes spent at once. |

#### Family Amount

A number, or an object:

| Property | Type | Notes |
|---|---|---|
| `base` | number | Optional (0). At least 0. The amount for the whole family. |
| `perMember` | number | Optional. At least 0. Added for each family member. Only for needs shared by the family, in a quota or a step's `minutes`. |

The object needs at least one of them.

#### Family Duration

A length of time, in minutes, that can grow with the size of the family: as a [Family Amount](#family-amount), except that the number, `base`, and `perMember` can each also be text with hours and minutes, or days, hours, and minutes, as for a [Duration](#duration).

| Property | Type | Notes |
|---|---|---|
| `base` | number or text | Optional (0). As for a [Family Amount](#family-amount). |
| `perMember` | number or text | Optional. As for a [Family Amount](#family-amount). |

#### Urgency Factor

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `when` | [Condition](#condition) | Can't use `hidden`. Who it applies to. |
| `needs` | list of string | Need ids, each listed once. |
| `factor` | number | More than 0. Multiplies those needs' urgency. |

#### Travel Time Factor

Walking takes its travel time times every factor that applies, rounded up.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `when` | [Condition](#condition) | Can't use `hidden`. Who it applies to. |
| `factor` | number | More than 0. Multiplies the walking time. |

## Portraits

A portrait is drawn from its layers, bottom to top, and cut off at its edges. A **color** is `#RRGGBB`, `#AARRGGBB`, or the id of a Portrait Color. In a layer, it can also be one of the layer's own `colors`, or a color name of the clothing it draws (which must be a color of every article of clothing in its `clothing`). The layer's own colors take the place of clothing colors and Portrait Colors with the same id, and clothing colors take the place of Portrait Colors.

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
| `when` | [Condition](#condition) | Can't use `hidden`. Who the variant is for. |
| `color` | string | Color. |

#### Portrait Scale

A stretch that doesn't change outline widths.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `originX` | number | Optional (0). The horizontal position that stays in place. |
| `originY` | number | Optional (0). The vertical position that stays in place. |
| `x` | number | Optional (1). More than 0. How much it stretches across. |
| `y` | number | Optional (1). More than 0. How much it stretches up and down. |
| `variants` | list of [Portrait Scale Variant](#portrait-scale-variant) | Optional. The first whose `when` is met replaces `x` and `y`. |

#### Portrait Scale Variant

| Property | Type | Notes |
|---|---|---|
| `when` | [Condition](#condition) | Can't use `hidden`. Who the variant is for. |
| `x` | number | Optional (1). More than 0. How much it stretches across. |
| `y` | number | Optional (1). More than 0. How much it stretches up and down. |

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
| `when` | [Condition](#condition) | Can't use `hidden`. Who the variant is for. |
| `x` | number | Optional (0). How far right. |
| `y` | number | Optional (0). How far down. |

#### Portrait Layer

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `when` | [Condition](#condition) | Optional. Can't use `hidden`. Who the layer is drawn for. |
| `at` | [Portrait Place](#portrait-place) | Optional. Without it, drawn anywhere. |
| `scale` | string | Optional. A Portrait Scale id, for shapes without their own. |
| `offset` | string | Optional. A Portrait Offset id, for all shapes. |
| `clothing` | list of string | Optional. Unique clothing item ids. The layer is only drawn while one of them is worn, and uses the color names of the first one in the list that is. |
| `hiddenBy` | [Portrait Hidden By](#portrait-hidden-by) | Optional. Without it, clothing never hides the layer. |
| `colors` | list of [Portrait Color](#portrait-color) | Optional. Unique ids. The layer's own colors, worked out after the Portrait Colors and its clothing's colors, each using only the layer colors before it. |
| `shapes` | list of [Portrait Shape](#portrait-shape) | Drawn bottom to top. |
| `ai` | boolean | Whether AI drew the layer. Not used by the game. |

#### Portrait Hidden By

When worn clothing hides a layer: when it covers every one of the `regions` at least as much as `coverage`. How much a region is covered is the most `coverage` any worn piece gives it.

| Property | Type | Notes |
|---|---|---|
| `regions` | list of string | Unique clothing slot ids, when there are clothing slots. The regions of the body that must be covered. |
| `coverage` | integer | From 1 to 10. How much each region must be covered. |

#### Portrait Place

Has `home` set to true, `quarterKinds`, `workplaces`, or several. A character is in a workplace in a business's place, or at home while `atWork`; in a home in any other house; and otherwise in their quarter's kind. On the new game screen the player is at home and not at work.

| Property | Type | Notes |
|---|---|---|
| `home` | boolean | Optional (false). Drawn in a home. |
| `quarterKinds` | list of string | Optional. Unique quarter kind ids. Drawn in a quarter of one of the kinds. |
| `workplaces` | list of string | Optional. Unique professions. Drawn in a place of the profession, or at home for someone working from home in it. |

#### Portrait Shape

Has a `path` and `fill`, `stroke`, or both; or has `include`, with nothing else but `when` and `clip`.

| Property | Type | Notes |
|---|---|---|
| `path` | string | Optional. WPF path markup (SVG path data, optionally starting with `F0` for even-odd fill, the default, or `F1` for nonzero). |
| `when` | [Condition](#condition) | Optional. Can't use `hidden`. Who the shape is drawn for. |
| `fill` | string | Optional. Color. |
| `gradient` | string | Optional, with `fill`. `vertical`, `horizontal`, or `radial`, from `fill` to `fillTo` across the shape's bounds. |
| `fillTo` | string | Optional, with `gradient`. Color. Without it, `fill` fades to transparent. |
| `stroke` | string | Optional. Color, with rounded ends and corners. |
| `strokeWidth` | number | Optional (1). More than 0. How wide the outline is. |
| `opacity` | number | Optional (1). From 0 to 1. How opaque it is. |
| `scale` | string | Optional. A Portrait Scale id, used in place of the layer's. |
| `include` | string | Optional. A Portrait Shape Set id, whose shapes are drawn in its place, in the layer's colors. |
| `clip` | boolean | Optional (false), with `include`. Whether the included shapes are only drawn over the layer's other drawn shapes that have a `fill`. |

#### Portrait Shape Set

Shapes that are only drawn where layers include them.

| Property | Type | Notes |
|---|---|---|
| `id` | string | |
| `scale` | string | Optional. A Portrait Scale id, for shapes without their own. Without it, the including layer's. |
| `shapes` | list of [Portrait Shape](#portrait-shape) | Drawn bottom to top. None can have `include`. |
| `ai` | boolean | Whether AI drew the shape set. Not used by the game. |

# Adding to the Data

Most mistakes stop the data from loading. These steps are **not checked**, and only show up as something missing in the game. Afterward, generate villages with the village report (see `Docs/CommandLine.md`) and check its `log` records.

Portraits.json is generated, so portrait art is added to SubCorona's `Art/Portraits/Portraits.sh`, as described in SubCorona's `Docs/Portraits.md`.

Saved games refer to properties, values, location and map feature templates, and needs by id, so renaming or removing one stops older saves from loading.

* **Profession:** give it the `text` entries the templates use, and `player: false` if the player shouldn't have it. Make sure there are enough business places for `maxBusinesses`, or businesses end up in full quarters with a warning, or give it none if it works from home. Add it to the value groups that fit, and to the conditions that list professions it should be in, such as those of the outfits it should be given. Give it a `work` outfit if it has work clothes, and `modesty` if it calls for more or less covering while working. Add portrait art: a workplace background, and anything that depends on the work as well as the clothes (drawn with `wears` and a profession condition).
* **Character property:** use it in a template `description` uses, with the `text` entries those templates need. Give it the `regions` it shows on (and `form` if it's about the body's form) if clothing should hide it, and add it to `families.inheritedProperties` if family members share it. Add portrait art if it changes how a character looks, with `hiddenBy` if clothing should cover it.
* **Character value:** add it to any `player` or `npc` `values` lists and value groups it belongs in, and check the conditions that list other values of the property: a `notValues` list lets it through, and a `values` list leaves it out. Outfits and modesty rules need checking too, so a character with the value is still given every kind of outfit and follows the right rule. Give it a `prominence` if its property has `regions` and it should be easier to hide. Add portrait art if the property shows in portraits. Its place in the list matters for a property with a `shape`, and for an inherited property with a `blend`.
* **Template:** use it from `description` or a template it uses, or it's never shown.
* **Quarter kind:** add quarters of the kind, add it to the `quarterKinds` of the business places that can be in it, and add a portrait background.
* **Quarter or house:** make sure there are enough for each kind and wealth level, or templates are reused with a warning.
* **Map feature:** set `borders` on the quarter kinds or quarters that belong beside it; otherwise an area is placed next to an outskirts quarter.
* **Day:** add it to the `hours.days` of the needs met on it, and check the special days still fall where they should.
* **Need:** add it to the `pausedBy` of level needs that shouldn't grow while it's met, and to the urgency factors that apply to it.
* **Clothing item:** add it to the outfits it belongs in, or nobody is given it. Give it `coverage` for every region it covers (which decides its modesty, what it hides from descriptions, and which portrait layers and character properties it hides), a `bulk` if it's light or bulky, and `accessory` if it's shared between outfits and isn't washed. Add portrait art drawn with `wears`, leaving out shapes that show the body's form, such as a paunch, for clothing whose bulk hides form.
* **Outfit:** make sure every character can be given an outfit of each kind they should have: one with no outfit of a kind they can have goes without. Check its clothing meets the modesty rules: `outside` for everyday clothes, alone and with the work clothes of the professions that can have them, and `home` for night clothes.
* **Clothing slot:** add it to the modesty rules if it should be checked, to the `slots` of the clothing anchored in it, to the `coverage` of the clothing that covers it, and to the `regions` of the character properties and portrait layers that show on it. Its place in the list decides the order worn clothing is described in.
* **Clothing layer:** its place in the list decides which clothing covers which, from the innermost out.
* **Modesty rule:** check every character follows a rule: the first one whose `when` they meet applies, and one who meets none can wear anything. Add it to the `modesty` of the professions that call for more or less covering from people following it.
