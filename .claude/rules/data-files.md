---
paths:
  - "OperaEtDies/Data/**"
---

# Data File Format Documentation

The format of the data files is documented in @../../Docs/DataFiles.md. When a change affects what the data files can contain or how they are validated, update that document in the same change.

When editing the documentation:

* Base it entirely on the code that reads the data files, not on the contents of any data files (such as those in the SubCorona repo).
* Keep it minimal but complete: every file, property, and validation rule should be covered, without examples, rationale, or implementation details that don't affect the file format.
* Document only what is currently true. Don't describe what used to be true or what has changed.
