---
paths:
  - "OperaEtDies/Data/**"
---

# Data File Format Documentation

The format of the data files is documented in @../../Docs/DataFiles.md. When a change affects what the data files can contain or how they are validated, update that document in the same change. When a data file is added or removed, also update the list of files mods can change in @../../Docs/Mods.md.

When editing the documentation:

* Base it entirely on the code that reads the data files, not on the contents of any data files (such as those in the SubCorona repo).
* Keep it minimal but complete: every file, property, and validation rule should be covered, without examples, rationale, or implementation details that don't affect the file format.
* Document only what is currently true. Don't describe what used to be true or what has changed.

The mod editor shows DataFiles.md as its help, and goes from each form field to its property's row. Its tests (`OperaEtDies.Test/Tools/ModEditorHelpTests.cs`) check that every class the data is read into has a `####` heading, and that every property the game reads has a row in that heading's table, named in its first column. A heading that isn't the class name split into words (such as "Business" for `ProfessionBusiness`) is listed in `Tools/ModEditor/Model/HelpTopics.cs`, so update that when a heading or class is renamed.
