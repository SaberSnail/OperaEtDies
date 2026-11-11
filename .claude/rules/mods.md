---
paths:
  - "OperaEtDies/Data/Mod*.cs"
  - "OperaEtDies/Data/JsonDataLoader.cs"
  - "OperaEtDies/Mods/**"
  - "OperaEtDies/Controls/ManageMods/**"
---

# Mod Documentation

How mods work, and the format of mod files, is documented in @../../Docs/Mods.md. When a change affects what mods can do, how mod files are read or applied, or which data files mods can change (including adding a new data file), update that document in the same change.

When editing the documentation, follow the same rules as for the data file documentation:

* Base it entirely on the code that reads and applies mod files, not on the contents of any mod or data files.
* Keep it minimal but complete: every operation, property, and validation rule should be covered. Examples are allowed here, since the path syntax is easier to follow with them.
* Document only what is currently true. Don't describe what used to be true or what has changed.
