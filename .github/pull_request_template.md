<!-- Merging this PR releases a new version. Its title decides the version: see "Releases" in Development.md.
The "Release notes" check shows the version and the notes, and fails when the title does not match the tasks. -->

## Tasks

<!-- The task files this PR adds, changes or closes. -->

## Checklist

- [ ] Every task moved to `tasks/done/` has `## Release note` (`None.` when users see no change), and `breaking: true` when an upgrade needs a change on the user's side.
- [ ] Behavior changed → `docs/llms-full.txt` and the page under `docs/` updated in the same commit.
