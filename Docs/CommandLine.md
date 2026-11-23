# Command Line Options

OperaEtDies.exe accepts these options. Options are matched ignoring case, can come in any order, and anything the game doesn't recognize is ignored. Run the game with `--help` for a short list.

| Option | Does |
|---|---|
| `--help` | Shows the list of options and exits. |
| `--no-mods` | Starts without loading any mods. |
| `--mod <folder>` | Loads the mod in the folder instead of the mods in the mod list. Can be given more than once. |
| `--developer` | Shows extra information and controls for developing the game. |
| `--generate-villages <count>` | Generates villages, writes them to standard output, and exits without showing the game. |
| `--seed <number>` | With `--generate-villages`, the random seed of the first village. |
| `--portraits <folder>` | With `--generate-villages`, also saves each person's portrait to the folder as a PNG file. |
| `--portraits-at-work` | With `--generate-villages`, draws everyone's portrait at work in their own workplace, instead of where they start the game. |
| `--through-save` | With `--generate-villages`, saves each village and loads it again before writing it, to check that saving keeps everything. |
| `--simulate-days <days>` | With `--generate-villages`, runs each village for this many days after it's created, writing what people do. |

If more than one is given, `--help` wins over `--generate-villages`, which wins over starting the game.

## Output from a windowed program

OperaEtDies.exe is a windowed program, not a console program, so anything it writes to standard output only appears if the output is redirected to a file or piped to another program. How to do that depends on the shell:

* **Git Bash:** `>` works as usual.
* **Command Prompt:** `>` works, but Command Prompt doesn't wait for a windowed program, so the prompt comes back while the file is still being written.
* **Windows PowerShell:** `>` doesn't work. PowerShell neither waits for a windowed program nor captures its output, so the file is created but stays empty. Pipe the output to `Out-File` instead, or use `Start-Process` with `-RedirectStandardOutput` (see the examples below).

In every shell, run the program from its own folder as `.\OperaEtDies.exe` (PowerShell) or `./OperaEtDies.exe` (Git Bash), or give its full path. A path starting with `\` points at the root of the drive.

## --help

Writes the list of options to standard output and exits. When the output isn't redirected, the list is also shown in a message box, since there's no console to show it in.

## --no-mods

Loads only the game's own data files, ignoring every mod in the game's mod list and any given with `--mod`. Without it, the mods in the mod list are applied in order, as described in [Mods.md](Mods.md). This also applies to `--generate-villages`.

## --mod

Loads the mod in the given folder instead of the mods in the game's mod list, which is left as it is for the next time. Each `--mod` adds one folder, and the mods are applied in the order they're given. A relative folder is relative to the folder the game was started from. A folder that doesn't exist stops the data from loading, as a missing folder in the mod list does, and a `--mod` with no folder after it is ignored. The Manage Mods screen still shows and changes the mod list, with a note that it isn't loaded this time. This also applies to `--generate-villages`. The [mod editor](ModEditor.md) uses it to run the game with the mod being edited.

```bash
OperaEtDies/bin/Debug/net9.0-windows/OperaEtDies.exe --mod "D:/Mods/My Mod" --developer
```

## --developer

Turns on developer UI. Every piece of developer UI has a thin, semi-transparent blue border, so it's clearly not part of the game. Currently:

* The main menu shows "Developer Mode" in its bottom-left corner.
* The villagers view shows each family's wealth level, and a button with the name of the family's house that closes the view and goes to the house.
* On the map of the town, which everyone can open from the sidebar, clicking a quarter goes there.
* On the new game screen, the player's gender has a second choice that allows any gender, even ones the player isn't usually allowed. Choosing one keeps it when randomizing. The properties that only apply with such a gender, such as a beard for a gender that grows one, are added to the screen, and are only shown while they apply.

## --generate-villages

Loads the data and generates the given number of villages, exactly as a new game does, each with a random player. It writes what was generated to standard output as tab-separated records, writes any data loading errors to standard error, and exits without showing any UI. The count must be a whole number of at least 1. About 500 villages take 5 seconds.

**Exit codes**

| Code | Meaning |
|---|---|
| 0 | The villages were generated. |
| 1 | The game data couldn't be loaded. The errors are written to standard error. |
| 2 | The options are invalid, such as a missing or non-numeric count. The problem is written to standard error. |

**Records**

Each line is one record, and its first field is the record type. The output starts with one header line per record type: the type with `#` in front of it, followed by the names of that type's columns. Check the headers rather than assuming where a column is, since columns get added.

| Type | One record for each |
|---|---|
| `village` | Village: its seed, counts of households, people, businesses, and locations, and its layout: the shape, how many landmarks, paths between quarters, and dead ends there are (and how many dead ends are landmarks), the most exits any quarter has, its map features, how many paths cross the river, and when the game starts (the time, and the id of the season). |
| `household` | Household: its last name, size, wealth, and house. |
| `person` | Person: their family role and work, the location they start the game in (`location`) and the kind of place it is for their portrait (`place`: `home`, `quarter:<quarter kind>`, or `workplace:<profession>`), the ids of the portrait layers drawn for them (`portraitLayers`, separated by spaces), the id of the need they're trying to meet (`goal`, empty if idle), their current task (`task`: `travel`, `busy`, `talk`, `wait`, `follow`, `meet`, `changeClothes`, or `changeBack`, empty if none) and when it ends (`taskEnd`), the index of the step of their goal they're on (`step`, empty if they aren't carrying out steps), the other sequences of steps they're in the middle of, on hold or waiting to set off for an appointment (`onHold`: `<need id>@<step index>`, followed by `~Waiting` or `~Due` for one waiting to set off, separated by spaces), the items in each of the bundles their steps took or made (`bundles`: `id=count`, separated by spaces), the places their steps remembered (`remembered`: `name=location`, separated by spaces), the clothes each outfit they changed into replaced (`replacedClothes`: `kind=count`, separated by spaces), how many friends they have (`friends`), one column for each need in `needs`, holding a level need's level or what's been done of a quota need this day or week (minutes, or runs for a quota measured in runs), plus one column for each character property in Characters.json, holding the value id they start the game with. |
| `business` | Business: its profession, owner, helpers, and location. |
| `clothing` | Piece of clothing: its owner (household and member numbers, as in the `person` record), the id of its clothing, its colors (`name=colorId`, separated by spaces), the outfit it's part of and that outfit's kind (`everyday`, `work`, `night`, or `nothing`), where it is (`worn`, `carried`, or the location it's kept in), its description, and when it can be worn again, if it was put away to be ready later (`readyAt`), with what it's shown doing until then (`delayActivity`). |
| `relationship` | Pair of people: who (the household and member numbers of each, as in the `person` record), their relationship score, whether they're friends, how they're related if they're in the same household (`familyTie`: `spouse`, `elder`, `elderInLaw`, or `other`, empty if they aren't), whether their houses are on the same quarter, and when they last talked (empty if they haven't). |
| `appointment` | Appointment still to keep: the worker and the client (household and member numbers, as in the `person` record), the id of the need and the index of the `ask` step that made it, where (`place`) and when (`time`) it is, whether each has set off for it, and whether it has started. |
| `job` | Job still to finish: the worker and the client (household and member numbers, as in the `person` record), the id of the need it's for, where it's done (`place`), and how many minutes it takes in all (`minutes`) and are done (`minutesDone`). |
| `event` | Entry in the event log: something the player saw happen where they were (since no one plays, the player lives their day like everyone else). When, what (`kind`: `arrived`, `left`, `busy`, `idle`, `changingClothes`, `talking`, `waiting`, `meeting`, `following`, `failed`, `gaveUpWaiting`, or `neverCame`), who it happened to and anyone else it involved (household and member numbers, as in the `person` record), where, the place they came from or went to (`place`), the id of the need and the index of the step they started or that failed, why a step failed (`cause`, as in the `failure` record), and the entry's text as the game shows it. Only the most recent 500 are kept. |
| `sighting` | Last time someone saw (or heard about) another person in the last `sightingHours` hours (from `schedules`): who saw whom (household and member numbers, as in the `person` record), where and when, what the person was doing (`activity`: `none`, `idle`, `busy`, `walking`, `left`, or `talking`), the id of the need they were busy with and what they were shown doing (`doing`), the next place on their way when walking or leaving (`destination`), and who told them about it if they didn't see it themselves (`source`, as `household-member`). If the person seen was asked what they were doing, it also has the id of the need they said was their goal (`toldGoal`, `idle` if none), where they said their walk ended (`toldDestination`), and what they said they were doing for it (`toldDoing`). |
| `location` | Quarter, house, and business location: its template, its connections, whether it's a landmark, and for a quarter, its position and size on the map, and the map features it borders, each with its direction. |
| `exit` | Direction of each path between quarters: where it starts and ends, its compass direction, what the path is called, and whether it crosses the river. Each path has two, one for each end. |
| `feature` | Map feature, such as the river or a forest: its type, its position and size on the map, and how many quarters border it. |
| `activity` | Task someone starts while `--simulate-days` runs: when (the time, and the id of the day of the week), who (their household and member numbers, as in the `person` record), the id of the need they're trying to meet (empty if idle), the index of the step of it they're on (`step`, empty if none), the task (`travel`, `busy`, `talk`, `wait`, `follow`, `meet`, `changeClothes`, `changeBack`, `startWork`, or `stopWork`), how many minutes it takes, where they are, and for `travel`, where they're going. |
| `failure` | Step that fails while `--simulate-days` runs: when, who (their household and member numbers, as in the `person` record), the id of the need, the index of the step and its kind, why it failed (`cause`: `no`, `nobody`, `unreachable`, `missing`, `left`, `interrupted`, `hours`, `noShow`, or `noOption`), and where they are. |
| `log` | Warning or error logged while loading or generating, such as a location template being used twice. |


Everything is written as of the time the game starts, when the player wakes up, or after `--simulate-days` if it's given.

## --seed

Sets the random seed of the first village generated by `--generate-villages`. Village `i` (counting from 0) uses the seed plus `i`, so any village can be generated again from the `seed` column of its `village` record: `--generate-villages 1 --seed <that seed>`. The seed must be a whole number. Without it, the first seed is random.

## --portraits

With `--generate-villages`, draws each person's portrait at its full size and saves it in the given folder as `<village>-<household>-<member>.png`, using the numbers from the `person` record. Each person is drawn where they start the game, unless `--portraits-at-work` is given. The folder is created if it doesn't exist, and existing files with the same names are replaced. Without Portraits.json in the data, nothing is saved. A missing folder name is an invalid option (exit code 2).

## --portraits-at-work

With `--generate-villages`, draws everyone at work in their own workplace instead of where they start the game: in their business's place, or at home for those who work from home, with the at work property set to working. This shows each profession's portrait art. It changes the portraits saved by `--portraits` and the `portraitLayers` column of the `person` records, but not the other columns.

## --simulate-days

With `--generate-villages`, runs each village for the given number of days after it's created, with the player deciding what to do like everyone else, and writes an `activity` record each time someone starts walking a path or spending time somewhere. The other records are written once it's done. The number must be a whole number of at least 0, and 0 runs nothing. Each day of 50 villages takes about a second. With `--through-save`, the village is saved and loaded after it runs.

## --through-save

With `--generate-villages`, saves each village as a saved game would be (in memory, not to a file), loads it again, and writes the loaded village instead of the generated one. Everything a save keeps is written from the loaded village. The `wealth` and `fortune` columns of the `household` records and the `shape` column of the `village` record only exist while a village is generated, so they always come from the generated village. Saving and loading keep everything the report shows if the output is the same with and without this option. A village that fails to load is written as a `log` record with the reason, in place of its other records.

## Examples

Each example generates 500 villages without mods, starting from seed 0, and saves them to `villages.tsv`.

**Git Bash**, from the folder holding the solution, after building:

```bash
dotnet build -v q -nologo
OperaEtDies/bin/Debug/net9.0-windows/OperaEtDies.exe --no-mods --generate-villages 500 --seed 0 > villages.tsv
```

**Command Prompt**, from the same folder. The path needs backslashes, and the file is finished a few seconds after the prompt returns:

```bat
OperaEtDies\bin\Debug\net9.0-windows\OperaEtDies.exe --no-mods --generate-villages 500 --seed 0 > villages.tsv
```

**Windows PowerShell**, from the folder holding OperaEtDies.exe, piping the output to a file:

```powershell
.\OperaEtDies.exe --no-mods --generate-villages 500 --seed 0 | Out-File -Encoding utf8 villages.tsv
```

In Windows PowerShell, `-Encoding utf8` starts the file with a byte order mark, which can trip up tools that read the first column of the first line. `Start-Process` saves the output exactly as the game writes it:

```powershell
Start-Process .\OperaEtDies.exe -ArgumentList '--no-mods --generate-villages 500 --seed 0' -Wait -NoNewWindow -RedirectStandardOutput villages.tsv
```

To check the exit code after `Start-Process`, add `-PassThru` and read the `ExitCode` of the process it returns.

**Reproducing one village** whose `village` record has seed 1234:

```bash
OperaEtDies/bin/Debug/net9.0-windows/OperaEtDies.exe --no-mods --generate-villages 1 --seed 1234 > village.tsv
```

**Saving portraits** of everyone in one village to a `portraits` folder:

```bash
OperaEtDies/bin/Debug/net9.0-windows/OperaEtDies.exe --no-mods --generate-villages 1 --seed 1234 --portraits portraits > village.tsv
```

**Saving portraits at work**, to see everyone's profession art:

```bash
OperaEtDies/bin/Debug/net9.0-windows/OperaEtDies.exe --no-mods --generate-villages 1 --seed 1234 --portraits portraits --portraits-at-work > village.tsv
```

**Seeing a week of village life**, with when everyone goes to bed:

```bash
OperaEtDies/bin/Debug/net9.0-windows/OperaEtDies.exe --no-mods --generate-villages 50 --seed 0 --simulate-days 7 > week.tsv
awk -F'\t' '$1=="activity" && $8=="busy" && $7=="need_fatigue"{split($3,a,", "); split(a[4],t,":"); print t[1]}' week.tsv | sort -n | uniq -c
```

**Checking saving and loading**, by comparing the report with and without `--through-save`:

```bash
OperaEtDies/bin/Debug/net9.0-windows/OperaEtDies.exe --no-mods --generate-villages 500 --seed 0 > villages.tsv
OperaEtDies/bin/Debug/net9.0-windows/OperaEtDies.exe --no-mods --generate-villages 500 --seed 0 --through-save > saved.tsv
cmp villages.tsv saved.tsv
```

**Seeing the options** from PowerShell (in a message box if you leave off the pipe):

```powershell
.\OperaEtDies.exe --help | Out-String
```
