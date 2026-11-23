See @../Docs/Design.md for general design principles and conventions.

Since this is a single-developer project, there is no need to use PRs. Do not create commits or push any changes. I will commit and push them myself.

Do not run the application for testing unless specifically requested. It is sufficient to verify that the application builds successfully. The village report below is the exception: run it whenever generation statistics or portraits would help.

## Todo List

`ToDo.md` at the repository root has two sections:

* **Long Term** comes first: items to address in the future, grouped into sub-projects of strongly related items. Each sub-project is a `###` heading followed by a bulleted list (not checkboxes).
* **Current Work** comes second: the project being worked on now. Its first line is a brief description of the project, followed by a checklist of items with checkboxes. A larger project can split its checklist into sub-projects, each a `###` heading with its own checklist. Items that don't fit a sub-project of their own go in a **Cleanup** sub-project, which stays last.

"Work" and "project" mean the same thing here (current work, current project). Manage the list like this:

* Clear Current Work when starting new work from Long Term, or when told the current work is complete. To clear it, move every unchecked item in it to Long Term, as a bullet point in the sub-project it fits best (or a new sub-project if none fits), then remove the rest of it, including its description and its sub-project headings.
* When starting work on an item or a whole sub-project from Long Term, clear Current Work first, then remove it from Long Term (along with its sub-project heading, if the sub-project is left empty) and put its brief description at the top of Current Work.
* As things that need to be addressed come up during the current project, add them to the Current Work checklist, in the sub-project they belong to, if it has sub-projects.
* When a Current Work item is addressed, check it off and leave it in place. Don't delete it.

## Village Report

To analyze village generation (people, families, professions, wealth, locations, template reuse), run the game with `--no-mods --generate-villages <count>` instead of writing a one-off test or script. It loads the data, generates that many villages exactly as a new game does (each with a random player), writes tab-separated records to standard output, and exits without showing any UI. Build first, then run the exe directly, redirecting the output to a file in the scratchpad directory (it's a GUI app, so output only appears when redirected or piped):

```bash
dotnet build -v q -nologo
OperaEtDies/bin/Debug/net9.0-windows/OperaEtDies.exe --no-mods --generate-villages 500 --seed 0 > <scratchpad>/villages.tsv
```

* `--seed <n>` is optional. Village `i` uses seed `n + i`, so `--generate-villages 1 --seed <seed>` reproduces any village from its `seed` column. Without it, the first seed is random.
* Exit code 0 is success, 1 means the data failed to load (errors go to standard error), and 2 means invalid arguments.
* About 500 villages take 5 seconds.
* The first field of each line is the record type. The output starts with one header line per type (the type prefixed with `#`) naming its columns, so check the headers rather than assuming column positions. The types are `village`, `household`, `person` (with one column per need and one per character property, holding the value id), `business`, `clothing` (each piece of clothing, with its owner, its outfit, and where it is), `relationship` (each pair of people, with their score and whether they're friends), `sighting` (the last time each person saw each other person), `event` (the event log: what the player saw happen where they were), `location` (with each quarter's position and size on the map), `exit` (one per direction of each path between quarters), `feature` (map features such as the river), `activity` (with `--simulate-days`), and `log` (warnings and errors logged while loading or generating, such as reused templates). To see a village's layout, draw each quarter's rectangle from its `location` record as a grid of letters with awk.
* Analyze the file with `awk -F'\t'`, `sort`, and `uniq -c` (Python and jq aren't available). For example, the owners of each profession: `awk -F'\t' '$1=="business"{print $4}' villages.tsv | sort | uniq -c | sort -rn`.
* `--portraits <folder>` also saves each person's portrait to the folder as `<village>-<household>-<member>.png`, and the `portraitLayers` column of each `person` record lists the portrait layers drawn for them.
* `--simulate-days <days>` runs each village for that many days after it's created and writes an `activity` record each time someone starts a task (when, who, their goal need, the task, how long it takes, and where). Use it to check schedules and needs, such as when people sleep, eat, and work.
* The code is in `OperaEtDies/Reports/VillageReport.cs`. When generation changes in a way the records don't capture, add columns there.

## Portraits

The portrait art is in SubCorona's `Data/Portraits.json`, which is generated by scripts in SubCorona's `Art/Portraits` folder. Before changing the portrait art, read SubCorona's `Docs/Portraits.md` and follow it: change `Art/Portraits/Portraits.sh` and run `Art/Portraits/Generate.sh` rather than editing Portraits.json, then look at the results with `Art/Portraits/Preview.sh`. When a character property value or a profession is added or changed, add or update its portrait art the same way. Changes to how Opera et Dies reads Portraits.json go in the code and `Docs/DataFiles.md` as usual, and the scripts need updating to match.

## Game State and Saving

Saved games must hold the whole game state, so keep to these rules whenever game state is added or changed:

* All mutable game state lives in `GameWorld` (in `OperaEtDies/GameState`) and the world objects it owns. Nothing else (view models, statics, services) holds game state; view models and other code read and change it through the world objects.
* Everything in the world (people, families, locations, businesses, map features, and anything added later) is a world object: it implements `IWorldObject`, has a `WorldId`, is created through the `WorldObjects` registry, and writes its own save record. Treat `WorldId` as a black box: never depend on its value, parse it, or show it to the player.
* World objects derive from `NotifyPropertyChangedBase`, not `ViewModelBase`, and use no WPF types. Presentation (portraits, screen state) belongs in the view models in `Controls` and `Windows`.
* Save records hold only the source of truth. Anything that can be worked out from other state (a location's people, a person's home and workplace, lists and lookups) is rebuilt on load. Text chosen at random is state and is saved; text built from the data is rebuilt.
* Save records refer to other world objects by `WorldId` and to the data by its ids. Loading fails with a clear message when an id isn't found.
* When game state is added or changed, update the save record and loading for it, the save format version if old saves can no longer load, the round-trip tests in `OperaEtDies.Test/GameState`, and `Docs/SaveFiles.md`.
* The village report writes everything it can from the world, so it doubles as a check on saving: its output must be the same with and without `--through-save`. When the report gets new columns for game state, write them from the world objects, not the generated town.

## Command Line Options

The game's command line options are defined in `OperaEtDies/CommandLineOptions.cs`, and `--help` lists them. They're explained in more detail, with examples, in `Docs/CommandLine.md`. When adding or changing an option, update the help text there and that document too. `--developer` turns on developer UI, which is always surrounded by the `DeveloperBorderStyle` border so it's clearly not part of the game.

## Mods

Mods are experimental. Never read mod files (the files in any mod folder, including the folders in the game's mod list) into context unless I explicitly ask you to.

When running the application, always pass the `--no-mods` command line argument so mods aren't loaded, unless I explicitly ask you to run it with mods.

The same goes for the portrait editor (`Tools/PortraitEditor`): never run it without `--no-mods` unless I specifically ask you to.

Never run the mod editor (`Tools/ModEditor`, documented in `Docs/ModEditor.md`) unless I explicitly ask you to. It has no `--no-mods`, and it interrupts other work on my computer. Building it is fine. The game and both tools build into the game's output folder (`OperaEtDies/bin/Debug/net9.0-windows`).

## Third-Party Packages

Every NuGet package is a supply-chain risk, so adding or updating one (including a version bump, or a package another package brings in) follows `Docs/Dependencies.md`: ask me first, inspect the package before it's restored, pin it exactly, update the lock files on purpose, and add its license to `ThirdPartyNotices.txt`. Never loosen the safeguards in `nuget.config` and `Directory.Build.props` (locked restores, one source, required signatures, the vulnerability audit) to get a build working; fix the cause instead.

## AI-Written Text and Art

Every long text field in the data (one that typically holds a full sentence or more, not a title or short fragment) is an object holding the `text` and a required `ai` flag (see `Docs/DataFiles.md`).

* When you create a long text field, set `ai` to true.
* When you make a significant change to one (anything more than an isolated short phrase), set `ai` to true.
* If a field's `ai` is false, ask me to confirm before changing its text and setting the flag to true. Don't change it until I say so.

Every portrait layer and portrait shape set also has a required `ai` flag. Portraits.json is generated, so the flag comes from SubCorona's `Art/Portraits/Portraits.sh`, where each layer and shape set is marked as drawn by AI unless it's started after `handDrawn`.

* When you create a layer or shape set, leave it marked as drawn by AI.
* When you make a significant change to one (anything more than an isolated small tweak to a shape), mark it as drawn by AI, removing its `handDrawn`.
* If one is marked as hand-drawn, ask me to confirm before changing it and marking it as drawn by AI. Don't change it until I say so.
